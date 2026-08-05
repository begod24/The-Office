using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Who is in the lobby and who has pressed ready. Server-authoritative.
    ///
    /// Lives on the in-scene <c>[Session]</c> NetworkObject in SCN_Boot rather than on a spawned
    /// prefab: the Boot scene is loaded on every client and never unloads, so the roster cannot
    /// be destroyed by a scene transition halfway through a run.
    /// </summary>
    public sealed class LobbyRoster : NetworkBehaviour
    {
        private readonly NetworkList<PlayerSlot> slots = new();

        /// <summary>Raised on every client whenever any slot is added, removed or edited.</summary>
        public event Action Changed;

        public int Count => slots.Count;

        public PlayerSlot this[int index] => slots[index];

        public bool AllReady
        {
            get
            {
                if (slots.Count == 0) return false;

                for (var i = 0; i < slots.Count; i++)
                    if (!slots[i].IsReady)
                        return false;

                return true;
            }
        }

        public bool IsReady(ulong clientId) =>
            TryFind(clientId, out var index) && slots[index].IsReady;

        public override void OnNetworkSpawn()
        {
            slots.OnListChanged += OnListChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

                // The host connects before this in-scene object spawns, so seeding from the
                // current connection list is not an optimisation — without it the host is
                // missing from its own lobby.
                foreach (var clientId in NetworkManager.ConnectedClientsIds) AddSlot(clientId);
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= OnListChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            Changed?.Invoke();
        }

        /// <summary>Client asks to flip its own ready flag. The server owns the decision.</summary>
        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;

            if (!TryFind(clientId, out var index)) return;

            var slot = slots[index];
            if (slot.IsReady == ready) return;

            slot.IsReady = ready;
            slots[index] = slot;
        }

        /// <summary>Server-side. Clears every ready flag, used when a run ends and players return.</summary>
        public void ClearReadyFlags()
        {
            if (!IsServer) return;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsReady) continue;

                slot.IsReady = false;
                slots[i] = slot;
            }
        }

        private void OnClientConnected(ulong clientId) => AddSlot(clientId);

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || !TryFind(clientId, out var index)) return;

            slots.RemoveAt(index);
        }

        private void AddSlot(ulong clientId)
        {
            if (!IsServer || TryFind(clientId, out _)) return;

            slots.Add(new PlayerSlot(clientId, BuildDisplayName(slots.Count + 1)));
        }

        /// <summary>
        /// Numbered by join order rather than by client id: NGO ids are not contiguous, and
        /// "EMPLOYEE 07" in a four-player lobby reads as a bug.
        /// </summary>
        private static FixedString32Bytes BuildDisplayName(int ordinal)
        {
            FixedString32Bytes name = default;
            name.Append("EMPLOYEE ");
            if (ordinal < 10) name.Append('0');
            name.Append(ordinal);
            return name;
        }

        private bool TryFind(ulong clientId, out int index)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].ClientId != clientId) continue;

                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        private void OnListChanged(NetworkListEvent<PlayerSlot> changeEvent) => Changed?.Invoke();

        public override void OnDestroy()
        {
            // NetworkList holds native memory. Leaking it survives play mode and shows up as a
            // native leak warning on the next domain reload. NetworkBehaviour.OnDestroy is
            // virtual and does its own cleanup, so hiding it instead of overriding would break
            // NGO in a way that only appears after a few play sessions.
            slots?.Dispose();
            base.OnDestroy();
        }
    }
}
