using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    /// <summary>
    /// Pipeline-level settings for the dark realism look. The Volume profile handles
    /// grading; this handles the things a Volume cannot reach — grading precision,
    /// shadow range on local lights, and ambient occlusion.
    ///
    /// Two settings are deliberately left where they are. Render scale stays at 1 and
    /// the renderer stays Forward+: the first is what would pixelate the image, the
    /// second is what lets a corridor hold twenty small lights at once.
    /// </summary>
    internal static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/Project/Settings";

        [MenuItem("Office/Setup/Configure Dark Render Pipeline", priority = 25)]
        public static void Configure()
        {
            var assets = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { SettingsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>)
                .Where(asset => asset != null)
                .ToArray();

            if (assets.Length == 0)
            {
                Debug.LogError($"[Render] No UniversalRenderPipelineAsset under {SettingsFolder}.");
                return;
            }

            foreach (var asset in assets) ConfigureAsset(asset);

            var renderers = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { SettingsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRendererData>)
                .Where(data => data != null)
                .ToArray();

            foreach (var renderer in renderers) EnsureAmbientOcclusion(renderer);

            AssetDatabase.SaveAssets();

            Debug.Log($"[Render] {assets.Length} pipeline asset(s) and {renderers.Length} " +
                      "renderer(s) configured for the dark look: HDR grading, local light " +
                      "shadows, SSAO. Render scale and Forward+ left untouched.");
        }

        private static void ConfigureAsset(UniversalRenderPipelineAsset asset)
        {
            // LDR grading banded the near-black gradients this look lives in.
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            asset.colorGradingLutSize = 32;

            asset.supportsHDR = true;
            asset.additionalLightsShadowmapResolution = 2048;

            // The building is 44 m across; beyond that the fog has taken the image anyway.
            asset.shadowDistance = 45f;

            // URP keeps the light-mode and shadow-support setters internal, so these three
            // go through SerializedObject. They are the difference between a corridor lit
            // by twenty small fittings and one lit by an average of them.
            var serialized = new SerializedObject(asset);

            Write(serialized, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            Write(serialized, "m_AdditionalLightShadowsSupported", true);
            Write(serialized, "m_SoftShadowsSupported", true);
            Write(serialized, "m_AnyShadowsSupported", true);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
        }

        private static void Write(SerializedObject serialized, string name, int value)
        {
            var property = serialized.FindProperty(name);

            if (property == null)
            {
                Debug.LogWarning($"[Render] Pipeline asset has no '{name}'. Skipped.");
                return;
            }

            property.intValue = value;
        }

        private static void Write(SerializedObject serialized, string name, bool value)
        {
            var property = serialized.FindProperty(name);

            if (property == null)
            {
                Debug.LogWarning($"[Render] Pipeline asset has no '{name}'. Skipped.");
                return;
            }

            property.boolValue = value;
        }

        private static void EnsureAmbientOcclusion(UniversalRendererData renderer)
        {
            var existing = renderer.rendererFeatures
                .FirstOrDefault(feature => feature is ScreenSpaceAmbientOcclusion);

            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                existing.name = "ScreenSpaceAmbientOcclusion";

                renderer.rendererFeatures.Add(existing);

                AssetDatabase.AddObjectToAsset(existing, renderer);
                renderer.SetDirty();
            }

            Tune(existing);

            // No ImportAsset here: reimporting a renderer immediately after adding a
            // sub-asset makes the native importer report an inconsistent result. SetDirty
            // plus the SaveAssets in Configure is enough to persist it.
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>
        /// SSAO's settings are internal to URP, so they are written through
        /// SerializedObject rather than the C# API.
        /// </summary>
        private static void Tune(ScriptableRendererFeature feature)
        {
            var serialized = new SerializedObject(feature);
            var settings = serialized.FindProperty("m_Settings");

            if (settings == null)
            {
                Debug.LogWarning("[Render] SSAO has no 'm_Settings' field — URP's layout has " +
                                 "changed. The feature is enabled but left at its defaults.");
                return;
            }

            Set(settings, "Intensity", 1.6f);
            Set(settings, "DirectLightingStrength", 0.35f);
            Set(settings, "Radius", 0.28f);
            Set(settings, "Falloff", 60f);
            Set(settings, "AfterOpaque", false);
            Set(settings, "Downsample", false);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedProperty settings, string name, float value)
        {
            var property = settings.FindPropertyRelative(name);

            if (property == null)
            {
                Debug.LogWarning($"[Render] SSAO setting '{name}' not found. Skipped.");
                return;
            }

            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
            else if (property.propertyType == SerializedPropertyType.Integer)
                property.intValue = Mathf.RoundToInt(value);
        }

        private static void Set(SerializedProperty settings, string name, bool value)
        {
            var property = settings.FindPropertyRelative(name);

            if (property == null)
            {
                Debug.LogWarning($"[Render] SSAO setting '{name}' not found. Skipped.");
                return;
            }

            property.boolValue = value;
        }
    }
}
