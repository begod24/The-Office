using System;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public sealed class LobbyRoster : NetworkBehaviour
    {
        private readonly NetworkList<PlayerSlot> slots = new();

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

            // Named from the seat rather than from this list's length. The two agreed only while
            // nobody ever left: a roster rebuilt after the second of three players dropped used
            // to hand the third player a name someone else was still wearing on their body.
            var seat = SeatRegistry.Take(clientId, clientId == NetworkManager.ServerClientId);

            slots.Add(new PlayerSlot(clientId, SeatRegistry.NameForSeat(seat)));
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
            slots?.Dispose();
            base.OnDestroy();
        }
    }
}
