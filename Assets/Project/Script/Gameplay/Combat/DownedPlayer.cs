using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class DownedPlayer : NetworkBehaviour, IInteractable
    {
        private const string ColliderName = "DownedInteractVolume";

        [SerializeField] private Health health;

        [Tooltip("Radius of the volume a teammate has to put the crosshair on. Generous on " +
                 "purpose — a body on the floor is a small target, and hunting for the pixel " +
                 "that answers is not the tension this sixty seconds is meant to carry. Not so " +
                 "generous that it becomes a boulder: it is solid while someone is down.")]
        [Min(0.1f)]
        [SerializeField] private float reachRadius = 0.6f;

        [Tooltip("Height off the floor the volume is centred at. Roughly a fallen body.")]
        [SerializeField] private float reachHeight = 0.5f;

        private SphereCollider volume;

        public string Prompt => IsAvailable
            ? $"REVIVE  [{Mathf.CeilToInt(Mathf.Max(0f, health.State.BleedOutRemaining))}]"
            : string.Empty;

        public bool IsAvailable => IsSpawned && health != null && health.State.IsDowned;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();

            EnsureVolume();
        }

        public override void OnNetworkSpawn()
        {
            if (health != null) health.Changed += OnVitalsChanged;

            ApplyVolume();
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.Changed -= OnVitalsChanged;

            if (volume != null) volume.enabled = false;
        }

        public void Interact(ulong clientId)
        {
            if (!IsServer || !IsAvailable) return;

            if (!ReviverIsStanding(clientId)) return;

            if (!health.ServerRevive()) return;

            Debug.Log($"[Revive] {clientId} revived {OwnerClientId}.");
        }

        private bool ReviverIsStanding(ulong clientId)
        {
            var players = Health.SpawnedPlayerList;

            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate == null || candidate.OwnerClientId != clientId) continue;

                return candidate.State.IsStanding;
            }

            return false;
        }

        private void OnVitalsChanged(VitalsState state) => ApplyVolume();

        private void ApplyVolume()
        {
            if (volume == null) return;

            volume.enabled = IsAvailable;
        }

        private void EnsureVolume()
        {
            if (volume != null) return;

            var child = new GameObject(ColliderName) { layer = PhysicsLayers.Interactable };
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.up * reachHeight;

            volume = child.AddComponent<SphereCollider>();
            volume.radius = reachRadius;
            volume.isTrigger = false;

            volume.enabled = false;
        }
    }
}
