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
            colour.postExposure.Override(0.45f);
            colour.contrast.Override(6f);
            colour.saturation.Override(-12f);
            colour.colorFilter.Override(new Color(1f, 0.975f, 0.935f));

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.9f, 0.93f, 1f));

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

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

            data.antialiasing = AntialiasingMode.None;
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
