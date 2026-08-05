using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    internal static class PostProcessBuilder
    {
        private const string SettingsFolder = "Assets/Project/Settings";
        private const string ProfilePath = SettingsFolder + "/VP_Office.asset";

        [MenuItem("Office/Setup/Build Post Process Profile", priority = 24)]
        public static void BuildProfileMenu() => BuildProfile();

        public static VolumeProfile BuildProfile()
        {
            EnsureFolder(SettingsFolder);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                ClearComponents(profile);
            }

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(-0.55f);
            colour.contrast.Override(14f);
            colour.saturation.Override(-22f);
            colour.colorFilter.Override(new Color(0.92f, 0.95f, 1f));

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(0.85f, 0.9f, 1f));

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.45f);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.75f);

            var aberration = Add<ChromaticAberration>(profile);
            aberration.intensity.Override(0.12f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        public static void BuildVolume()
        {
            var profile = BuildProfile();

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            volume.sharedProfile = profile;
        }

        public static void EnablePostProcessing(Camera camera)
        {
            if (camera == null) return;

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void ClearComponents(VolumeProfile profile)
        {
            var existing = profile.components.ToArray();

            foreach (var component in existing)
            {
                if (component == null) continue;

                AssetDatabase.RemoveObjectFromAsset(component);
                Object.DestroyImmediate(component, true);
            }

            profile.components.Clear();
        }

        private static void EnsureFolder(string folder)
        {
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
