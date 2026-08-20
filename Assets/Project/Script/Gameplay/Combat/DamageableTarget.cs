using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
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

        private float respawnAtTime = -1f;

        public TargetDefinition Definition => definition;

        public void ServerInitialise(int definition) => pendingDefinitionId = definition;

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingDefinitionId != ContentDefinition.NoId)
                definitionId.Value = pendingDefinitionId;

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

            viewDefinitionId = ContentDefinition.NoId;
            definition = null;
        }

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

            view = ContentViewFactory.Build(definition, transform, Vector3.zero,
                Quaternion.identity, PhysicsLayers.Prop, solid: true);
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

            respawnAtTime = definition != null && definition.Respawns
                ? Time.time + definition.RespawnSeconds
                : -1f;
        }

        private void ApplyAliveState()
        {
            if (view == null) return;

            var alive = health == null || health.IsAlive;
            if (view.activeSelf != alive) view.SetActive(alive);
        }

        private void Update()
        {
            if (!IsServer || respawnAtTime < 0f || Time.time < respawnAtTime) return;

            respawnAtTime = -1f;

            if (health != null) health.ServerRestore();
        }
    }
}
