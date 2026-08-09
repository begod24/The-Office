using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The two spatial questions every weapon behaviour has to answer the same way: where is
    /// a target, and is there a wall in between.
    /// </summary>
    /// <remarks>
    /// Shared because both are easy to get subtly wrong in ways that only show up as unfair
    /// hits. A target's transform origin sits on the floor for most props and at the feet for
    /// a character, so measuring reach to it makes crouching next to something count as out of
    /// range while standing on it counts as in range.
    /// </remarks>
    public static class CombatGeometry
    {
        /// <summary>
        /// Where a hit on <paramref name="target"/> should be measured from and drawn at: the
        /// centre of its bounds rather than its transform origin.
        /// </summary>
        /// <remarks>
        /// A <c>GetComponentInChildren</c> per resolved hit, which happens a few times a
        /// second on the server, not per frame. Caching it would mean tracking colliders that
        /// are enabled and disabled as things break, for a lookup that does not show up in a
        /// profile.
        /// </remarks>
        public static Vector3 AimPoint(NetworkObject target)
        {
            if (target == null) return Vector3.zero;

            var collider = target.GetComponentInChildren<Collider>();

            return collider != null ? collider.bounds.center : target.transform.position;
        }

        /// <summary>
        /// True when a wall stands between the attacker and the point they claim to have hit.
        /// </summary>
        /// <remarks>
        /// The server's own check, and the reason a modified client cannot hit through
        /// geometry: the owner's probe already stops at walls, but the owner's probe is a
        /// claim. Cast from roughly eye height on the body — the server has no trustworthy
        /// pitch for a remote player, but it does know how tall they are, and a line from the
        /// feet would be blocked by every desk in the office.
        /// <para>
        /// Only <see cref="PhysicsLayers.OcclusionMask"/> blocks, so props and other
        /// interactables never shield a target. Standing behind a chair has to remain a bad
        /// idea.
        /// </para>
        /// </remarks>
        public static bool IsOccluded(Transform body, Vector3 point, float eyeHeight)
        {
            if (body == null) return false;

            var origin = body.position + Vector3.up * eyeHeight;
            var offset = point - origin;
            var distance = offset.magnitude;

            // Already touching. A linecast over no distance reports whatever the origin is
            // inside of, which at point-blank range is the target itself.
            if (distance <= 0.01f) return false;

            return Physics.Raycast(origin, offset / distance, distance,
                PhysicsLayers.OcclusionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
