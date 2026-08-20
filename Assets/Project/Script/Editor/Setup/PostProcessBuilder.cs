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

            // Graded for a picture that is legible first and moody second. The reference
            // frames this look is chasing are dark, but nothing in them is *lost*: floors,
            // walls and props all keep readable mid-tones, and the darkness is atmosphere
            // rather than absence. The previous grade sat a stop and a half down with heavy
            // contrast on top, which the palette pass then quantised — and a dark mid-tone
            // that lands on the first palette step is not dark, it is gone.
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

            // Present, not crushing. A heavy vignette and a low internal resolution fight each
            // other: the corners lose their few pixels entirely instead of getting darker.
            var vignette = Add<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);

            // Low. This runs before the palette pass and at the internal resolution, so what
            // grain survives arrives as noise on the blocks themselves — which is the texture
            // the reference frames have. Turned up, it stops being texture and starts being a
            // second, moving dither fighting the ordered one.
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

            // No anti-aliasing, deliberately. GDD §12.1 wants a visible pixel grid, and
            // smoothing the edges is the exact opposite request — FXAA would spend frame time
            // undoing what the render scale and the retro pass are for.
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
