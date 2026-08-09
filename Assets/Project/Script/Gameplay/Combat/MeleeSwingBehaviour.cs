using Unity.Netcode;
using UnityEngine;
using Office.Data;

namespace Office.Gameplay
{
    /// <summary>
    /// A swing with whatever is in the player's hand. Covers everything from a fist to a fire
    /// extinguisher — the difference between those is entirely in the
    /// <see cref="WeaponProfile"/>, which is why they share one behaviour.
    /// </summary>
    /// <remarks>
    /// Stateless and shared, per <see cref="IWeaponBehaviour"/>. The one piece of mutable
    /// storage is a static hit buffer, which is safe for the same reason it was safe on
    /// <c>PlayerAttacker</c>: a probe runs to completion inside one <c>Update</c>, only the
    /// owner probes, and the server's half of this class does not cast at all.
    /// </remarks>
    public sealed class MeleeSwingBehaviour : IWeaponBehaviour
    {
        /// <summary>The shared instance. There is no per-player state to justify another.</summary>
        public static readonly MeleeSwingBehaviour Instance = new();

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>
        /// Roughly where a standing player's shoulders are. Used only for the server's
        /// line-of-sight check, which needs a believable origin rather than an exact one.
        /// </summary>
        private const float ServerEyeHeight = 1.5f;

        private MeleeSwingBehaviour()
        {
        }

        /// <inheritdoc />
        /// <remarks>
        /// A sphere rather than a ray, and a wider one than interaction uses: a swing is an
        /// arc, and pixel-hunting a moving enemy is not the difficulty this game is after.
        /// Level geometry sits in the mask on purpose — a wall in the way has to be found
        /// first so that it blocks everything behind it.
        /// </remarks>
        public WeaponAim Probe(in WeaponContext context)
        {
            var aim = context.Aim;
            if (aim == null) return WeaponAim.AtNothing(Vector3.zero);

            var origin = aim.position;
            var direction = aim.forward;
            var range = context.Config.Range;

            var count = Physics.SphereCastNonAlloc(
                origin, context.Config.ProbeRadius, direction, Hits, range,
                PhysicsLayers.AttackMask, QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            var nearestPoint = origin + direction * range;
            NetworkObject nearest = null;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance >= nearestDistance) continue;

                // Plain geometry still counts: finding it here is what blocks everything
                // behind it, even though it resolves to no target.
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

            // The target's own centre, never the point the client sent. Measuring reach to a
            // client-supplied position is the same thing as having no reach check at all.
            point = CombatGeometry.AimPoint(target);

            if (!IsWithinReach(context, point)) return WeaponOutcome.Missed;
            if (CombatGeometry.IsOccluded(context.Body, point, ServerEyeHeight))
                return WeaponOutcome.Missed;

            var direction = (point - context.Body.position).normalized;

            var applied = damageable.ApplyDamage(new DamageInfo(
                context.Profile.Damage, context.Profile.DamageType, context.AttackerClientId,
                point, direction));

            // Zero from a target that is alive and in reach means a resistance table ate it.
            // That is a different event from a miss, and the player has to be able to hear it.
            return applied > 0f ? WeaponOutcome.Connected : WeaponOutcome.Absorbed;
        }

        // Measured from the body, not from a camera: the server has no pitch for a remote
        // player worth trusting, and CombatConfig.ServerReach already carries the tolerance
        // for the interpolation window that costs.
        private static bool IsWithinReach(in WeaponContext context, Vector3 point)
        {
            var reach = context.Config.ServerReach;
            return (point - context.Body.position).sqrMagnitude <= reach * reach;
        }
    }
}
