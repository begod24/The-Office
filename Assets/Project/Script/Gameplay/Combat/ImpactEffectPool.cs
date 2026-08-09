using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Office.Gameplay
{
    /// <summary>
    /// One pool per effect prefab, parked under a root that outlives scene loads.
    /// </summary>
    /// <remarks>
    /// Built on <see cref="ObjectPool{T}"/> from <c>UnityEngine.Pool</c> rather than on a
    /// hand-written queue. The generic pooling problem — capacity, collection checks, release
    /// callbacks — is solved in the engine, and the part worth writing is the part that is
    /// actually ours: which prefab, where the instances live, and when they come back.
    /// <para>
    /// <b>Instances live under a persistent root</b>, for the reason
    /// <c>NetworkObjectPool</c> documents at length: a pooled object is a real GameObject in
    /// whatever scene created it, and a run ends by unloading that scene. Without the root,
    /// every parked effect would be destroyed and the pool would hand out Unity-nulls that
    /// look fine to <c>CountInactive</c>.
    /// </para>
    /// </remarks>
    public sealed class ImpactEffectPool : IImpactEffectPool
    {
        private readonly Dictionary<ImpactEffect, ObjectPool<ImpactEffect>> pools = new(8);

        /// <summary>
        /// Which pool a live instance belongs to. A finished effect knows only itself, and
        /// walking every pool to find its home would turn a per-hit callback into a scan.
        /// </summary>
        private readonly Dictionary<ImpactEffect, ObjectPool<ImpactEffect>> owners = new(64);

        // One delegate for the whole service rather than one per instance: subscribing the
        // Finished event would otherwise allocate a closure for every effect ever created.
        private readonly Action<ImpactEffect> onFinished;

        private Transform root;

        public ImpactEffectPool() => onFinished = Release;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Prewarm(ImpactEffect prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var pool = Resolve(prefab);

            // Get-then-release is the only way to fill an ObjectPool<T>. Safe here because
            // nothing plays until Play is called, and Get leaves the instance inactive-then-
            // active only for the moment between the two loops.
            var taken = new ImpactEffect[count];

            for (var i = 0; i < count; i++) taken[i] = pool.Get();
            for (var i = 0; i < count; i++) pool.Release(taken[i]);
        }

        /// <inheritdoc />
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
                // On: releasing the same effect twice is a real bug that otherwise surfaces
                // much later, as one effect playing in two places at once.
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 64);

            pools[prefab] = pool;
            return pool;
        }

        // The pool reference is captured rather than passed, because ObjectPool<T> hands the
        // create function no arguments and an instance has to know its home before it is ever
        // handed out. The closure reads the variable, not a copy of it, and ObjectPool<T> never
        // calls createFunc from its own constructor — so by the first Get it is assigned.
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

            // Orphaned — its pool was cleared while it was still playing. Destroying it is the
            // only correct answer; putting it back would resurrect a pool that is gone.
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
