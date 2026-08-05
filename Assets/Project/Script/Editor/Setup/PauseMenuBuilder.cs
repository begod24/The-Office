using Office.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Office.Editor
{
    internal static class PauseMenuBuilder
    {
        private const string RootName = "[PauseMenu]";

        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private static readonly Color DimVeil = new(0f, 0f, 0f, 0.62f);
        private static readonly Color TextPrimary = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color TextNormal = new(0.60f, 0.60f, 0.58f, 1f);
        private static readonly Color TextDim = new(0.42f, 0.42f, 0.41f, 1f);

        private static readonly (string Label, MainMenuAction Action)[] Items =
        {
            ("Resume", MainMenuAction.Resume),
            ("Settings", MainMenuAction.Settings),
            ("Main Menu", MainMenuAction.MainMenu)
        };

        private static TMP_FontAsset font;

        [MenuItem("Office/Setup/Rebuild Pause Menu In Open Scene", priority = 45)]
        public static void RebuildInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName)
                    Object.DestroyImmediate(root);

            Build();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Setup] Pause menu rebuilt in '{scene.name}'. Save the scene to keep it.");
        }

        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            EnsureEventSystem();

            var root = new GameObject(RootName);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the HUD (sortingOrder 10).
            canvas.sortingOrder = 30;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            root.AddComponent<GraphicRaycaster>();

            var panel = CreateRect("Panel", root.transform);
            Stretch(panel);

            var veil = panel.gameObject.AddComponent<Image>();
            veil.color = DimVeil;

            var column = CreateRect("Column", panel);
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

            var title = CreateLabel("Title", column, "PAUSED", 84f,
                TextAlignmentOptions.BottomLeft, TextPrimary);
            title.characterSpacing = 4f;
            AddLayoutElement(title.gameObject, preferredHeight: 92f);

            var subtitle = CreateLabel("Subtitle", column, "THE SHIFT GOES ON WITHOUT YOU", 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            subtitle.characterSpacing = 6f;
            AddLayoutElement(subtitle.gameObject, preferredHeight: 28f);

            var spacer = CreateRect("Spacer", column);
            AddLayoutElement(spacer.gameObject, preferredHeight: 44f);

            var items = new Object[Items.Length];
            for (var i = 0; i < Items.Length; i++)
                items[i] = BuildItem(column, Items[i].Label, Items[i].Action);

            var flexible = CreateRect("FlexibleSpacer", column);
            var flexibleElement = flexible.gameObject.AddComponent<LayoutElement>();
            flexibleElement.flexibleHeight = 1f;

            var hintLabel = CreateLabel("HintLabel", column, string.Empty, 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(hintLabel.gameObject, preferredHeight: 30f);

            var settings = SettingsPanelBuilder.Build(panel, out var settingsBack);
            settings.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);

            var screen = root.AddComponent<PauseScreen>();

            Wire(screen,
                ("hintLabel", hintLabel),
                ("panelRoot", panel.gameObject),
                ("menuGroup", column.gameObject),
                ("settingsGroup", settings.gameObject),
                ("settingsBackButton", settingsBack));
            WireArray(screen, "items", items);
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

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
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
