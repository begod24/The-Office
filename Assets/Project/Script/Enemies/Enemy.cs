using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Office.Enemies
{
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

        private float despawnAtTime = -1f;

        public EnemyDefinition Definition => definition;

        public NavMeshAgent Agent => agent;

        public Health Health => health;

        public void ServerInitialise(int definition) => pendingDefinitionId = definition;

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

            if (agent != null) agent.enabled = false;

            DestroyView();

            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

        private void OnDefinitionChanged(int previous, int current) => ApplyDefinition(current);

        private void ApplyDefinition(int current)
        {
            if (current == viewDefinitionId) return;

            viewDefinitionId = current;
            DestroyView();

            definition = ContentViewFactory.Resolve<EnemyDefinition>(current, this);
            if (definition == null) return;

            ApplyBody(definition);

            view = ContentViewFactory.Build(definition, transform, Vector3.zero,
                Quaternion.identity, PhysicsLayers.Enemy, solid: false);

            ApplyAgent(definition);
        }

        private void ApplyBody(EnemyDefinition source)
        {
            if (body == null) return;

            body.radius = source.BodyRadius;
            body.height = Mathf.Max(source.BodyHeight, source.BodyRadius * 2f);
            body.center = new Vector3(0f, body.height * 0.5f, 0f);
        }

        private void ApplyAgent(EnemyDefinition source)
        {
            if (agent == null) return;

            if (!IsServer)
            {
                agent.enabled = false;
                return;
            }

            agent.enabled = false;

            agent.radius = source.BodyRadius;
            agent.height = source.BodyHeight;
            agent.speed = source.PatrolSpeed;
            agent.acceleration = source.Acceleration;
            agent.angularSpeed = source.TurnSpeed;

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

        private void OnVitalsChanged(VitalsState state)
        {
            ApplyAliveState();

            if (!IsServer || !state.IsDead) return;

            despawnAtTime = definition != null && definition.CorpseExpires
                ? Time.time + definition.CorpseSeconds
                : -1f;
        }

        private void ApplyAliveState()
        {
            var alive = health == null || health.IsAlive;

            if (body != null) body.enabled = alive;

            if (IsServer && agent != null && !alive && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;

                agent.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsServer || despawnAtTime < 0f || Time.time < despawnAtTime) return;

            despawnAtTime = -1f;

            if (IsSpawned) NetworkObject.Despawn();
        }
    }
}
