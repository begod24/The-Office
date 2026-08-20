using System.Collections.Generic;
using System.IO;
using System.Linq;
using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    internal static class ItemContentBuilder
    {
        private const string ItemDefinitionFolder = "Assets/Project/ScriptableObject/Items";
        private const string RegistryPath = "Assets/Project/ScriptableObject/Config/REG_Definitions.asset";

        private const string ItemPrefabFolder = "Assets/Project/Prefab/Items";
        private const string WorldItemPrefabPath = ItemPrefabFolder + "/PF_WorldItem.prefab";

        private const string IconFolder = "Assets/Project/Art/Textures/Items";
        private const string MaterialFolder = "Assets/Project/Art/Materials/Items";

        private const int IconSize = 64;

        [MenuItem("Office/Content/Build All (samples, carrier, registry)", priority = 0)]
        public static void BuildAll()
        {
            BuildSampleItems();

            CombatContentBuilder.BuildAll();

            BuildWorldItemPrefab();
            RebuildRegistry();

            AssetDatabase.SaveAssets();
            Debug.Log("[Content] Done. Rebuild the session prefab and the boot scene to pick it up.");
        }

        [MenuItem("Office/Content/Build World Item Prefab", priority = 20)]
        public static void BuildWorldItemPrefab()
        {
            var root = new GameObject("PF_WorldItem") { layer = PhysicsLayers.Interactable };

            var networkObject = root.AddComponent<NetworkObject>();

            networkObject.SynchronizeTransform = true;

            root.AddComponent<WorldItem>();

            EnsureFolder(ItemPrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, WorldItemPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            NetworkPrefabRegistry.Register(
                AssetDatabase.LoadAssetAtPath<GameObject>(WorldItemPrefabPath));

            Debug.Log($"[Content] Carrier prefab written to {WorldItemPrefabPath}. " +
                      "It is the only network prefab items will ever need.");
        }

        public static GameObject LoadWorldItemPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(WorldItemPrefabPath);

        [MenuItem("Office/Content/Rebuild Definition Registry", priority = 30)]
        public static void RebuildRegistry()
        {
            var all = LoadAll<ContentDefinition>();

            AssignIds(all);

            var registry = CreateOrLoad<DefinitionRegistry>(RegistryPath);
            var serialized = new SerializedObject(registry);

            WriteArray(serialized, "definitions", all);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            registry.Invalidate();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            var breakdown = all
                .GroupBy(definition => definition.GetType().Name)
                .OrderBy(group => group.Key, System.StringComparer.Ordinal)
                .Select(group => $"{group.Count()} {group.Key}");

            Debug.Log($"[Content] Registry rebuilt: {all.Length} definitions " +
                      $"({string.Join(", ", breakdown)}).");
        }

        public static DefinitionRegistry LoadRegistry() =>
            AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(RegistryPath);

        private static void AssignIds(ContentDefinition[] definitions)
        {
            var taken = new Dictionary<int, ContentDefinition>(definitions.Length);
            var next = 1;

            foreach (var definition in definitions)
            {
                if (!definition.HasValidId) continue;

                if (taken.TryGetValue(definition.Id, out var owner))
                {
                    Debug.LogWarning(
                        $"[Content] '{definition.name}' duplicates id {definition.Id}, already held " +
                        $"by '{owner.name}'. Reassigning — this only happens when an asset was " +
                        "copy-pasted rather than created.", definition);
                    continue;
                }

                taken.Add(definition.Id, definition);
                next = Mathf.Max(next, definition.Id + 1);
            }

            foreach (var definition in definitions)
            {
                if (definition.HasValidId && taken.TryGetValue(definition.Id, out var owner) &&
                    ReferenceEquals(owner, definition))
                    continue;

                var serialized = new SerializedObject(definition);
                serialized.FindProperty("id").intValue = next;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                taken.Add(next, definition);
                EditorUtility.SetDirty(definition);

                Debug.Log($"[Content] '{definition.name}' assigned id {next}.", definition);
                next++;
            }
        }

        private static T[] LoadAll<T>() where T : ContentDefinition =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();

        private static void WriteArray(SerializedObject serialized, string field, Object[] values)
        {
            var property = serialized.FindProperty(field);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Content] DefinitionRegistry has no array field '{field}'.");
                return;
            }

            property.arraySize = values.Length;

            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        [MenuItem("Office/Content/Build Sample Items", priority = 10)]
        public static void BuildSampleItems()
        {
            BuildSampleItem("ITM_Stapler", "STAPLER", "TAKE", 1,
                PrimitiveType.Cube, new Vector3(0.22f, 0.07f, 0.07f),
                new Color(0.72f, 0.24f, 0.20f));

            BuildSampleItem("ITM_Keycard", "KEYCARD", "TAKE", 4,
                PrimitiveType.Cube, new Vector3(0.09f, 0.005f, 0.055f),
                new Color(0.24f, 0.62f, 0.78f));

            BuildSampleItem("ITM_CoffeeCup", "COFFEE", "TAKE", 2,
                PrimitiveType.Cylinder, new Vector3(0.09f, 0.06f, 0.09f),
                new Color(0.86f, 0.82f, 0.74f));

            BuildSampleItem("ITM_LaserPointer", "LASER", "TAKE", 1,
                PrimitiveType.Cylinder, new Vector3(0.025f, 0.06f, 0.025f),
                new Color(0.18f, 0.72f, 0.42f));

            AssetDatabase.SaveAssets();
            Debug.Log("[Content] Sample items built. Run 'Rebuild Definition Registry' next.");
        }

        private static void BuildSampleItem(string assetName, string displayName, string verb,
            int maxStack, PrimitiveType shape, Vector3 size, Color colour)
        {
            var view = BuildViewPrefab(assetName, shape, size, colour);
            var icon = BuildIcon(assetName, colour);

            var definition = CreateOrLoad<ItemDefinition>($"{ItemDefinitionFolder}/{assetName}.asset");
            var serialized = new SerializedObject(definition);

            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("viewPrefab").objectReferenceValue = view;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("maxStack").intValue = maxStack;
            serialized.FindProperty("pickupVerb").stringValue = verb;

            serialized.FindProperty("groundOffset").floatValue =
                shape == PrimitiveType.Cylinder ? size.y : size.y * 0.5f;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(definition);
        }

        private static GameObject BuildViewPrefab(string assetName, PrimitiveType shape,
            Vector3 size, Color colour)
        {
            var path = $"{ItemPrefabFolder}/VIEW_{assetName}.prefab";

            var root = GameObject.CreatePrimitive(shape);
            root.name = $"VIEW_{assetName}";
            root.layer = PhysicsLayers.Interactable;
            root.transform.localScale = size;
            root.GetComponent<MeshRenderer>().sharedMaterial =
                CreateOrLoadMaterial($"M_Item_{assetName}", colour);

            EnsureFolder(ItemPrefabFolder);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return saved;
        }

        private static Sprite BuildIcon(string assetName, Color colour)
        {
            var path = $"{IconFolder}/ICO_{assetName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            EnsureFolder(IconFolder);

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
            {
                name = $"ICO_{assetName}",
                filterMode = FilterMode.Point
            };

            var pixels = new Color32[IconSize * IconSize];
            var fill = (Color32)colour;
            var edge = (Color32)(colour * 1.4f);

            for (var y = 0; y < IconSize; y++)
            for (var x = 0; x < IconSize; x++)
            {
                var border = x < 6 || y < 6 || x >= IconSize - 6 || y >= IconSize - 6;
                var corner = (x < 10 && y < 10) || (x < 10 && y >= IconSize - 10) ||
                             (x >= IconSize - 10 && y < 10) ||
                             (x >= IconSize - 10 && y >= IconSize - 10);

                pixels[y * IconSize + x] = corner
                    ? new Color32(0, 0, 0, 0)
                    : border ? edge : fill;
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize),
                new Vector2(0.5f, 0.5f), IconSize);
            sprite.name = $"ICO_{assetName}";

            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.AddObjectToAsset(sprite, texture);

            AssetDatabase.SaveAssets();

            return sprite;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static Material CreateOrLoadMaterial(string assetName, Color colour)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = colour
            };
            material.SetFloat("_Smoothness", 0.12f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
