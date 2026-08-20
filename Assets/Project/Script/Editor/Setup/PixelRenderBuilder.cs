using System.Collections.Generic;
using Office.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    /// <summary>
    /// Wires the PS1 look into the pipeline: a real low internal resolution on the render
    /// pipeline asset, and the palette pass on its renderer.
    /// </summary>
    /// <remarks>
    /// Both halves live in one menu item because neither is right alone. The render scale is
    /// what makes the picture genuinely low resolution and therefore genuinely pixelated; the
    /// feature is what stops a low-resolution picture reading as a small window by giving it a
    /// period-correct palette.
    /// <para>
    /// <b>This replaced the VHS build.</b> The old pass and its shader are gone — see
    /// <see cref="PixelArtFeature"/> for why a moving artefact and a pixel grid cannot share a
    /// screen. Re-running this removes anything the old builder left behind, by name, because
    /// the type it was serialised as no longer exists to match on.
    /// </para>
    /// <para>
    /// Idempotent like every other builder: it removes the feature it previously added before
    /// adding one, so re-running it leaves exactly one — and it leaves every feature it did not
    /// author, such as the ambient occlusion, alone.
    /// </para>
    /// </remarks>
    internal static class PixelRenderBuilder
    {
        private const string SettingsFolder = "Assets/Project/Settings";
        private const string PipelinePath = SettingsFolder + "/PC_RPAsset.asset";
        private const string RendererPath = SettingsFolder + "/PC_Renderer.asset";
        private const string ShaderPath = "Assets/Project/Art/Shaders/S_PixelArt.shader";

        private const string FeatureName = "Pixel Art";

        /// <summary>Names this builder is allowed to delete off the renderer.</summary>
        /// <remarks>
        /// The old VHS feature is matched by name rather than by type: its class was deleted
        /// with the effect, so the sub-asset left inside PC_Renderer has no script to load and
        /// cannot be tested with <c>is</c>. Left there it is a broken entry that logs on every
        /// import.
        /// </remarks>
        private static readonly string[] OwnedNames = { FeatureName, "Retro Film" };

        /// <summary>
        /// Fraction of the window the picture is actually rendered at. Half of 1080p is
        /// 960×540.
        /// </summary>
        /// <remarks>
        /// <b>This is the one number to move if the look needs adjusting</b>, and it was moved
        /// twice already. 0.30 put the grid unmistakably in the foreground and read as too
        /// coarse; 0.62 is so clean the grid is only felt on high-contrast edges. Half sits
        /// where the reference frames do: the blocks are plainly there on an edge or a
        /// gradient, and a room still reads as a room at a glance.
        /// <para>
        /// Worth knowing which way the failure lies. Going lower is not free legibility spent
        /// on style — at quarter scale a stapler across an unlit office is about four pixels,
        /// and a player cannot be frightened by a shape they cannot resolve. Going higher
        /// costs nothing but the look itself, which is why the safe direction to experiment in
        /// is up.
        /// </para>
        /// </remarks>
        private const float RenderScale = 0.50f;

        /// <summary>Nearest-neighbour. <see cref="UpscalingFilterSelection.Point"/>.</summary>
        private const int PointUpscale = 2;

        [MenuItem("Office/Setup/Build Pixel Render", priority = 25)]
        public static void Build()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (shader == null)
            {
                Debug.LogError($"[Render] '{ShaderPath}' is missing. The pixel pass cannot be " +
                               "built without its shader.");
                return;
            }

            if (!ConfigurePipeline()) return;
            if (!ConfigureRenderer(shader)) return;

            AssetDatabase.SaveAssets();

            Debug.Log($"[Render] Pixel render built: {RenderScale:0.##} render scale with a " +
                      "point upscale, and the palette pass on PC_Renderer. It shows in the " +
                      "Game view only — the Scene view is left clean so the level stays " +
                      "workable.");
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

        // ------------------------------------------------------------------ the palette

        private static bool ConfigureRenderer(Shader shader)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);

            if (renderer == null)
            {
                Debug.LogError($"[Render] '{RendererPath}' is missing.");
                return false;
            }

            RemoveOwnedFeatures(renderer);

            var feature = ScriptableObject.CreateInstance<PixelArtFeature>();
            feature.name = FeatureName;

            var featureSerialized = new SerializedObject(feature);
            featureSerialized.FindProperty("shader").objectReferenceValue = shader;
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
        /// two of them means the pass runs twice.
        /// <para>
        /// Only what this builder authored. The renderer also carries features nobody here
        /// added — the ambient occlusion is one — and a purge that cleared the list would take
        /// them with it and leave no trace of what went missing.
        /// </para>
        /// </remarks>
        private static void RemoveOwnedFeatures(ScriptableRendererData renderer)
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

                // A null entry is either an orphan from a previous run whose sub-asset went
                // away, or the old VHS feature whose script no longer exists. Both have to go,
                // or the map and the list stop lining up by index.
                if (value != null && !IsOwned(value)) continue;

                if (value != null) stale.Add(value);

                // Unity clears an object reference on the first delete rather than removing
                // the element, so the second call is what actually shortens the array.
                features.DeleteArrayElementAtIndex(i);

                if (features.arraySize > i &&
                    features.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    features.DeleteArrayElementAtIndex(i);

                if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var feature in stale)
            {
                AssetDatabase.RemoveObjectFromAsset(feature);
                Object.DestroyImmediate(feature, true);
            }

            // Sub-assets whose script is gone are not reachable through the list any more, but
            // they are still inside the file. Sweeping the asset itself is the only way to
            // reach one, and it is the difference between a clean rebuild and a renderer that
            // logs a missing MonoBehaviour on every import from now on.
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer)))
            {
                if (sub == null || sub == renderer) continue;
                if (sub is not ScriptableObject || !IsOwned(sub)) continue;
                if (IsListed(renderer, sub)) continue;

                AssetDatabase.RemoveObjectFromAsset(sub);
                Object.DestroyImmediate(sub, true);
            }
        }

        private static bool IsOwned(Object candidate)
        {
            if (candidate is PixelArtFeature) return true;

            foreach (var name in OwnedNames)
                if (candidate.name == name)
                    return true;

            return false;
        }

        private static bool IsListed(ScriptableRendererData renderer, Object candidate)
        {
            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");

            if (features == null) return false;

            for (var i = 0; i < features.arraySize; i++)
                if (features.GetArrayElementAtIndex(i).objectReferenceValue == candidate)
                    return true;

            return false;
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
