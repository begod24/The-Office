using UnityEngine;

namespace Office.UI
{
    public sealed class HudObjectivesPanel : MonoBehaviour
    {
        private const string PlaceholderText = "---";

        [SerializeField] private HudObjectiveRow[] rows;

        public int Capacity => rows == null ? 0 : rows.Length;

        public void Set(int index, string text, HudObjectiveState state)
        {
            if (rows == null || index < 0 || index >= rows.Length || rows[index] == null) return;

            rows[index].Set(text, state);
        }

        public void HideFrom(int index)
        {
            if (rows == null) return;

            for (var i = Mathf.Max(0, index); i < rows.Length; i++) rows[i].Hide();
        }

        public void ShowPlaceholders()
        {
            if (rows == null) return;

            for (var i = 0; i < rows.Length; i++)
                rows[i].Set(PlaceholderText, HudObjectiveState.Pending);
        }
    }
}
