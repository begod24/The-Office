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
    /// <summary>
    /// The navigation agent the office is built for, and the bake that answers it.
    /// </summary>
    /// <remarks>
    /// <b>Unity's built-in Humanoid agent cannot be used here.</b> It is half a metre wide, and a
    /// baked mesh is eroded by the agent's radius on both sides of every obstacle — so a doorway
    /// has to be wider than a metre before any of it survives. Real office doors are 0.8–0.9 m
    /// and the greybox partition's is exactly one; baking with Humanoid produces a mesh with no
    /// connection through the only door in the sandbox, and the failure is silent. An enemy just
    /// stands there.
    /// <para>
    /// So the project owns an agent type of its own. One type, not one per enemy: every agent
    /// sizes its own radius and height from its <see cref="EnemyDefinition"/>, but they all walk
    /// the same baked mesh, and the mesh has to be baked for the tightest thing that uses it.
    /// A crawling enemy that wants under a desk needs a second type and a second bake — that is
    /// the day to add one, not before.
    /// </para>
    /// <para>
    /// The id is a fixed constant rather than the hash Unity assigns through the Navigation
    /// window, because it has to match on every machine and in every scene. A surface baked
    /// against one id and an agent asking for another produce a mesh nothing can stand on, and
    /// nothing is logged.
    /// </para>
    /// </remarks>
    internal static class NavigationSetup
    {
        /// <summary>
        /// The office agent's type id. Humanoid is 0; the Navigation window hands out large
        /// negative hashes, so a small positive number cannot collide with either.
        /// </summary>
        public const int OfficeAgentTypeId = 1;

        public const string OfficeAgentName = "Office";

        // Enough to pass a 1 m doorway with room on both sides, which Humanoid's 0.5 is not.
        private const float AgentRadius = 0.25f;

        // Head height for anything that walks upright. Deliberately not an enemy's own height:
        // this is the clearance the mesh guarantees, and it has to hold for the tallest user.
        private const float AgentHeight = 1.8f;

        // The player's step offset, from CFG_PlayerMovement. Anything a player can walk up, an
        // enemy has to be able to follow them up, or a chase ends on a 0.3 m riser.
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

            // Square metres. Below this an island is dropped, which is what keeps desktops and
            // window sills out of the mesh.
            Set(element, "minRegionArea", 2f);

            // Everything else in the record — buildHeightMesh, maxJobWorkers,
            // preserveTilesOutsideBounds — is left at Unity's default on purpose. The set of
            // fields moves between versions (serializedVersion 3 dropped accuratePlacement), and
            // Set warns rather than throws when one is gone.

            // Derived from the radius rather than typed: a cell wider than a third of the agent
            // rounds a doorway shut again, which is the failure this whole file exists to avoid.
            Set(element, "manualCellSize", 0f);
            Set(element, "cellSize", AgentRadius / 3f);
            Set(element, "manualTileSize", 0f);
            Set(element, "tileSize", 256f);
        }

        /// <remarks>
        /// One setter that reads the property's own type rather than two overloads that assume
        /// it. NavMeshAreas.asset is an internal Unity format: several of these fields are
        /// written as integers in the YAML and are booleans in the object model, and assigning
        /// through the wrong accessor logs an error and leaves the value alone — which would
        /// produce a bake that silently ignored half of what was asked for.
        /// </remarks>
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

        // ------------------------------------------------------------------ baking

        /// <summary>
        /// Re-bakes the open scene's navigation without regenerating the rest of it.
        /// </summary>
        /// <remarks>
        /// The same escape hatch the HUD, inventory and pause menu already have, and it exists
        /// for the same reason: a full <c>Build ... Scene</c> opens an empty scene and saves over
        /// the file, which is the right move when the builder changed and much too large a move
        /// when only the floor did. Moving a wall and re-baking is a thing that happens all day.
        /// <para>
        /// Reuses whatever surface the scene already has rather than adding a second. Two
        /// surfaces over the same floor bake two overlapping meshes, and an agent placed on the
        /// pair picks one — which looks like an enemy that ignores a wall on some spawns only.
        /// </para>
        /// </remarks>
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

        /// <summary>Where a scene's baked mesh lives. Beside the scenes, never inside one.</summary>
        public static string DataPathFor(string sceneName) =>
            $"Assets/Project/Scenes/Navigation/NavMesh_{sceneName}.asset";

        /// <summary>
        /// Adds a baked <see cref="NavMeshSurface"/> to the open scene and returns it.
        /// </summary>
        /// <remarks>
        /// The data is written to its own asset and then <b>reloaded from that path</b> before it
        /// is assigned. Architecture §7.1: a reference to an asset created moments earlier goes
        /// stale on the next import, and assigning the stale wrapper writes a silent null — which
        /// here means a scene that saves with a surface holding no mesh at all.
        /// <para>
        /// Baking at edit time is right while levels are hand-built. Procedural floors cannot be
        /// baked ahead of knowing their shape, so <c>Office.LevelGen</c> will bake on the server
        /// at run start instead; the agent type above is the part both paths share.
        /// </para>
        /// </remarks>
        public static NavMeshSurface BuildSurface(Transform parent, string dataAssetPath)
        {
            var host = new GameObject("Navigation");
            host.transform.SetParent(parent, false);

            var surface = host.AddComponent<NavMeshSurface>();

            Bake(surface, dataAssetPath);
            return surface;
        }

        /// <summary>Configures one surface the way the office needs it and bakes it to disk.</summary>
        private static void Bake(NavMeshSurface surface, string dataAssetPath)
        {
            EnsureOfficeAgentType();

            surface.agentTypeID = OfficeAgentTypeId;
            surface.collectObjects = CollectObjects.All;

            // Colliders, not renderers: the mesh has to agree with what a player actually walks
            // on, and PhysicsLayers.WalkableMask is expressed in layers a collider carries.
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
                // Refreshed, not just created: CreateAsset writes into the AssetDatabase, and
                // a folder it has not imported yet is a folder it will refuse to write to.
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
