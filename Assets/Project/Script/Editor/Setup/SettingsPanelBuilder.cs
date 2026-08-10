using Office.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Office.Editor
{
    // Terminal-styled settings column, shared by the main menu and pause menu builders. The
    // host screen owns showing/hiding it and the Back button; SettingsScreen owns what the
    // rows do, and ISettingsService owns what they mean.
    internal static class SettingsPanelBuilder
    {
        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private const float RowHeight = 48f;
        private const float CaptionHeight = 28f;
        private const float SliderWidth = 380f;
        private const float SliderHeight = 28f;
        private const float TrackThickness = 4f;
        private const float HandleWidth = 14f;

        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color TextNormal = new(0.60f, 0.60f, 0.58f, 1f);
        private static readonly Color TextDim = new(0.42f, 0.42f, 0.41f, 1f);
        private static readonly Color TrackDark = new(1f, 1f, 1f, 0.12f);

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

            AddSpacer(column, "TitleSpacer", 32f);

            var displayModeValue = BuildPickerRow(column, "DisplayMode", "DISPLAY MODE",
                "Fullscreen", out var displayModePrev, out var displayModeNext);

            var resolutionValue = BuildPickerRow(column, "Resolution", "RESOLUTION",
                "1920 x 1080", out var resolutionPrev, out var resolutionNext);

            AddSpacer(column, "AudioSpacer", 24f);

            var masterSlider = BuildSliderRow(column, "Master", "MASTER VOLUME",
                out var masterValue);

            var musicSlider = BuildSliderRow(column, "Music", "MUSIC VOLUME", out var musicValue);

            var flexible = CreateRect("FlexibleSpacer", column);
            var flexibleElement = flexible.gameObject.AddComponent<LayoutElement>();
            flexibleElement.flexibleHeight = 1f;

            // Says what Apply did, or why it could not. Above the buttons rather than beside
            // them: the line grows and the row must not reflow when it does.
            var noteLabel = CreateLabel("NoteLabel", column, string.Empty, 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(noteLabel.gameObject, preferredHeight: CaptionHeight);

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
                ("displayModeValue", displayModeValue),
                ("displayModePrevButton", displayModePrev),
                ("displayModeNextButton", displayModeNext),
                ("resolutionValue", resolutionValue),
                ("resolutionPrevButton", resolutionPrev),
                ("resolutionNextButton", resolutionNext),
                ("applyButton", applyButton),
                ("masterSlider", masterSlider),
                ("masterValue", masterValue),
                ("musicSlider", musicSlider),
                ("musicValue", musicValue),
                ("noteLabel", noteLabel));

            return column;
        }

        private static TMP_Text BuildPickerRow(RectTransform parent, string name, string caption,
            string placeholder, out Button prevButton, out Button nextButton)
        {
            AddCaption(parent, $"{name}Caption", caption);

            var row = CreateRect($"{name}Row", parent);
            AddLayoutElement(row.gameObject, preferredHeight: RowHeight);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            prevButton = CreateArrowButton("PrevButton", row, "<");

            var value = CreateLabel("Value", row, placeholder, 32f,
                TextAlignmentOptions.Center, TextPrimary);
            AddLayoutElement(value.gameObject, preferredWidth: 280f, preferredHeight: 40f);

            nextButton = CreateArrowButton("NextButton", row, ">");

            return value;
        }

        private static Slider BuildSliderRow(RectTransform parent, string name, string caption,
            out TMP_Text value)
        {
            AddCaption(parent, $"{name}Caption", caption);

            var row = CreateRect($"{name}Row", parent);
            AddLayoutElement(row.gameObject, preferredHeight: RowHeight);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var slider = CreateSlider("Slider", row);

            value = CreateLabel("Value", row, "100%", 28f,
                TextAlignmentOptions.MidlineRight, TextPrimary);
            AddLayoutElement(value.gameObject, preferredWidth: 96f, preferredHeight: 36f);

            return slider;
        }

        /// <summary>
        /// A slider drawn as a terminal readout: a hairline track, a lit fill and a block for
        /// a handle.
        /// </summary>
        /// <remarks>
        /// Built by hand rather than from Unity's default, whose sprites and 20px handle do
        /// not belong on this screen. The geometry below is the same one uGUI expects —
        /// <c>Slider</c> writes the anchors of the fill and the handle every time the value
        /// moves, so both have to be children of rects it can drive rather than of the row's
        /// layout group, which would fight it for the same numbers.
        /// </remarks>
        private static Slider CreateSlider(string name, Transform parent)
        {
            var root = CreateRect(name, parent);
            AddLayoutElement(root.gameObject, preferredHeight: SliderHeight,
                preferredWidth: SliderWidth);

            // The whole bar takes the drag, not just the four pixels the track draws — the
            // same invisible hit area the terminal buttons use.
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            var background = CreateRect("Background", root);
            background.anchorMin = new Vector2(0f, 0.5f);
            background.anchorMax = new Vector2(1f, 0.5f);
            background.sizeDelta = new Vector2(0f, TrackThickness);

            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = TrackDark;
            backgroundImage.raycastTarget = false;

            var fillArea = CreateRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);

            // Inset by half a handle at each end so the fill stops under the handle rather
            // than sticking out past it at the extremes.
            fillArea.sizeDelta = new Vector2(-HandleWidth, TrackThickness);

            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);

            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = TextPrimary;
            fillImage.raycastTarget = false;

            var handleArea = CreateRect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(HandleWidth * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-HandleWidth * 0.5f, 0f);

            var handle = CreateRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(HandleWidth, 0f);

            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = TextPrimary;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.transition = Selectable.Transition.ColorTint;
            slider.colors = ButtonColours();

            return slider;
        }

        private static void AddCaption(RectTransform parent, string name, string text)
        {
            var caption = CreateLabel(name, parent, text, 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            caption.characterSpacing = 6f;
            AddLayoutElement(caption.gameObject, preferredHeight: CaptionHeight);
        }

        private static void AddSpacer(RectTransform parent, string name, float height)
        {
            var spacer = CreateRect(name, parent);
            AddLayoutElement(spacer.gameObject, preferredHeight: height);
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
