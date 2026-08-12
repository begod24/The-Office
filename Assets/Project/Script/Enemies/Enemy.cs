using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Office.Enemies
{
    /// <summary>
    /// The single networked carrier for everything in the office that hunts.
    /// </summary>
    /// <remarks>
    /// Shaped exactly like <see cref="DamageableTarget"/>, and for the same reasons. One
    /// registered prefab — <c>PF_Enemy</c> — carries a replicated <see cref="EnemyDefinition"/>
    /// id; what the player sees is the definition's view prefab, built locally on every machine.
    /// A new enemy is an asset and a mesh, never a netcode change.
    /// <para>
    /// It owns the parts of an enemy that are the same whatever it is: the body it is hit on,
    /// the navigation agent it moves with, and the definition both are sized from. Deciding
    /// where to move belongs to <see cref="EnemyBrain"/>; taking damage belongs to
    /// <see cref="Health"/>, which is configured from the definition before the object spawns.
    /// </para>
    /// <para>
    /// <b>The collider lives here, not in the art.</b> One prefab serves every enemy, so the
    /// capsule a swing connects with and the agent that walks are both sized from the
    /// definition and must agree. The view is built with its own colliders stripped, which
    /// leaves an artist free to hand over a mesh with whatever collision Blender put on it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Enemy : NetworkBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private CapsuleCollider body;
        [SerializeField] private NavMeshAgent agent;

        [Tooltip("Metres the spawn point may be off the navigation mesh before the enemy is " +
                 "abandoned. A marker dropped by hand is never exactly on the mesh.")]
        [Min(0.1f)]
        [SerializeField] private float navSnapDistance = 3f;

        private readonly NetworkVariable<int> definitionId = new();

        private int pendingDefinitionId = ContentDefinition.NoId;
        private int viewDefinitionId = ContentDefinition.NoId;
        private GameObject view;
        private EnemyDefinition definition;

        // Server only. Negative means nothing is pending.
        private float despawnAtTime = -1f;

        public EnemyDefinition Definition => definition;

        /// <summary>The agent this enemy moves with, or null on a client. See <see cref="EnemyBrain"/>.</summary>
        public NavMeshAgent Agent => agent;

        public Health Health => health;

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Applied in <see cref="OnNetworkSpawn"/> so it
        /// rides the spawn payload rather than arriving as a delta a late client could miss.
        /// </summary>
        public void ServerInitialise(int definition) => pendingDefinitionId = definition;

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Pushes the definition's numbers into
        /// <see cref="Health"/>.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="ServerInitialise"/> and called by the spawner, because it
        /// has to happen while the object is still unspawned — <see cref="Health"/> refuses a
        /// late configure rather than letting two clients disagree about how tough something is.
        /// </remarks>
        public bool ServerConfigureHealth(EnemyDefinition source)
        {
            if (source == null || health == null) return false;

            health.ServerConfigure(source.MaxHealth, source.Responses);
            return true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingDefinitionId != ContentDefinition.NoId)
                definitionId.Value = pendingDefinitionId;

            // Consumed. This carrier is pooled, so the same instance comes back for the next
            // enemy and must not re-apply what the last spawn asked for.
            pendingDefinitionId = ContentDefinition.NoId;
            despawnAtTime = -1f;

            definitionId.OnValueChanged += OnDefinitionChanged;
            ApplyDefinition(definitionId.Value);

            if (health != null) health.Changed += OnVitalsChanged;

            ApplyAliveState();
        }

        public override void OnNetworkDespawn()
        {
            definitionId.OnValueChanged -= OnDefinitionChanged;

            if (health != null) health.Changed -= OnVitalsChanged;

            // The agent goes down before the object is recycled: an enabled agent on a pooled
            // instance that reappears somewhere off the mesh logs once per frame and never
            // moves again.
            if (agent != null) agent.enabled = false;

            DestroyView();

            // The cache has to go with the view: the same pooled instance coming back for the
            // same definition would otherwise take the early-out in ApplyDefinition, skip the
            // rebuild, and spawn something with no mesh.
            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

        // ------------------------------------------------------------------ configuration

        private void OnDefinitionChanged(int previous, int current) => ApplyDefinition(current);

        private void ApplyDefinition(int current)
        {
            if (current == viewDefinitionId) return;

            viewDefinitionId = current;
            DestroyView();

            definition = ContentViewFactory.Resolve<EnemyDefinition>(current, this);
            if (definition == null) return;

            ApplyBody(definition);

            // Decoration only: this carrier already supplies the capsule that PhysicsLayers
            // .AttackMask looks for, and a second collider inside the art would let a swing
            // resolve against a mesh nobody sized.
            view = ContentViewFactory.Build(definition, transform, Vector3.zero,
                Quaternion.identity, PhysicsLayers.Enemy, solid: false);

            ApplyAgent(definition);
        }

        // Runs on every machine. The capsule is what a swing hits, and the probe runs on the
        // attacking client — so a body that only existed on the server would be unhittable.
        private void ApplyBody(EnemyDefinition source)
        {
            if (body == null) return;

            body.radius = source.BodyRadius;
            body.height = Mathf.Max(source.BodyHeight, source.BodyRadius * 2f);
            body.center = new Vector3(0f, body.height * 0.5f, 0f);
        }

        /// <remarks>
        /// The agent exists on the server and nowhere else. On a client it would fight the
        /// replicated transform for the same object and win half the frames, which reads as an
        /// enemy that stutters only for the people not hosting.
        /// </remarks>
        private void ApplyAgent(EnemyDefinition source)
        {
            if (agent == null) return;

            if (!IsServer)
            {
                agent.enabled = false;
                return;
            }

            // Off first: radius and height cannot be changed on a live agent without it
            // re-registering itself, and Warp on a disabled agent is a no-op.
            agent.enabled = false;

            agent.radius = source.BodyRadius;
            agent.height = source.BodyHeight;
            agent.speed = source.PatrolSpeed;
            agent.acceleration = source.Acceleration;
            agent.angularSpeed = source.TurnSpeed;

            // Never let the agent brake for its own target: the brain stops it by clearing the
            // path, and a stopping distance would leave it sliding at attack range.
            agent.stoppingDistance = 0f;

            if (!TrySnapToNavMesh())
            {
                Debug.LogWarning(
                    $"[Enemy] '{source.name}' spawned {navSnapDistance:0.#}m or more from the " +
                    "navigation mesh and cannot move. Bake the scene's NavMeshSurface, or move " +
                    "the marker onto the floor.", this);
                return;
            }

            agent.enabled = true;
        }

        // Enabling an agent that is not close enough to the mesh logs and leaves it inert, so
        // the position is corrected first rather than after the failure.
        private bool TrySnapToNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, navSnapDistance,
                    NavMesh.AllAreas))
            {
                transform.position = hit.position;
                return true;
            }

            return false;
        }

        private void DestroyView()
        {
            if (view == null) return;

            Destroy(view);
            view = null;
        }

        // ------------------------------------------------------------------ death

        private void OnVitalsChanged(VitalsState state)
        {
            ApplyAliveState();

            if (!IsServer || !state.IsDead) return;

            // The body stays put. Despawning it in the same frame it dies makes a kill read as
            // the enemy having never been there, which is worse feedback than a corpse.
            despawnAtTime = definition != null && definition.CorpseExpires
                ? Time.time + definition.CorpseSeconds
                : -1f;
        }

        // Driven by replicated vitals, so it runs identically on every machine and the dead
        // state never has to be sent separately.
        private void ApplyAliveState()
        {
            var alive = health == null || health.IsAlive;

            if (body != null) body.enabled = alive;

            // A corpse must stop steering. Left enabled it keeps walking the last path it was
            // given, which is the single most obvious way for a kill to look broken.
            if (IsServer && agent != null && !alive && agent.enabled)
            {
                // isStopped throws on an agent that is not on the mesh, which is exactly the
                // state a corpse can be in if the floor moved out from under it.
                if (agent.isOnNavMesh) agent.isStopped = true;

                agent.enabled = false;
            }
        }

        // Server only, and only while a body is actually waiting to go.
        private void Update()
        {
            if (!IsServer || despawnAtTime < 0f || Time.time < despawnAtTime) return;

            despawnAtTime = -1f;

            // Through NGO, so the pool gets it back on every machine at once.
            if (IsSpawned) NetworkObject.Despawn();
        }
    }
}
