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
            var melee = definition != null ? definition.GetModule<MeleeModule>() : null;

            if (melee == null) return Unarmed(config);

            var durability = definition.GetModule<DurabilityModule>();

            // A DurabilityModule is what switches wear on. Without one the cost is dropped
            // rather than carried, so a designer who authors a cost and forgets the module
            // gets an item that never breaks instead of one that breaks on the first swing.
            var profile = new WeaponProfile(
                melee.Damage,
                melee.DamageType,
                melee.AttackCooldown,
                melee.StaminaCost,
                melee.NoiseRadius,
                durability != null ? melee.DurabilityCost : 0,
                durability != null ? durability.MaxUses : 0,
                durability != null && durability.BreaksInto != null
                    ? durability.BreaksInto.Id
                    : ContentDefinition.NoId);

            // One behaviour today. When GDD §8.3's staple gun arrives it is a ProjectileModule
            // and a ProjectileBehaviour, chosen here by which module the item carries — not by
            // a flag on MeleeModule, and not by a switch inside PlayerAttacker.
            return new WeaponLoadout(MeleeSwingBehaviour.Instance, profile);
        }

        /// <summary>Bare hands. Shoving, not punching — the office is not a brawler.</summary>
        public static WeaponLoadout Unarmed(CombatConfig config)
        {
            var profile = new WeaponProfile(
                config.UnarmedDamage,
                config.UnarmedDamageType,
                config.UnarmedCooldown,
                staminaCost: 0f,
                config.UnarmedNoiseRadius,
                durabilityCost: 0,
                maxUses: 0,
                ContentDefinition.NoId);

            return new WeaponLoadout(MeleeSwingBehaviour.Instance, profile);
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
