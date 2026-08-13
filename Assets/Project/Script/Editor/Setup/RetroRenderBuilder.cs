using System.Collections.Generic;
using Office.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    /// <summary>
    /// Wires the PS1 look into the pipeline: a real low internal resolution on the render
    /// pipeline asset, and the tape layer on its renderer.
    /// </summary>
    /// <remarks>
    /// Both halves live in one menu item because neither is right alone. The render scale is
    /// what makes the picture genuinely low resolution; the feature is what makes a low
    /// resolution picture read as a tape rather than as a small window. They also share a
    /// number — the feature has to be told how tall the picture actually is, and working that
    /// out twice by hand is how the scanlines end up half a pixel off.
    /// <para>
    /// Idempotent like every other builder: it removes the feature it previously added before
    /// adding one, so re-running it leaves exactly one.
    /// </para>
    /// </remarks>
    internal static class RetroRenderBuilder
    {
        private const string SettingsFolder = "Assets/Project/Settings";
        private const string PipelinePath = SettingsFolder + "/PC_RPAsset.asset";
        private const string RendererPath = SettingsFolder + "/PC_Renderer.asset";
        private const string ShaderPath = "Assets/Project/Art/Shaders/S_RetroFilm.shader";

        private const string FeatureName = "Retro Film";

        /// <summary>
        /// Fraction of the window the picture is actually rendered at. Just over half of 1080p
        /// is roughly 1050×590.
        /// </summary>
        /// <remarks>
        /// Deliberately well above the 320×240 GDD §12.1 quotes. That number describes the
        /// hardware being referenced, not the look being aimed at — the games this project is
        /// chasing (Iron Lung, the PSX-horror wave) run a soft pixel grid over an otherwise
        /// legible picture, rather than a genuinely 240-line one. A true quarter-scale buffer
        /// costs the thing the horror actually needs: at 480×270 a stapler across an unlit
        /// office is four pixels, and a player cannot be frightened by a shape they cannot
        /// resolve. The grid should be felt, not read.
        /// </remarks>
        private const float RenderScale = 0.55f;

        /// <summary>
        /// The height the scale above produces on a 1080p screen. The feature needs the picture
        /// height in rows and cannot read the window, so this is the one place the two agree.
        /// </summary>
        private const float ReferenceHeight = 1080f;

        /// <summary>Nearest-neighbour. <see cref="UpscalingFilterSelection.Point"/>.</summary>
        private const int PointUpscale = 2;

        [MenuItem("Office/Setup/Build Retro Render", priority = 25)]
        public static void Build()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (shader == null)
            {
                Debug.LogError($"[Render] '{ShaderPath}' is missing. The retro pass cannot be " +
                               "built without its shader.");
                return;
            }

            if (!ConfigurePipeline()) return;
            if (!ConfigureRenderer(shader)) return;

            AssetDatabase.SaveAssets();

            Debug.Log($"[Render] Retro render built: {RenderScale:0.##} render scale with a " +
                      "point upscale, and the tape pass on PC_Renderer. It shows in the Game " +
                      "view only — the Scene view is left clean so the level stays workable.");
        }

        // ------------------------------------------------------------------ the picture

        private static bool ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (pipeline == null)
            {
                Debug.LogError($"[Render] '{PipelinePath}' is missing.");
                return false;
            }

            var serialized = new SerializedObject(pipeline);

            if (!TrySet(serialized, "m_RenderScale", p => p.floatValue = RenderScale)) return false;

            // Point, not Linear. A linear upscale is what makes a low-resolution image look
            // like a blurry high-resolution one instead of like pixels, and it is the single
            // setting that decides whether any of this reads as intended.
            if (!TrySet(serialized, "m_UpscalingFilter", p => p.enumValueIndex = PointUpscale))
                return false;

            // Anti-aliasing and a deliberate pixel grid are the same argument from opposite
            // sides. MSAA off; the camera's FXAA is turned off in PostProcessBuilder.
            TrySet(serialized, "m_MSAA", p => p.intValue = 1);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            return true;
        }

        // ------------------------------------------------------------------ the tape

        private static bool ConfigureRenderer(Shader shader)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);

            if (renderer == null)
            {
                Debug.LogError($"[Render] '{RendererPath}' is missing.");
                return false;
            }

            RemoveExistingFeatures(renderer);

            var feature = ScriptableObject.CreateInstance<RetroFilmFeature>();
            feature.name = FeatureName;

            var featureSerialized = new SerializedObject(feature);
            featureSerialized.FindProperty("shader").objectReferenceValue = shader;
            featureSerialized.FindProperty("pixelHeight").floatValue =
                Mathf.Round(ReferenceHeight * RenderScale);
            featureSerialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(feature, renderer);

            // The list and the id map are written together. The map is how the renderer
            // re-associates a feature with its sub-asset after a reimport, and a list entry
            // without one comes back null — as a renderer that silently stopped applying the
            // effect, which is the worst possible way for this to fail.
            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");

            if (features == null || map == null)
            {
                Debug.LogError("[Render] PC_Renderer has no feature list. The URP version and " +
                               "this builder have drifted apart.");
                return false;
            }

            var index = features.arraySize;
            features.InsertArrayElementAtIndex(index);
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;

            map.InsertArrayElementAtIndex(index);
            map.GetArrayElementAtIndex(index).longValue = LocalIdOf(feature);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);

            return true;
        }

        /// <remarks>
        /// Removed from the list, the map and the asset file. Leaving an orphaned sub-asset
        /// behind is not cosmetic: it is still a live feature object inside the renderer, and
        /// two of them means the tape runs twice.
        /// </remarks>
        private static void RemoveExistingFeatures(ScriptableRendererData renderer)
        {
            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");

            if (features == null || map == null) return;

            var stale = new List<Object>();

            for (var i = features.arraySize - 1; i >= 0; i--)
            {
                var element = features.GetArrayElementAtIndex(i);
                var value = element.objectReferenceValue;

                // A null entry is an orphan from a previous run whose sub-asset went away. It
                // has to go too, or the map and the list stop lining up by index.
                if (value != null && value is not RetroFilmFeature) continue;

                if (value != null) stale.Add(value);

                // Unity clears an object reference on the first delete rather than removing
                // the element, so the second call is what actually shortens the array.
                features.DeleteArrayElementAtIndex(i);

                if (features.arraySize > i && features.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    features.DeleteArrayElementAtIndex(i);

                if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var feature in stale)
            {
                AssetDatabase.RemoveObjectFromAsset(feature);
                Object.DestroyImmediate(feature, true);
            }
        }

        private static long LocalIdOf(Object asset)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out var localId);
            return localId;
        }

        private static bool TrySet(SerializedObject serialized, string field,
            System.Action<SerializedProperty> write)
        {
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Render] '{serialized.targetObject.name}' has no field " +
                               $"'{field}'. The URP version and this builder have drifted apart.");
                return false;
            }

            write(property);
            return true;
        }
    }
}
