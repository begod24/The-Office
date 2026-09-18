using System;
using System.Collections.Generic;
using Office.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// One per connected client, spawned on connection and despawned on disconnection. It is the
    /// thing that outlives the avatar: bodies come and go with every run, and everything that has
    /// to survive that gap lives here instead.
    ///
    /// Three features in the GDD need exactly this and none of them can be built on the body —
    /// §15 reconnecting into the same run, §7.1 a dead player who stays as a spectator, and
    /// §7.3.1 a dead player's voice still carrying through the office equipment. All three ask a
    /// question about a player whose body no longer exists.
    ///
    /// The server writes every field. Clients read them to draw a roster, a nameplate or a
    /// spectator list without asking the server for anything.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PersistentPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<int> seat = new(-1);

        private readonly NetworkVariable<FixedString32Bytes> displayName = new();

        private readonly NetworkVariable<PlayerStatus> status = new(PlayerStatus.Lobby);

        private static readonly List<PersistentPlayer> Active = new(4);

        public static IReadOnlyList<PersistentPlayer> All => Active;

        public ulong ClientId => OwnerClientId;

        public int Seat => seat.Value;

        public FixedString32Bytes DisplayName => displayName.Value;

        public PlayerStatus Status => status.Value;

        public event Action<PlayerStatus> StatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public static bool TryGet(ulong clientId, out PersistentPlayer player)
        {
            for (var i = 0; i < Active.Count; i++)
            {
                if (Active[i] == null || Active[i].OwnerClientId != clientId) continue;

                player = Active[i];
                return true;
            }

            player = null;
            return false;
        }

        /// <summary>
        /// Called by the spawner on the server, before anything reads the object.
        /// </summary>
        public void ServerInitialise(int seatIndex, FixedString32Bytes name)
        {
            if (!IsServer) return;

            seat.Value = seatIndex;
            displayName.Value = name;
            status.Value = PlayerStatus.Lobby;
        }

        /// <summary>
        /// Pushed in from the body rather than read off it. Office.Network cannot see
        /// Office.Gameplay — Architecture §1 keeps the dependency pointing the other way — so the
        /// avatar reports to the record instead of the record inspecting the avatar.
        /// </summary>
        public void ServerSetStatus(PlayerStatus next)
        {
            if (!IsServer) return;

            status.Value = next;
        }

        public override void OnNetworkSpawn()
        {
            // It has to survive the lobby-to-level scene load, the same way PF_Session does. A
            // record of the player that dies with the scene the player left is not a record.
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else
                Debug.LogError("[Player] PF_PersistentPlayer must be a scene root for " +
                               "DontDestroyOnLoad to apply. It will not survive the run's scene " +
                               "load where it is.", this);

            Active.Add(this);

            status.OnValueChanged += OnStatusChanged;

            StatusChanged?.Invoke(status.Value);
        }

        public override void OnNetworkDespawn()
        {
            status.OnValueChanged -= OnStatusChanged;

            Active.Remove(this);
        }

        private void OnStatusChanged(PlayerStatus previous, PlayerStatus current) =>
            StatusChanged?.Invoke(current);

        public override string ToString() =>
            $"{displayName.Value} (seat {seat.Value}, client {OwnerClientId}, {status.Value})";
    }
}
