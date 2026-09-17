using System.IO;
using Office.Data;
using Office.Enemies;
using Office.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Office.Editor
{
    internal static class EnemyContentBuilder
    {
        internal const string DefinitionFolder = "Assets/Project/ScriptableObject/Enemies";
        internal const string PrefabFolder = "Assets/Project/Prefab/Enemies";
        private const string MaterialFolder = "Assets/Project/Art/Materials/Enemies";

        private const string EnemyPrefabPath = PrefabFolder + "/PF_Enemy.prefab";

        [MenuItem("Office/Content/Build Enemy Content", priority = 16)]
        public static void BuildAll()
        {
            BuildEnemyPrefab();
            BuildEnemyDefinitions();
            RiggedEnemyBuilder.BuildAll();

            AssetDatabase.SaveAssets();

            ItemContentBuilder.RebuildRegistry();

            Debug.Log("[Enemy] Enemy content built. EnemySpawner reads EnemyPlacement markers " +
                      "when the run starts — add a marker to a level, or rebuild the sandbox " +
                      "for its test pair. Rebuild the session prefab if PF_Enemy was missing " +
                      "when it was last built.");
        }

        public static GameObject LoadEnemyPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);

        [MenuItem("Office/Content/Build Enemy Prefab", priority = 21)]
        public static void BuildEnemyPrefab()
        {
            var root = new GameObject("PF_Enemy") { layer = PhysicsLayers.Enemy };

            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.SynchronizeTransform = true;

            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.25f;
            body.height = 0.9f;
            body.center = new Vector3(0f, 0.45f, 0f);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = NavigationSetup.OfficeAgentTypeId;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;

            agent.enabled = false;

            var health = root.AddComponent<Health>();
            var enemy = root.AddComponent<Enemy>();
            var brain = root.AddComponent<EnemyBrain>();

            SetBool(health, "canBeDowned", false);

            Wire(enemy, ("health", health), ("body", body), ("agent", agent));
            Wire(brain, ("enemy", enemy), ("health", health));

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            NetworkPrefabRegistry.Register(
                AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath));

            Debug.Log($"[Enemy] Carrier prefab written to {EnemyPrefabPath}.");
        }

        private static void BuildEnemyDefinitions()
        {
            var view = BuildGreyboxView("VIEW_ENM_Stapler", new Color(0.62f, 0.16f, 0.14f),
                new Vector3(0.36f, 0.18f, 0.62f));

            var stapler = CreateOrLoad<EnemyDefinition>($"{DefinitionFolder}/ENM_Stapler.asset");
            var serialized = new SerializedObject(stapler);

            serialized.FindProperty("displayName").stringValue = "STAPLER";
            serialized.FindProperty("viewPrefab").objectReferenceValue = view;

            serialized.FindProperty("maxHealth").floatValue = 20f;

            serialized.FindProperty("bodyRadius").floatValue = 0.25f;
            serialized.FindProperty("bodyHeight").floatValue = 0.6f;

            serialized.FindProperty("patrolSpeed").floatValue = 1.4f;
            serialized.FindProperty("chaseSpeed").floatValue = 4.2f;
            serialized.FindProperty("acceleration").floatValue = 16f;
            serialized.FindProperty("turnSpeed").floatValue = 900f;

            serialized.FindProperty("sightRadius").floatValue = 12f;
            serialized.FindProperty("sightAngle").floatValue = 140f;
            serialized.FindProperty("memorySeconds").floatValue = 4f;
            serialized.FindProperty("hearingRadius").floatValue = 16f;

            serialized.FindProperty("attackDamage").floatValue = 8f;
            serialized.FindProperty("attackDamageType").intValue = (int)DamageType.Cutting;
            serialized.FindProperty("attackRange").floatValue = 1.1f;
            serialized.FindProperty("attackWindup").floatValue = 0.3f;
            serialized.FindProperty("attackCooldown").floatValue = 0.9f;

            serialized.FindProperty("corpseSeconds").floatValue = 6f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stapler);
        }

        private static GameObject BuildGreyboxView(string assetName, Color color, Vector3 size)
        {
            var path = $"{PrefabFolder}/{assetName}.prefab";

            var root = new GameObject(assetName);

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            mesh.transform.localScale = size;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = Material(assetName, color);

            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? prefab;
        }

        private static Material Material(string assetName, Color color)
        {
            var path = $"{MaterialFolder}/M_{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };

            material.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        internal static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        internal static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Enemy] '{target.GetType().Name}' has no field '{field}'. " +
                                   "The builder and the component have drifted apart.");
                    continue;
                }

                if (value == null)
                    Debug.LogError($"[Enemy] '{target.GetType().Name}.{field}' was given null.");

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Enemy] '{target.GetType().Name}' has no field '{field}'.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
