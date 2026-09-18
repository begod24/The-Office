using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Keeps exactly one <see cref="PersistentPlayer"/> alive per connected client, from the
    /// moment they connect to the moment they drop. Sits on PF_Session next to the roster and the
    /// body spawner, and unlike the body spawner it does not care what phase the session is in:
    /// the record exists in the lobby, through the run, and in the lobby after it.
    /// </summary>
    public sealed class PersistentPlayerSpawner : NetworkBehaviour
    {
        [Tooltip("PF_PersistentPlayer. Must be registered in the network prefab list, or clients " +
                 "cannot resolve it.")]
        [SerializeField] private GameObject persistentPlayerPrefab;

        private readonly Dictionary<ulong, NetworkObject> spawned = new(4);

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // The host connects before this object exists, so the clients already in are caught
            // here rather than by the callback that will never fire for them.
            foreach (var clientId in NetworkManager.ConnectedClientsIds) SpawnFor(clientId);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

                foreach (var instance in spawned.Values)
                    if (instance != null && instance.IsSpawned)
                        instance.Despawn();

                SeatRegistry.Clear();
            }

            spawned.Clear();
        }

        private void OnClientConnected(ulong clientId) => SpawnFor(clientId);

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (spawned.Remove(clientId, out var instance) &&
                instance != null && instance.IsSpawned)
                instance.Despawn();

            // Released only here. Until GDD §15 reconnection lands there is nothing to come back
            // to, and holding the seat would give a four-player lobby three usable seats after
            // one person's network hiccup.
            SeatRegistry.Release(clientId);
        }

        private void SpawnFor(ulong clientId)
        {
            if (!IsServer || !IsSpawned) return;
            if (spawned.ContainsKey(clientId)) return;

            if (persistentPlayerPrefab == null)
            {
                Debug.LogError("[Player] PersistentPlayerSpawner has no prefab assigned. Run " +
                               "'Office/Setup/Build Persistent Player Prefab', then rebuild the " +
                               "session prefab.", this);
                return;
            }

            var instance = Instantiate(persistentPlayerPrefab);
            var networkObject = instance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("[Player] PF_PersistentPlayer has no NetworkObject.", instance);
                Destroy(instance);
                return;
            }

            var record = instance.GetComponent<PersistentPlayer>();

            if (record == null)
            {
                Debug.LogError("[Player] PF_PersistentPlayer has no PersistentPlayer component.",
                    instance);
                Destroy(instance);
                return;
            }

            networkObject.SpawnWithOwnership(clientId);

            var seat = SeatRegistry.Take(clientId, clientId == NetworkManager.ServerClientId);
            record.ServerInitialise(seat, SeatRegistry.NameForSeat(seat));

            spawned[clientId] = networkObject;
        }
    }
}
