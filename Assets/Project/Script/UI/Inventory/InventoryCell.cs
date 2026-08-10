using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// One cell of the inventory grid: the same slot the hotbar draws, with room for the
    /// things a full-screen readout can afford — how worn the item is, and whether it is the
    /// one in the player's hand.
    /// </summary>
    /// <remarks>
    /// Two highlights, not one. <b>Cursor</b> is where the player is pointing and moves with
    /// every key press; <b>equipped</b> is what they are holding and only changes when they
    /// confirm. Collapsing them into one state makes browsing the grid swap the item in your
    /// hand, which in a horror game is the difference between reading a label and swinging a
    /// stapler at nothing.
    /// </remarks>
    public sealed class InventoryCell : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private static readonly Color FrameIdle = new(0.75f, 0.76f, 0.78f, 0.35f);
        private static readonly Color FrameEquipped = new(0.86f, 0.86f, 0.84f, 0.75f);
        private static readonly Color FrameCursor = new(0.95f, 0.95f, 0.93f, 1f);
        private static readonly Color FrameLocked = new(0.75f, 0.76f, 0.78f, 0.16f);

        private static readonly Color FillIdle = new(0.02f, 0.02f, 0.03f, 0.55f);
        private static readonly Color FillCursor = new(0.16f, 0.17f, 0.19f, 0.85f);

        private static readonly Color TextDim = new(0.72f, 0.72f, 0.70f, 0.70f);
        private static readonly Color TextPrimary = new(0.90f, 0.90f, 0.88f, 1f);

        private static readonly Color WearFull = new(0.86f, 0.86f, 0.84f, 0.85f);
        private static readonly Color WearLow = new(0.78f, 0.29f, 0.22f, 1f);

        // The item is under the cursor, not in the cell. Faded rather than hidden so the
        // player can still see where they picked it up from.
        private static readonly Color IconCarried = new(1f, 1f, 1f, 0.25f);

        // At or below this fraction of its life left, the wear bar turns red.
        private const float WearCritical = 0.25f;

        [Tooltip("The four border lines of the cell. Tinted together to show state.")]
        [SerializeField] private Image[] frameEdges;

        [SerializeField] private Image fill;

        [Tooltip("Transparent, and the only raycast target here — it is what the mouse hits.")]
        [SerializeField] private Image hitArea;

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private TMP_Text countLabel;

        [Tooltip("The 'E' tag on the slot the player is actually holding.")]
        [SerializeField] private TMP_Text equippedLabel;

        [Tooltip("Hidden on anything that never wears out, which is most of the game.")]
        [SerializeField] private GameObject wearGroup;

        [SerializeField] private RectTransform wearFill;
        [SerializeField] private Image wearImage;

        [Tooltip("The diagonal cross drawn over a slot the player does not have yet.")]
        [SerializeField] private GameObject lockedMark;

        /// <summary>Position in the grid, assigned by the screen that owns it.</summary>
        public int Index { get; private set; } = -1;

        /// <summary>False once the cell is drawn as a slot beyond the player's capacity.</summary>
        public bool IsLive { get; private set; }

        /// <summary>The mouse moved onto this cell, or it was clicked.</summary>
        public event Action<InventoryCell> Focused;

        public event Action<InventoryCell> Clicked;

        /// <summary>The player started carrying this cell's contents with the mouse.</summary>
        public event Action<InventoryCell, PointerEventData> DragBegan;

        public event Action<PointerEventData> Dragged;

        /// <summary>
        /// The drag finished, wherever it landed. Always raised on the cell it started from,
        /// and always after <see cref="Dropped"/>, so cleaning up here cannot swallow a drop.
        /// </summary>
        public event Action<InventoryCell> DragEnded;

        /// <summary>Something was released over this cell. First argument is where it came from.</summary>
        public event Action<InventoryCell, InventoryCell> Dropped;

        public void Initialise(int index)
        {
            Index = index;

            if (numberLabel != null) numberLabel.text = (index + 1).ToString();

            SetLocked();
        }

        // ------------------------------------------------------------------- contents

        /// <summary>A slot the player has, holding nothing.</summary>
        public void SetEmpty()
        {
            IsLive = true;

            if (lockedMark != null) lockedMark.SetActive(false);
            if (numberLabel != null) numberLabel.enabled = true;

            ClearContents();
        }

        /// <summary>A slot the player does not have yet. Drawn, never reachable.</summary>
        public void SetLocked()
        {
            IsLive = false;

            if (lockedMark != null) lockedMark.SetActive(true);
            if (numberLabel != null) numberLabel.enabled = false;

            ClearContents();
            Paint(FrameLocked, FillIdle);
        }

        public void SetItem(Sprite sprite, int count)
        {
            SetEmpty();

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = Color.white;
            }

            if (countLabel == null) return;

            countLabel.text = count > 1 ? count.ToString() : string.Empty;
            countLabel.enabled = count > 1;
        }

        /// <summary>
        /// How much life the item has left, 1 when fresh. Pass a negative fraction for
        /// anything that never wears out — the bar disappears rather than reading full,
        /// which would imply a stapler and a coffee mug wear the same way.
        /// </summary>
        public void SetWear(float condition)
        {
            if (wearGroup != null) wearGroup.SetActive(condition >= 0f);

            if (condition < 0f) return;

            condition = Mathf.Clamp01(condition);

            if (wearFill != null) wearFill.anchorMax = new Vector2(condition, 1f);
            if (wearImage != null) wearImage.color = condition <= WearCritical ? WearLow : WearFull;
        }

        // ---------------------------------------------------------------------- state

        public void SetState(bool underCursor, bool equipped, bool carried)
        {
            if (equippedLabel != null) equippedLabel.enabled = equipped;
            if (icon != null) icon.color = carried ? IconCarried : Color.white;

            if (!IsLive)
            {
                Paint(FrameLocked, FillIdle);
                return;
            }

            var frame = underCursor ? FrameCursor : equipped ? FrameEquipped : FrameIdle;
            Paint(frame, underCursor ? FillCursor : FillIdle);

            if (numberLabel != null) numberLabel.color = underCursor ? TextPrimary : TextDim;
        }

        public void OnPointerEnter(PointerEventData eventData) => Focused?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

        // Unity only raises these four on a component that implements them, so the cell has to
        // carry the handlers even though the screen owns every decision they lead to: which
        // slot moves where is a question about the inventory, not about one cell.
        public void OnBeginDrag(PointerEventData eventData) => DragBegan?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(this);

        public void OnDrop(PointerEventData eventData)
        {
            // pointerDrag is whatever handled OnBeginDrag, which for a drag that started in
            // this grid is another cell root. Anything else released over the grid is not ours.
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<InventoryCell>()
                : null;

            if (source != null) Dropped?.Invoke(source, this);
        }

        private void ClearContents()
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (countLabel != null)
            {
                countLabel.text = string.Empty;
                countLabel.enabled = false;
            }

            if (equippedLabel != null) equippedLabel.enabled = false;
            if (wearGroup != null) wearGroup.SetActive(false);
        }

        private void Paint(Color frame, Color background)
        {
            if (frameEdges != null)
                foreach (var edge in frameEdges)
                    if (edge != null)
                        edge.color = frame;

            if (fill != null) fill.color = background;

            // Kept transparent and kept a raycast target: an empty locked cell still has to
            // swallow the click, or the veil behind it takes one.
            if (hitArea != null) hitArea.color = Color.clear;
        }
    }
}
