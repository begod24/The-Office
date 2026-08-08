using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Anything a swing, a projectile or the world can hurt.
    /// </summary>
    /// <remarks>
    /// Shaped after <see cref="IInteractable"/>, and trusted the same way: the client only
    /// ever <em>asks</em>. <see cref="PlayerAttacker"/> sends a request, the server
    /// re-resolves the target, re-checks reach and cooldown, and only the server calls
    /// <see cref="ApplyDamage"/>. Reading <see cref="IsAlive"/> on a client is fine — it
    /// drives the HUD and nothing else.
    /// <para>
    /// Implementations live on the root of a spawned NetworkObject; colliders may sit
    /// anywhere below it. That is where the server looks the component up from the reference
    /// the client sent.
    /// </para>
    /// </remarks>
    public interface IDamageable
    {
        /// <summary>False once this is dead. A downed player is still alive.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Server only. Returns the damage actually taken after resistances — zero when the
        /// target was immune, already dead, or otherwise unaffected.
        /// </summary>
        float ApplyDamage(in DamageInfo info);
    }

    /// <summary>One hit, before the target's resistances are applied.</summary>
    /// <remarks>
    /// Carries where and from which direction so that reactions — knockback, a blood decal,
    /// which way a body falls — do not need a second lookup. Readonly because a hit is a
    /// fact: anything that wants different numbers builds a different one.
    /// </remarks>
    public readonly struct DamageInfo
    {
        /// <summary>Before resistances. The target multiplies this by its own response table.</summary>
        public readonly float Amount;

        public readonly DamageType Type;

        /// <summary>Who swung. <see cref="World"/> for anomalies, falls and hazards.</summary>
        public readonly ulong SourceClientId;

        public readonly Vector3 Point;

        /// <summary>Unit vector the hit travelled along.</summary>
        public readonly Vector3 Direction;

        /// <summary>Not a client. Damage from the level itself.</summary>
        public const ulong World = ulong.MaxValue;

        public DamageInfo(float amount, DamageType type, ulong sourceClientId,
            Vector3 point, Vector3 direction)
        {
            Amount = amount;
            Type = type;
            SourceClientId = sourceClientId;
            Point = point;
            Direction = direction;
        }

        public bool IsFromWorld => SourceClientId == World;

        public override string ToString() => $"{Amount:0.#} {Type} from {SourceClientId}";
    }
}
