using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Modules/Melee", fileName = "MOD_Melee")]
    public sealed class MeleeModule : ItemModule
    {
        [Header("Damage")]
        [Min(0f)]
        [SerializeField] private float damage = 12f;

        [Tooltip("Flags, so one swing can read as several things at once — a wet mop is " +
                 "Blunt and Water. The target's response table decides what that is worth.")]
        [SerializeField] private DamageType damageType = DamageType.Blunt;

        [Header("Reach")]
        [Tooltip("Metres this connects at. A fire extinguisher is not a coffee mug — reach " +
                 "belongs to the weapon, while the tolerance the server adds on top of it " +
                 "belongs to CombatConfig.")]
        [Min(0.1f)]
        [SerializeField] private float range = 2.2f;

        [Header("Cost and rhythm")]
        [Tooltip("Seconds before this item can swing again. Enforced by the server, so a " +
                 "modified client gains nothing by asking faster.")]
        [Min(0.05f)]
        [SerializeField] private float attackCooldown = 0.6f;

        [Min(0f)]
        [SerializeField] private float staminaCost = 8f;

        [Header("Consequence")]
        [Tooltip("GDD §8.1: fighting is loud and pulls enemies in. Metres. Zero means silent, " +
                 "which should be rare and deliberate.")]
        [Min(0f)]
        [SerializeField] private float noiseRadius = 12f;

        [Tooltip("How much durability one swing spends. Ignored unless the item also carries " +
                 "a DurabilityModule.")]
        [Min(0)]
        [SerializeField] private int durabilityCost = 1;

        public float Damage => damage;

        public DamageType DamageType => damageType;

        public float Range => range;

        public float AttackCooldown => attackCooldown;

        public float StaminaCost => staminaCost;

        public float NoiseRadius => noiseRadius;

        public int DurabilityCost => durabilityCost;
    }
}
