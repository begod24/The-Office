using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public static class CombatGeometry
    {
        public static Vector3 AimPoint(NetworkObject target)
        {
            if (target == null) return Vector3.zero;

            var collider = target.GetComponentInChildren<Collider>();

            return collider != null ? collider.bounds.center : target.transform.position;
        }

        public static bool IsOccluded(Transform body, Vector3 point, float eyeHeight)
        {
            if (body == null) return false;

            var origin = body.position + Vector3.up * eyeHeight;
            var offset = point - origin;
            var distance = offset.magnitude;

            if (distance <= 0.01f) return false;

            return Physics.Raycast(origin, offset / distance, distance,
                PhysicsLayers.OcclusionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
