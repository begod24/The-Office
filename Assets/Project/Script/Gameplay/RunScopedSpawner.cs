using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Server-side owner of everything one run puts into the world from level-authored
    /// markers: spawns on the way in, despawns on the way out.
    /// </summary>
    /// <remarks>
    /// Lives on <c>PF_Session</c>, which is server-spawned and survives scene swaps — the only
    /// thing that can own run-scoped spawning. Subclasses supply the prefab, the markers and
    /// how to configure one instance; the phase handshake, the pool, the bookkeeping and the
    /// teardown are the same for every kind of content and belong here.
    /// <para>
    /// <b>Why markers rather than in-scene NetworkObjects.</b> With <c>EnableSceneManagement</c>
    /// off, NGO cannot resolve an in-scene placed NetworkObject on a remote client: it arrives
    /// as an ordinary spawn, the client finds no matching prefab, and logs
    /// <c>NetworkPrefab could not be found</c> while the host sees nothing wrong. So markers
    /// stay inert scene data on every machine and the server spawns registered prefabs from
    /// them.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPlacement">The marker component this spawner reads.</typeparam>
    public abstract class RunScopedSpawner<TPlacement> : NetworkBehaviour
        where TPlacement : Component
    {
        [SerializeField] private SessionDirector director;

        private readonly List<NetworkObject> spawned = new(32);

        private GameState lastPhase = GameState.Lobby;

        /// <summary>
        /// The registered network prefab to spawn. Declared by the subclass so the inspector
        /// field can be named after what it holds.
        /// </summary>
        protected abstract GameObject Prefab { get; }

        /// <summary>Every marker currently in a loaded scene. Never null.</summary>
        protected abstract IReadOnlyList<TPlacement> Placements { get; }

        /// <summary>Human-readable tag for this spawner's log lines, e.g. <c>Item</c>.</summary>
        protected abstract string LogCategory { get; }

        /// <summary>Everything this spawner has put into the world and not yet taken back.</summary>
        protected IReadOnlyList<NetworkObject> Spawned => spawned;

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Write whatever the instance needs to carry into
        /// its spawn payload. Returning false abandons the spawn.
        /// </summary>
        protected abstract bool TryConfigure(NetworkObject instance, TPlacement placement);

        // Awake rather than OnNetworkSpawn: the director raises the first phase during its own
        // spawn, and a subscriber that waits for the network is already too late for it.
        // Virtual rather than private so that a subclass needing its own Awake has to call
        // base — hiding this one would silently unsubscribe the spawner from the run.
        protected virtual void Awake()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
        }

        public override void OnDestroy()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
            base.OnDestroy();
        }

        public override void OnNetworkDespawn() => spawned.Clear();

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
        // client has the run scene loaded. Spawning earlier would drop objects into a scene a
        // slow machine has not finished loading.
        private void SpawnPlacements()
        {
            if (spawned.Count > 0) return;

            var placements = Placements;
            if (placements == null) return;

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null) continue;

                ServerSpawnAt(placement, placement.transform.position,
                    placement.transform.rotation);
            }
        }

        /// <summary>
        /// Server only. Spawns one instance for <paramref name="placement"/>. Returns the
        /// carrier, or null when it could not spawn.
        /// </summary>
        protected NetworkObject ServerSpawnAt(TPlacement placement, Vector3 position,
            Quaternion rotation)
        {
            var instance = ServerCreate(position, rotation);
            if (instance == null) return null;

            if (!TryConfigure(instance, placement))
            {
                ReleaseUnspawned(instance);
                return null;
            }

            instance.Spawn();
            spawned.Add(instance);
            return instance;
        }

        /// <summary>
        /// Server only. Takes an unspawned instance from the pool, or instantiates one. The
        /// caller must either <c>Spawn()</c> it — recording it with <see cref="Track"/> — or
        /// hand it back with <see cref="ReleaseUnspawned"/>.
        /// </summary>
        protected NetworkObject ServerCreate(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return null;

            var prefab = Prefab;

            if (prefab == null)
            {
                Debug.LogError($"[{LogCategory}] {GetType().Name} has no prefab assigned.", this);
                return null;
            }

            // Through the pool when one is registered, so a run that spawns and despawns the
            // same thing a hundred times allocates once. Despawn hands it back automatically —
            // NGO routes that through the prefab handler on every machine, which is the whole
            // reason the pool has to own both directions.
            var instance = ServiceLocator.TryGet<INetworkObjectPool>(out var pool)
                ? pool.Acquire(prefab, position, rotation)
                : Instantiate(prefab, position, rotation).GetComponent<NetworkObject>();

            if (instance != null) return instance;

            Debug.LogError($"[{LogCategory}] '{prefab.name}' has no NetworkObject.", this);
            return null;
        }

        /// <summary>Records an instance the subclass spawned itself, so teardown reaches it.</summary>
        protected void Track(NetworkObject instance)
        {
            if (instance != null) spawned.Add(instance);
        }

        /// <summary>
        /// Disposes of an instance that was taken but never spawned. Destroying it outright is
        /// correct even when it came from the pool: the pool only recycles through NGO's
        /// despawn path, and an unspawned object never travels it.
        /// </summary>
        protected static void ReleaseUnspawned(NetworkObject instance)
        {
            if (instance != null) Destroy(instance.gameObject);
        }

        private void DespawnAll()
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                var instance = spawned[i];
                if (instance != null && instance.IsSpawned) instance.Despawn();
            }

            spawned.Clear();
        }
    }
}
