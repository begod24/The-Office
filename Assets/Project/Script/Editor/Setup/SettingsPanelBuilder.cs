using Office.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Office.Editor
{
    // Terminal-styled settings column (resolution picker), shared by the main menu
    // and pause menu builders. The host screen owns showing/hiding it and the Back
    // button; SettingsScreen owns the resolution logic.
    internal static class SettingsPanelBuilder
    {
        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color TextNormal = new(0.60f, 0.60f, 0.58f, 1f);
        private static readonly Color TextDim = new(0.42f, 0.42f, 0.41f, 1f);

        private static TMP_FontAsset font;

        public static RectTransform Build(Transform parent, out Button backButton)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var column = CreateRect("SettingsColumn", parent);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.offsetMin = new Vector2(110f, 84f);
            column.offsetMax = new Vector2(750f, -84f);

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", column, "SETTINGS", 84f,
                TextAlignmentOptions.BottomLeft, TextPrimary);
            title.characterSpacing = 4f;
            AddLayoutElement(title.gameObject, preferredHeight: 92f);

            var rule = CreateRect("Rule", column);
            var ruleImage = rule.gameObject.AddComponent<Image>();
            ruleImage.color = new Color(1f, 1f, 1f, 0.08f);
            ruleImage.raycastTarget = false;
            AddLayoutElement(rule.gameObject, preferredHeight: 2f);

            var spacer = CreateRect("Spacer", column);
            AddLayoutElement(spacer.gameObject, preferredHeight: 40f);

            var resolutionValue = BuildResolutionRow(column, out var prevButton, out var nextButton);

            var flexible = CreateRect("FlexibleSpacer", column);
            var flexibleElement = flexible.gameObject.AddComponent<LayoutElement>();
            flexibleElement.flexibleHeight = 1f;

            // Bottom row: Back on the left, Apply on the right.
            var bottomRow = CreateRect("BottomRow", column);
            AddLayoutElement(bottomRow.gameObject, preferredHeight: 44f);

            var bottomLayout = bottomRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.childAlignment = TextAnchor.MiddleLeft;
            bottomLayout.childControlWidth = true;
            bottomLayout.childControlHeight = true;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = false;

            backButton = CreateTerminalButton("BackButton", bottomRow, "Back", 44f, 30f);

            var bottomSpacer = CreateRect("Spacer", bottomRow);
            var bottomSpacerElement = bottomSpacer.gameObject.AddComponent<LayoutElement>();
            bottomSpacerElement.flexibleWidth = 1f;

            var applyButton = CreateTerminalButton("ApplyButton", bottomRow, "Apply", 44f, 30f);

            var screen = column.gameObject.AddComponent<SettingsScreen>();
            Wire(screen,
                ("resolutionValue", resolutionValue),
                ("prevButton", prevButton),
                ("nextButton", nextButton),
                ("applyButton", applyButton));

            return column;
        }

        private static TMP_Text BuildResolutionRow(RectTransform parent, out Button prevButton,
            out Button nextButton)
        {
            var caption = CreateLabel("ResolutionCaption", parent, "RESOLUTION", 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            caption.characterSpacing = 6f;
            AddLayoutElement(caption.gameObject, preferredHeight: 28f);

            var row = CreateRect("ResolutionRow", parent);
            AddLayoutElement(row.gameObject, preferredHeight: 48f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            prevButton = CreateArrowButton("PrevButton", row, "<");

            var value = CreateLabel("Value", row, "1920 x 1080", 32f,
                TextAlignmentOptions.Center, TextPrimary);
            AddLayoutElement(value.gameObject, preferredWidth: 280f, preferredHeight: 40f);

            nextButton = CreateArrowButton("NextButton", row, ">");

            return value;
        }

        private static Button CreateArrowButton(string name, RectTransform parent, string text)
        {
            var holder = CreateRect(name, parent);
            AddLayoutElement(holder.gameObject, preferredWidth: 44f, preferredHeight: 44f);

            var hitArea = holder.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            var label = CreateLabel("Label", holder, text, 34f,
                TextAlignmentOptions.Center, Color.white);
            Stretch((RectTransform)label.transform);

            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColours();

            return button;
        }

        private static Button CreateTerminalButton(string name, Transform parent, string text,
            float height, float size)
        {
            var row = CreateRect(name, parent);
            AddLayoutElement(row.gameObject, preferredHeight: height);

            var hitArea = row.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateLabel("Prefix", row, ">", size, TextAlignmentOptions.MidlineLeft, TextDim);

            var label = CreateLabel("Label", row, text, size, TextAlignmentOptions.MidlineLeft,
                Color.white);

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColours();

            return button;
        }

        private static ColorBlock ButtonColours() => new()
        {
            normalColor = TextNormal,
            highlightedColor = TextPrimary,
            pressedColor = Color.white,
            selectedColor = TextPrimary,
            disabledColor = new Color(0.30f, 0.30f, 0.29f, 1f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private static void AddLayoutElement(GameObject target, float preferredHeight = -1f,
            float preferredWidth = -1f)
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
    }
}
