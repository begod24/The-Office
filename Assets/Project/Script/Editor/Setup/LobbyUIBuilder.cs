using Office.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Office.Editor
{
    internal static class LobbyUIBuilder
    {
        private const string RowPrefabPath = "Assets/Project/Prefab/UI/PF_LobbyRow.prefab";
        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private static readonly Color Backdrop = new(0.015f, 0.015f, 0.02f, 1f);
        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color TextNormal = new(0.60f, 0.60f, 0.58f, 1f);
        private static readonly Color TextDim = new(0.42f, 0.42f, 0.41f, 1f);
        private static readonly Color Rule = new(1f, 1f, 1f, 0.08f);

        private static readonly Color RowRemote = new(0.05f, 0.05f, 0.06f, 1f);

        private static TMP_FontAsset font;

        private static void LoadFont() =>
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        public static void BuildRowPrefab()
        {
            LoadFont();

            var root = new GameObject("PF_LobbyRow", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(0f, 46f);

            var background = root.AddComponent<Image>();
            background.color = RowRemote;

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var element = root.AddComponent<LayoutElement>();
            element.minHeight = 46f;
            element.preferredHeight = 46f;

            element.flexibleHeight = 0f;

            var nameLabel = CreateLabel("Name", rect, "EMPLOYEE 01", 20f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(nameLabel.gameObject, flexibleWidth: 1f);

            var statusLabel = CreateLabel("Status", rect, "WAITING", 18f,
                TextAlignmentOptions.MidlineRight, TextDim);
            AddLayoutElement(statusLabel.gameObject, preferredWidth: 140f);

            var row = root.AddComponent<LobbyPlayerRow>();
            Wire(row,
                ("nameLabel", nameLabel),
                ("statusLabel", statusLabel),
                ("background", background));

            EnsureFolder("Assets/Project/Prefab/UI");
            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
        }

        public static LobbyPlayerRow LoadRowPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);

            if (asset == null)
            {
                Debug.LogError($"[Setup] {RowPrefabPath} is missing. Build it before the scene.");
                return null;
            }

            return asset.GetComponent<LobbyPlayerRow>();
        }

        public static void Build(LobbyPlayerRow rowPrefab)
        {
            LoadFont();

            BuildCamera();
            BuildEventSystem();

            var canvas = BuildCanvas();
            var backdrop = CreateRect("Backdrop", canvas.transform);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = Backdrop;

            var column = CreateRect("Column", canvas.transform);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.offsetMin = new Vector2(110f, 84f);
            column.offsetMax = new Vector2(750f, -84f);

            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 12f;
            columnLayout.childAlignment = TextAnchor.UpperLeft;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            BuildHeader(column);

            var offlineGroup = BuildOfflineGroup(column, out var hostButton, out var codeInput,
                out var joinButton, out var backButton);

            var sessionGroup = BuildSessionGroup(column, out var codeLabel, out var copyButton,
                out var rowRoot, out var readyButton, out var readyLabel, out var startButton,
                out var startLabel, out var leaveButton);

            var status = CreateLabel("Status", column, string.Empty, 18f,
                TextAlignmentOptions.TopLeft, TextDim);
            status.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(status.gameObject, preferredHeight: 60f);

            var screenObject = new GameObject("[LobbyScreen]");
            var screen = screenObject.AddComponent<LobbyScreen>();

            Wire(screen,
                ("offlineGroup", offlineGroup),
                ("sessionGroup", sessionGroup),
                ("hostButton", hostButton),
                ("joinButton", joinButton),
                ("codeInput", codeInput),
                ("backButton", backButton),
                ("codeLabel", codeLabel),
                ("copyButton", copyButton),
                ("readyButton", readyButton),
                ("readyLabel", readyLabel),
                ("startButton", startButton),
                ("startLabel", startLabel),
                ("leaveButton", leaveButton),
                ("rowRoot", rowRoot),
                ("rowPrefab", rowPrefab),
                ("statusLabel", status));
        }

        private static void BuildHeader(RectTransform parent)
        {
            var title = CreateLabel("Title", parent, "OFFICE", 84f,
                TextAlignmentOptions.BottomLeft, TextPrimary);
            title.characterSpacing = 4f;
            AddLayoutElement(title.gameObject, preferredHeight: 92f);

            var subtitle = CreateLabel("Subtitle", parent, "NIGHT SHIFT — LOBBY", 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            subtitle.characterSpacing = 6f;
            AddLayoutElement(subtitle.gameObject, preferredHeight: 28f);

            var rule = CreateRect("Rule", parent);
            rule.gameObject.AddComponent<Image>().color = Rule;
            AddLayoutElement(rule.gameObject, preferredHeight: 2f);

            var spacer = CreateRect("Spacer", parent);
            AddLayoutElement(spacer.gameObject, preferredHeight: 24f);
        }

        private static GameObject BuildOfflineGroup(RectTransform parent, out Button hostButton,
            out TMP_InputField codeInput, out Button joinButton, out Button backButton)
        {
            var group = CreateGroup("OfflineGroup", parent, 10f);

            hostButton = CreateTerminalButton("HostButton", group, "Host a Shift", out _, 48f, 30f);

            var separator = CreateLabel("Or", group, "or join with a code", 20f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(separator.gameObject, preferredHeight: 30f);

            codeInput = CreateInputField("CodeInput", group, "JOIN CODE");
            joinButton = CreateTerminalButton("JoinButton", group, "Join", out _, 48f, 30f);

            var spacer = CreateRect("Spacer", group);
            AddLayoutElement(spacer.gameObject, preferredHeight: 24f);

            backButton = CreateTerminalButton("BackButton", group, "Back", out _, 44f, 26f);

            return group.gameObject;
        }

        private static GameObject BuildSessionGroup(RectTransform parent, out TMP_Text codeLabel,
            out Button copyButton, out RectTransform rowRoot, out Button readyButton,
            out TMP_Text readyLabel, out Button startButton, out TMP_Text startLabel,
            out Button leaveButton)
        {
            var group = CreateGroup("SessionGroup", parent, 10f);

            var codeRow = CreateRect("CodeRow", group);
            var codeRowLayout = codeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            codeRowLayout.spacing = 10f;
            codeRowLayout.childAlignment = TextAnchor.MiddleLeft;
            codeRowLayout.childControlWidth = true;
            codeRowLayout.childControlHeight = true;
            codeRowLayout.childForceExpandWidth = false;
            codeRowLayout.childForceExpandHeight = true;
            AddLayoutElement(codeRow.gameObject, preferredHeight: 56f);

            codeLabel = CreateLabel("CodeLabel", codeRow, "------", 40f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            codeLabel.characterSpacing = 12f;
            AddLayoutElement(codeLabel.gameObject, flexibleWidth: 1f);

            copyButton = CreateTerminalButton("CopyButton", codeRow, "Copy", out _, 44f, 24f);
            AddLayoutElement(copyButton.gameObject, preferredWidth: 140f);

            var listLabel = CreateLabel("ListLabel", group, "ON SHIFT", 20f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            listLabel.characterSpacing = 6f;
            AddLayoutElement(listLabel.gameObject, preferredHeight: 26f);

            var rule = CreateRect("Rule", group);
            rule.gameObject.AddComponent<Image>().color = Rule;
            AddLayoutElement(rule.gameObject, preferredHeight: 1f);

            rowRoot = CreateRect("PlayerList", group);
            var listLayout = rowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            AddLayoutElement(rowRoot.gameObject, preferredHeight: 200f);

            var spacer = CreateRect("Spacer", group);
            AddLayoutElement(spacer.gameObject, preferredHeight: 12f);

            readyButton = CreateTerminalButton("ReadyButton", group, "Ready",
                out readyLabel, 48f, 30f);
            startButton = CreateTerminalButton("StartButton", group, "Start the Shift",
                out startLabel, 48f, 30f);
            leaveButton = CreateTerminalButton("LeaveButton", group, "Leave", out _, 44f, 26f);

            return group.gameObject;
        }

        private static void BuildCamera()
        {
            var cameraObject = new GameObject("LobbyCamera") { tag = "MainCamera" };
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

            // Match height so the column always fits vertically, on any aspect ratio.
            scaler.matchWidthOrHeight = 1f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static TMP_DefaultControls.Resources ControlResources() => new()
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd")
        };

        private static RectTransform CreateGroup(string name, RectTransform parent, float spacing)
        {
            var group = CreateRect(name, parent);

            var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return group;
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

        // Terminal-styled button: "> Label" text only, no face. The content label is
        // the tint target so runtime code can overwrite its text ("READY" and so on)
        // while the "> " prefix stays put.
        private static Button CreateTerminalButton(string name, Transform parent, string text,
            out TMP_Text contentLabel, float height, float size)
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

            var prefix = CreateLabel("Prefix", row, ">", size, TextAlignmentOptions.MidlineLeft,
                TextDim);

            contentLabel = CreateLabel("Label", row, text, size, TextAlignmentOptions.MidlineLeft,
                Color.white);

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = contentLabel;
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

            _ = prefix;
            return button;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent,
            string placeholder)
        {
            var created = TMP_DefaultControls.CreateInputField(ControlResources());
            created.name = name;
            created.transform.SetParent(parent, false);

            created.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            var input = created.GetComponent<TMP_InputField>();
            input.characterLimit = 8;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;

            if (input.textComponent != null)
            {
                if (font != null) input.textComponent.font = font;
                input.textComponent.fontSize = 24f;
                input.textComponent.color = TextPrimary;
                input.textComponent.characterSpacing = 8f;
            }

            if (input.placeholder is TMP_Text placeholderText)
            {
                if (font != null) placeholderText.font = font;
                placeholderText.text = placeholder;
                placeholderText.fontSize = 20f;
                placeholderText.color = TextDim;
            }

            AddLayoutElement(created, preferredHeight: 52f);
            return input;
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

            if (preferredWidth > 0f) element.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
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
