using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Office.Network;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public abstract class RunScopedSpawner<TPlacement> : NetworkBehaviour
        where TPlacement : Component
    {
        [SerializeField] private SessionDirector director;

        private readonly List<NetworkObject> spawned = new(32);

        private GameState lastPhase = GameState.Lobby;

        protected abstract GameObject Prefab { get; }

        protected abstract IReadOnlyList<TPlacement> Placements { get; }

        protected abstract string LogCategory { get; }

        protected IReadOnlyList<NetworkObject> Spawned => spawned;

        protected abstract bool TryConfigure(NetworkObject instance, TPlacement placement);

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

        protected NetworkObject ServerCreate(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return null;

            var prefab = Prefab;

            if (prefab == null)
            {
                Debug.LogError($"[{LogCategory}] {GetType().Name} has no prefab assigned.", this);
                return null;
            }

            var instance = ServiceLocator.TryGet<INetworkObjectPool>(out var pool)
                ? pool.Acquire(prefab, position, rotation)
                : Instantiate(prefab, position, rotation).GetComponent<NetworkObject>();

            if (instance != null) return instance;

            Debug.LogError($"[{LogCategory}] '{prefab.name}' has no NetworkObject.", this);
            return null;
        }

        protected void Track(NetworkObject instance)
        {
            if (instance != null) spawned.Add(instance);
        }

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
