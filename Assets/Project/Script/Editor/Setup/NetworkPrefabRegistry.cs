using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    internal static class NetworkPrefabRegistry
    {
        private const string ListPath = "Assets/DefaultNetworkPrefabs.asset";

        public static void Register(params GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);

            if (list == null)
            {
                Debug.LogError($"[Setup] {ListPath} is missing. Nothing was registered.");
                return;
            }

            var added = 0;

            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;

                if (prefab.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogError($"[Setup] '{prefab.name}' has no NetworkObject and cannot be " +
                                   "a network prefab.", prefab);
                    continue;
                }

                if (list.Contains(prefab)) continue;

                list.Add(new NetworkPrefab { Prefab = prefab });
                added++;

                Debug.Log($"[Setup] Registered '{prefab.name}' as a network prefab.", prefab);
            }

            if (added == 0) return;

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }
    }
}
