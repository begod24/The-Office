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
    /// <summary>
    /// Content pipeline for enemies: the one networked carrier every enemy shares, and the
    /// greybox definitions that make it into something in particular.
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="CombatContentBuilder"/>'s target half, because an enemy is the
    /// same arrangement with a brain attached: one registered prefab, a definition per kind, a
    /// local view built from the definition. Adding the second enemy is an asset and a mesh.
    /// <para>
    /// The greybox capsule here is meant to be replaced. What is not throwaway is that replacing
    /// it means assigning a different <c>viewPrefab</c> on the definition and nothing else — no
    /// prefab surgery, no registry entry, no netcode change.
    /// </para>
    /// </remarks>
    internal static class EnemyContentBuilder
    {
        private const string DefinitionFolder = "Assets/Project/ScriptableObject/Enemies";
        private const string PrefabFolder = "Assets/Project/Prefab/Enemies";
        private const string MaterialFolder = "Assets/Project/Art/Materials/Enemies";

        private const string EnemyPrefabPath = PrefabFolder + "/PF_Enemy.prefab";

        [MenuItem("Office/Content/Build Enemy Content", priority = 16)]
        public static void BuildAll()
        {
            BuildEnemyPrefab();
            BuildEnemyDefinitions();

            AssetDatabase.SaveAssets();

            // Ids come from the registry, and an enemy with id 0 resolves to nothing on every
            // machine including the one that authored it.
            ItemContentBuilder.RebuildRegistry();

            Debug.Log("[Enemy] Enemy content built. EnemySpawner reads EnemyPlacement markers " +
                      "when the run starts — add a marker to a level, or rebuild the sandbox " +
                      "for its test pair. Rebuild the session prefab if PF_Enemy was missing " +
                      "when it was last built.");
        }

        public static GameObject LoadEnemyPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);

        // ------------------------------------------------------------------- carrier

        /// <summary>
        /// The single network prefab every enemy is. One entry in the prefab list, forever.
        /// </summary>
        [MenuItem("Office/Content/Build Enemy Prefab", priority = 21)]
        public static void BuildEnemyPrefab()
        {
            var root = new GameObject("PF_Enemy") { layer = PhysicsLayers.Enemy };

            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.SynchronizeTransform = true;

            // Server authority, unlike the player's. An enemy is decided by the machine that
            // runs its brain, and owner authority on a server-owned object would still be the
            // server — right up until someone changes ownership and quietly hands a client the
            // ability to walk an enemy wherever they like.
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            // Sized from the definition at spawn. The numbers here only decide what the prefab
            // looks like in the inspector.
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.25f;
            body.height = 0.9f;
            body.center = new Vector3(0f, 0.45f, 0f);

            // Kinematic, and only so the capsule counts as a moving collider. Without it every
            // step an enemy takes rebuilds the static collision tree, which is a cost paid by
            // a swarm and nobody else.
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = NavigationSetup.OfficeAgentTypeId;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;

            // Off in the prefab. Enabling an agent that is not standing on a navigation mesh
            // logs and leaves it inert, and an instantiated prefab is at the world origin —
            // Enemy.ApplyAgent turns it on once the position is real and the server is the one
            // asking.
            agent.enabled = false;

            var health = root.AddComponent<Health>();
            var enemy = root.AddComponent<Enemy>();
            var brain = root.AddComponent<EnemyBrain>();

            // GDD §7.1 gives the downed state to players. An enemy at zero is destroyed, and
            // leaving this on would give every stapler a sixty-second bleed-out instead.
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

        // ------------------------------------------------------------------- definitions

        /// <summary>
        /// The first enemy: GDD §9.1 #13, a fast melee swarm.
        /// </summary>
        /// <remarks>
        /// Chosen to be first because it needs the least. Melee means no projectile behaviour,
        /// small means no rig, and a swarm exercises the object pool that was built for exactly
        /// this and has had nothing to recycle since.
        /// <para>
        /// Its chase speed sits under the player's sprint on purpose. An enemy that cannot be
        /// outrun turns every encounter into a fight, and GDD §8.1 wants combat to be the last
        /// resort rather than the only answer.
        /// </para>
        /// </remarks>
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

            // No face, so a wide cone. Breaking line of sight still works — that is the counter
            // GDD §9.1 hands the player, and it is geometry rather than angle.
            serialized.FindProperty("sightRadius").floatValue = 12f;
            serialized.FindProperty("sightAngle").floatValue = 140f;
            serialized.FindProperty("memorySeconds").floatValue = 4f;
            serialized.FindProperty("hearingRadius").floatValue = 16f;

            serialized.FindProperty("attackDamage").floatValue = 8f;
            serialized.FindProperty("attackDamageType").intValue = (int)DamageType.Cutting;
            serialized.FindProperty("attackRange").floatValue = 1.1f;
            serialized.FindProperty("attackWindup").floatValue = 0.3f;
            serialized.FindProperty("attackCooldown").floatValue = 0.9f;

            // Short. A swarm that leaves its dead behind fills a corridor with bodies the player
            // has to walk through, and the pool never gets them back.
            serialized.FindProperty("corpseSeconds").floatValue = 6f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stapler);
        }

        /// <summary>
        /// A box standing in for a mesh. Colliders are stripped — the carrier owns the one the
        /// swing connects with, so art can arrive with whatever collision it likes.
        /// </summary>
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

            // Reloaded from the path rather than reused: the wrapper returned above goes stale
            // on the next import, and a stale wrapper assigned to a SerializedProperty writes a
            // silent null. Architecture §7.1 names this trap.
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? prefab;
        }

        // ------------------------------------------------------------------- helpers

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

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
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

        private static void SetBool(Object target, string field, bool value)
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
