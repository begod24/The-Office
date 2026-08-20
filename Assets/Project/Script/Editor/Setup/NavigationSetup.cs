using System.IO;
using Office.Data;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Office.Editor
{
    internal static class NavigationSetup
    {
        public const int OfficeAgentTypeId = 1;

        public const string OfficeAgentName = "Office";

        private const float AgentRadius = 0.25f;

        private const float AgentHeight = 1.8f;

        private const float AgentClimb = 0.35f;

        private const float AgentSlope = 45f;

        private const string SettingsPath = "ProjectSettings/NavMeshAreas.asset";

        [MenuItem("Office/Setup/Create Navigation Agent Type", priority = 24)]
        public static void EnsureOfficeAgentType()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);

            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[Setup] {SettingsPath} could not be loaded. Agent type not created.");
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            var settings = serialized.FindProperty("m_Settings");
            var names = serialized.FindProperty("m_SettingNames");

            if (settings == null || names == null)
            {
                Debug.LogError("[Setup] NavMeshAreas.asset has an unexpected layout — " +
                               "'m_Settings' or 'm_SettingNames' is missing. Add the agent type " +
                               "by hand in Window/AI/Navigation instead.");
                return;
            }

            var index = IndexOf(settings, OfficeAgentTypeId);
            var created = index < 0;

            if (created)
            {
                index = settings.arraySize;
                settings.InsertArrayElementAtIndex(index);
                names.InsertArrayElementAtIndex(index);
            }

            Write(settings.GetArrayElementAtIndex(index));
            names.GetArrayElementAtIndex(index).stringValue = OfficeAgentName;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Setup] Navigation agent '{OfficeAgentName}' (id {OfficeAgentTypeId}) " +
                      $"{(created ? "created" : "updated")}: radius {AgentRadius}, height " +
                      $"{AgentHeight}, climb {AgentClimb}. Re-bake every NavMeshSurface after " +
                      "changing these.");
        }

        private static int IndexOf(SerializedProperty settings, int agentTypeId)
        {
            for (var i = 0; i < settings.arraySize; i++)
            {
                var element = settings.GetArrayElementAtIndex(i);
                var id = element.FindPropertyRelative("agentTypeID");

                if (id != null && id.intValue == agentTypeId) return i;
            }

            return -1;
        }

        private static void Write(SerializedProperty element)
        {
            Set(element, "agentTypeID", OfficeAgentTypeId);
            Set(element, "agentRadius", AgentRadius);
            Set(element, "agentHeight", AgentHeight);
            Set(element, "agentSlope", AgentSlope);
            Set(element, "agentClimb", AgentClimb);
            Set(element, "ledgeDropHeight", 0f);
            Set(element, "maxJumpAcrossDistance", 0f);

            Set(element, "minRegionArea", 2f);

            Set(element, "manualCellSize", 0f);
            Set(element, "cellSize", AgentRadius / 3f);
            Set(element, "manualTileSize", 0f);
            Set(element, "tileSize", 256f);
        }

        private static void Set(SerializedProperty element, string name, float value)
        {
            var property = element.FindPropertyRelative(name);

            if (property == null)
            {
                Debug.LogWarning($"[Setup] Navigation agent settings have no field '{name}'. " +
                                 "Skipped — Unity's format has changed.");
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = value;
                    break;

                case SerializedPropertyType.Integer:
                    property.intValue = Mathf.RoundToInt(value);
                    break;

                case SerializedPropertyType.Boolean:
                    property.boolValue = !Mathf.Approximately(value, 0f);
                    break;

                default:
                    Debug.LogWarning($"[Setup] Navigation field '{name}' is a " +
                                     $"{property.propertyType} and was not written.");
                    break;
            }
        }

        [MenuItem("Office/Setup/Bake Navigation In Open Scene", priority = 43)]
        public static void BakeInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                Debug.LogError("[Setup] No scene is open. Nothing was baked.");
                return;
            }

            var existing = Object.FindObjectsByType<NavMeshSurface>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (existing.Length > 1)
                Debug.LogWarning($"[Setup] {scene.name} has {existing.Length} NavMeshSurfaces. " +
                                 "Only the first was baked — delete the rest, they overlap.");

            var surface = existing.Length > 0 ? existing[0] : null;

            if (surface == null)
            {
                var host = new GameObject("Navigation");
                SceneManager.MoveGameObjectToScene(host, scene);
                surface = host.AddComponent<NavMeshSurface>();
            }

            Bake(surface, DataPathFor(scene.name));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Setup] Navigation re-baked in {scene.name} and the scene saved.");
        }

        public static string DataPathFor(string sceneName) =>
            $"Assets/Project/Scenes/Navigation/NavMesh_{sceneName}.asset";

        public static NavMeshSurface BuildSurface(Transform parent, string dataAssetPath)
        {
            var host = new GameObject("Navigation");
            host.transform.SetParent(parent, false);

            var surface = host.AddComponent<NavMeshSurface>();

            Bake(surface, dataAssetPath);
            return surface;
        }

        private static void Bake(NavMeshSurface surface, string dataAssetPath)
        {
            EnsureOfficeAgentType();

            surface.agentTypeID = OfficeAgentTypeId;
            surface.collectObjects = CollectObjects.All;

            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = PhysicsLayers.WalkableMask;

            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[Setup] The navigation bake produced nothing. Check that the " +
                               "floor has a collider on a layer in PhysicsLayers.WalkableMask.");
                return;
            }

            var folder = Path.GetDirectoryName(dataAssetPath);

            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(surface.navMeshData, dataAssetPath);
            AssetDatabase.SaveAssets();

            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataAssetPath);

            if (surface.navMeshData == null)
                Debug.LogError($"[Setup] {dataAssetPath} did not reload after being written. " +
                               "The scene would save with an empty surface.");
            else
                Debug.Log($"[Setup] Navigation baked to {dataAssetPath}.");
        }
    }
}
