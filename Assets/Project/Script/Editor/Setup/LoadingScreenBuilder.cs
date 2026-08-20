using System.Collections.Generic;
using Office.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Office.Editor
{
    internal static class LoadingScreenBuilder
    {
        private const string RootName = "[LoadingScreen]";

        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";
        private const string ArtPath = "Assets/Project/Art/Loader/Image_Loader.png";

        private const int Segments = 44;

        private const float BarWidth = 620f;
        private const float BarHeight = 26f;
        private const float Margin = 84f;

        private static readonly Color Backdrop = new(0.015f, 0.015f, 0.02f, 1f);
        private static readonly Color TextPrimary = new(0.82f, 0.86f, 0.88f, 1f);
        private static readonly Color TextDim = new(0.62f, 0.66f, 0.68f, 0.85f);
        private static readonly Color SegmentLit = new(0.78f, 0.84f, 0.86f, 1f);
        private static readonly Color SegmentDark = new(0.62f, 0.68f, 0.70f, 0.16f);

        private static TMP_FontAsset font;

        [MenuItem("Office/Setup/Build Loading Screen In Open Scene", priority = 45)]
        public static void RebuildInOpenScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName)
                    Object.DestroyImmediate(root);

            if (Build() == null) return;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Setup] Loading screen rebuilt in '{scene.name}'. Save the scene to keep it.");
        }

        public static GameObject Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (font == null && !AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("[Setup] The loading screen needs a font. Run " +
                               "'Office/Setup/Import TextMeshPro Essentials' first.");
                return null;
            }

            var root = new GameObject(RootName);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 200;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            root.AddComponent<GraphicRaycaster>();

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            BuildBackdrop(root.transform);

            var title = CreateLabel("Title", root.transform, "OFFICE TECH", 46f,
                TextAlignmentOptions.TopLeft, TextPrimary);
            PlaceTopLeft(title.rectTransform, new Vector2(760f, 60f), Vector2.zero);
            title.characterSpacing = 8f;

            var status = CreateLabel("Status", root.transform, "SYSTEM BOOT...", 30f,
                TextAlignmentOptions.TopLeft, TextDim);
            PlaceTopLeft(status.rectTransform, new Vector2(760f, 40f), new Vector2(18f, -66f));
            status.characterSpacing = 6f;

            var bar = BuildBar(root.transform, out var percent);

            var screen = root.AddComponent<LoadingScreen>();

            Wire(screen,
                ("group", group),
                ("bar", bar),
                ("percentLabel", percent),
                ("statusLabel", status));

            return root;
        }

        private static void BuildBackdrop(Transform parent)
        {
            var fill = CreateRect("Backdrop", parent);
            Stretch(fill);
            CreateImage(fill, Backdrop);

            var sprite = LoadArtSprite();
            if (sprite == null) return;

            var art = CreateRect("Art", parent);
            Stretch(art);

            var image = art.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            image.preserveAspect = false;
            image.type = Image.Type.Simple;
        }

        private static Sprite LoadArtSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (existing != null) return existing;

            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning($"[Setup] {ArtPath} not found. The loading screen falls back " +
                                 "to a plain dark backdrop.");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        }

        private static HudSegmentBar BuildBar(Transform parent, out TMP_Text percentLabel)
        {
            var row = CreateRect("BootBar", parent);
            PlaceTopLeft(row, new Vector2(BarWidth + 160f, BarHeight), new Vector2(18f, -120f));

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var track = CreateRect("Track", row);

            var trackElement = track.gameObject.AddComponent<LayoutElement>();
            trackElement.preferredWidth = BarWidth;
            trackElement.preferredHeight = BarHeight;

            var ticks = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            ticks.spacing = 3f;
            ticks.childAlignment = TextAnchor.MiddleLeft;
            ticks.childControlWidth = true;
            ticks.childControlHeight = true;
            ticks.childForceExpandWidth = true;
            ticks.childForceExpandHeight = true;

            var segments = new Object[Segments];

            for (var i = 0; i < Segments; i++)
            {
                var segment = CreateRect($"Tick_{i + 1}", track);

                var element = segment.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = 1f;
                element.preferredHeight = BarHeight;

                segments[i] = CreateImage(segment, SegmentLit);
            }

            var bar = track.gameObject.AddComponent<HudSegmentBar>();
            WireArray(bar, "segments", segments);

            var serialized = new SerializedObject(bar);
            SetColour(serialized, "filled", SegmentLit);
            SetColour(serialized, "drained", SegmentDark);
            SetColour(serialized, "critical", SegmentLit);
            serialized.FindProperty("criticalThreshold").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            percentLabel = CreateLabel("Percent", row, "0%", 30f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);

            var percentElement = percentLabel.gameObject.AddComponent<LayoutElement>();
            percentElement.preferredWidth = 120f;
            percentElement.preferredHeight = BarHeight;

            return bar;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(Margin + offset.x, -Margin + offset.y);
        }

        private static void SetColour(SerializedObject serialized, string field, Color colour)
        {
            var property = serialized.FindProperty(field);
            if (property != null) property.colorValue = colour;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private static Image CreateImage(RectTransform rect, Color colour)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateLabel(string name, Transform parent, string text, float size,
            TextAlignmentOptions alignment, Color colour)
        {
            var rect = CreateRect(name, parent);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;

            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = colour;
            label.raycastTarget = false;

            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Setup] '{target.GetType().Name}' has no field '{field}'.");
                    continue;
                }

                if (value == null)
                    Debug.LogError($"[Setup] '{target.GetType().Name}.{field}' was given null.");

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}.{field}' is not a serialised array.");
                return;
            }

            property.arraySize = values.Count;

            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
