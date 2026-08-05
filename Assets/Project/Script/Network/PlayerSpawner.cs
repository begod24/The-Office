using System.Collections.Generic;
using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Creates and destroys player objects around a run. Server only.
    ///
    /// NetworkManager's automatic player spawning is deliberately switched off
    /// (<c>NetworkConfig.PlayerPrefab</c> is null): it fires the instant a client connects,
    /// which in this game means spawning a capsule into the lobby, where there is no floor to
    /// stand on and nothing for it to do. Players exist only during a run.
    /// </summary>
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
            // The spawn pose is chosen by PlayerSpawnAnchor on the object itself, because the
            // owner has to apply it — movement is owner-authoritative and a server-side
            // transform write would be overwritten on the next frame.
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

                // NGO despawns a disconnected client's player object itself; this only keeps
                // the local bookkeeping from holding a destroyed reference.
                spawned.RemoveAt(i);
            }
        }
    }
}
