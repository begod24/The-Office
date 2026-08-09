using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// How one kind of weapon finds a target and applies itself to it.
    /// </summary>
    /// <remarks>
    /// Every weapon in GDD §8.3 shares the same request path — the owner presses a button,
    /// the server rules on it, everyone hears the result — and differs only in how the target
    /// is found. A melee swing sphere-casts, a staple gun spawns a projectile, a laser pointer
    /// traces a beam that ignores geometry differently again. Behind this interface that
    /// difference is one class each; in front of it, <see cref="PlayerAttacker"/> never learns
    /// what it is holding.
    /// <para>
    /// <b>Implementations must be stateless.</b> One instance is shared by every player on the
    /// machine, including both sides of a host. Everything that varies arrives in
    /// <see cref="WeaponContext"/>; anything a weapon needs to remember between uses belongs
    /// on the attacker or in the item's stack, both of which are per-player and replicated.
    /// </para>
    /// </remarks>
    public interface IWeaponBehaviour
    {
        /// <summary>
        /// Owner side. Works out what the player is aiming at.
        /// </summary>
        /// <remarks>
        /// Runs on the owner because movement is owner-authoritative — the client's aim is the
        /// only aim that exists, and the server's copy of the body trails it by the
        /// interpolation window. The result is therefore a claim, and
        /// <see cref="ServerResolve"/> re-checks all of it.
        /// </remarks>
        WeaponAim Probe(in WeaponContext context);

        /// <summary>
        /// Server side. Re-resolves the claim and applies the weapon.
        /// </summary>
        /// <param name="point">Where the impact actually happened, for effects. Only
        /// meaningful when the outcome is not <see cref="WeaponOutcome.Missed"/>.</param>
        WeaponOutcome ServerResolve(in WeaponContext context, in WeaponAim aim,
            NetworkManager manager, out Vector3 point);
    }

    /// <summary>What the server decided one use of a weapon amounted to.</summary>
    /// <remarks>
    /// Three states rather than a bool, because <see cref="Absorbed"/> is the entire lesson of
    /// GDD §9.2. A player who cannot tell "my stapler passed through it" from "my stapler
    /// bounced off it" never learns that digital things need light, and Gate 10 asks for that
    /// rule to be learnable in five minutes without being told. It is only learnable if the
    /// feedback differs, and the feedback can only differ if the server says which it was.
    /// <para>
    /// Sent as a byte in the confirmation RPC, so the values are fixed.
    /// </para>
    /// </remarks>
    public enum WeaponOutcome : byte
    {
        /// <summary>Nothing in reach, nothing in line of sight, or the target was already dead.</summary>
        Missed = 0,

        /// <summary>Connected, and the target's response table left nothing of it.</summary>
        Absorbed = 1,

        /// <summary>Connected and hurt.</summary>
        Connected = 2
    }

    /// <summary>Everything a weapon behaviour is allowed to know about the player using it.</summary>
    /// <remarks>
    /// Passed by <c>in</c> and built fresh per use. It carries transforms rather than the
    /// components that own them so that a behaviour cannot reach sideways into movement,
    /// inventory or health — the attacker has already decided those questions by the time a
    /// behaviour runs.
    /// </remarks>
    public readonly struct WeaponContext
    {
        /// <summary>Reach, probe radius and the server's tolerances. Never null.</summary>
        public readonly CombatConfig Config;

        /// <summary>The resolved numbers for this use.</summary>
        public readonly WeaponProfile Profile;

        /// <summary>The player's body. The only transform the server trusts.</summary>
        public readonly Transform Body;

        /// <summary>
        /// Where the player is looking. The owner's camera, and <c>null</c> on the server —
        /// which has no pitch for a remote player worth aiming with. A behaviour that reads
        /// this outside <see cref="IWeaponBehaviour.Probe"/> is reaching for something that is
        /// not there.
        /// </summary>
        public readonly Transform Aim;

        /// <summary>Who is swinging. Rides along into <see cref="DamageInfo"/>.</summary>
        public readonly ulong AttackerClientId;

        public WeaponContext(CombatConfig config, in WeaponProfile profile, Transform body,
            Transform aim, ulong attackerClientId)
        {
            Config = config;
            Profile = profile;
            Body = body;
            Aim = aim;
            AttackerClientId = attackerClientId;
        }
    }
}
