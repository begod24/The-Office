using System.Collections.Generic;
using Office.Data;
using Office.Gameplay;
using Unity.Collections;
using UnityEngine;

namespace Office.Enemies
{
    // Every walker's foot probes for one frame, resolved as a single batched command instead of a
    // Physics.Raycast per leg per walker. GDD §9.1 is built on swarms, and the probes are the one
    // cost that scales with how many of them a player can see rather than with how many the server
    // is thinking about: forty walkers stepping at once is forty times the casts, on every machine
    // at once. They are all the same shape — straight down, same mask, same length — which is what
    // RaycastCommand exists for, and the batch runs on worker threads besides.
    public static class GroundProbes
    {
        private const int MinCommandsPerJob = 16;

        private static readonly List<ProceduralWalker> Walkers = new(64);
        private static readonly List<ProbeRequest> Requests = new(256);

        private static NativeArray<RaycastCommand> commands;
        private static NativeArray<RaycastHit> results;

        private static Driver driver;

        internal readonly struct ProbeRequest
        {
            public readonly ProceduralWalker Walker;
            public readonly int Leg;
            public readonly Vector3 Origin;
            public readonly float Distance;

            public ProbeRequest(ProceduralWalker walker, int leg, Vector3 origin, float distance)
            {
                Walker = walker;
                Leg = leg;
                Origin = origin;
                Distance = distance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Walkers.Clear();
            Requests.Clear();

            Release();

            driver = null;
        }

        internal static void Register(ProceduralWalker walker)
        {
            if (walker == null || Walkers.Contains(walker)) return;

            Walkers.Add(walker);
            EnsureDriver();
        }

        internal static void Unregister(ProceduralWalker walker) => Walkers.Remove(walker);

        private static void EnsureDriver()
        {
            if (driver != null) return;

            var holder = new GameObject("~GroundProbes");
            Object.DontDestroyOnLoad(holder);

            driver = holder.AddComponent<Driver>();
        }

        private static void Resolve(float deltaTime)
        {
            Requests.Clear();

            var hasViewer = TryGetViewer(out var viewer);

            for (var i = Walkers.Count - 1; i >= 0; i--)
            {
                var walker = Walkers[i];

                if (walker == null)
                {
                    Walkers.RemoveAt(i);
                    continue;
                }

                walker.CollectProbes(deltaTime, viewer, hasViewer, Requests);
            }

            var count = Requests.Count;
            if (count == 0) return;

            Ensure(count);

            var parameters = new QueryParameters(PhysicsLayers.WalkableMask, false,
                QueryTriggerInteraction.Ignore, false);

            for (var i = 0; i < count; i++)
            {
                var request = Requests[i];

                commands[i] = new RaycastCommand(request.Origin, Vector3.down, parameters,
                    request.Distance);
            }

            RaycastCommand.ScheduleBatch(commands.GetSubArray(0, count),
                results.GetSubArray(0, count), MinCommandsPerJob).Complete();

            for (var i = 0; i < count; i++)
            {
                var request = Requests[i];
                var hit = results[i];

                // A batched miss leaves the whole hit zeroed rather than nulling a collider, and a
                // real one always carries a unit normal — even a ray that starts inside something
                // reports the face it came through. So a zero normal is the miss.
                request.Walker.ApplyProbe(request.Leg, hit.normal.sqrMagnitude > 0f, hit.point.y);
            }
        }

        // The machine's own point of view. Probing is a presentation cost, so what decides whether
        // it is worth paying is the distance to the player looking at the thing — not the server's
        // idea of where anyone is.
        private static bool TryGetViewer(out Vector3 position)
        {
            var local = Health.Local;

            if (local != null)
            {
                position = local.transform.position;
                return true;
            }

            var camera = Camera.main;

            if (camera != null)
            {
                position = camera.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        private static void Ensure(int count)
        {
            if (commands.IsCreated && commands.Length >= count) return;

            Release();

            var capacity = Mathf.NextPowerOfTwo(Mathf.Max(64, count));

            commands = new NativeArray<RaycastCommand>(capacity, Allocator.Persistent);
            results = new NativeArray<RaycastHit>(capacity, Allocator.Persistent);
        }

        private static void Release()
        {
            if (commands.IsCreated) commands.Dispose();
            if (results.IsCreated) results.Dispose();

            commands = default;
            results = default;
        }

        // Runs before every walker's own LateUpdate, so each of them finds its feet already
        // resolved. Well before: ProceduralWalker sits at the default order and EnemyView at 50.
        [DefaultExecutionOrder(-200)]
        private sealed class Driver : MonoBehaviour
        {
            private void LateUpdate() => Resolve(Time.deltaTime);

            private void OnDestroy()
            {
                if (ReferenceEquals(driver, this)) driver = null;

                Release();
            }
        }
    }
}
