using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Slider position (0..1) to the linear gain an <c>AudioSource</c> wants, and back.
    /// </summary>
    /// <remarks>
    /// Loudness is heard roughly logarithmically while <c>AudioSource.volume</c> is linear
    /// amplitude, so a slider wired straight into it spends its top half doing almost nothing
    /// audible and its bottom half falling off a cliff. Squaring the position is the cheap
    /// approximation of the decibel curve players expect: half way down is about −12 dB, which
    /// reads as "half as loud".
    /// <para>
    /// <b>Settings store the position, never the gain.</b> The position is what the player set
    /// and what the slider has to show again; the gain is a detail of playback. Storing the
    /// gain would move every saved slider the first time this curve is tuned.
    /// </para>
    /// </remarks>
    public static class VolumeCurve
    {
        public static float ToGain(float slider)
        {
            var position = Mathf.Clamp01(slider);
            return position * position;
        }

        public static float ToSlider(float gain) => Mathf.Sqrt(Mathf.Clamp01(gain));

        /// <summary>What the label next to the slider shows.</summary>
        public static int ToPercent(float slider) => Mathf.RoundToInt(Mathf.Clamp01(slider) * 100f);
    }
}
