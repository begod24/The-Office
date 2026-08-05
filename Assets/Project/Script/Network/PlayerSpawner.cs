using System.Collections.Generic;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public sealed class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private SessionDirector director;
        [SerializeField] private GameObject playerPrefab;

        private readonly List<NetworkObject> spawned = new(4);

        private GameState lastPhase = GameState.Lobby;

        private void Awake()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
        }

        public override void OnDestroy()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            spawned.Clear();
        }

        private void OnPhaseChanged(GameState phase)
        {
            if (!IsServer)
            {
                lastPhase = phase;
                return;
            }

            var wasInRun = lastPhase == GameState.InRun;
            lastPhase = phase;

            if (phase == GameState.InRun) SpawnMissingPlayers();
            else if (wasInRun) DespawnAll();
        }

        private void SpawnMissingPlayers()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[Spawn] PlayerSpawner has no player prefab assigned.");
                return;
            }

            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                    client.PlayerObject != null)
                    continue;

                SpawnFor(clientId);
            }
        }

        private void SpawnFor(ulong clientId)
        {
            var instance = Instantiate(playerPrefab);
            var networkObject = instance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("[Spawn] Player prefab has no NetworkObject.");
                Destroy(instance);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId);
            spawned.Add(networkObject);
        }

        private void DespawnAll()
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                var networkObject = spawned[i];
                if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn();
            }

            spawned.Clear();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                var networkObject = spawned[i];

                if (networkObject == null)
                {
                    spawned.RemoveAt(i);
                    continue;
                }

                if (networkObject.OwnerClientId != clientId) continue;

                spawned.RemoveAt(i);
            }
        }
    }
}
