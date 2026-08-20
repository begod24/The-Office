using System.IO;
using Office.Data;
using Office.Gameplay;
using Office.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Editor
{
    internal static class PowerContentBuilder
    {
        private const string SessionPrefabPath = "Assets/Project/Prefab/Systems/PF_Session.prefab";
        private const string SwitchPrefabPath = "Assets/Project/Prefab/Props/PF_PowerSwitch.prefab";
        private const string MaterialFolder = "Assets/Project/Art/Materials/Props";

        [MenuItem("Office/Content/Build Power Content", priority = 34)]
        public static void Build()
        {
            var prefab = BuildSwitchPrefab();
            if (prefab == null) return;

            NetworkPrefabRegistry.Register(prefab);

            WireSession(prefab);

            AssetDatabase.SaveAssets();

            Debug.Log($"[Power] {SwitchPrefabPath} built and registered, and PF_Session now " +
                      "carries RunOutcome and PowerSwitchSpawner. Place a " +
                      "PowerSwitchPlacement marker in the level — " +
                      "'Office/Setup/Add Power Switch To Open Scene' puts one on the generator.");
        }

        public static GameObject LoadSwitchPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(SwitchPrefabPath);

        private static GameObject BuildSwitchPrefab()
        {
            var root = new GameObject("PF_PowerSwitch") { layer = PhysicsLayers.Interactable };

            root.AddComponent<NetworkObject>();

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.34f, 0.5f, 0.18f);
            box.center = new Vector3(0f, 0f, 0.04f);

            var housing = Box(root.transform, "Housing", Vector3.zero,
                new Vector3(0.3f, 0.45f, 0.12f),
                Material("M_Prop_SwitchHousing", new Color(0.16f, 0.16f, 0.17f), emissive: false));

            var indicator = Box(root.transform, "Indicator", new Vector3(0f, 0.09f, -0.075f),
                new Vector3(0.12f, 0.12f, 0.04f),
                Material("M_Prop_SwitchLamp", new Color(0.85f, 0.20f, 0.15f), emissive: true));

            var lever = Box(root.transform, "Lever", new Vector3(0f, -0.08f, -0.08f),
                new Vector3(0.16f, 0.07f, 0.05f),
                Material("M_Prop_SwitchLever", new Color(0.62f, 0.60f, 0.55f), emissive: false));
            lever.name = "Lever";

            var component = root.AddComponent<PowerSwitch>();
            Wire(component, ("indicator", indicator.GetComponent<MeshRenderer>()));

            EnsureFolder(Path.GetDirectoryName(SwitchPrefabPath));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, SwitchPrefabPath);
            Object.DestroyImmediate(root);

            if (saved == null) Debug.LogError($"[Power] Could not write {SwitchPrefabPath}.");

            return saved;
        }

        private static void WireSession(GameObject switchPrefab)
        {
            var session = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);

            if (session == null)
            {
                Debug.LogError($"[Power] {SessionPrefabPath} is missing. Run " +
                               "'Office/Setup/Build Session Prefab' first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(SessionPrefabPath);

            try
            {
                var director = contents.GetComponent<SessionDirector>();

                if (director == null)
                {
                    Debug.LogError("[Power] PF_Session has no SessionDirector. Nothing wired.");
                    return;
                }

                var outcome = contents.GetComponent<RunOutcome>()
                              ?? contents.AddComponent<RunOutcome>();

                var spawner = contents.GetComponent<PowerSwitchSpawner>()
                              ?? contents.AddComponent<PowerSwitchSpawner>();

                Wire(outcome, ("director", director));
                Wire(spawner,
                    ("director", director),
                    ("outcome", outcome),
                    ("switchPrefab", switchPrefab));

                PrefabUtility.SaveAsPrefabAsset(contents, SessionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [MenuItem("Office/Setup/Add Power Switch To Open Scene", priority = 44)]
        public static void AddMarkerToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            if (Object.FindFirstObjectByType<PowerSwitchPlacement>() != null)
            {
                Debug.Log("[Power] The open scene already has a switch marker. Nothing added.");
                return;
            }

            var generator = GameObject.Find("Generator");

            var marker = new GameObject("Place_PowerSwitch");
            marker.AddComponent<PowerSwitchPlacement>();

            if (generator != null)
            {
                marker.transform.SetParent(generator.transform.parent, false);

                var bounds = BoundsOf(generator);

                marker.transform.position = new Vector3(
                    bounds.min.x - 0.25f,
                    bounds.min.y + 1.1f,
                    bounds.center.z);

                marker.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                Debug.LogWarning("[Power] No object called 'Generator' in the open scene. The " +
                                 "marker went to the origin — move it by hand.");
            }

            Selection.activeGameObject = marker;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Power] Marker added to '{scene.name}' at {marker.transform.position}. " +
                      "Save the scene to keep it.");
        }

        private static Bounds BoundsOf(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;

            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = PhysicsLayers.Interactable;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = box.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            return box;
        }

        private static Material Material(string assetName, Color colour, bool emissive)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = colour
            };

            material.SetFloat("_Smoothness", 0.1f);

            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", colour * 2f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Power] '{target.GetType().Name}' has no field '{field}'.");
                    continue;
                }

                if (value == null)
                    Debug.LogError($"[Power] '{target.GetType().Name}.{field}' was given null.");

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
