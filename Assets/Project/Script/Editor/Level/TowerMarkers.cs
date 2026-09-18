using Office.Data;
using Office.Enemies;
using Office.Gameplay;
using Office.Network;
using UnityEditor;
using UnityEngine;
using static Office.Editor.Level.TowerGeometry;

namespace Office.Editor.Level
{
    /// <summary>
    /// Everything the concept art's legend puts on the plan: player spawns, enemy
    /// encounters, objectives, keys and loot. All of it on floor 5, because that is the
    /// floor the playtest walks.
    ///
    /// Placement rules the drawing implies but does not state: enemies go where there is
    /// room to walk, never in a doorway; nothing large spawns inside the player's first
    /// sightline; and every encounter is reachable by two routes, because the ring means
    /// there is always a way round.
    /// </summary>
    internal static class TowerMarkers
    {
        private const string ItemFolder = "Assets/Project/ScriptableObject/Items";
        private const string TargetFolder = "Assets/Project/ScriptableObject/Props";
        private const string EnemyFolder = "Assets/Project/ScriptableObject/Enemies";

        internal static void Build(Transform root)
        {
            var baseY = FloorY(5);

            BuildSpawnPoints(root, baseY);
            BuildEnemies(BlockoutKit.Group(root, "EnemyPlacements"), baseY);
            BuildItems(BlockoutKit.Group(root, "ItemPlacements"), baseY);
            BuildTargets(BlockoutKit.Group(root, "TargetPlacements"), baseY);
            BuildObjectives(BlockoutKit.Group(root, "Objectives"), baseY);
        }

        /// <summary>
        /// The four spawns sit in the cubicle floor — the desks the team fell asleep at
        /// (GDD §5.1). They face north, up the open space towards the ring, so the first
        /// thing a player sees on spawn is the route out of the room.
        /// </summary>
        private static void BuildSpawnPoints(Transform root, float baseY)
        {
            var host = new GameObject("SpawnPoints");
            host.transform.SetParent(root, false);

            var component = host.AddComponent<PlayerSpawnPoints>();
            var offsets = new[] { -9f, -6f, -3f, 0f };
            var points = new Object[offsets.Length];

            for (var i = 0; i < offsets.Length; i++)
            {
                var point = BlockoutKit.Marker(host.transform, $"Spawn_{i + 1}",
                    new Vector3(offsets[i], baseY, -13f));

                points[i] = point.transform;
            }

            var serialized = new SerializedObject(component);
            var array = serialized.FindProperty("points");

            array.arraySize = points.Length;

            for (var i = 0; i < points.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEnemies(Transform parent, float baseY)
        {
            // The ceiling fan is 4.6 m across its legs — it only fits in an open space, so
            // it goes in the dev floor and never in the ring or a doorway.
            Enemy(parent, "Enemy_CeilingFan_Dev", "ENM_CeilingFan", new Vector3(-6f, baseY, 12f));
            Enemy(parent, "Enemy_Stapler_Dev", "ENM_Stapler", new Vector3(-11f, baseY, 15f));
            Enemy(parent, "Enemy_Projector_Conference", "ENM_Projector",
                new Vector3(3f, baseY, 14f), 180f);
            Enemy(parent, "Enemy_WaterCooler_LiftLobby", "ENM_WaterCooler",
                new Vector3(-14f, baseY, 2f), 90f);
            Enemy(parent, "Enemy_Stapler_RingNorthEast", "ENM_Stapler",
                new Vector3(9f, baseY, 7f));
            Enemy(parent, "Enemy_Stapler_BreakRoom", "ENM_Stapler",
                new Vector3(-17f, baseY, -13f));
            Enemy(parent, "Enemy_Stapler_Admin", "ENM_Stapler", new Vector3(17f, baseY, 10f));
        }

        private static void BuildItems(Transform parent, float baseY)
        {
            Item(parent, "Place_Keycards_StorageEast", "ITM_Keycard",
                new Vector3(18f, baseY + 0.6f, 5f), 2);
            Item(parent, "Place_Coffee_BreakRoom", "ITM_CoffeeCup",
                new Vector3(-19f, baseY + 0.9f, -11f), 3);
            Item(parent, "Place_Stapler_Cubicles", "ITM_Stapler",
                new Vector3(2f, baseY + 0.9f, -12f), 1);
            Item(parent, "Place_LaserPointer_Conference", "ITM_LaserPointer",
                new Vector3(0f, baseY + 0.9f, 13f), 1);
            Item(parent, "Place_Stapler_Admin", "ITM_Stapler",
                new Vector3(19f, baseY + 0.9f, 10f), 1);
        }

        private static void BuildTargets(Transform parent, float baseY)
        {
            Target(parent, "Target_Cabinet_A", "TGT_FilingCabinet", new Vector3(12f, baseY, -15f));
            Target(parent, "Target_Cabinet_B", "TGT_FilingCabinet", new Vector3(14f, baseY, -15f));
            Target(parent, "Target_Anomaly_RingSouthWest", "TGT_Anomaly",
                new Vector3(-9f, baseY, -7f));
        }

        /// <summary>
        /// Restore Power is the vertical slice's only objective (GDD §16). The switch sits
        /// in the server closet on the far side of the lift lobby, so reaching it means
        /// crossing the floor rather than walking a corridor.
        /// </summary>
        private static void BuildObjectives(Transform parent, float baseY)
        {
            var marker = BlockoutKit.Marker(parent, "Objective_RestorePower",
                new Vector3(-19.5f, baseY + 1.2f, -6f), 90f);

            var placement = marker.AddComponent<PowerSwitchPlacement>();
            var serialized = new SerializedObject(placement);

            serialized.FindProperty("zoneId").intValue = 0;
            serialized.FindProperty("completesRun").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Enemy(Transform parent, string name, string definitionName,
            Vector3 position, float yaw = 0f)
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                $"{EnemyFolder}/{definitionName}.asset");

            if (!Found(definition, definitionName, "Build Enemy Content")) return;

            var marker = BlockoutKit.Marker(parent, name, position, yaw);
            Wire(marker.AddComponent<EnemyPlacement>(), definition);
        }

        private static void Item(Transform parent, string name, string definitionName,
            Vector3 position, int count)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                $"{ItemFolder}/{definitionName}.asset");

            if (!Found(definition, definitionName, "Build Sample Items")) return;

            var marker = BlockoutKit.Marker(parent, name, position);
            var placement = marker.AddComponent<ItemPlacement>();

            var serialized = new SerializedObject(placement);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("count").intValue = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Target(Transform parent, string name, string definitionName,
            Vector3 position)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TargetDefinition>(
                $"{TargetFolder}/{definitionName}.asset");

            if (!Found(definition, definitionName, "Build Combat Content")) return;

            var marker = BlockoutKit.Marker(parent, name, position);
            Wire(marker.AddComponent<TargetPlacement>(), definition);
        }

        private static void Wire(Component placement, Object definition)
        {
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Found(Object definition, string definitionName, string menuCommand)
        {
            if (definition != null) return true;

            Debug.LogWarning($"[Blockout] '{definitionName}' not found — run " +
                             $"'Office/Content/{menuCommand}'. Marker skipped.");
            return false;
        }
    }
}
