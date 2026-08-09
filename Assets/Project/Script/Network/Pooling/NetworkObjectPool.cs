using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// The pool itself: one queue per prefab, plus the NGO handler that keeps clients using
    /// the same queue.
    /// </summary>
    /// <remarks>
    /// <b>Why parked instances live under a persistent root.</b> A pooled object is a real
    /// GameObject sitting inactive in whatever scene it happened to be created in. Runs end
    /// by unloading the run scene, which would destroy every parked instance and leave the
    /// queue full of Unity-null entries that look fine to <c>Count</c> and blow up on use.
    /// Moving each one to <c>DontDestroyOnLoad</c> makes the pool outlive the thing it is
    /// pooling for. See <see cref="Park"/> for why it is done that way and not by reparenting
    /// them under a tidy root.
    /// </remarks>
    public sealed class NetworkObjectPool : INetworkObjectPool
    {
        private readonly Dictionary<GameObject, Queue<NetworkObject>> queues = new();
        private readonly Dictionary<GameObject, PooledPrefabHandler> handlers = new();

        private NetworkManager manager;

        /// <summary>
        /// Registers <paramref name="prefab"/> with NGO and optionally fills the queue up
        /// front. Idempotent: a host runs both the server and client start paths.
        /// </summary>
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

            // Both ends must agree, and for opposite reasons: the server so its own spawns
            // recycle, the client so an arriving spawn message does not Instantiate.
            manager.PrefabHandler.AddHandler(prefab, handler);

            for (var i = 0; i < prewarm; i++)
            {
                var instance = Create(prefab, Vector3.zero, Quaternion.identity);
                if (instance == null) break;

                Park(instance);
                queues[prefab].Enqueue(instance);
            }
        }

        /// <summary>Hands every prefab back to NGO and destroys what is parked.</summary>
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

        /// <inheritdoc />
        public NetworkObject Acquire(GameObject prefab, Vector3 position, Quaternion rotation) =>
            Take(prefab, position, rotation);

        // Called by the handler on clients, and by Acquire on the server. One path, so the
        // two cannot drift.
        internal NetworkObject Take(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!queues.TryGetValue(prefab, out var queue))
                return Create(prefab, position, rotation);

            while (queue.Count > 0)
            {
                var pooled = queue.Dequeue();

                // Something destroyed it behind our back. Drop it and keep looking rather
                // than handing back a Unity-null.
                if (pooled == null) continue;

                pooled.transform.SetPositionAndRotation(position, rotation);

                Reset(pooled);
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return Create(prefab, position, rotation);
        }

        // Called by the handler on every machine when the object despawns.
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

        /// <summary>Puts an instance to sleep somewhere a scene unload cannot reach it.</summary>
        /// <remarks>
        /// <b>Not by reparenting.</b> NGO watches <c>OnTransformParentChanged</c> to replicate
        /// hierarchy changes, and refuses them on an object that is not spawned: it logs
        /// "NetworkObject can only be re-parented after being spawned" and then <em>reverts</em>
        /// the change. A parked object would therefore stay in the run scene and be destroyed
        /// with it — the exact failure the parking was meant to prevent, with error spam on top.
        /// <para>
        /// <see cref="Object.DontDestroyOnLoad"/> moves the object to another scene without
        /// touching its parent, which is the part that was actually needed. It only works on
        /// root objects, and everything pooled here is one.
        /// </para>
        /// </remarks>
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

        // A reused body carries whatever momentum it had when it despawned. Left alone, a
        // dropped item would reappear already flying.
        private static void Reset(NetworkObject instance)
        {
            if (!instance.TryGetComponent<Rigidbody>(out var body)) return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// The NGO side of one pooled prefab. NGO owns creation and destruction of networked
        /// objects, so pooling is only possible by answering these two calls.
        /// </summary>
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
