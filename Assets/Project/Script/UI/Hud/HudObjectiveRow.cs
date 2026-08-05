using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>How far along a single objective line is.</summary>
    public enum HudObjectiveState
    {
        /// <summary>Known but not started — an empty box.</summary>
        Pending,

        /// <summary>Currently being worked on — a filled box in the accent colour.</summary>
        Active,

        /// <summary>Done — a filled box, dimmed text.</summary>
        Complete
    }

    /// <summary>
    /// One objective line: a state box and a label.
    ///
    /// The box is two nested Images rather than a glyph. A checkbox character would depend on the
    /// font asset containing it, and the HUD font is a placeholder that will be swapped — a
    /// missing glyph would silently turn the whole objective list into fallback squares.
    /// </summary>
    public sealed class HudObjectiveRow : MonoBehaviour
    {
        private static readonly Color TextActive = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color TextPending = new(0.72f, 0.72f, 0.70f, 1f);
        private static readonly Color TextComplete = new(0.55f, 0.55f, 0.54f, 1f);
        private static readonly Color BoxAccent = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color BoxComplete = new(0.42f, 0.78f, 0.45f, 1f);

        [SerializeField] private TMP_Text label;

        [Tooltip("The square inside the box frame. Hidden while the objective is pending.")]
        [SerializeField] private Image marker;

        public void Set(string text, HudObjectiveState state)
        {
            gameObject.SetActive(true);

            if (label != null)
            {
                label.text = text;

                label.color = state switch
                {
                    HudObjectiveState.Active => TextActive,
                    HudObjectiveState.Complete => TextComplete,
                    _ => TextPending
                };

                // Reads as struck through without needing a second label or a strikethrough glyph.
                label.fontStyle = state == HudObjectiveState.Complete
                    ? FontStyles.Strikethrough
                    : FontStyles.Normal;
            }

            if (marker == null) return;

            marker.enabled = state != HudObjectiveState.Pending;
            marker.color = state == HudObjectiveState.Complete ? BoxComplete : BoxAccent;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
