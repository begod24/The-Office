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

            // The look brief is the second concept frame: near-black corridors, cold
            // cast, a single warm emergency source, film grain — and NO pixelisation.
            // That last part is the whole reason render scale and nearest-neighbour
            // upscaling stay out of this stack. Grain and chromatic aberration give the
            // image its texture; downsampling would give it aliasing instead.

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(-0.1f);
            colour.contrast.Override(10f);
            colour.saturation.Override(-30f);
            colour.colorFilter.Override(new Color(0.94f, 0.97f, 1f));

            var balance = Add<WhiteBalance>(profile);
            balance.temperature.Override(-18f);
            balance.tint.Override(4f);

            // Shadows pushed towards cyan, highlights left almost neutral. This is what
            // reads as "fluorescent light in a dead building" rather than "blue filter".
            var tonal = Add<ShadowsMidtonesHighlights>(profile);
            tonal.shadows.Override(new Vector4(0.86f, 0.95f, 1.08f, -0.06f));
            tonal.midtones.Override(new Vector4(0.98f, 1f, 1.02f, 0f));
            tonal.highlights.Override(new Vector4(1.02f, 1f, 0.97f, -0.03f));

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(0.86f, 0.92f, 1f));

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.45f);

            var aberration = Add<ChromaticAberration>(profile);
            aberration.intensity.Override(0.08f);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.75f);

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

            // SMAA, not TAA: this is a dark scene full of thin blockout edges, and TAA
            // smears them while the camera turns. Neither one resamples the image, which
            // is what keeps the result sharp instead of pixelated.
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
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
