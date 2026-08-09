using Office.Data;

namespace Office.Gameplay
{
    /// <summary>
    /// Turns "what is in the player's hand" into "what happens when they press attack".
    /// </summary>
    /// <remarks>
    /// The single place that reads weapon modules. Everything downstream sees a
    /// <see cref="WeaponLoadout"/> and never asks what kind of item produced it, which is what
    /// lets a new weapon be an asset plus one branch here rather than an edit to the attacker,
    /// the HUD and the audio system.
    /// <para>
    /// Pure and static on purpose: the owner and the server both call it, with their own copy
    /// of the same definition, and must get the same answer. Anything stateful here would let
    /// the two drift and turn into rejected swings that look like lag.
    /// </para>
    /// </remarks>
    public static class WeaponResolver
    {
        /// <summary>
        /// What <paramref name="definition"/> is worth as a weapon. A null definition, an item
        /// with no <see cref="MeleeModule"/>, and an empty hand all resolve to the unarmed
        /// numbers in <paramref name="config"/> — which is why nothing anywhere has to ask
        /// "is this a weapon".
        /// </summary>
        public static WeaponLoadout Resolve(ItemDefinition definition, CombatConfig config)
        {
            if (definition == null) return Unarmed(config);

            // Ranged first. An item carrying both modules is a firearm with a heavy grip, not a
            // club that happens to shoot — and the ordering is what keeps its shots free of the
            // stamina cost its melee half would otherwise impose.
            var ranged = definition.GetModule<RangedModule>();
            if (ranged != null) return FromRanged(ranged, definition);

            var melee = definition.GetModule<MeleeModule>();
            if (melee != null) return FromMelee(melee, definition);

            return Unarmed(config);
        }

        private static WeaponLoadout FromRanged(RangedModule ranged, ItemDefinition definition)
        {
            var wear = ResolveWear(definition, ranged.DurabilityCost);

            var profile = new WeaponProfile(
                ranged.Damage,
                ranged.DamageType,
                ranged.AttackCooldown,
                ranged.Range,
                // Not a value anyone can author. See RangedModule.
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

        /// <summary>Bare hands. Shoving, not punching — the office is not a brawler.</summary>
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

        /// <summary>
        /// A <see cref="DurabilityModule"/> is what switches wear on. Without one the authored
        /// cost is dropped rather than carried, so a designer who sets a cost and forgets the
        /// module gets an item that never breaks instead of one that breaks on its first use.
        /// </summary>
        private static (int Cost, int MaxUses, int BreaksIntoId) ResolveWear(
            ItemDefinition definition, int authoredCost)
        {
            var durability = definition.GetModule<DurabilityModule>();

            if (durability == null) return (0, 0, ContentDefinition.NoId);

            return (authoredCost, durability.MaxUses,
                durability.BreaksInto != null ? durability.BreaksInto.Id : ContentDefinition.NoId);
        }
    }

    /// <summary>A weapon's numbers and the behaviour that applies them, resolved together.</summary>
    /// <remarks>
    /// They travel as a pair because they are only ever correct as a pair: a profile with the
    /// wrong behaviour is a melee swing that does a projectile's damage.
    /// </remarks>
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
