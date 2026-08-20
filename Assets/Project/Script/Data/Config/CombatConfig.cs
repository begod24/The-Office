using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Config/Combat", fileName = "CFG_Combat")]
    public sealed class CombatConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Metres from the camera a swing connects. Deliberately shorter than the " +
                 "interaction range — reaching for a switch is not the same gesture as hitting " +
                 "something.")]
        [Min(0.1f)]
        [SerializeField] private float range = 2.2f;

        [Tooltip("Radius of the swing probe. Wider than the interaction probe: a melee swing " +
                 "is an arc, and pixel-hunting a moving enemy is not the difficulty we want.")]
        [Min(0f)]
        [SerializeField] private float probeRadius = 0.35f;

        [Header("Server validation")]
        [Tooltip("The server's copy of a player trails the owner by the interpolation window, " +
                 "so its reach check is deliberately generous. It exists to stop someone " +
                 "hitting across the floor, not to be frame accurate.")]
        [Min(1f)]
        [SerializeField] private float serverRangeTolerance = 1.8f;

        [Tooltip("Slack on the server's cooldown check, as a fraction. Without it, a client " +
                 "with honest timing loses swings to ordinary jitter.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float cooldownTolerance = 0.15f;

        [Header("Unarmed")]
        [Tooltip("Used when the selected slot holds nothing, or holds an item with no " +
                 "MeleeModule. Shoving, not punching — the office is not a brawler.")]
        [Min(0f)]
        [SerializeField] private float unarmedDamage = 3f;

        [Min(0.05f)]
        [SerializeField] private float unarmedCooldown = 0.5f;

        [SerializeField] private DamageType unarmedDamageType = DamageType.Blunt;

        [Min(0f)]
        [SerializeField] private float unarmedNoiseRadius = 4f;

        public float Range => range;

        public float ProbeRadius => probeRadius;

        public float ServerRangeTolerance => serverRangeTolerance;

        public float ServerReachFor(float weaponRange) => weaponRange * serverRangeTolerance;

        public float ServerReach => ServerReachFor(range);

        public float CooldownTolerance => cooldownTolerance;

        public float UnarmedDamage => unarmedDamage;

        public float UnarmedCooldown => unarmedCooldown;

        public DamageType UnarmedDamageType => unarmedDamageType;

        public float UnarmedNoiseRadius => unarmedNoiseRadius;
    }
}
