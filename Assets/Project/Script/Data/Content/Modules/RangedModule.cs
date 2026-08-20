using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Modules/Ranged", fileName = "MOD_Ranged")]
    public sealed class RangedModule : ItemModule
    {
        [Header("Damage")]
        [Min(0f)]
        [SerializeField] private float damage = 14f;

        [Tooltip("Flags, so one shot can read as several things at once. The target's response " +
                 "table decides what that is worth.")]
        [SerializeField] private DamageType damageType = DamageType.Cutting;

        [Header("Reach")]
        [Tooltip("Metres the shot carries. Far longer than any melee reach — being able to " +
                 "answer something across a room is the whole reason to carry this.")]
        [Min(0.5f)]
        [SerializeField] private float range = 18f;

        [Header("Rhythm")]
        [Tooltip("Seconds between shots. Enforced by the server, so a modified client gains " +
                 "nothing by asking faster.")]
        [Min(0.05f)]
        [SerializeField] private float attackCooldown = 0.35f;

        [Header("Consequence")]
        [Tooltip("GDD §8.1: fighting is loud and pulls enemies in. Metres. A gunshot carries " +
                 "further than a swing, which is the price of the range.")]
        [Min(0f)]
        [SerializeField] private float noiseRadius = 20f;

        [Tooltip("How much durability one shot spends. Ignored unless the item also carries " +
                 "a DurabilityModule.")]
        [Min(0)]
        [SerializeField] private int durabilityCost = 1;

        public float Damage => damage;

        public DamageType DamageType => damageType;

        public float Range => range;

        public float AttackCooldown => attackCooldown;

        public float NoiseRadius => noiseRadius;

        public int DurabilityCost => durabilityCost;
    }
}
