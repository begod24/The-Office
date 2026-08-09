using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The single networked carrier for anything in the world that can be hit but does not
    /// think: breakable props and practice targets.
    /// </summary>
    /// <remarks>
    /// Shaped exactly like <see cref="WorldItem"/>, and for the same reasons. One registered
    /// prefab — <c>PF_Target</c> — carries a replicated <see cref="TargetDefinition"/> id; what
    /// the player sees is the definition's view prefab, built locally on every machine. A new
    /// target is an asset and a mesh, never a netcode change.
    /// <para>
    /// Damage itself is not implemented here. <see cref="Health"/> already owns authority,
    /// replication and the resistance table, and it is configured from the definition before
    /// the object spawns — which is why this class has no <see cref="IDamageable"/> of its own
    /// and no second copy of the rules.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DamageableTarget : NetworkBehaviour
    {
        [SerializeField] private Health health;

        private readonly NetworkVariable<int> definitionId = new();

        private int pendingDefinitionId = ContentDefinition.NoId;
        private int viewDefinitionId = ContentDefinition.NoId;
        private GameObject view;
        private TargetDefinition definition;

        // Server only. Negative means nothing is pending.
        private float respawnAtTime = -1f;

        public TargetDefinition Definition => definition;

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Applied in <see cref="OnNetworkSpawn"/> so it
        /// rides the spawn payload rather than arriving as a delta a late client could miss.
        /// </summary>
        public void ServerInitialise(int definition) => pendingDefinitionId = definition;

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingDefinitionId != ContentDefinition.NoId)
                definitionId.Value = pendingDefinitionId;

            // Consumed. This carrier is pooled, so the same instance comes back for the next
            // target and must not re-apply what the last spawn asked for.
            pendingDefinitionId = ContentDefinition.NoId;
            respawnAtTime = -1f;

            definitionId.OnValueChanged += OnDefinitionChanged;
            ApplyDefinition(definitionId.Value);

            if (health != null) health.Changed += OnVitalsChanged;

            ApplyAliveState();
        }

        public override void OnNetworkDespawn()
        {
            definitionId.OnValueChanged -= OnDefinitionChanged;

            if (health != null) health.Changed -= OnVitalsChanged;

            DestroyView();

            // The cache has to go with the view: the same pooled instance coming back for the
            // same definition would otherwise take the early-out in ApplyDefinition, skip the
            // rebuild, and spawn something with no mesh and no collider.
            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

        // ------------------------------------------------------------------ configuration

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Pushes the definition's numbers into
        /// <see cref="Health"/>.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="ServerInitialise"/> and called by the spawner, because it
        /// has to happen while the object is still unspawned — <see cref="Health"/> refuses a
        /// late configure rather than letting two clients disagree about how tough something is.
        /// </remarks>
        public bool ServerConfigureHealth(TargetDefinition source)
        {
            if (source == null || health == null) return false;

            health.ServerConfigure(source.MaxHealth, source.Responses);
            return true;
        }

        private void OnDefinitionChanged(int previous, int current) => ApplyDefinition(current);

        private void ApplyDefinition(int current)
        {
            if (current == viewDefinitionId) return;

            viewDefinitionId = current;
            DestroyView();

            definition = ContentViewFactory.Resolve<TargetDefinition>(current, this);
            if (definition == null) return;

            // Solid: the collider is what the attack probe hits, and Prop is the layer
            // PhysicsLayers.AttackMask looks for.
            view = ContentViewFactory.Build(definition, transform, Vector3.zero,
                Quaternion.identity, PhysicsLayers.Prop, solid: true);
        }

        private void DestroyView()
        {
            if (view == null) return;

            Destroy(view);
            view = null;
        }

        // ------------------------------------------------------------------ broken state

        private void OnVitalsChanged(VitalsState state)
        {
            ApplyAliveState();

            if (!IsServer || !state.IsDead) return;

            // Nothing scheduled for a prop that is meant to stay broken. The run ending
            // despawns it, and the next run spawns it whole from the marker again.
            respawnAtTime = definition != null && definition.Respawns
                ? Time.time + definition.RespawnSeconds
                : -1f;
        }

        // Driven by replicated vitals, so it runs identically on every machine and the broken
        // state never has to be sent separately.
        private void ApplyAliveState()
        {
            if (view == null) return;

            var alive = health == null || health.IsAlive;
            if (view.activeSelf != alive) view.SetActive(alive);
        }

        // Server only, and only while something is actually waiting to come back.
        private void Update()
        {
            if (!IsServer || respawnAtTime < 0f || Time.time < respawnAtTime) return;

            respawnAtTime = -1f;

            if (health != null) health.ServerRestore();
        }
    }
}
