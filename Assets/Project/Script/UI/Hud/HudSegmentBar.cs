using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// A health bar drawn as discrete ticks rather than a filled rectangle.
    ///
    /// Segments over a smooth fill on purpose: at HUD scale a squad row is ~90 px wide, and a
    /// continuous bar at 40% and at 45% is the same picture. Ticks give a countable number, which
    /// is what a teammate shouts across voice chat ("I'm on two bars").
    ///
    /// The bar owns no health value of its own — it is told what to draw. Health is authoritative
    /// state and belongs in <see cref="Office.Data.RunState"/>, never in a UI component.
    /// </summary>
    public sealed class HudSegmentBar : MonoBehaviour
    {
        [SerializeField] private Image[] segments;

        [SerializeField] private Color filled = new(0.86f, 0.86f, 0.84f, 1f);
        [SerializeField] private Color drained = new(0.86f, 0.86f, 0.84f, 0.12f);
        [SerializeField] private Color critical = new(0.78f, 0.29f, 0.22f, 1f);

        [Tooltip("At or below this fraction the lit segments switch to the critical colour.")]
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;

        private float value = 1f;

        public float Value => value;

        public int SegmentCount => segments == null ? 0 : segments.Length;

        private void Awake() => Apply();

        public void SetValue(float normalized)
        {
            value = Mathf.Clamp01(normalized);
            Apply();
        }

        private void Apply()
        {
            if (segments == null) return;

            // Ceil, not Round: any health above zero must light at least one tick. A player who
            // reads an empty bar and is still standing assumes the HUD is broken.
            var lit = value <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(value * segments.Length));
            var lowColour = value <= criticalThreshold ? critical : filled;

            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                segments[i].color = i < lit ? lowColour : drained;
            }
        }
    }
}
