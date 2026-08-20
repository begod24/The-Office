using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class HudSegmentBar : MonoBehaviour
    {
        [SerializeField] private Image[] segments;

        [SerializeField] private Color filled = new(0.86f, 0.86f, 0.84f, 1f);
        [SerializeField] private Color drained = new(0.86f, 0.86f, 0.84f, 0.12f);
        [SerializeField] private Color critical = new(0.78f, 0.29f, 0.22f, 1f);

        [Tooltip("Lit colour for the bar that belongs to the player reading it. Brighter than " +
                 "the teammates' so that 'which one is mine' is answered by the bar itself, " +
                 "not only by the label beside it.")]
        [SerializeField] private Color highlighted = new(0.62f, 1f, 0.55f, 1f);

        [Tooltip("At or below this fraction the lit segments switch to the critical colour.")]
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;

        private float value = 1f;
        private bool highlight;

        public float Value => value;

        public int SegmentCount => segments == null ? 0 : segments.Length;

        private void Awake() => Apply();

        public void SetValue(float normalized)
        {
            value = Mathf.Clamp01(normalized);
            Apply();
        }

        /// <summary>
        /// Marks this bar as the local player's. Critical still wins — running out of health
        /// has to look the same on every row, or the player learns to read one warning colour
        /// for themselves and another for everyone else.
        /// </summary>
        public void SetHighlighted(bool isLocal)
        {
            if (highlight == isLocal) return;

            highlight = isLocal;
            Apply();
        }

        private void Apply()
        {
            if (segments == null) return;

            var lit = value <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(value * segments.Length));

            var lowColour = value <= criticalThreshold
                ? critical
                : highlight ? highlighted : filled;

            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                segments[i].color = i < lit ? lowColour : drained;
            }
        }
    }
}
