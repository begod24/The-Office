using System.Linq;
using Office.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Editor.Level
{
    /// <summary>
    /// Builds the four-floor blockout into SCN_Sandbox.
    ///
    /// This replaces the blockout and nothing else. The HUD, inventory, pause menu,
    /// post-process volume and fallback camera in that scene were built by other tools
    /// and are left alone — Docs/CodeReview.md §"Build Sandbox Scene" flags full-scene
    /// regeneration as the way hand-placed work silently disappears, and it is right.
    /// </summary>
    internal static class TowerBlockout
    {
        internal const string RootName = "Tower";

        private const int TopFloor = 8;

        private const string ScenePath = "Assets/Project/Scenes/SCN_Sandbox.unity";

        /// <summary>Roots the old greybox owned. Everything else in the scene survives.</summary>
        private static readonly string[] Superseded =
        {
            "Greybox", "SpawnPoints", "Directional Light", "Navigation", RootName
        };

        [MenuItem("Office/Level/Build Tower Blockout (Sandbox)", priority = 0)]
        public static void BuildInSandbox()
        {
            var scene = OpenSandbox();
            if (!scene.IsValid()) return;

            var removed = RemoveSuperseded(scene);

            BlockoutPalette.Load();

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            BuildFloors(root.transform);
            TowerCores.Build(root.transform, TopFloor);
            TowerPlates.BuildRoof(root.transform, TopFloor);
            TowerMarkers.Build(root.transform);
            BlockoutLighting.Build(root.transform);

            NavigationSetup.BuildSurface(root.transform,
                NavigationSetup.DataPathFor(SceneNames.Sandbox));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();

            Debug.Log($"[Blockout] Tower built in {SceneNames.Sandbox}: floors 5-8, " +
                      $"{root.GetComponentsInChildren<MeshFilter>().Length} meshes. " +
                      $"Removed {removed} superseded root(s). " +
                      "Play from SCN_Boot and start a run — RunSceneFlow loads this scene " +
                      "additively, so players and enemies spawn from the markers here.");
        }

        private static void BuildFloors(Transform root)
        {
            for (var floor = 5; floor <= TopFloor; floor++)
            {
                var group = BlockoutKit.Group(root, TowerGeometry.FloorLabel(floor));

                TowerPlates.Build(group, floor);

                if (floor == 5) TowerFloor5.Build(group);
                else TowerShellFloors.Build(group, floor);
            }
        }

        private static Scene OpenSandbox()
        {
            var active = SceneManager.GetActiveScene();

            if (active.path == ScenePath) return active;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Blockout] Cancelled — the open scene has unsaved changes.");
                return default;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
                Debug.LogError($"[Blockout] {ScenePath} could not be opened. Nothing was built.");

            return scene;
        }

        private static int RemoveSuperseded(Scene scene)
        {
            var doomed = scene.GetRootGameObjects()
                .Where(root => Superseded.Contains(root.name))
                .ToArray();

            foreach (var root in doomed) Object.DestroyImmediate(root);

            return doomed.Length;
        }
    }
}
