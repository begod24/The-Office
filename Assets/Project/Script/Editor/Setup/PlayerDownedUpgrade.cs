using Office.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    internal static class PlayerDownedUpgrade
    {
        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/Project/Prefab/Player/PF_Player.prefab",
            "Assets/Project/Prefab/Player/PF_Player_Man.prefab",
            "Assets/Project/Prefab/Player/PF_Player_Woman.prefab"
        };

        [MenuItem("Office/Setup/Upgrade Player Prefabs For Revive", priority = 45)]
        public static void Upgrade()
        {
            var upgraded = 0;

            foreach (var path in PlayerPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Revive] '{path}' does not exist. Skipped.");
                    continue;
                }

                if (UpgradeOne(path)) upgraded++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[Revive] {upgraded} player prefab(s) now carry DownedPlayer and " +
                      "SpectatorCamera, with PlayerRig reading the same Health.");
        }

        private static bool UpgradeOne(string path)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var health = contents.GetComponent<Health>();
                var rig = contents.GetComponent<PlayerRig>();

                if (health == null || rig == null)
                {
                    Debug.LogError($"[Revive] '{path}' has no Health or no PlayerRig. It is not " +
                                   "a player prefab this can upgrade.");
                    return false;
                }

                var downed = contents.GetComponent<DownedPlayer>()
                             ?? contents.AddComponent<DownedPlayer>();

                var spectator = contents.GetComponent<SpectatorCamera>()
                                ?? contents.AddComponent<SpectatorCamera>();

                Wire(rig, ("health", health));
                Wire(downed, ("health", health));
                Wire(spectator, ("rig", rig), ("health", health));

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Revive] '{target.GetType().Name}' has no field '{field}'.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
