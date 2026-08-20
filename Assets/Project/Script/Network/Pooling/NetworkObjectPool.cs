using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public sealed class NetworkObjectPool : INetworkObjectPool
    {
        private readonly Dictionary<GameObject, Queue<NetworkObject>> queues = new();
        private readonly Dictionary<GameObject, PooledPrefabHandler> handlers = new();

        private NetworkManager manager;

        public void Register(NetworkManager networkManager, GameObject prefab, int prewarm)
        {
            if (networkManager == null || prefab == null) return;
            if (handlers.ContainsKey(prefab)) return;

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[Pool] '{prefab.name}' has no NetworkObject and cannot be pooled.");
                return;
            }

            manager = networkManager;

            var handler = new PooledPrefabHandler(this, prefab);

            handlers[prefab] = handler;
            queues[prefab] = new Queue<NetworkObject>(Mathf.Max(4, prewarm));

            manager.PrefabHandler.AddHandler(prefab, handler);

            for (var i = 0; i < prewarm; i++)
            {
                var instance = Create(prefab, Vector3.zero, Quaternion.identity);
                if (instance == null) break;

                Park(instance);
                queues[prefab].Enqueue(instance);
            }
        }

        public void Clear()
        {
            foreach (var pair in handlers)
                if (manager != null && manager.PrefabHandler != null)
                    manager.PrefabHandler.RemoveHandler(pair.Key);

            foreach (var queue in queues.Values)
                while (queue.Count > 0)
                {
                    var parked = queue.Dequeue();
                    if (parked != null) Object.Destroy(parked.gameObject);
                }

            handlers.Clear();
            queues.Clear();

            manager = null;
        }

        public bool IsPooled(GameObject prefab) => prefab != null && queues.ContainsKey(prefab);

        public NetworkObject Acquire(GameObject prefab, Vector3 position, Quaternion rotation) =>
            Take(prefab, position, rotation);

        internal NetworkObject Take(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!queues.TryGetValue(prefab, out var queue))
                return Create(prefab, position, rotation);

            while (queue.Count > 0)
            {
                var pooled = queue.Dequeue();

                if (pooled == null) continue;

                pooled.transform.SetPositionAndRotation(position, rotation);

                Reset(pooled);
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return Create(prefab, position, rotation);
        }

        internal void Return(GameObject prefab, NetworkObject instance)
        {
            if (instance == null) return;

            if (prefab == null || !queues.TryGetValue(prefab, out var queue))
            {
                Object.Destroy(instance.gameObject);
                return;
            }

            Park(instance);
            queue.Enqueue(instance);
        }

        private static void Park(NetworkObject instance)
        {
            if (instance == null) return;

            instance.gameObject.SetActive(false);

            if (instance.transform.parent != null)
            {
                Debug.LogWarning($"[Pool] '{instance.name}' was returned while parented to " +
                                 $"'{instance.transform.parent.name}'. It stays in that scene and " +
                                 "will be destroyed when the scene unloads. Detaching it here is " +
                                 "not possible — NGO rejects reparenting an unspawned object.");
                return;
            }

            Object.DontDestroyOnLoad(instance.gameObject);
        }

        private NetworkObject Create(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            return instance.GetComponent<NetworkObject>();
        }

        private static void Reset(NetworkObject instance)
        {
            if (!instance.TryGetComponent<Rigidbody>(out var body)) return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private sealed class PooledPrefabHandler : INetworkPrefabInstanceHandler
        {
            private readonly NetworkObjectPool pool;
            private readonly GameObject prefab;

            public PooledPrefabHandler(NetworkObjectPool pool, GameObject prefab)
            {
                this.pool = pool;
                this.prefab = prefab;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position,
                Quaternion rotation) =>
                pool.Take(prefab, position, rotation);

            public void Destroy(NetworkObject networkObject) => pool.Return(prefab, networkObject);
        }
    }
}
