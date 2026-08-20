using UnityEngine;

namespace Office.Data
{
    public static class VolumeCurve
    {
        public static float ToGain(float slider)
        {
            var position = Mathf.Clamp01(slider);
            return position * position;
        }

        public static float ToSlider(float gain) => Mathf.Sqrt(Mathf.Clamp01(gain));

        public static int ToPercent(float slider) => Mathf.RoundToInt(Mathf.Clamp01(slider) * 100f);
    }
}
