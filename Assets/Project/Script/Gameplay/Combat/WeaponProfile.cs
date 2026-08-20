using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    public readonly struct WeaponProfile
    {
        public readonly float Damage;

        public readonly DamageType DamageType;

        public readonly float Cooldown;

        public readonly float Range;

        public readonly float StaminaCost;

        public readonly float NoiseRadius;

        public readonly int DurabilityCost;

        public readonly int MaxUses;

        public readonly int BreaksIntoId;

        public WeaponProfile(float damage, DamageType damageType, float cooldown, float range,
            float staminaCost, float noiseRadius, int durabilityCost, int maxUses,
            int breaksIntoId)
        {
            Damage = damage;
            DamageType = damageType;

            Cooldown = Mathf.Max(0.05f, cooldown);
            Range = Mathf.Max(0.1f, range);
            StaminaCost = Mathf.Max(0f, staminaCost);
            NoiseRadius = Mathf.Max(0f, noiseRadius);
            DurabilityCost = Mathf.Max(0, durabilityCost);
            MaxUses = Mathf.Max(0, maxUses);
            BreaksIntoId = breaksIntoId;
        }

        public bool Wears => MaxUses > 0 && DurabilityCost > 0;
    }
}
