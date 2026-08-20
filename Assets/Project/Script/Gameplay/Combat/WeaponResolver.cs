using Office.Data;

namespace Office.Gameplay
{
    public static class WeaponResolver
    {
        public static WeaponLoadout Resolve(ItemDefinition definition, CombatConfig config)
        {
            if (definition == null) return Unarmed(config);

            var ranged = definition.GetModule<RangedModule>();
            if (ranged != null) return FromRanged(ranged, definition);

            var melee = definition.GetModule<MeleeModule>();
            if (melee != null) return FromMelee(melee, definition);

            return Unarmed(config);
        }

        public static bool TryResolve(ItemDefinition definition, out WeaponProfile profile)
        {
            if (definition != null)
            {
                var ranged = definition.GetModule<RangedModule>();

                if (ranged != null)
                {
                    profile = FromRanged(ranged, definition).Profile;
                    return true;
                }

                var melee = definition.GetModule<MeleeModule>();

                if (melee != null)
                {
                    profile = FromMelee(melee, definition).Profile;
                    return true;
                }
            }

            profile = default;
            return false;
        }

        private static WeaponLoadout FromRanged(RangedModule ranged, ItemDefinition definition)
        {
            var wear = ResolveWear(definition, ranged.DurabilityCost);

            var profile = new WeaponProfile(
                ranged.Damage,
                ranged.DamageType,
                ranged.AttackCooldown,
                ranged.Range,
                staminaCost: 0f,
                ranged.NoiseRadius,
                wear.Cost,
                wear.MaxUses,
                wear.BreaksIntoId);

            return new WeaponLoadout(RangedShotBehaviour.Instance, profile);
        }

        private static WeaponLoadout FromMelee(MeleeModule melee, ItemDefinition definition)
        {
            var wear = ResolveWear(definition, melee.DurabilityCost);

            var profile = new WeaponProfile(
                melee.Damage,
                melee.DamageType,
                melee.AttackCooldown,
                melee.Range,
                melee.StaminaCost,
                melee.NoiseRadius,
                wear.Cost,
                wear.MaxUses,
                wear.BreaksIntoId);

            return new WeaponLoadout(MeleeSwingBehaviour.Instance, profile);
        }

        public static WeaponLoadout Unarmed(CombatConfig config)
        {
            var profile = new WeaponProfile(
                config.UnarmedDamage,
                config.UnarmedDamageType,
                config.UnarmedCooldown,
                config.Range,
                staminaCost: 0f,
                config.UnarmedNoiseRadius,
                durabilityCost: 0,
                maxUses: 0,
                ContentDefinition.NoId);

            return new WeaponLoadout(MeleeSwingBehaviour.Instance, profile);
        }

        private static (int Cost, int MaxUses, int BreaksIntoId) ResolveWear(
            ItemDefinition definition, int authoredCost)
        {
            var durability = definition.GetModule<DurabilityModule>();

            if (durability == null) return (0, 0, ContentDefinition.NoId);

            return (authoredCost, durability.MaxUses,
                durability.BreaksInto != null ? durability.BreaksInto.Id : ContentDefinition.NoId);
        }
    }

    public readonly struct WeaponLoadout
    {
        public readonly IWeaponBehaviour Behaviour;
        public readonly WeaponProfile Profile;

        public WeaponLoadout(IWeaponBehaviour behaviour, in WeaponProfile profile)
        {
            Behaviour = behaviour;
            Profile = profile;
        }
    }
}
