using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public sealed class RangedShotBehaviour : IWeaponBehaviour
    {
        public static readonly RangedShotBehaviour Instance = new();

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private const float ServerEyeHeight = 1.5f;

        private RangedShotBehaviour()
        {
        }

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

        public WeaponOutcome ServerResolve(in WeaponContext context, in WeaponAim aim,
            NetworkManager manager, out Vector3 point)
        {
            point = Vector3.zero;

            if (!aim.Target.TryGet(out var target, manager)) return WeaponOutcome.Missed;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return WeaponOutcome.Missed;

            point = CombatGeometry.AimPoint(target);

            if (!IsWithinRange(context, point)) return WeaponOutcome.Missed;

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
