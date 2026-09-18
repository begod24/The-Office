using System.IO;
using Office.Gameplay;
using Office.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    /// <summary>
    /// Builds PF_PersistentPlayer — the per-client record that outlives the body. It carries no
    /// art, no collider and no transform sync: it is a row in a table that happens to be a
    /// NetworkObject, and every field on it is replicated by the component alone.
    /// </summary>
    internal static class PersistentPlayerBuilder
    {
        public const string PrefabPath =
            "Assets/Project/Prefab/Player/PF_PersistentPlayer.prefab";

        [MenuItem("Office/Setup/Build Persistent Player Prefab", priority = 22)]
        public static void Build()
        {
            var root = new GameObject("PF_PersistentPlayer");

            root.AddComponent<NetworkObject>();
            root.AddComponent<PersistentPlayer>();

            var folder = Path.GetDirectoryName(PrefabPath);

            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                Directory.CreateDirectory(folder);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            NetworkPrefabRegistry.Register(Load());

            Debug.Log($"[Setup] Persistent player prefab written to {PrefabPath}.");
        }

        public static GameObject Load() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        /// <summary>
        /// Adds the reporter to the existing player prefab without rebuilding it. PF_Player_Man
        /// and PF_Player_Woman are variants of this one, so they inherit the component rather than
        /// each needing their own pass.
        /// </summary>
        [MenuItem("Office/Setup/Upgrade Player Prefab For Persistent Player", priority = 46)]
        public static void UpgradePlayerPrefab()
        {
            const string playerPath = "Assets/Project/Prefab/Player/PF_Player.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(playerPath) == null)
            {
                Debug.LogError($"[Player] '{playerPath}' does not exist. Build the player prefab " +
                               "first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(playerPath);

            try
            {
                var health = contents.GetComponent<Health>();

                if (health == null)
                {
                    Debug.LogError($"[Player] '{playerPath}' has no Health. It is not a player " +
                                   "prefab this can upgrade.");
                    return;
                }

                var reporter = contents.GetComponent<PlayerStatusReporter>()
                               ?? contents.AddComponent<PlayerStatusReporter>();

                var serialized = new SerializedObject(reporter);
                serialized.FindProperty("health").objectReferenceValue = health;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, playerPath);

                Debug.Log("[Player] PF_Player now reports its vitals to the persistent player. " +
                          "The Man and Woman variants inherit it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
