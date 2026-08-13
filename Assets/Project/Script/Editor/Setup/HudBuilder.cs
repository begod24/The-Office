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
    internal static class HudBuilder
    {
        private const string RootName = "[HUD]";

        private const string FontPath = "Assets/Project/Fonts/blockblueprint.asset";

        private const string UiSpriteFolder = "Assets/Project/Art/UI";

        private const int SquadRows = 4;
        private const int ObjectiveRows = 3;

        // Five, not twelve. The bar is read at a glance from the corner of the eye while
        // something is chasing the player, and at that size a fine bar is a smear — what has
        // to survive is "how many blocks are left", which needs them countable.
        private const int HealthSegments = 5;

        // One cell per hand slot. Shared with PlayerInventory so the two cannot drift.
        private const int HotbarSlots = GameplayConstants.HotbarSlots;

        private const float SlotSize = 56f;
        private const float SlotSpacing = 6f;
        private const float ScreenMargin = 28f;

        // Below the crosshair, clear of the stamina rule that sits just under it.
        private const float PromptOffset = 54f;

        private static readonly Color Frame = new(0.80f, 0.81f, 0.79f, 0.55f);
        private static readonly Color Fill = new(0.03f, 0.03f, 0.04f, 0.72f);
        private static readonly Color TextPrimary = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color TextDim = new(0.72f, 0.72f, 0.70f, 0.70f);
        private static readonly Color Rule = new(1f, 1f, 1f, 0.12f);
        private static readonly Color Crosshair = new(0.90f, 0.90f, 0.88f, 0.55f);

        // Green, and only here. GDD §12.2 keeps the office grey so that the few saturated
        // things carry meaning — a teammate's health is exactly the reading that has to be
        // findable without looking straight at it.
        private static readonly Color SegmentFilled = new(0.45f, 0.83f, 0.40f, 1f);
        private static readonly Color SegmentDrained = new(0.45f, 0.83f, 0.40f, 0.13f);
        private static readonly Color SegmentCritical = new(0.85f, 0.25f, 0.20f, 1f);

        private static readonly Color Danger = new(0.85f, 0.25f, 0.20f, 1f);

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

            // Smaller reference than the menus (1920x1080) renders the whole HUD
            // ~20% larger on screen without touching individual element sizes.
            scaler.referenceResolution = new Vector2(1600f, 900f);

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
            var held = BuildHeldItem(root.transform);
            var crosshair = BuildCrosshair(root.transform);
            var prompt = BuildInteractPrompt(root.transform);
            var downed = BuildDownedBanner(root.transform, out var downedLabel);
            BuildStamina(root.transform);

            Wire(screen,
                ("objectives", objectives),
                ("squad", squad),
                ("hotbar", hotbar),
                ("heldItem", held),
                ("crosshair", crosshair),
                ("interactPrompt", prompt),
                ("downedBanner", downed),
                ("downedLabel", downedLabel));

            return true;
        }

        /// <summary>
        /// The one thing on this HUD that is allowed to shout. Everything else is a readout;
        /// this is a state the player cannot be left to infer from a bar going empty.
        /// </summary>
        private static GameObject BuildDownedBanner(Transform parent, out TMP_Text label)
        {
            var root = CreateRect("Downed", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 110f);
            root.sizeDelta = new Vector2(460f, 76f);

            label = CreateLabel("Label", root, "DOWNED", 40f,
                TextAlignmentOptions.Center, Danger);
            Stretch(label.rectTransform);
            label.characterSpacing = 10f;

            var hint = CreateLabel("Hint", root, "WAIT FOR A TEAMMATE", 16f,
                TextAlignmentOptions.Center, TextDim);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            hint.rectTransform.sizeDelta = new Vector2(0f, 22f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            hint.characterSpacing = 6f;

            root.gameObject.SetActive(false);
            return root.gameObject;
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

            var title = CreateLabel("Title", content, "[OBJECTIVES]", 15f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            title.characterSpacing = 6f;
            AddLayoutElement(title.gameObject, preferredHeight: 20f);

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

            // The empty box is always drawn and the filled one sits on top of it, rather than
            // swapping one sprite for another: an objective that is not started still has to
            // show *where* its mark will go, or a pending list reads as an empty list.
            var holder = CreateRect("Box", row);
            AddLayoutElement(holder.gameObject, preferredWidth: 18f, flexibleWidth: 0f);

            var empty = CreateRect("Empty", holder);
            Centre(empty, new Vector2(15f, 15f));
            CreateImage(empty, TextDim, Sprite("checkbox"));

            var marker = CreateRect("Marker", holder);
            Centre(marker, new Vector2(15f, 15f));
            var markerImage = CreateImage(marker, TextPrimary, Sprite("checkbox-filled"));
            markerImage.enabled = false;

            var label = CreateLabel("Label", row, "---", 16f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            AddLayoutElement(label.gameObject, flexibleWidth: 1f);

            var component = row.gameObject.AddComponent<HudObjectiveRow>();
            Wire(component, ("label", label), ("marker", markerImage));

            return component;
        }

        private static HudSquadPanel BuildSquad(Transform parent)
        {
            const float rowHeight = 34f;
            const float spacing = 6f;

            var panel = CreateFrame("Squad", parent);
            var content = panel.Content;

            panel.Root.anchorMin = Vector2.zero;
            panel.Root.anchorMax = Vector2.zero;
            panel.Root.pivot = Vector2.zero;
            panel.Root.anchoredPosition = new Vector2(ScreenMargin, ScreenMargin);
            panel.Root.sizeDelta = new Vector2(290f,
                SquadRows * rowHeight + (SquadRows - 1) * spacing + 52f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("Title", content, "[TEAM STATUS]", 15f,
                TextAlignmentOptions.MidlineLeft, TextDim);
            title.characterSpacing = 6f;
            AddLayoutElement(title.gameObject, preferredHeight: 20f);

            var rows = new Object[SquadRows];
            for (var i = 0; i < SquadRows; i++) rows[i] = BuildSquadRow(content, i, rowHeight);

            var component = content.gameObject.AddComponent<HudSquadPanel>();
            WireArray(component, "rows", rows);

            return component;
        }

        /// <remarks>
        /// Two lines in the space of one: the seat and the name on top, the bar underneath.
        /// A single row would have to choose between a readable name and a readable bar, and
        /// the name is what tells a player *whose* bar is emptying — which is the entire
        /// reason a co-op HUD draws teammates at all.
        /// </remarks>
        private static HudPlayerRow BuildSquadRow(RectTransform parent, int index, float height)
        {
            var row = CreateRect($"Player_{index + 1}", parent);
            AddLayoutElement(row.gameObject, preferredHeight: height);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // The seat tag in its own box, so P1..P4 reads as a column even when the names
            // beside it are different lengths.
            var tagHolder = CreateRect("Tag", row);
            AddLayoutElement(tagHolder.gameObject, preferredWidth: 30f, flexibleWidth: 0f);

            var tagBackground = CreateImage(tagHolder, new Color(1f, 1f, 1f, 0.07f));

            var tagLabel = CreateLabel("Label", tagHolder, $"P{index + 1}", 14f,
                TextAlignmentOptions.Center, TextPrimary);
            Stretch(tagLabel.rectTransform);

            var body = CreateRect("Body", row);
            AddLayoutElement(body.gameObject, flexibleWidth: 1f);

            var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyLayout.spacing = 3f;
            bodyLayout.childAlignment = TextAnchor.MiddleLeft;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            var nameLabel = CreateLabel("Name", body, "---", 14f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(nameLabel.gameObject, preferredHeight: 16f);

            var bar = BuildHealthBar(body);

            // Occupies the bar's place rather than sitting beside it: a downed teammate has no
            // health worth drawing, and the seconds left are what replaces it.
            var status = CreateLabel("Status", body, "OFFLINE", 13f,
                TextAlignmentOptions.MidlineLeft, Danger);
            AddLayoutElement(status.gameObject, preferredHeight: 12f);
            status.gameObject.SetActive(false);

            var component = row.gameObject.AddComponent<HudPlayerRow>();
            Wire(component,
                ("tagLabel", tagLabel),
                ("nameLabel", nameLabel),
                ("health", bar),
                ("statusLabel", status),
                ("tagBackground", tagBackground));

            return component;
        }

        private static HudSegmentBar BuildHealthBar(RectTransform parent)
        {
            var bar = CreateRect("Health", parent);
            AddLayoutElement(bar.gameObject, preferredHeight: 12f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Control on, expand off. A LayoutElement's preferredWidth is only read when the
            // group controls the width — with it off the group leaves each segment at its own
            // rect size and they run off the side of the panel. Expand stays off so the
            // segments keep that preferred width instead of sharing the row.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Fixed-width blocks rather than a bar that shares the row: a segment has to be the
            // same size on every player, or a four-person squad and a two-person one would
            // report the same health at different scales.
            var segments = new Object[HealthSegments];

            for (var i = 0; i < HealthSegments; i++)
            {
                var segment = CreateRect($"Tick_{i + 1}", bar);

                var element = segment.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 16f;
                element.minWidth = 16f;
                element.preferredHeight = 10f;
                element.minHeight = 10f;

                segments[i] = CreateImage(segment, SegmentFilled, Sprite("Green Box inner"));
            }

            var component = bar.gameObject.AddComponent<HudSegmentBar>();
            WireArray(component, "segments", segments);

            SetColour(component, "filled", SegmentFilled);
            SetColour(component, "drained", SegmentDrained);
            SetColour(component, "critical", SegmentCritical);

            return component;
        }

        /// <summary>
        /// Bottom right: what is in the hand, and how much of it is left. GDD §14.
        /// </summary>
        private static HudHeldItem BuildHeldItem(Transform parent)
        {
            var panel = CreateFrame("HeldItem", parent);
            var content = panel.Content;

            panel.Root.anchorMin = new Vector2(1f, 0f);
            panel.Root.anchorMax = new Vector2(1f, 0f);
            panel.Root.pivot = new Vector2(1f, 0f);
            panel.Root.anchoredPosition = new Vector2(-ScreenMargin, ScreenMargin);
            panel.Root.sizeDelta = new Vector2(230f, 62f);

            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 14, 8, 8);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var iconHolder = CreateRect("Icon", content);
            AddLayoutElement(iconHolder.gameObject, preferredWidth: 40f, flexibleWidth: 0f);

            var iconFrame = CreateRect("Frame", iconHolder);
            Stretch(iconFrame, 2f);
            CreateImage(iconFrame, new Color(1f, 1f, 1f, 0.10f), Sprite("icon-container"));

            var iconRect = CreateRect("Sprite", iconFrame);
            Stretch(iconRect, 8f);
            var icon = CreateImage(iconRect, Color.white);
            icon.preserveAspect = true;
            icon.enabled = false;

            var text = CreateRect("Text", content);
            AddLayoutElement(text.gameObject, flexibleWidth: 1f);

            var textLayout = text.gameObject.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2f;
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;

            var nameLabel = CreateLabel("Name", text, "---", 15f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            nameLabel.characterSpacing = 4f;
            AddLayoutElement(nameLabel.gameObject, preferredHeight: 18f);

            var counter = CreateLabel("Counter", text, string.Empty, 20f,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            AddLayoutElement(counter.gameObject, preferredHeight: 22f);

            var component = panel.Root.gameObject.AddComponent<HudHeldItem>();
            Wire(component,
                ("root", panel.Root.gameObject),
                ("icon", icon),
                ("nameLabel", nameLabel),
                ("counterLabel", counter));

            panel.Root.gameObject.SetActive(false);

            return component;
        }

        /// <remarks>
        /// The slots sit inside one bordered panel rather than floating as separate boxes, so
        /// the hand reads as a single object the eye can find in the dark — the same framing
        /// the objectives and the squad use.
        /// </remarks>
        private static HudHotbar BuildHotbar(Transform parent)
        {
            const float padding = 10f;

            var panel = CreateFrame("Hotbar", parent);
            var bar = panel.Content;

            panel.Root.anchorMin = new Vector2(0.5f, 0f);
            panel.Root.anchorMax = new Vector2(0.5f, 0f);
            panel.Root.pivot = new Vector2(0.5f, 0f);
            panel.Root.anchoredPosition = new Vector2(0f, ScreenMargin);
            panel.Root.sizeDelta = new Vector2(
                HotbarSlots * SlotSize + (HotbarSlots - 1) * SlotSpacing + padding * 2f,
                SlotSize + padding * 2f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var slots = new Object[HotbarSlots];
            for (var i = 0; i < HotbarSlots; i++) slots[i] = BuildSlot(bar, i);

            var component = panel.Root.gameObject.AddComponent<HudHotbar>();
            WireArray(component, "slots", slots);

            return component;
        }

        private static HudSlot BuildSlot(RectTransform parent, int index)
        {
            var slot = CreateFrame($"Slot_{index + 1}", parent);
            var content = slot.Content;
            slot.Root.sizeDelta = new Vector2(SlotSize, SlotSize);

            var container = CreateRect("Container", content);
            Stretch(container);
            CreateImage(container, new Color(1f, 1f, 1f, 0.08f), Sprite("icon-container"));

            var icon = CreateRect("Icon", content);
            Stretch(icon, 10f);
            var iconImage = CreateImage(icon, Color.white);
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            var number = CreateLabel("Number", content, (index + 1).ToString(), 12f,
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

        // Thin white line just below the crosshair. HudStaminaBar keeps it invisible
        // until stamina is actually being spent.
        private static void BuildStamina(Transform parent)
        {
            var root = CreateRect("Stamina", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -34f);
            root.sizeDelta = new Vector2(120f, 3f);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var track = CreateRect("Track", root);
            Stretch(track);
            CreateImage(track, new Color(1f, 1f, 1f, 0.10f));

            var fillRect = CreateRect("Fill", root);
            Stretch(fillRect);
            fillRect.pivot = new Vector2(0f, 0.5f);
            CreateImage(fillRect, new Color(0.90f, 0.90f, 0.88f, 0.85f));

            var component = root.gameObject.AddComponent<HudStaminaBar>();
            Wire(component, ("group", group), ("fill", fillRect));
        }

        // Sits below the crosshair, where the eye already is. HudScreen disables the label
        // when there is nothing to say, so an empty prompt costs no layout pass.
        private static TMP_Text BuildInteractPrompt(Transform parent)
        {
            var label = CreateLabel("InteractPrompt", parent, string.Empty, 15f,
                TextAlignmentOptions.Center, TextPrimary);

            var rect = (RectTransform)label.transform;
            Centre(rect, new Vector2(420f, 26f));
            rect.anchoredPosition = new Vector2(0f, -PromptOffset);

            label.enabled = false;
            return label;
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

        private static Image CreateImage(RectTransform rect, Color colour) =>
            CreateImage(rect, colour, null);

        private static Image CreateImage(RectTransform rect, Color colour, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;

            // A null sprite leaves Unity's built-in white quad, which is what every plain
            // block and rule in this HUD wants.
            if (sprite != null) image.sprite = sprite;

            return image;
        }

        /// <summary>
        /// One of the authored marks in <c>Art/UI</c>, or null with a warning.
        /// </summary>
        /// <remarks>
        /// Null rather than an error, and the caller draws its plain-colour block instead: a
        /// missing decoration should degrade to a readable HUD, not to no HUD. The warning
        /// names the file so the reason is one line away.
        /// </remarks>
        private static Sprite Sprite(string assetName)
        {
            var path = $"{UiSpriteFolder}/{assetName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                Debug.LogWarning($"[Setup] '{path}' did not load as a sprite. The HUD falls " +
                                 "back to a plain block. Check the texture's import type.");

            return sprite;
        }

        private static void SetColour(Object target, string field, Color colour)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}' has no colour field " +
                               $"'{field}'. The builder and the component have drifted apart.");
                return;
            }

            property.colorValue = colour;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
