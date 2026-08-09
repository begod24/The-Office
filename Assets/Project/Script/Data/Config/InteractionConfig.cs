using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Config/Interaction", fileName = "CFG_Interaction")]
    public sealed class InteractionConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Metres from the camera the player can reach.")]
        [Min(0.1f)]
        [SerializeField] private float range = 2.6f;

        [Tooltip("Radius of the probe. A sphere is far kinder than a ray on small props — " +
                 "zero makes the player pixel-hunt.")]
        [Min(0f)]
        [SerializeField] private float probeRadius = 0.1f;

        [Header("Server validation")]
        [Tooltip("The server's copy of a player trails the owner by the interpolation window, " +
                 "so its range check is deliberately generous. It exists to stop reaching " +
                 "across the floor, not to be frame accurate.")]
        [Min(1f)]
        [SerializeField] private float serverRangeTolerance = 1.8f;

        [Header("Dropping")]
        [Tooltip("Metres in front of the camera a dropped item appears.")]
        [Min(0f)]
        [SerializeField] private float dropDistance = 0.9f;

        public float Range => range;

        public float ProbeRadius => probeRadius;

        public float ServerRangeTolerance => serverRangeTolerance;

        public float DropDistance => dropDistance;

        public float ServerReach => range * serverRangeTolerance;
    }
}
