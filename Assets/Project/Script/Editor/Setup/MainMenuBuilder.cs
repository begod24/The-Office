using Office.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Office.Editor
{
    internal static class MainMenuBuilder
    {
        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private static readonly Color Backdrop = new(0.015f, 0.015f, 0.02f, 1f);
        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color TextNormal = new(0.60f, 0.60f, 0.58f, 1f);
        private static readonly Color TextDim = new(0.42f, 0.42f, 0.41f, 1f);

        private static readonly (string Label, MainMenuAction Action)[] Items =
        {
            ("Continue", MainMenuAction.Continue),
            ("New Game", MainMenuAction.NewGame),
            ("Join Friends", MainMenuAction.JoinFriends),
            ("Host Lobby", MainMenuAction.HostLobby),
            ("Settings", MainMenuAction.Settings),
            ("Credits", MainMenuAction.Credits),
            ("Exit", MainMenuAction.Exit)
        };

        private static TMP_FontAsset font;

        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (font == null)
                Debug.LogWarning($"[Setup] {FontPath} is missing — the menu falls back " +
                                 "to the default TMP font.");

            BuildCamera();
            BuildEventSystem();

            var canvas = BuildCanvas();

            // Placeholder — swap the colour for the office-render background art later.
            var backdrop = CreateRect("Background", canvas.transform);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = Backdrop;
            backdropImage.raycastTarget = false;

            var menu = BuildMenuColumn(canvas.transform, out var buildLabel, out var hintLabel,
                out var items);

            var credits = BuildCreditsColumn(canvas.transform, out var creditsBack);
            credits.gameObject.SetActive(false);

            var screenObject = new GameObject("[MainMenuScreen]");
            var screen = screenObject.AddComponent<MainMenuScreen>();

            Wire(screen,
                ("buildLabel", buildLabel),
                ("hintLabel", hintLabel),
                ("menuGroup", menu.gameObject),
                ("creditsGroup", credits.gameObject),
                ("creditsBackButton", creditsBack));
            WireArray(screen, "items", items);
        }

        private static RectTransform BuildCreditsColumn(Transform parent, out Button backButton)
        {
            var column = CreateColumn("CreditsColumn", parent);

            var title = CreateLabel("Title", column, "CREDITS", 84f,
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

            BuildCreditRow(column, "GAME DESIGNER & DEVELOPER", "Bekbolat Aldiyarov");
            BuildCreditRow(column, "3D MODELS & LEVEL DESIGN", "Sanzhar");
            BuildCreditRow(column, "MUSIC ARTIST", "Nurezh");

            var flexible = CreateRect("FlexibleSpacer", column);
            var flexibleElement = flexible.gameObject.AddComponent<LayoutElement>();
            flexibleElement.flexibleHeight = 1f;

            backButton = CreateTerminalButton("BackButton", column, "Back", 44f, 30f);

            return column;
        }

        private static void BuildCreditRow(RectTransform parent, string role, string person)
        {
            var roleLabel = CreateLabel($"Role_{person}", parent, role, 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            roleLabel.characterSpacing = 6f;
            AddLayoutElement(roleLabel.gameObject, preferredHeight: 28f);

            var personLabel = CreateLabel($"Person_{person}", parent, person, 36f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(personLabel.gameObject, preferredHeight: 44f);

            var spacer = CreateRect($"Spacer_{person}", parent);
            AddLayoutElement(spacer.gameObject, preferredHeight: 18f);
        }

        private static RectTransform CreateColumn(string name, Transform parent)
        {
            var column = CreateRect(name, parent);
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

            return column;
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
            button.colors = new ColorBlock
            {
                normalColor = TextNormal,
                highlightedColor = TextPrimary,
                pressedColor = Color.white,
                selectedColor = TextPrimary,
                disabledColor = new Color(0.30f, 0.30f, 0.29f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            return button;
        }

        private static RectTransform BuildMenuColumn(Transform parent, out TMP_Text buildLabel,
            out TMP_Text hintLabel, out Object[] items)
        {
            var column = CreateRect("MenuColumn", parent);
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

            var title = CreateLabel("Title", column, "OFFICE", 132f,
                TextAlignmentOptions.BottomLeft, TextPrimary);
            title.characterSpacing = 4f;
            AddLayoutElement(title.gameObject, preferredHeight: 140f);

            buildLabel = CreateLabel("BuildLabel", column, "Build 0.9.31", 24f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(buildLabel.gameObject, preferredHeight: 30f);

            var pending = CreateLabel("PendingLabel", column, "Release Pending...", 24f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(pending.gameObject, preferredHeight: 30f);

            var spacer = CreateRect("Spacer", column);
            AddLayoutElement(spacer.gameObject, preferredHeight: 56f);

            items = new Object[Items.Length];
            for (var i = 0; i < Items.Length; i++)
                items[i] = BuildItem(column, Items[i].Label, Items[i].Action);

            var flexible = CreateRect("FlexibleSpacer", column);
            var flexibleElement = flexible.gameObject.AddComponent<LayoutElement>();
            flexibleElement.flexibleHeight = 1f;

            hintLabel = CreateLabel("HintLabel", column, string.Empty, 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(hintLabel.gameObject, preferredHeight: 30f);

            return column;
        }

        private static MainMenuItem BuildItem(RectTransform parent, string text,
            MainMenuAction action)
        {
            var row = CreateRect($"Item_{action}", parent);
            AddLayoutElement(row.gameObject, preferredHeight: 48f);

            var hitArea = row.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var label = CreateLabel("Label", row, $"> {text}", 34f,
                TextAlignmentOptions.MidlineLeft, TextNormal);

            var cursorRect = CreateRect("Cursor", row);
            var cursor = cursorRect.gameObject.AddComponent<Image>();
            cursor.color = TextNormal;
            cursor.raycastTarget = false;
            cursor.enabled = false;
            AddLayoutElement(cursorRect.gameObject, preferredHeight: 30f, preferredWidth: 18f);

            var item = row.gameObject.AddComponent<MainMenuItem>();
            Wire(item, ("button", button), ("label", label), ("cursor", cursor));
            WireEnum(item, "action", (int)action);

            return item;
        }

        private static void BuildCamera()
        {
            var cameraObject = new GameObject("MenuCamera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.cullingMask = 0;
        }

        private static void BuildEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas BuildCanvas()
        {
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Match height so the vertical layout is identical on every aspect ratio;
            // wider screens only gain horizontal breathing room.
            scaler.matchWidthOrHeight = 1f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

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

        private static void WireEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}' has no field '{field}'.");
                return;
            }

            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}.{field}' is not a serialised array.");
                return;
            }

            property.arraySize = values.Length;

            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
