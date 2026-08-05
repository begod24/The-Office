using System.Collections.Generic;
using Office.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Office.Editor
{
    internal static class HudBuilder
    {
        private const string RootName = "[HUD]";

        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private const int SquadRows = 4;
        private const int HealthSegments = 12;
        private const int HotbarSlots = 5;
        private const int ObjectiveRows = 3;

        private const float SlotSize = 66f;
        private const float SlotSpacing = 8f;
        private const float ScreenMargin = 28f;

        private static readonly Color Frame = new(0.75f, 0.76f, 0.78f, 0.45f);
        private static readonly Color Fill = new(0.02f, 0.02f, 0.03f, 0.55f);
        private static readonly Color TextPrimary = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color TextDim = new(0.72f, 0.72f, 0.70f, 0.70f);
        private static readonly Color Rule = new(1f, 1f, 1f, 0.12f);
        private static readonly Color Portrait = new(0.18f, 0.18f, 0.20f, 1f);
        private static readonly Color Crosshair = new(0.90f, 0.90f, 0.88f, 0.50f);
        private static readonly Color SegmentFilled = new(0.86f, 0.86f, 0.84f, 1f);

        private static TMP_FontAsset font;

        [MenuItem("Office/Setup/Rebuild HUD In Open Scene", priority = 43)]
        public static void RebuildInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName)
                    Object.DestroyImmediate(root);

            if (!Build()) return;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Setup] HUD rebuilt in '{scene.name}'. Save the scene to keep it.");
        }

        public static bool Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (font == null && !AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("[Setup] The HUD needs a font. Run " +
                               "'Office/Setup/Import TextMeshPro Essentials' first.");
                return false;
            }

            var root = new GameObject(RootName);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 10;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Match height so corner panels keep the same size relative to the screen
            // on every aspect ratio; ultrawide monitors only gain horizontal space.
            scaler.matchWidthOrHeight = 1f;

            var group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var screen = root.AddComponent<HudScreen>();

            var objectives = BuildObjectives(root.transform);
            var squad = BuildSquad(root.transform);
            var hotbar = BuildHotbar(root.transform);
            var crosshair = BuildCrosshair(root.transform);

            Wire(screen,
                ("objectives", objectives),
                ("squad", squad),
                ("hotbar", hotbar),
                ("crosshair", crosshair));

            return true;
        }

        private static HudObjectivesPanel BuildObjectives(Transform parent)
        {
            var panel = CreateFrame("Objectives", parent);
            var content = panel.Content;

            panel.Root.anchorMin = new Vector2(0f, 1f);
            panel.Root.anchorMax = new Vector2(0f, 1f);
            panel.Root.pivot = new Vector2(0f, 1f);
            panel.Root.anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);
            panel.Root.sizeDelta = new Vector2(330f, 148f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", content, "OBJECTIVES", 18f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            title.characterSpacing = 10f;
            AddLayoutElement(title.gameObject, preferredHeight: 22f);

            var rule = CreateRect("Rule", content);
            CreateImage(rule, Rule);
            AddLayoutElement(rule.gameObject, preferredHeight: 1f);

            var rows = new Object[ObjectiveRows];
            for (var i = 0; i < ObjectiveRows; i++) rows[i] = BuildObjectiveRow(content, i);

            var component = content.gameObject.AddComponent<HudObjectivesPanel>();
            WireArray(component, "rows", rows);

            return component;
        }

        private static HudObjectiveRow BuildObjectiveRow(RectTransform parent, int index)
        {
            var row = CreateRect($"Objective_{index + 1}", parent);
            AddLayoutElement(row.gameObject, preferredHeight: 24f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var holder = CreateRect("Box", row);
            AddLayoutElement(holder.gameObject, preferredWidth: 16f, flexibleWidth: 0f);

            var box = CreateFrame("Frame", holder);
            Centre(box.Root, new Vector2(14f, 14f));

            var marker = CreateRect("Marker", box.Content);
            Stretch(marker, 2f);
            var markerImage = CreateImage(marker, TextPrimary);
            markerImage.enabled = false;

            var label = CreateLabel("Label", row, "---", 17f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(label.gameObject, flexibleWidth: 1f);

            var component = row.gameObject.AddComponent<HudObjectiveRow>();
            Wire(component, ("label", label), ("marker", markerImage));

            return component;
        }

        private static HudSquadPanel BuildSquad(Transform parent)
        {
            const float rowHeight = 30f;
            const float spacing = 4f;

            var panel = CreateFrame("Squad", parent);
            var content = panel.Content;

            panel.Root.anchorMin = Vector2.zero;
            panel.Root.anchorMax = Vector2.zero;
            panel.Root.pivot = Vector2.zero;
            panel.Root.anchoredPosition = new Vector2(ScreenMargin, ScreenMargin);
            panel.Root.sizeDelta = new Vector2(300f,
                SquadRows * rowHeight + (SquadRows - 1) * spacing + 20f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rows = new Object[SquadRows];
            for (var i = 0; i < SquadRows; i++) rows[i] = BuildSquadRow(content, i, rowHeight);

            var component = content.gameObject.AddComponent<HudSquadPanel>();
            WireArray(component, "rows", rows);

            return component;
        }

        private static HudPlayerRow BuildSquadRow(RectTransform parent, int index, float height)
        {
            var row = CreateRect($"Player_{index + 1}", parent);
            AddLayoutElement(row.gameObject, preferredHeight: height);

            var background = CreateImage(row, new Color(0.08f, 0.08f, 0.09f, 0.35f));

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 8, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var portraitHolder = CreateRect("Portrait", row);
            AddLayoutElement(portraitHolder.gameObject, preferredWidth: 24f, flexibleWidth: 0f);

            var portraitFrame = CreateFrame("Frame", portraitHolder);
            Centre(portraitFrame.Root, new Vector2(24f, 24f));

            var portrait = portraitFrame.Fill;
            portrait.color = Portrait;

            var label = CreateLabel("Tag", row, $"P{index + 1}", 17f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(label.gameObject, preferredWidth: 30f, flexibleWidth: 0f);

            var bar = BuildHealthBar(row);

            var component = row.gameObject.AddComponent<HudPlayerRow>();
            Wire(component,
                ("label", label),
                ("health", bar),
                ("portrait", portrait),
                ("background", background));

            return component;
        }

        private static HudSegmentBar BuildHealthBar(RectTransform parent)
        {
            var bar = CreateRect("Health", parent);
            AddLayoutElement(bar.gameObject, flexibleWidth: 1f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var segments = new Object[HealthSegments];

            for (var i = 0; i < HealthSegments; i++)
            {
                var segment = CreateRect($"Tick_{i + 1}", bar);
                segment.sizeDelta = new Vector2(5f, 13f);
                segments[i] = CreateImage(segment, SegmentFilled);
            }

            var component = bar.gameObject.AddComponent<HudSegmentBar>();
            WireArray(component, "segments", segments);

            return component;
        }

        private static HudHotbar BuildHotbar(Transform parent)
        {
            var bar = CreateRect("Hotbar", parent);
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, ScreenMargin);
            bar.sizeDelta = new Vector2(
                HotbarSlots * SlotSize + (HotbarSlots - 1) * SlotSpacing, SlotSize);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var slots = new Object[HotbarSlots];
            for (var i = 0; i < HotbarSlots; i++) slots[i] = BuildSlot(bar, i);

            var component = bar.gameObject.AddComponent<HudHotbar>();
            WireArray(component, "slots", slots);

            return component;
        }

        private static HudSlot BuildSlot(RectTransform parent, int index)
        {
            var slot = CreateFrame($"Slot_{index + 1}", parent);
            var content = slot.Content;
            slot.Root.sizeDelta = new Vector2(SlotSize, SlotSize);

            var icon = CreateRect("Icon", content);
            Stretch(icon, 8f);
            var iconImage = CreateImage(icon, Color.white);
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            var number = CreateLabel("Number", content, (index + 1).ToString(), 13f,
                TextAlignmentOptions.TopLeft, TextDim);
            number.rectTransform.anchorMin = new Vector2(0f, 1f);
            number.rectTransform.anchorMax = new Vector2(0f, 1f);
            number.rectTransform.pivot = new Vector2(0f, 1f);
            number.rectTransform.sizeDelta = new Vector2(20f, 18f);
            number.rectTransform.anchoredPosition = new Vector2(4f, -2f);

            var count = CreateLabel("Count", content, string.Empty, 15f,
                TextAlignmentOptions.BottomRight, TextPrimary);
            count.rectTransform.anchorMin = new Vector2(1f, 0f);
            count.rectTransform.anchorMax = new Vector2(1f, 0f);
            count.rectTransform.pivot = new Vector2(1f, 0f);
            count.rectTransform.sizeDelta = new Vector2(28f, 18f);
            count.rectTransform.anchoredPosition = new Vector2(-4f, 3f);
            count.enabled = false;

            var component = slot.Root.gameObject.AddComponent<HudSlot>();
            Wire(component,
                ("icon", iconImage),
                ("numberLabel", number),
                ("countLabel", count));

            WireArray(component, "frameEdges", slot.Edges);

            return component;
        }

        private static GameObject BuildCrosshair(Transform parent)
        {
            var root = CreateRect("Crosshair", parent);
            Centre(root, new Vector2(16f, 16f));

            var horizontal = CreateRect("Horizontal", root);
            Centre(horizontal, new Vector2(14f, 2f));
            CreateImage(horizontal, Crosshair);

            var vertical = CreateRect("Vertical", root);
            Centre(vertical, new Vector2(2f, 14f));
            CreateImage(vertical, Crosshair);

            return root.gameObject;
        }

        private readonly struct Panel
        {
            public readonly RectTransform Root;

            public readonly RectTransform Content;

            public readonly Image Fill;

            public readonly Image[] Edges;

            public Panel(RectTransform root, RectTransform content, Image fill, Image[] edges)
            {
                Root = root;
                Content = content;
                Fill = fill;
                Edges = edges;
            }
        }

        private static Panel CreateFrame(string name, Transform parent)
        {
            var root = CreateRect(name, parent);

            var fillRect = CreateRect("Fill", root);
            Stretch(fillRect);
            var fill = CreateImage(fillRect, Fill);

            var edges = new[]
            {
                CreateEdge("Edge_Top", root, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f)),
                CreateEdge("Edge_Bottom", root, Vector2.zero, new Vector2(1f, 0f),
                    new Vector2(0f, 1f)),
                CreateEdge("Edge_Left", root, Vector2.zero, new Vector2(0f, 1f),
                    new Vector2(1f, 0f)),
                CreateEdge("Edge_Right", root, new Vector2(1f, 0f), Vector2.one,
                    new Vector2(1f, 0f))
            };

            var content = CreateRect("Content", root);
            Stretch(content, 1f);

            return new Panel(root, content, fill, edges);
        }

        private static Image CreateEdge(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;

            rect.pivot = Mathf.Approximately(anchorMin.x, anchorMax.x)
                ? new Vector2(anchorMin.x, 0.5f)
                : new Vector2(0.5f, anchorMin.y);

            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            return CreateImage(rect, Frame);
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

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Centre(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void AddLayoutElement(GameObject target, float preferredHeight = -1f,
            float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.AddComponent<LayoutElement>();

            if (preferredHeight > 0f)
            {
                element.minHeight = preferredHeight;
                element.preferredHeight = preferredHeight;
            }

            if (preferredWidth > 0f)
            {
                element.minWidth = preferredWidth;
                element.preferredWidth = preferredWidth;
            }

            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
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
