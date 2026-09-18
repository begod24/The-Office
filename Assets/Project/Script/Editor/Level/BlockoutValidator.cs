using System.Collections.Generic;
using System.Text;
using Office.Enemies;
using Office.Gameplay;
using Office.Network;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static Office.Editor.Level.TowerGeometry;

namespace Office.Editor.Level
{
    /// <summary>
    /// Answers the only question a blockout has to answer: can a player actually get
    /// where the level expects them to go?
    ///
    /// A screenshot cannot tell you that a doorway is 5 cm too narrow for the agent, or
    /// that a stair landed 4 cm proud of the slab above it and broke the link between two
    /// floors. A path query can, and it fails loudly instead of silently.
    /// </summary>
    internal static class BlockoutValidator
    {
        private const float SampleRadius = 1.2f;

        [MenuItem("Office/Level/Validate Blockout Reachability", priority = 60)]
        public static void Validate()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();

            if (surface == null || surface.navMeshData == null)
            {
                Debug.LogError("[Blockout] No baked NavMeshSurface in the open scene. " +
                               "Run 'Office/Level/Build Tower Blockout (Sandbox)' first.");
                return;
            }

            // A baked surface is not a loaded surface. Outside play mode the asset sits
            // on disk and NavMesh queries answer nothing anywhere, which looks exactly
            // like a level with no floor in it. Re-register the data before asking.
            surface.RemoveData();
            surface.AddData();

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = NavigationSetup.OfficeAgentTypeId,
                areaMask = NavMesh.AllAreas
            };

            var report = new StringBuilder();
            var failures = 0;

            report.AppendLine("[Blockout] Reachability report");
            report.AppendLine();

            var spawns = CollectSpawns();

            if (spawns.Count == 0)
            {
                Debug.LogError("[Blockout] No PlayerSpawnPoints in the scene. Nothing to test.");
                return;
            }

            report.AppendLine("SPAWNS");

            foreach (var spawn in spawns)
                failures += OnNavMesh(report, spawn.Key, spawn.Value, filter) ? 0 : 1;

            var origin = spawns[0].Value;

            report.AppendLine();
            report.AppendLine($"ROUTES FROM {spawns[0].Key}");

            foreach (var target in CollectTargets())
                failures += Reachable(report, origin, target.Key, target.Value, filter) ? 0 : 1;

            report.AppendLine();
            report.AppendLine(failures == 0
                ? "RESULT: every marker is reachable from spawn."
                : $"RESULT: {failures} problem(s) above. Each one is a place a player " +
                  "cannot stand or cannot walk to.");

            if (failures == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        private static List<KeyValuePair<string, Vector3>> CollectSpawns()
        {
            var results = new List<KeyValuePair<string, Vector3>>(4);
            var host = Object.FindFirstObjectByType<PlayerSpawnPoints>();

            if (host == null) return results;

            foreach (Transform child in host.transform)
                results.Add(new KeyValuePair<string, Vector3>(child.name, child.position));

            return results;
        }

        /// <summary>
        /// Everything a run has to reach: every enemy's post, the objective, and one
        /// landing on each floor above — the stairwells are only real if a path query
        /// can climb them.
        /// </summary>
        private static List<KeyValuePair<string, Vector3>> CollectTargets()
        {
            var results = new List<KeyValuePair<string, Vector3>>(24);

            foreach (var enemy in Object.FindObjectsByType<EnemyPlacement>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                results.Add(new(enemy.name, enemy.transform.position));

            foreach (var switchPoint in Object.FindObjectsByType<PowerSwitchPlacement>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                results.Add(new(switchPoint.name, switchPoint.transform.position));

            foreach (var item in Object.FindObjectsByType<ItemPlacement>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                results.Add(new(item.name, item.transform.position));

            for (var floor = 5; floor <= 8; floor++)
            {
                var y = FloorY(floor);

                results.Add(new($"F{floor}_StairA_arrival",
                    new Vector3(-18f, y, StairAZMin + 1.5f)));

                results.Add(new($"F{floor}_StairB_arrival",
                    new Vector3(15.5f, y, StairBZMax - 1.5f)));

                results.Add(new($"F{floor}_RingNorth", new Vector3(0f, y, RingZMax - 1f)));
                results.Add(new($"F{floor}_RingSouth", new Vector3(0f, y, RingZMin + 1f)));
            }

            return results;
        }

        private static bool OnNavMesh(StringBuilder report, string name, Vector3 point,
            NavMeshQueryFilter filter)
        {
            if (NavMesh.SamplePosition(point, out var hit, SampleRadius, filter))
            {
                report.AppendLine($"  ok      {name}  (nav {hit.distance:0.00} m away)");
                return true;
            }

            report.AppendLine($"  NO NAV  {name} at {point} — nothing walkable within " +
                              $"{SampleRadius} m. A player spawned here falls or freezes.");
            return false;
        }

        private static bool Reachable(StringBuilder report, Vector3 origin, string name,
            Vector3 target, NavMeshQueryFilter filter)
        {
            if (!NavMesh.SamplePosition(origin, out var from, SampleRadius, filter))
            {
                report.AppendLine($"  NO NAV  origin is off the navmesh — cannot test {name}.");
                return false;
            }

            if (!NavMesh.SamplePosition(target, out var to, SampleRadius, filter))
            {
                report.AppendLine($"  NO NAV  {name} at {target} — nothing walkable within " +
                                  $"{SampleRadius} m.");
                return false;
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from.position, to.position, filter, path);

            switch (path.status)
            {
                case NavMeshPathStatus.PathComplete:
                    report.AppendLine($"  ok      {name}  ({Length(path):0.0} m walk)");
                    return true;

                case NavMeshPathStatus.PathPartial:
                    report.AppendLine($"  BLOCKED {name} — path runs out part way. The route " +
                                      "is cut by geometry or a doorway too narrow for the agent.");
                    return false;

                default:
                    report.AppendLine($"  NO PATH {name} — no route at all from spawn.");
                    return false;
            }
        }

        private static float Length(NavMeshPath path)
        {
            var total = 0f;

            for (var i = 1; i < path.corners.Length; i++)
                total += Vector3.Distance(path.corners[i - 1], path.corners[i]);

            return total;
        }
    }
}
