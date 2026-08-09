using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// One use of one weapon, reduced to numbers, resolved identically on the owner and on
    /// the server.
    /// </summary>
    /// <remarks>
    /// The owner needs these to predict — to stop asking for a swing it cannot have, and to
    /// spend stamina at the moment the button goes down. The server needs the same numbers to
    /// rule on the request. Building the profile from the same modules on both sides is what
    /// keeps the prediction and the ruling in agreement; a client that lies about what it is
    /// holding gets the server's profile, not its own.
    /// <para>
    /// Deliberately flat, and deliberately not a reference to the modules it came from.
    /// <see cref="WeaponResolver"/> is the only thing that reads modules, so a new kind of
    /// weapon changes one file rather than every system that asks "how much does this hurt".
    /// </para>
    /// </remarks>
    public readonly struct WeaponProfile
    {
        /// <summary>Before the target's resistances. Those belong to the target.</summary>
        public readonly float Damage;

        /// <summary>Flags — one swing can read as several things at once.</summary>
        public readonly DamageType DamageType;

        /// <summary>Seconds between uses. The server enforces it; the owner predicts it.</summary>
        public readonly float Cooldown;

        public readonly float StaminaCost;

        /// <summary>
        /// Metres this use is audible over. GDD §8.1 makes fighting loud. Nothing listens yet
        /// because enemies do not exist, but the number is authored and resolved so the
        /// hearing system has something to read on the day it arrives.
        /// </summary>
        public readonly float NoiseRadius;

        /// <summary>Uses spent per swing. Meaningless when <see cref="MaxUses"/> is zero.</summary>
        public readonly int DurabilityCost;

        /// <summary>Ceiling from the item's <see cref="DurabilityModule"/>. Zero never wears out.</summary>
        public readonly int MaxUses;

        /// <summary>
        /// What the item leaves behind when it breaks, or <see cref="ContentDefinition.NoId"/>
        /// when it simply ceases to exist.
        /// </summary>
        public readonly int BreaksIntoId;

        public WeaponProfile(float damage, DamageType damageType, float cooldown,
            float staminaCost, float noiseRadius, int durabilityCost, int maxUses,
            int breaksIntoId)
        {
            Damage = damage;
            DamageType = damageType;

            // A zero cooldown is a weapon that fires every frame, on the server as well as on
            // the owner. Floored here rather than trusted from an asset.
            Cooldown = Mathf.Max(0.05f, cooldown);
            StaminaCost = Mathf.Max(0f, staminaCost);
            NoiseRadius = Mathf.Max(0f, noiseRadius);
            DurabilityCost = Mathf.Max(0, durabilityCost);
            MaxUses = Mathf.Max(0, maxUses);
            BreaksIntoId = breaksIntoId;
        }

        /// <summary>True when using this spends durability and can eventually break it.</summary>
        public bool Wears => MaxUses > 0 && DurabilityCost > 0;
    }
}
