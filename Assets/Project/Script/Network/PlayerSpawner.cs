using System.Collections.Generic;
using Office.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Office.Network
{
    public sealed class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private SessionDirector director;

        [Tooltip("Prefab for the even seats — the host is always seat 0.")]
        [FormerlySerializedAs("playerPrefab")]
        [SerializeField] private GameObject manPrefab;

        [Tooltip("Prefab for the odd seats. Falls back to the man prefab when empty.")]
        [SerializeField] private GameObject womanPrefab;

        private readonly List<NetworkObject> spawned = new(4);

        // Seat per client, kept across runs so a player keeps their character.
        private readonly Dictionary<ulong, int> seats = new(4);

        private GameState lastPhase = GameState.Lobby;

        private void Awake()
        {
            if (director == null) return;

            director.PhaseChanged += OnPhaseChanged;
            director.ClientReadyDuringRun += OnClientReadyDuringRun;
        }

        public override void OnDestroy()
        {
            if (director != null)
            {
                director.PhaseChanged -= OnPhaseChanged;
                director.ClientReadyDuringRun -= OnClientReadyDuringRun;
            }

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
            seats.Clear();
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
            if (manPrefab == null)
            {
                Debug.LogError("[Spawn] PlayerSpawner has no player prefab assigned.");
                return;
            }

            foreach (var clientId in NetworkManager.ConnectedClientsIds)
                if (NeedsBody(clientId))
                    SpawnFor(clientId);
        }

        /// <summary>
        /// A client that reported its scene ready while the run was already going. Spawning
        /// hangs off the InRun edge, and a late joiner never produces one — without this they
        /// sit in the run scene as a camera with no body.
        /// </summary>
        private void OnClientReadyDuringRun(ulong clientId)
        {
            if (!IsServer || !IsSpawned) return;

            if (manPrefab == null)
            {
                Debug.LogError("[Spawn] PlayerSpawner has no player prefab assigned.");
                return;
            }

            if (!NeedsBody(clientId)) return;

            SpawnFor(clientId);
        }

        // Connected and bodiless. The connection half matters for the late-join path: a
        // client can disconnect between reporting its scene ready and this running.
        private bool NeedsBody(ulong clientId) =>
            NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject == null;

        private void SpawnFor(ulong clientId)
        {
            var prefab = TakeSeat(clientId) % 2 == 0 || womanPrefab == null
                ? manPrefab
                : womanPrefab;

            var instance = Instantiate(prefab);
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

        // Seats alternate man / woman, so a four player session reads man, woman,
        // man, woman. The host holds seat 0; a seat freed by a leaver is reused.
        private int TakeSeat(ulong clientId)
        {
            if (seats.TryGetValue(clientId, out var seat)) return seat;

            seat = clientId == NetworkManager.ServerClientId ? 0 : 1;
            while (seats.ContainsValue(seat)) seat++;

            seats[clientId] = seat;
            return seat;
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
            seats.Remove(clientId);

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
