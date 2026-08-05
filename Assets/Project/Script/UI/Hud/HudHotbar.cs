using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// The bottom-centre item bar. Display only: it never decides what is selected, it is told.
    ///
    /// Selection lives with the inventory, which will be server-authoritative like everything
    /// else — a hotbar that tracked its own index would drift out of sync with the hands the
    /// player is actually holding an item in.
    /// </summary>
    public sealed class HudHotbar : MonoBehaviour
    {
        [SerializeField] private HudSlot[] slots;

        /// <summary>-1 while nothing is selected, which is also the state an empty hand is in.</summary>
        public int SelectedIndex { get; private set; } = -1;

        public int Count => slots == null ? 0 : slots.Length;

        private void Awake()
        {
            if (slots == null) return;

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                slots[i].SetNumber(i + 1);
                slots[i].SetSelected(false);
            }
        }

        public void SetItem(int index, Sprite icon, int count = 1)
        {
            if (!InRange(index)) return;

            slots[index].SetItem(icon, count);
        }

        public void ClearSlot(int index)
        {
            if (!InRange(index)) return;

            slots[index].Clear();
        }

        public void ClearAll()
        {
            if (slots == null) return;

            foreach (var slot in slots)
                if (slot != null)
                    slot.Clear();

            SetSelected(-1);
        }

        /// <summary>Pass an out-of-range index (or -1) to deselect everything.</summary>
        public void SetSelected(int index)
        {
            SelectedIndex = InRange(index) ? index : -1;

            if (slots == null) return;

            for (var i = 0; i < slots.Length; i++)
                if (slots[i] != null)
                    slots[i].SetSelected(i == SelectedIndex);
        }

        private bool InRange(int index) =>
            slots != null && index >= 0 && index < slots.Length && slots[index] != null;
    }
}
