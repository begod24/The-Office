using Office.Core;
using Office.Data;
using Office.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class InventoryScreen : MonoBehaviour
    {
        private static readonly Color ConditionFine = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color ConditionCaution = new(0.85f, 0.72f, 0.35f, 1f);
        private static readonly Color ConditionDanger = new(0.78f, 0.29f, 0.22f, 1f);

        private const float CautionAt = 0.6f;
        private const float DangerAt = 0.25f;

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Every drawn cell, including the ones past the player's capacity.")]
        [SerializeField] private InventoryCell[] cells;

        [SerializeField] private InventoryDetailPanel detail;

        [Tooltip("The same row the HUD draws, so the objective stays readable with the map open.")]
        [SerializeField] private HudObjectivesPanel objectives;

        [Header("Condition")]
        [SerializeField] private TMP_Text conditionLabel;
        [SerializeField] private HudSegmentBar conditionBar;

        [Header("Drag")]
        [Tooltip("Follows the cursor while an item is being carried. Never a raycast target — " +
                 "an icon under the pointer would be what every drop landed on.")]
        [SerializeField] private RectTransform dragGhost;

        [SerializeField] private Image dragGhostIcon;

        [Header("Layout")]
        [Tooltip("Cells per row. The cursor wraps inside a row and inside a column. Written by " +
                 "InventoryBuilder from the same constant this defaults to, so the two cannot " +
                 "disagree unless someone edits the value in the inspector by hand.")]
        [Min(1)]
        [SerializeField] private int columns = InventoryGrid.Columns;

        private IEventBus bus;
        private DefinitionRegistry definitions;

        private PlayerInventory inventory;
        private Health health;

        private Canvas canvas;
        private RectTransform canvasRect;

        private bool open;
        private bool paused;
        private int cursor = InventoryGrid.None;

        private int carrying = -1;

        private int LiveCount => inventory != null && inventory.IsSpawned && cells != null
            ? Mathf.Min(cells.Length, inventory.Capacity)
            : 0;

        private void Start()
        {
            ServiceLocator.TryGet(out bus);
            ServiceLocator.TryGet(out definitions);

            canvas = GetComponent<Canvas>();
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;

            if (cells != null)
                for (var i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == null) continue;

                    cells[i].Initialise(i);
                    cells[i].Focused += OnCellFocused;
                    cells[i].Clicked += OnCellClicked;
                    cells[i].DragBegan += OnCellDragBegan;
                    cells[i].Dragged += OnCellDragged;
                    cells[i].DragEnded += OnCellDragEnded;
                    cells[i].Dropped += OnCellDropped;
                }

            StopCarrying();

            PlayerInventory.LocalChanged += BindInventory;
            BindInventory(PlayerInventory.Local);

            Health.LocalChanged += BindHealth;
            BindHealth(Health.Local);

            bus?.Subscribe<LocalPauseChanged>(OnPauseChanged);

            if (objectives != null) objectives.ShowPlaceholders();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (cells != null)
                foreach (var cell in cells)
                {
                    if (cell == null) continue;

                    cell.Focused -= OnCellFocused;
                    cell.Clicked -= OnCellClicked;
                    cell.DragBegan -= OnCellDragBegan;
                    cell.Dragged -= OnCellDragged;
                    cell.DragEnded -= OnCellDragEnded;
                    cell.Dropped -= OnCellDropped;
                }

            PlayerInventory.LocalChanged -= BindInventory;
            BindInventory(null);

            Health.LocalChanged -= BindHealth;
            BindHealth(null);

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);

            if (open) bus?.Publish(new LocalInventoryChanged(false));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (Pressed(keyboard?.tabKey) || Pressed(gamepad?.selectButton))
            {
                SetOpen(!open);
                return;
            }

            if (!open) return;

            if (Pressed(keyboard?.escapeKey) || Pressed(gamepad?.buttonEast))
            {
                SetOpen(false);
                return;
            }

            var count = LiveCount;
            if (count == 0) return;

            var right = Pressed(keyboard?.rightArrowKey) || Pressed(keyboard?.dKey) ||
                        Pressed(gamepad?.dpad.right);
            var left = Pressed(keyboard?.leftArrowKey) || Pressed(keyboard?.aKey) ||
                       Pressed(gamepad?.dpad.left);
            var down = Pressed(keyboard?.downArrowKey) || Pressed(keyboard?.sKey) ||
                       Pressed(gamepad?.dpad.down);
            var up = Pressed(keyboard?.upArrowKey) || Pressed(keyboard?.wKey) ||
                     Pressed(gamepad?.dpad.up);

            var column = (right ? 1 : 0) - (left ? 1 : 0);
            var row = (down ? 1 : 0) - (up ? 1 : 0);

            if (column != 0) MoveCursor(InventoryGrid.StepColumn(cursor, count, columns, column));
            else if (row != 0) MoveCursor(InventoryGrid.StepRow(cursor, count, columns, row));

            if (Pressed(keyboard?.spaceKey) || Pressed(keyboard?.enterKey) ||
                Pressed(gamepad?.buttonSouth))
                Equip(cursor);

            if (Pressed(keyboard?.gKey) || Pressed(gamepad?.buttonWest)) Drop(cursor);
        }

        private static bool Pressed(ButtonControl control) =>
            control != null && control.wasPressedThisFrame;

        private void SetOpen(bool value)
        {
            if (value && (paused || inventory == null || !inventory.IsSpawned)) return;

            if (open == value) return;

            open = value;

            if (!value) StopCarrying();

            if (panelRoot != null) panelRoot.SetActive(value);

            bus?.Publish(new LocalInventoryChanged(value));

            if (!value) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MoveCursor(InventoryGrid.Clamp(inventory.SelectedIndex, LiveCount));
        }

        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;

            if (paused) SetOpen(false);
        }

        private void MoveCursor(int index)
        {
            cursor = index;
            Refresh();
        }

        private void OnCellFocused(InventoryCell cell)
        {
            if (!open || !cell.IsLive) return;

            MoveCursor(cell.Index);
        }

        private void OnCellDragBegan(InventoryCell cell, PointerEventData eventData)
        {
            StopCarrying();

            if (!open || !cell.IsLive || cell.Index >= LiveCount) return;

            var stack = inventory[cell.Index];

            if (stack.IsEmpty) return;

            carrying = cell.Index;

            var definition = Resolve(stack.DefinitionId);

            if (dragGhostIcon != null)
            {
                dragGhostIcon.sprite = definition != null ? definition.Icon : null;
                dragGhostIcon.enabled = dragGhostIcon.sprite != null;
            }

            if (dragGhost != null) dragGhost.gameObject.SetActive(true);

            MoveGhost(eventData);
            Refresh();
        }

        private void OnCellDragged(PointerEventData eventData)
        {
            if (carrying < 0) return;

            MoveGhost(eventData);
        }

        private void OnCellDragEnded(InventoryCell cell) => StopCarrying();

        private void OnCellDropped(InventoryCell source, InventoryCell target)
        {
            if (!open || carrying < 0 || inventory == null) return;
            if (!source.IsLive || !target.IsLive || source.Index == target.Index) return;

            inventory.RequestMove(source.Index, target.Index);

            MoveCursor(target.Index);
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (dragGhost == null || canvasRect == null || canvas == null) return;

            var pointerCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, pointerCamera, out var local))
                dragGhost.anchoredPosition = local;
        }

        private void StopCarrying()
        {
            var was = carrying;
            carrying = -1;

            if (dragGhost != null) dragGhost.gameObject.SetActive(false);

            if (was >= 0) Refresh();
        }

        private void OnCellClicked(InventoryCell cell)
        {
            if (!open || !cell.IsLive) return;

            MoveCursor(cell.Index);
            Equip(cell.Index);
        }

        private void Equip(int index)
        {
            if (inventory == null || index < 0 || index >= LiveCount) return;

            if (index < GameplayConstants.HotbarSlots)
            {
                inventory.Select(index);
                return;
            }

            if (inventory[index].IsEmpty) return;

            inventory.RequestMove(index, inventory.SelectedIndex);
        }

        private void Drop(int index)
        {
            if (inventory == null || index < 0 || index >= LiveCount) return;

            inventory.RequestDrop(index);
        }

        private void BindHealth(Health next)
        {
            if (ReferenceEquals(health, next)) return;

            if (health != null) health.Changed -= OnVitalsChanged;

            health = next;

            if (health != null) health.Changed += OnVitalsChanged;

            RefreshCondition();
        }

        private void OnVitalsChanged(VitalsState state) => RefreshCondition();

        private void RefreshCondition()
        {
            var normalised = health != null ? health.Normalised : 0f;

            if (conditionBar != null) conditionBar.SetValue(normalised);

            if (conditionLabel == null) return;

            var state = health != null ? health.State : default;

            string text;
            Color colour;

            if (health == null)
            {
                text = "---";
                colour = ConditionCaution;
            }
            else if (state.IsDead)
            {
                text = "DEAD";
                colour = ConditionDanger;
            }
            else if (state.IsDowned)
            {
                text = $"DOWN {Mathf.CeilToInt(state.BleedOutRemaining)}";
                colour = ConditionDanger;
            }
            else if (normalised <= DangerAt)
            {
                text = "DANGER";
                colour = ConditionDanger;
            }
            else if (normalised <= CautionAt)
            {
                text = "CAUTION";
                colour = ConditionCaution;
            }
            else
            {
                text = "FINE";
                colour = ConditionFine;
            }

            conditionLabel.text = text;
            conditionLabel.color = colour;
        }

        private void BindInventory(PlayerInventory next)
        {
            if (ReferenceEquals(inventory, next)) return;

            if (inventory != null) inventory.Changed -= Refresh;

            inventory = next;

            if (inventory != null) inventory.Changed += Refresh;

            if (inventory == null) SetOpen(false);

            Refresh();
        }

        private void Refresh()
        {
            if (cells == null) return;

            var count = LiveCount;
            cursor = InventoryGrid.Clamp(cursor, count);

            var selected = count > 0 ? inventory.SelectedIndex : -1;

            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;

                if (i >= count)
                {
                    cell.SetLocked();
                    cell.SetState(false, false, false);
                    continue;
                }

                var stack = inventory[i];

                if (stack.IsEmpty)
                {
                    cell.SetEmpty();
                    cell.SetWear(-1f);
                }
                else
                {
                    var definition = Resolve(stack.DefinitionId);

                    cell.SetItem(definition != null ? definition.Icon : null, stack.Count);
                    cell.SetWear(Condition(definition, stack));
                }

                cell.SetState(i == cursor, i == selected, i == carrying);
            }

            RefreshDetail(count);
        }

        private void RefreshDetail(int count)
        {
            if (detail == null) return;

            if (cursor < 0 || cursor >= count)
            {
                detail.Clear();
                return;
            }

            var stack = inventory[cursor];

            if (stack.IsEmpty)
            {
                detail.Clear();
                return;
            }

            detail.Show(Resolve(stack.DefinitionId), stack);
        }

        private static float Condition(ItemDefinition definition, in ItemStack stack)
        {
            if (definition == null) return -1f;

            if (!WeaponResolver.TryResolve(definition, out var profile) || !profile.Wears)
                return -1f;

            return ItemWear.NormalisedCondition(stack, profile.MaxUses);
        }

        private ItemDefinition Resolve(int definitionId) =>
            definitions != null && definitions.TryGet<ItemDefinition>(definitionId, out var found)
                ? found
                : null;
    }
}
