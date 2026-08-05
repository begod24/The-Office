using UnityEngine;

namespace Office.UI
{
    public sealed class HudHotbar : MonoBehaviour
    {
        [SerializeField] private HudSlot[] slots;

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
