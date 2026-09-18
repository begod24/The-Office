using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Mirrors this body's vitals onto the client's <see cref="PersistentPlayer"/>, which outlives
    /// it.
    ///
    /// Pushed from here rather than pulled from there: Office.Network cannot see Office.Gameplay
    /// — Architecture §1 keeps the dependency pointing this way — so the body reports to the
    /// record instead of the record inspecting the body. It also means the record needs no
    /// knowledge of what a body is, which is the point of it surviving one.
    ///
    /// Server only. The status is a NetworkVariable the server writes; every client reads the
    /// result rather than computing its own.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerStatusReporter : NetworkBehaviour
    {
        [SerializeField] private Health health;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer || health == null) return;

            health.Changed += OnVitalsChanged;

            Report(health.State);
        }

        public override void OnNetworkDespawn()
        {
            if (health != null) health.Changed -= OnVitalsChanged;

            if (!IsServer) return;

            // The body is gone — end of the run, or a death that despawned it. Whoever owned it
            // is back to being a connected player with no body, which is what Lobby means.
            if (PersistentPlayer.TryGet(OwnerClientId, out var record))
                record.ServerSetStatus(PlayerStatus.Lobby);
        }

        private void OnVitalsChanged(VitalsState state) => Report(state);

        private void Report(VitalsState state)
        {
            // Absent rather than an error: the record despawns the moment its client drops, and
            // the body outlives it by the frame or two it takes the spawner to notice.
            if (!PersistentPlayer.TryGet(OwnerClientId, out var record)) return;

            record.ServerSetStatus(StatusFor(state));
        }

        private static PlayerStatus StatusFor(VitalsState state)
        {
            if (state.IsDead) return PlayerStatus.Dead;

            return state.IsDowned ? PlayerStatus.Downed : PlayerStatus.Alive;
        }
    }
}
