using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// Top-left objective list (GDD §14: the player always knows what the floor wants from them).
    ///
    /// Deliberately dumb — it renders three lines and nothing else. There is no objective system
    /// yet; when one lands it will drive this panel from replicated
    /// <see cref="Office.Data.ObjectiveState"/> entries, and the panel must not have grown any
    /// opinions about progress or ordering in the meantime.
    /// </summary>
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

        /// <summary>Hides every row from <paramref name="index"/> onwards. Call after the last set.</summary>
        public void HideFrom(int index)
        {
            if (rows == null) return;

            for (var i = Mathf.Max(0, index); i < rows.Length; i++) rows[i].Hide();
        }

        /// <summary>
        /// Empty slots with the panel still visible. The frame is part of the screen composition,
        /// so it has to be on screen while the layout is being judged, objectives or not.
        /// </summary>
        public void ShowPlaceholders()
        {
            if (rows == null) return;

            for (var i = 0; i < rows.Length; i++)
                rows[i].Set(PlaceholderText, HudObjectiveState.Pending);
        }
    }
}
