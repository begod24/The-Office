using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    /// <summary>
    /// The run-time grade: a URP volume profile and the global volume that applies it.
    ///
    /// Built from code like everything else in <see cref="ProjectSetup"/>, and for one extra
    /// reason: a volume profile is a single asset with a dozen hidden sub-objects, so a value
    /// tweaked by hand in the inspector is invisible in a diff. Every number here is a decision
    /// somebody can read and argue with.
    ///
    /// The look is the one from the reference frame — dark, desaturated, grainy, with a heavy
    /// vignette so the edges of the screen stop carrying information. Post-processing does not
    /// make a scene dark on its own, though: the greybox sandbox is lit for measuring geometry,
    /// not for atmosphere, and it will still read brighter than the reference until the lighting
    /// pass lands.
    /// </summary>
    internal static class PostProcessBuilder
    {
        private const string SettingsFolder = "Assets/Project/Settings";
        private const string ProfilePath = SettingsFolder + "/VP_Office.asset";

        [MenuItem("Office/Setup/Build Post Process Profile", priority = 24)]
        public static void BuildProfileMenu() => BuildProfile();

        /// <summary>
        /// Creates or repopulates the profile asset. The asset is reused rather than deleted and
        /// recreated: deleting it would mint a new GUID and quietly break every scene that
        /// already references it.
        /// </summary>
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

            // Neutral rather than ACES: ACES crushes the shadows of an already dark scene into
            // pure black, and the game is played by reading shapes in the dark.
            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(-0.55f);
            colour.contrast.Override(14f);
            colour.saturation.Override(-22f);
            colour.colorFilter.Override(new Color(0.92f, 0.95f, 1f));

            // Above 1.0 so only actual light sources bloom — monitors, exit signs, the fluorescent
            // tubes. A lower threshold makes every grey wall glow.
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

            // Kept low on purpose. Chromatic aberration is the first effect players turn off, and
            // at this strength it reads as a cheap lens rather than as a headache.
            var aberration = Add<ChromaticAberration>(profile);
            aberration.intensity.Override(0.12f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Reloaded from disk: the instance created above goes stale as soon as the
            // AssetDatabase reimports the asset, and assigning a stale wrapper to a scene
            // reference writes a silent null.
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        /// <summary>Adds the global volume to the open scene. Assumes the profile already exists.</summary>
        public static void BuildVolume()
        {
            var profile = BuildProfile();

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            // sharedProfile, not profile: the profile getter instantiates a runtime copy, which
            // in the editor means an unsaved clone that silently replaces the asset reference.
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// URP cameras render without post-processing unless asked. Every camera the player can
        /// look through has to opt in, or the volume above does nothing at all.
        /// </summary>
        public static void EnablePostProcessing(Camera camera)
        {
            if (camera == null) return;

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            // FXAA rather than SMAA: it is a fraction of the cost and the render target is going
            // to be downsampled for the PS1 look anyway.
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            // Added without overrides, then each parameter this file cares about is overridden
            // explicitly. A blanket override would pin every untouched parameter to its default
            // and stop the project-wide default profile from ever changing anything.
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
