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

        private static readonly Color Backdrop = new(0.06f, 0.06f, 0.07f, 1f);
        private static readonly Color Panel = new(0.11f, 0.115f, 0.13f, 0.96f);
        private static readonly Color ButtonFace = new(0.20f, 0.21f, 0.24f, 1f);
        private static readonly Color Accent = new(0.78f, 0.29f, 0.22f, 1f);
        private static readonly Color TextPrimary = new(0.86f, 0.85f, 0.83f, 1f);
        private static readonly Color TextDim = new(0.55f, 0.55f, 0.54f, 1f);

        private static readonly Color RowRemote = new(0.135f, 0.14f, 0.16f, 1f);

        public static void BuildRowPrefab()
        {
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
            AddLayoutElement(statusLabel.gameObject, preferredWidth: 120f);

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
            BuildCamera();
            BuildEventSystem();

            var canvas = BuildCanvas();
            var backdrop = CreateRect("Backdrop", canvas.transform);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = Backdrop;

            var panel = CreateRect("Panel", canvas.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 760f);

            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = Panel;

            var panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(36, 36, 32, 32);
            panelLayout.spacing = 14f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            BuildHeader(panel);

            var offlineGroup = BuildOfflineGroup(panel, out var hostButton, out var codeInput,
                out var joinButton);

            var sessionGroup = BuildSessionGroup(panel, out var codeLabel, out var copyButton,
                out var rowRoot, out var readyButton, out var startButton, out var leaveButton);

            var status = CreateLabel("Status", panel, string.Empty, 17f,
                TextAlignmentOptions.Top, TextDim);
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
                ("codeLabel", codeLabel),
                ("copyButton", copyButton),
                ("readyButton", readyButton),
                ("readyLabel", readyButton.GetComponentInChildren<TMP_Text>()),
                ("startButton", startButton),
                ("startLabel", startButton.GetComponentInChildren<TMP_Text>()),
                ("leaveButton", leaveButton),
                ("rowRoot", rowRoot),
                ("rowPrefab", rowPrefab),
                ("statusLabel", status));
        }

        private static void BuildHeader(RectTransform parent)
        {
            var title = CreateLabel("Title", parent, "OFFICE", 46f,
                TextAlignmentOptions.Center, TextPrimary);
            title.characterSpacing = 18f;
            AddLayoutElement(title.gameObject, preferredHeight: 58f);

            var subtitle = CreateLabel("Subtitle", parent, "NIGHT SHIFT — RELEASE 08:00:00", 16f,
                TextAlignmentOptions.Center, Accent);
            subtitle.characterSpacing = 6f;
            AddLayoutElement(subtitle.gameObject, preferredHeight: 26f);

            var rule = CreateRect("Rule", parent);
            rule.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            AddLayoutElement(rule.gameObject, preferredHeight: 2f);
        }

        private static GameObject BuildOfflineGroup(RectTransform parent, out Button hostButton,
            out TMP_InputField codeInput, out Button joinButton)
        {
            var group = CreateGroup("OfflineGroup", parent, 12f);

            hostButton = CreateButton("HostButton", group, "HOST A SHIFT", Accent, 52f);

            var separator = CreateLabel("Or", group, "or join with a code", 15f,
                TextAlignmentOptions.Center, TextDim);
            AddLayoutElement(separator.gameObject, preferredHeight: 28f);

            codeInput = CreateInputField("CodeInput", group, "JOIN CODE");
            joinButton = CreateButton("JoinButton", group, "JOIN", ButtonFace, 48f);

            AddLayoutElement(group.gameObject, preferredHeight: 230f);
            return group.gameObject;
        }

        private static GameObject BuildSessionGroup(RectTransform parent, out TMP_Text codeLabel,
            out Button copyButton, out RectTransform rowRoot, out Button readyButton,
            out Button startButton, out Button leaveButton)
        {
            var group = CreateGroup("SessionGroup", parent, 12f);

            var codeRow = CreateRect("CodeRow", group);
            var codeRowLayout = codeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            codeRowLayout.spacing = 10f;
            codeRowLayout.childControlWidth = true;
            codeRowLayout.childControlHeight = true;
            codeRowLayout.childForceExpandWidth = false;
            codeRowLayout.childForceExpandHeight = true;
            AddLayoutElement(codeRow.gameObject, preferredHeight: 56f);

            codeLabel = CreateLabel("CodeLabel", codeRow, "------", 34f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            codeLabel.characterSpacing = 12f;
            AddLayoutElement(codeLabel.gameObject, flexibleWidth: 1f);

            copyButton = CreateButton("CopyButton", codeRow, "COPY", ButtonFace, 44f);
            AddLayoutElement(copyButton.gameObject, preferredWidth: 110f);

            var listLabel = CreateLabel("ListLabel", group, "ON SHIFT", 15f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            listLabel.characterSpacing = 6f;
            AddLayoutElement(listLabel.gameObject, preferredHeight: 24f);

            rowRoot = CreateRect("PlayerList", group);
            var listLayout = rowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            AddLayoutElement(rowRoot.gameObject, preferredHeight: 200f);

            readyButton = CreateButton("ReadyButton", group, "READY", ButtonFace, 52f);
            startButton = CreateButton("StartButton", group, "START THE SHIFT", Accent, 52f);
            leaveButton = CreateButton("LeaveButton", group, "LEAVE", ButtonFace, 44f);

            AddLayoutElement(group.gameObject, preferredHeight: 480f);
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
            scaler.matchWidthOrHeight = 0.5f;

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
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = colour;
            label.raycastTarget = false;

            return label;
        }

        private static Button CreateButton(string name, Transform parent, string text,
            Color face, float height)
        {
            var created = TMP_DefaultControls.CreateButton(ControlResources());
            created.name = name;
            created.transform.SetParent(parent, false);

            created.GetComponent<Image>().color = face;

            var label = created.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = 19f;
            label.characterSpacing = 4f;
            label.color = TextPrimary;

            AddLayoutElement(created, preferredHeight: height);
            return created.GetComponent<Button>();
        }

        private static TMP_InputField CreateInputField(string name, Transform parent,
            string placeholder)
        {
            var created = TMP_DefaultControls.CreateInputField(ControlResources());
            created.name = name;
            created.transform.SetParent(parent, false);

            created.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            var input = created.GetComponent<TMP_InputField>();
            input.characterLimit = 8;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;

            if (input.textComponent != null)
            {
                input.textComponent.fontSize = 24f;
                input.textComponent.color = TextPrimary;
                input.textComponent.characterSpacing = 8f;
            }

            if (input.placeholder is TMP_Text placeholderText)
            {
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
