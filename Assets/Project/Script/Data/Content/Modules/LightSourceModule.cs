using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Makes an item emit light and burn a battery doing it.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="MeleeModule"/>: a flashlight has this and not
    /// that, a stapler has that and not this, and a laser pointer has both. That is the
    /// whole argument for modules in one sentence.
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Modules/Light Source", fileName = "MOD_Light")]
    public sealed class LightSourceModule : ItemModule
    {
        [Header("Beam")]
        [Min(0f)]
        [SerializeField] private float range = 14f;

        [Tooltip("Spot angle in degrees. A wide, short cone reads as a lantern; a narrow, " +
                 "long one as a torch.")]
        [Range(1f, 179f)]
        [SerializeField] private float angle = 55f;

        [Min(0f)]
        [SerializeField] private float intensity = 2.5f;

        [Header("Battery")]
        [Tooltip("Charge spent per second while lit, against a full charge of 100. Zero means " +
                 "the light never runs out — mains powered fixtures, not carried items.")]
        [Min(0f)]
        [SerializeField] private float drainPerSecond = 1f;

        public float Range => range;

        public float Angle => angle;

        public float Intensity => intensity;

        public float DrainPerSecond => drainPerSecond;
    }
}
