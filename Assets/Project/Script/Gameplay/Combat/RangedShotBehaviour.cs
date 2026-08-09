using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// A shot fired down the line of sight. The staple gun, and anything else that reaches
    /// across a room rather than across an arm.
    /// </summary>
    /// <remarks>
    /// <b>Hitscan, not a spawned projectile.</b> At office distances a staple arrives inside a
    /// frame, so a travelling object would be a networked entity whose whole life is one tick —
    /// it would cost a spawn, a despawn and a pool slot to model something nobody can see move.
    /// The impact effect already plays at the point that was hit, which is the part a player
    /// actually reads. GDD §8.3's slower projectiles, if they arrive, are a second behaviour
    /// next to this one rather than a change to it.
    /// <para>
    /// Stateless and shared, per <see cref="IWeaponBehaviour"/>. The static hit buffer is safe
    /// for the same reason it is in <see cref="MeleeSwingBehaviour"/>: only the owner probes,
    /// and a probe finishes inside one <c>Update</c>.
    /// </para>
    /// </remarks>
    public sealed class RangedShotBehaviour : IWeaponBehaviour
    {
        /// <summary>The shared instance. There is no per-player state to justify another.</summary>
        public static readonly RangedShotBehaviour Instance = new();

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>
        /// Roughly where a standing player's shoulders are. The server's firing origin, and
        /// only ever an approximation — it has no pitch for a remote player.
        /// </summary>
        private const float ServerEyeHeight = 1.5f;

        private RangedShotBehaviour()
        {
        }

        /// <inheritdoc />
        /// <remarks>
        /// A ray rather than the sphere a swing uses. A shot is aimed, and forgiving it into a
        /// cone would make the staple gun better at close range than the thing you are supposed
        /// to be swinging.
        /// </remarks>
        public WeaponAim Probe(in WeaponContext context)
        {
            var aim = context.Aim;
            if (aim == null) return WeaponAim.AtNothing(Vector3.zero);

            var origin = aim.position;
            var direction = aim.forward;
            var range = context.Profile.Range;

            var count = Physics.RaycastNonAlloc(
                origin, direction, Hits, range,
                PhysicsLayers.AttackMask, QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            var nearestPoint = origin + direction * range;
            NetworkObject nearest = null;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance >= nearestDistance) continue;

                // Plain geometry still counts: finding it here is what stops the shot at the
                // wall instead of through it, even though it resolves to no target.
                var damageable = hit.collider.GetComponentInParent<IDamageable>();

                nearestDistance = hit.distance;
                nearestPoint = hit.point;
                nearest = damageable is NetworkBehaviour behaviour && behaviour.IsSpawned
                    ? behaviour.NetworkObject
                    : null;
            }

            return new WeaponAim(
                nearest != null ? new NetworkObjectReference(nearest) : default,
                nearestPoint);
        }

        /// <inheritdoc />
        public WeaponOutcome ServerResolve(in WeaponContext context, in WeaponAim aim,
            NetworkManager manager, out Vector3 point)
        {
            point = Vector3.zero;

            if (!aim.Target.TryGet(out var target, manager)) return WeaponOutcome.Missed;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return WeaponOutcome.Missed;

            point = CombatGeometry.AimPoint(target);

            if (!IsWithinRange(context, point)) return WeaponOutcome.Missed;

            // The same check that stops a melee swing through a wall, and it matters more here:
            // the client's ray already stopped at geometry, but that ray is a claim, and at
            // eighteen metres there is a great deal more wall to shoot through.
            if (CombatGeometry.IsOccluded(context.Body, point, ServerEyeHeight))
                return WeaponOutcome.Missed;

            var direction = (point - context.Body.position).normalized;

            var applied = damageable.ApplyDamage(new DamageInfo(
                context.Profile.Damage, context.Profile.DamageType, context.AttackerClientId,
                point, direction));

            return applied > 0f ? WeaponOutcome.Connected : WeaponOutcome.Absorbed;
        }

        private static bool IsWithinRange(in WeaponContext context, Vector3 point)
        {
            var reach = context.Config.ServerReachFor(context.Profile.Range);
            return (point - context.Body.position).sqrMagnitude <= reach * reach;
        }
    }
}
