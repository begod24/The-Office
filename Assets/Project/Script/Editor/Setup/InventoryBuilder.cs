using System.Collections.Generic;
using Office.Data;
using Office.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Office.Editor
{
    /// <summary>
    /// The full-screen inventory, generated the way the HUD and the pause menu are.
    /// </summary>
    /// <remarks>
    /// Same reference resolution as the menus rather than the HUD's: this is a screen the
    /// player reads standing still, not a corner they glance at, and matching the pause overlay
    /// keeps the two the same size when one replaces the other.
    /// </remarks>
    internal static class InventoryBuilder
    {
        private const string RootName = "[Inventory]";

        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        // Four across is what the hotbar already reads left to right, so a slot keeps the
        // number the player learned. GameplayConstants.InventorySlots is the only thing that
        // decides how many cells are live, here and in the HUD; any drawn beyond it are locked.
        private const int Columns = 4;
        private const int Rows = 2;

        private const int Cells = Columns * Rows;

        private const int LiveCells = GameplayConstants.InventorySlots;

        private const int ConditionSegments = 12;
        private const int ObjectiveRows = 3;

        private const float CellSize = 140f;
        private const float CellSpacing = 12f;
        private const float ScreenMargin = 72f;

        private static readonly Color Veil = new(0f, 0f, 0f, 0.78f);
        private static readonly Color Frame = new(0.75f, 0.76f, 0.78f, 0.45f);
        private static readonly Color Fill = new(0.02f, 0.02f, 0.03f, 0.72f);
        private static readonly Color CellFill = new(0.02f, 0.02f, 0.03f, 0.55f);
        private static readonly Color CellFrame = new(0.75f, 0.76f, 0.78f, 0.35f);
        private static readonly Color LockedFrame = new(0.75f, 0.76f, 0.78f, 0.16f);
        private static readonly Color TextPrimary = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color TextDim = new(0.72f, 0.72f, 0.70f, 0.70f);
        private static readonly Color Rule = new(1f, 1f, 1f, 0.12f);
        private static readonly Color WearTrack = new(1f, 1f, 1f, 0.10f);
        private static readonly Color WearFill = new(0.86f, 0.86f, 0.84f, 0.85f);

        private static TMP_FontAsset font;

        [MenuItem("Office/Setup/Rebuild Inventory In Open Scene", priority = 46)]
        public static void RebuildInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName)
                    Object.DestroyImmediate(root);

            if (!Build()) return;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Setup] Inventory rebuilt in '{scene.name}'. Save the scene to keep it.");
        }

        public static bool Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (font == null && !AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("[Setup] The inventory needs a font. Run " +
                               "'Office/Setup/Import TextMeshPro Essentials' first.");
                return false;
            }

            // A grid smaller than the player's capacity hides real slots, and hides them
            // silently: the hotbar would still address them by number and this screen simply
            // would not draw them. Widen Columns or Rows rather than shrinking the capacity.
            if (LiveCells > Cells)
            {
                Debug.LogError($"[Setup] The inventory grid draws {Cells} cells but a player " +
                               $"has {LiveCells} slots. Add a row.");
                return false;
            }

            var root = new GameObject(RootName);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Over the HUD (10) and under the pause overlay (30) — pausing has to be able to
            // cover this, never the other way round.
            canvas.sortingOrder = 20;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            root.AddComponent<GraphicRaycaster>();

            var panel = CreateRect("Panel", root.transform);
            Stretch(panel);

            var veil = CreateImage(panel, Veil);

            // The veil swallows clicks rather than letting them reach the HUD canvas below,
            // which is still there, just invisible.
            veil.raycastTarget = true;

            var objectives = BuildObjectives(panel);
            var cells = BuildGrid(panel);
            var detail = BuildDetail(panel);
            var condition = BuildCondition(panel, out var conditionLabel);
            BuildHints(panel);

            panel.gameObject.SetActive(false);

            // Outside the panel and after it, so it draws over the grid it is being carried
            // across, and so closing the panel cannot take it down mid-drag.
            var ghost = BuildDragGhost(root.transform);

            var screen = root.AddComponent<InventoryScreen>();

            Wire(screen,
                ("panelRoot", panel.gameObject),
                ("detail", detail),
                ("objectives", objectives),
                ("conditionLabel", conditionLabel),
                ("conditionBar", condition),
                ("dragGhost", ghost.rectTransform),
                ("dragGhostIcon", ghost));

            WireArray(screen, "cells", cells);
            WireInt(screen, "columns", Columns);

            return true;
        }

        // ------------------------------------------------------------------------- grid

        private static Object[] BuildGrid(RectTransform parent)
        {
            const float gridHeight = Rows * CellSize + (Rows - 1) * CellSpacing;

            var panel = CreateFrame("Grid", parent);

            panel.Root.anchorMin = new Vector2(1f, 1f);
            panel.Root.anchorMax = new Vector2(1f, 1f);
            panel.Root.pivot = new Vector2(1f, 1f);
            panel.Root.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);
            panel.Root.sizeDelta = new Vector2(
                Columns * CellSize + (Columns - 1) * CellSpacing + 36f, gridHeight + 90f);

            var layout = panel.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", panel.Content, "INVENTORY", 20f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            title.characterSpacing = 10f;
            AddLayoutElement(title.gameObject, preferredHeight: 24f);

            var rule = CreateRect("Rule", panel.Content);
            CreateImage(rule, Rule);
            AddLayoutElement(rule.gameObject, preferredHeight: 1f);

            var grid = CreateRect("Cells", panel.Content);
            AddLayoutElement(grid.gameObject, preferredHeight: gridHeight);

            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CellSize, CellSize);
            gridLayout.spacing = new Vector2(CellSpacing, CellSpacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            var cells = new Object[Cells];
            for (var i = 0; i < Cells; i++) cells[i] = BuildCell(grid, i);

            return cells;
        }

        private static InventoryCell BuildCell(RectTransform parent, int index)
        {
            var cell = CreateFrame($"Cell_{index + 1}", parent);
            var content = cell.Content;

            // The one raycast target in the cell, so hovering anywhere inside it — icon,
            // number, empty space — is the same event.
            var hit = CreateRect("Hit", cell.Root);
            Stretch(hit);
            var hitImage = CreateImage(hit, Color.clear);
            hitImage.raycastTarget = true;

            var icon = CreateRect("Icon", content);
            Stretch(icon, 24f);
            var iconImage = CreateImage(icon, Color.white);
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            var number = CreateLabel("Number", content, (index + 1).ToString(), 18f,
                TextAlignmentOptions.TopLeft, TextDim);
            Corner(number.rectTransform, new Vector2(0f, 1f), new Vector2(26f, 22f),
                new Vector2(8f, -6f));

            var equipped = CreateLabel("Equipped", content, "E", 18f,
                TextAlignmentOptions.TopRight, TextPrimary);
            Corner(equipped.rectTransform, new Vector2(1f, 1f), new Vector2(26f, 22f),
                new Vector2(-8f, -6f));
            equipped.enabled = false;

            var count = CreateLabel("Count", content, string.Empty, 22f,
                TextAlignmentOptions.BottomRight, TextPrimary);
            Corner(count.rectTransform, new Vector2(1f, 0f), new Vector2(40f, 26f),
                new Vector2(-8f, 14f));
            count.enabled = false;

            var wear = BuildWearBar(content, out var wearFill, out var wearImage);

            var locked = BuildLockedMark(content);

            var component = cell.Root.gameObject.AddComponent<InventoryCell>();

            Wire(component,
                ("fill", cell.Fill),
                ("hitArea", hitImage),
                ("icon", iconImage),
                ("numberLabel", number),
                ("countLabel", count),
                ("equippedLabel", equipped),
                ("wearGroup", wear),
                ("wearFill", wearFill),
                ("wearImage", wearImage),
                ("lockedMark", locked));

            WireArray(component, "frameEdges", cell.Edges);

            foreach (var edge in cell.Edges) edge.color = index < LiveCells ? CellFrame : LockedFrame;
            cell.Fill.color = CellFill;

            return component;
        }

        // A thin rule along the bottom of the cell. Hidden by InventoryCell on anything that
        // never wears out, which is most of what the player picks up.
        private static GameObject BuildWearBar(RectTransform parent, out RectTransform fillRect,
            out Image fillImage)
        {
            var root = CreateRect("Wear", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.offsetMin = new Vector2(10f, 8f);
            root.offsetMax = new Vector2(-10f, 12f);

            var track = CreateRect("Track", root);
            Stretch(track);
            CreateImage(track, WearTrack);

            fillRect = CreateRect("Fill", root);
            Stretch(fillRect);
            fillImage = CreateImage(fillRect, WearFill);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        // Corner to corner: a 140 cell has a 198 diagonal, so each bar ends exactly on a
        // corner and nothing spills into the neighbouring cell.
        private static GameObject BuildLockedMark(RectTransform parent)
        {
            var root = CreateRect("Locked", parent);
            Stretch(root);

            const float length = CellSize * 1.4143f;

            foreach (var angle in new[] { 45f, -45f })
            {
                var bar = CreateRect($"Bar_{(angle > 0f ? "A" : "B")}", root);
                Centre(bar, new Vector2(3f, length));
                bar.localRotation = Quaternion.Euler(0f, 0f, angle);
                CreateImage(bar, LockedFrame);
            }

            return root.gameObject;
        }

        // The item under the cursor while it is being dragged between slots. Anchored to the
        // centre of the canvas because that is where ScreenPointToLocalPointInRectangle puts
        // its origin — the screen writes the result straight into anchoredPosition.
        private static Image BuildDragGhost(Transform parent)
        {
            var root = CreateRect("DragGhost", parent);
            Centre(root, new Vector2(CellSize * 0.72f, CellSize * 0.72f));

            var icon = CreateImage(root, new Color(1f, 1f, 1f, 0.9f));
            icon.preserveAspect = true;

            // CreateImage already leaves this off, and here it is the whole point: an icon
            // sitting under the pointer would be what every drop landed on.
            icon.raycastTarget = false;

            root.gameObject.SetActive(false);
            return icon;
        }

        // ----------------------------------------------------------------------- detail

        private static InventoryDetailPanel BuildDetail(RectTransform parent)
        {
            var panel = CreateFrame("Detail", parent);

            panel.Root.anchorMin = new Vector2(1f, 0f);
            panel.Root.anchorMax = new Vector2(1f, 0f);
            panel.Root.pivot = new Vector2(1f, 0f);
            panel.Root.anchoredPosition = new Vector2(-ScreenMargin, 108f);
            panel.Root.sizeDelta = new Vector2(
                Columns * CellSize + (Columns - 1) * CellSpacing + 36f, 300f);

            var group = CreateRect("Group", panel.Content);
            Stretch(group);

            var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 20, 20);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var name = CreateLabel("Name", group, "---", 36f,
                TextAlignmentOptions.BottomLeft, TextPrimary);
            AddLayoutElement(name.gameObject, preferredHeight: 44f);

            var itemClass = CreateLabel("Class", group, "---", 20f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            itemClass.characterSpacing = 6f;
            AddLayoutElement(itemClass.gameObject, preferredHeight: 26f);

            var rule = CreateRect("Rule", group);
            CreateImage(rule, Rule);
            AddLayoutElement(rule.gameObject, preferredHeight: 1f);

            var body = CreateLabel("Body", group, string.Empty, 22f,
                TextAlignmentOptions.TopLeft, TextPrimary);
            var bodyElement = body.gameObject.AddComponent<LayoutElement>();
            bodyElement.flexibleHeight = 1f;

            var empty = CreateLabel("Empty", panel.Content, "EMPTY SLOT", 24f,
                TextAlignmentOptions.Center, TextDim);
            Stretch(empty.rectTransform);
            empty.characterSpacing = 6f;

            var component = panel.Root.gameObject.AddComponent<InventoryDetailPanel>();

            Wire(component,
                ("content", group.gameObject),
                ("nameLabel", name),
                ("classLabel", itemClass),
                ("bodyLabel", body),
                ("emptyLabel", empty));

            return component;
        }

        // -------------------------------------------------------------------- condition

        private static HudSegmentBar BuildCondition(RectTransform parent, out TMP_Text valueLabel)
        {
            var panel = CreateFrame("Condition", parent);

            panel.Root.anchorMin = Vector2.zero;
            panel.Root.anchorMax = Vector2.zero;
            panel.Root.pivot = Vector2.zero;
            panel.Root.anchoredPosition = new Vector2(ScreenMargin, ScreenMargin);
            panel.Root.sizeDelta = new Vector2(420f, 150f);

            var layout = panel.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 18);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", panel.Content, "CONDITION", 18f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            title.characterSpacing = 10f;
            AddLayoutElement(title.gameObject, preferredHeight: 22f);

            valueLabel = CreateLabel("Value", panel.Content, "FINE", 40f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(valueLabel.gameObject, preferredHeight: 48f);

            var bar = CreateRect("Bar", panel.Content);
            AddLayoutElement(bar.gameObject, preferredHeight: 16f);

            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 3f;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = false;

            var segments = new Object[ConditionSegments];

            for (var i = 0; i < ConditionSegments; i++)
            {
                var segment = CreateRect($"Tick_{i + 1}", bar);

                var element = segment.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = 1f;
                element.preferredHeight = 16f;
                element.minHeight = 16f;

                segments[i] = CreateImage(segment, TextPrimary);
            }

            var component = bar.gameObject.AddComponent<HudSegmentBar>();
            WireArray(component, "segments", segments);

            return component;
        }

        // ------------------------------------------------------------------- objectives

        // The same two components the HUD draws its objectives with, at reading size. The HUD
        // itself is hidden while this screen is up, so without this the objective would vanish
        // exactly when the player stopped to think about it.
        private static HudObjectivesPanel BuildObjectives(RectTransform parent)
        {
            var panel = CreateFrame("Objectives", parent);

            panel.Root.anchorMin = new Vector2(0f, 1f);
            panel.Root.anchorMax = new Vector2(0f, 1f);
            panel.Root.pivot = new Vector2(0f, 1f);
            panel.Root.anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);
            panel.Root.sizeDelta = new Vector2(560f, 178f);

            var layout = panel.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 18);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", panel.Content, "OBJECTIVES", 20f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            title.characterSpacing = 10f;
            AddLayoutElement(title.gameObject, preferredHeight: 24f);

            var rule = CreateRect("Rule", panel.Content);
            CreateImage(rule, Rule);
            AddLayoutElement(rule.gameObject, preferredHeight: 1f);

            var rows = new Object[ObjectiveRows];
            for (var i = 0; i < ObjectiveRows; i++) rows[i] = BuildObjectiveRow(panel.Content, i);

            var component = panel.Content.gameObject.AddComponent<HudObjectivesPanel>();
            WireArray(component, "rows", rows);

            return component;
        }

        private static HudObjectiveRow BuildObjectiveRow(RectTransform parent, int index)
        {
            var row = CreateRect($"Objective_{index + 1}", parent);
            AddLayoutElement(row.gameObject, preferredHeight: 30f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var holder = CreateRect("Box", row);
            AddLayoutElement(holder.gameObject, preferredWidth: 18f, flexibleWidth: 0f);

            var box = CreateFrame("Frame", holder);
            Centre(box.Root, new Vector2(16f, 16f));

            var marker = CreateRect("Marker", box.Content);
            Stretch(marker, 2f);
            var markerImage = CreateImage(marker, TextPrimary);
            markerImage.enabled = false;

            var label = CreateLabel("Label", row, "---", 22f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(label.gameObject, flexibleWidth: 1f);

            var component = row.gameObject.AddComponent<HudObjectiveRow>();
            Wire(component, ("label", label), ("marker", markerImage));

            return component;
        }

        // ------------------------------------------------------------------------ hints

        private static void BuildHints(RectTransform parent)
        {
            var row = CreateRect("Hints", parent);
            row.anchorMin = new Vector2(1f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-ScreenMargin, ScreenMargin - 30f);
            row.sizeDelta = new Vector2(1000f, 34f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            BuildHint(row, "WASD", "MOVE");
            BuildHint(row, "LMB", "DRAG");
            BuildHint(row, "SPACE", "EQUIP");
            BuildHint(row, "G", "DROP");
            BuildHint(row, "TAB", "CLOSE");
        }

        private static void BuildHint(RectTransform parent, string key, string action)
        {
            var hint = CreateRect($"Hint_{action}", parent);

            var layout = hint.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var holder = CreateRect("Key", hint);
            var width = Mathf.Max(34f, key.Length * 14f + 16f);
            AddLayoutElement(holder.gameObject, preferredHeight: 30f, preferredWidth: width,
                flexibleWidth: 0f);

            var box = CreateFrame("Frame", holder);
            Stretch(box.Root);

            var keyLabel = CreateLabel("Label", box.Content, key, 18f,
                TextAlignmentOptions.Center, TextPrimary);
            Stretch(keyLabel.rectTransform);

            var label = CreateLabel("Action", hint, action, 20f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            label.characterSpacing = 4f;
            AddLayoutElement(label.gameObject, preferredHeight: 30f,
                preferredWidth: action.Length * 13f + 6f, flexibleWidth: 0f);
        }

        // ---------------------------------------------------------------------- helpers

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

        private static void Corner(RectTransform rect, Vector2 corner, Vector2 size,
            Vector2 offset)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
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

        private static void WireInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}' has no field '{field}'.");
                return;
            }

            property.intValue = value;
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
