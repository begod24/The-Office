using System;
using Office.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Office.UI
{
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

        private static readonly Color IconCarried = new(1f, 1f, 1f, 0.25f);

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

        public int Index { get; private set; } = -1;

        public bool IsLive { get; private set; }

        public event Action<InventoryCell> Focused;

        public event Action<InventoryCell> Clicked;

        public event Action<InventoryCell, PointerEventData> DragBegan;

        public event Action<PointerEventData> Dragged;

        public event Action<InventoryCell> DragEnded;

        public event Action<InventoryCell, InventoryCell> Dropped;

        public void Initialise(int index)
        {
            Index = index;

            if (numberLabel != null)
                numberLabel.text = index < GameplayConstants.HotbarSlots
                    ? (index + 1).ToString()
                    : string.Empty;

            SetLocked();
        }

        public void SetEmpty()
        {
            IsLive = true;

            if (lockedMark != null) lockedMark.SetActive(false);
            if (numberLabel != null) numberLabel.enabled = true;

            ClearContents();
        }

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

        public void SetWear(float condition)
        {
            if (wearGroup != null) wearGroup.SetActive(condition >= 0f);

            if (condition < 0f) return;

            condition = Mathf.Clamp01(condition);

            if (wearFill != null) wearFill.anchorMax = new Vector2(condition, 1f);
            if (wearImage != null) wearImage.color = condition <= WearCritical ? WearLow : WearFull;
        }

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

        public void OnBeginDrag(PointerEventData eventData) => DragBegan?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(this);

        public void OnDrop(PointerEventData eventData)
        {
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

            if (hitArea != null) hitArea.color = Color.clear;
        }
    }
}
