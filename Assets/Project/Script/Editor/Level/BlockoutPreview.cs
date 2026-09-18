using UnityEditor;
using UnityEngine;

namespace Office.Editor.Level
{
    /// <summary>
    /// Fixed camera vantages for reviewing the blockout. Level work is judged from eye
    /// height inside the space, not from an orbit above it, so these put the Scene view
    /// exactly where a player would stand - same height, same field of view.
    /// </summary>
    internal static class BlockoutPreview
    {
        private const float EyeHeight = 1.7f;

        [MenuItem("Office/Level/Preview/1 - Spawn (Cubicles, facing north)", priority = 20)]
        public static void Spawn() => Stand(new Vector3(-6f, 0f, -15f), 0f);

        [MenuItem("Office/Level/Preview/2 - Ring corridor (south leg, facing east)", priority = 21)]
        public static void RingCorridor() => Stand(new Vector3(-9f, 0f, -7f), 90f);

        [MenuItem("Office/Level/Preview/3 - Lift lobby (facing north)", priority = 22)]
        public static void LiftLobby() => Stand(new Vector3(-14f, 0f, -6f), 0f);

        [MenuItem("Office/Level/Preview/4 - Conference room (facing north)", priority = 23)]
        public static void Conference() => Stand(new Vector3(3f, 0f, 9f), 0f);

        [MenuItem("Office/Level/Preview/5 - Stairwell A (facing north)", priority = 24)]
        public static void StairwellA() => Stand(new Vector3(-18f, 0f, 9f), 0f);

        [MenuItem("Office/Level/Preview/6 - Dev open space (facing east)", priority = 25)]
        public static void DevFloor() => Stand(new Vector3(-13f, 0f, 14f), 90f);

        /// <summary>
        /// Top-down plan of floor 5. Hides everything that would be in the way — the
        /// floors above and floor 5's own ceiling — because a plan view of a building
        /// with a roof on it is a picture of a roof.
        /// </summary>
        [MenuItem("Office/Level/Preview/7 - Floor plan of F5 (top down)", priority = 40)]
        public static void Plan()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;

            HideAbove();

            view.orthographic = true;
            view.LookAt(new Vector3(0f, TowerGeometry.FloorY(5) + 1.6f, 0f),
                Quaternion.Euler(90f, 0f, 0f), 24f);

            Configure(view);
        }

        [MenuItem("Office/Level/Preview/8 - Show all floors again", priority = 41)]
        public static void ShowEverything()
        {
            SceneVisibilityManager.instance.ShowAll();
            SceneView.lastActiveSceneView?.Repaint();
        }

        private static void HideAbove()
        {
            var manager = SceneVisibilityManager.instance;
            manager.ShowAll();

            var tower = GameObject.Find(TowerBlockout.RootName);

            if (tower == null)
            {
                Debug.LogWarning($"[Blockout] No '{TowerBlockout.RootName}' root in the open " +
                                 "scene. Build the blockout first.");
                return;
            }

            foreach (Transform child in tower.transform)
            {
                var isUpperFloor = child.name.StartsWith("F6")
                                   || child.name.StartsWith("F7")
                                   || child.name.StartsWith("F8")
                                   || child.name == "Roof";

                if (isUpperFloor) manager.Hide(child.gameObject, true);
            }

            var floor5 = tower.transform.Find(TowerGeometry.FloorLabel(5));
            var plates = floor5 != null ? floor5.Find("Plates") : null;

            if (plates == null) return;

            foreach (Transform piece in plates)
                if (piece.name.StartsWith("Ceiling")) manager.Hide(piece.gameObject, true);
        }

        /// <summary>Puts the Scene camera at eye height on floor 5, facing a compass yaw.</summary>
        private static void Stand(Vector3 groundPosition, float yaw)
        {
            var view = SceneView.lastActiveSceneView;

            if (view == null)
            {
                Debug.LogWarning("[Blockout] No Scene view is open.");
                return;
            }

            var eye = groundPosition + Vector3.up * (TowerGeometry.FloorY(5) + EyeHeight);
            var rotation = Quaternion.Euler(0f, yaw, 0f);

            view.orthographic = false;

            // SceneView orbits its pivot, so the pivot goes in front of the eye by the
            // same distance the view's size implies - that is what lands the camera on
            // the spot rather than behind it.
            const float distance = 4f;
            view.LookAt(eye + rotation * Vector3.forward * distance, rotation, distance * 0.5f);

            Configure(view);
        }

        private static void Configure(SceneView view)
        {
            view.sceneLighting = true;
            view.drawGizmos = false;
            view.sceneViewState.showFog = true;
            view.sceneViewState.showImageEffects = true;
            view.sceneViewState.showSkybox = false;
            view.Repaint();
        }
    }
}
