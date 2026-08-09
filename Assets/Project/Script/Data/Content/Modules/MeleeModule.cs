using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Makes an item swingable. Present on anything from a stapler to a fire extinguisher.
    /// </summary>
    /// <remarks>
    /// An item without this module is not a weapon — the attack system finds no module and
    /// falls back to the unarmed numbers in <see cref="CombatConfig"/> rather than
    /// special-casing "is this a weapon" anywhere.
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Modules/Melee", fileName = "MOD_Melee")]
    public sealed class MeleeModule : ItemModule
    {
        [Header("Damage")]
        [Min(0f)]
        [SerializeField] private float damage = 12f;

        [Tooltip("Flags, so one swing can read as several things at once — a wet mop is " +
                 "Blunt and Water. The target's response table decides what that is worth.")]
        [SerializeField] private DamageType damageType = DamageType.Blunt;

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

        public float AttackCooldown => attackCooldown;

        public float StaminaCost => staminaCost;

        public float NoiseRadius => noiseRadius;

        public int DurabilityCost => durabilityCost;
    }
}
