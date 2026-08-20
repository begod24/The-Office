using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Office.Gameplay
{
    public sealed class ImpactEffectPool : IImpactEffectPool
    {
        private readonly Dictionary<ImpactEffect, ObjectPool<ImpactEffect>> pools = new(8);

        private readonly Dictionary<ImpactEffect, ObjectPool<ImpactEffect>> owners = new(64);

        private readonly Action<ImpactEffect> onFinished;

        private Transform root;

        public ImpactEffectPool() => onFinished = Release;

        public void Play(ImpactEffect prefab, Vector3 position, Vector3 normal, AudioClip clip,
            float volume = 1f)
        {
            if (prefab == null) return;

            var instance = Resolve(prefab).Get();
            if (instance == null) return;

            var rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            instance.Play(position, rotation, clip, volume);
        }

        public void Prewarm(ImpactEffect prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var pool = Resolve(prefab);

            var taken = new ImpactEffect[count];

            for (var i = 0; i < count; i++) taken[i] = pool.Get();
            for (var i = 0; i < count; i++) pool.Release(taken[i]);
        }

        public void Clear()
        {
            foreach (var pool in pools.Values) pool.Clear();

            pools.Clear();
            owners.Clear();

            if (root != null) Object.Destroy(root.gameObject);
            root = null;
        }

        private ObjectPool<ImpactEffect> Resolve(ImpactEffect prefab)
        {
            if (pools.TryGetValue(prefab, out var existing)) return existing;

            ObjectPool<ImpactEffect> pool = null;

            pool = new ObjectPool<ImpactEffect>(
                createFunc: () => Create(prefab, pool),
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 64);

            pools[prefab] = pool;
            return pool;
        }

        private ImpactEffect Create(ImpactEffect prefab, ObjectPool<ImpactEffect> pool)
        {
            var instance = Object.Instantiate(prefab, EnsureRoot());

            instance.Finished += onFinished;
            owners[instance] = pool;

            return instance;
        }

        private static void OnRelease(ImpactEffect instance)
        {
            if (instance == null) return;

            instance.StopImmediate();
            instance.gameObject.SetActive(false);
        }

        private void OnDestroyInstance(ImpactEffect instance)
        {
            if (instance == null) return;

            instance.Finished -= onFinished;
            owners.Remove(instance);

            Object.Destroy(instance.gameObject);
        }

        private void Release(ImpactEffect instance)
        {
            if (instance == null) return;

            if (owners.TryGetValue(instance, out var pool))
            {
                pool.Release(instance);
                return;
            }

            Object.Destroy(instance.gameObject);
        }

        private Transform EnsureRoot()
        {
            if (root != null) return root;

            var holder = new GameObject("~ImpactEffects");
            Object.DontDestroyOnLoad(holder);

            root = holder.transform;
            return root;
        }
    }
}
