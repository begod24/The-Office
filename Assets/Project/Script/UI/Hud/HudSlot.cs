using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// One hotbar cell: number, item icon and a stack count.
    ///
    /// Empty for now — there is no inventory. The cell exists early because its size decides how
    /// much of the lower screen the HUD eats, and that is a composition question worth answering
    /// before item art is drawn to fit it.
    /// </summary>
    public sealed class HudSlot : MonoBehaviour
    {
        private static readonly Color FrameIdle = new(0.75f, 0.76f, 0.78f, 0.45f);
        private static readonly Color FrameSelected = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color NumberIdle = new(0.72f, 0.72f, 0.70f, 0.7f);
        private static readonly Color NumberSelected = new(0.90f, 0.90f, 0.88f, 1f);

        [Tooltip("The four border lines of the cell. Tinted together to show selection.")]
        [SerializeField] private Image[] frameEdges;

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private TMP_Text countLabel;

        public bool IsEmpty { get; private set; } = true;

        private void Awake() => Clear();

        /// <summary>A stack count of one or less hides the number: "1" on every item is noise.</summary>
        public void SetItem(Sprite sprite, int count = 1)
        {
            IsEmpty = false;

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (countLabel != null)
            {
                countLabel.text = count > 1 ? count.ToString() : string.Empty;
                countLabel.enabled = count > 1;
            }
        }

        public void Clear()
        {
            IsEmpty = true;

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
        }

        public void SetSelected(bool selected)
        {
            if (frameEdges != null)
            {
                var colour = selected ? FrameSelected : FrameIdle;

                foreach (var edge in frameEdges)
                    if (edge != null)
                        edge.color = colour;
            }

            if (numberLabel != null) numberLabel.color = selected ? NumberSelected : NumberIdle;
        }

        public void SetNumber(int number)
        {
            if (numberLabel != null) numberLabel.text = number.ToString();
        }
    }
}
