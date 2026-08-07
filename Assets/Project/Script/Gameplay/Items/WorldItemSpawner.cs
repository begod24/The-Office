using System.Collections.Generic;
using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Server-side owner of every item lying in the world: fills the run scene from the
    /// level's <see cref="ItemPlacement"/> markers, and spawns dropped items on request.
    /// </summary>
    /// <remarks>
    /// Lives on <c>PF_Session</c> next to <see cref="PlayerSpawner"/> and works the same
    /// way, for the same reason: the session object is server-spawned and survives scene
    /// swaps, so it is the only thing that can own run-scoped spawning.
    /// </remarks>
    public sealed class WorldItemSpawner : NetworkBehaviour
    {
        [SerializeField] private SessionDirector director;

        [Tooltip("The single networked carrier for every item. Must be registered in the " +
                 "network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject worldItemPrefab;

        private readonly List<NetworkObject> spawned = new(32);

        private GameState lastPhase = GameState.Lobby;

        /// <summary>The live server instance, or null off the server. Set at network spawn.</summary>
        public static WorldItemSpawner Server { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Server = null;

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
            if (IsServer) Server = this;
        }

        public override void OnNetworkDespawn()
        {
            if (ReferenceEquals(Server, this)) Server = null;

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

            if (phase == GameState.InRun) SpawnPlacements();
            else if (wasInRun) DespawnAll();
        }

        // Runs on the InRun edge, which the scene-ready handshake guarantees is after every
        // client has the run scene loaded. Spawning earlier would drop objects into a scene
        // a slow machine has not finished loading.
        private void SpawnPlacements()
        {
            if (spawned.Count > 0) return;

            foreach (var placement in ItemPlacement.All)
            {
                if (placement == null) continue;

                if (placement.Definition == null)
                {
                    Debug.LogWarning($"[Item] Placement '{placement.name}' has no definition. Skipped.",
                        placement);
                    continue;
                }

                ServerSpawn(
                    new ItemStack(placement.Definition.Id, placement.Count),
                    placement.transform.position,
                    placement.transform.rotation);
            }
        }

        /// <summary>Server only. Returns the spawned carrier, or null when it could not spawn.</summary>
        public NetworkObject ServerSpawn(ItemStack contents, Vector3 position, Quaternion rotation)
        {
            if (!IsServer || contents.IsEmpty) return null;

            if (worldItemPrefab == null)
            {
                Debug.LogError("[Item] WorldItemSpawner has no PF_WorldItem assigned.");
                return null;
            }

            var instance = Instantiate(worldItemPrefab, position, rotation);

            var item = instance.GetComponent<WorldItem>();
            var networkObject = instance.GetComponent<NetworkObject>();

            if (item == null || networkObject == null)
            {
                Debug.LogError("[Item] PF_WorldItem is missing WorldItem or NetworkObject.");
                Destroy(instance);
                return null;
            }

            // Before Spawn, so the contents ride along with the spawn message.
            item.ServerInitialise(contents);
            networkObject.Spawn();

            spawned.Add(networkObject);
            return networkObject;
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
    }
}
