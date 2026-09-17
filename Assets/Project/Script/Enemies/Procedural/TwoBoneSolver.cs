using UnityEngine;

namespace Office.Enemies
{
    public static class TwoBoneSolver
    {
        // Analytic, not iterative: one cosine rule places the knee, two swings point the bones
        // at it. GDD §9.1 is built on swarms, and a solver that costs a fixed handful of
        // operations per leg is the difference between twenty of them and five.
        //
        // The caller is expected to restore the chain's rest pose before every solve. Rotating
        // from whatever pose the previous frame left behind would let twist accumulate, which
        // reads as a leg slowly screwing itself off.
        public static void Solve(Transform upper, Transform lower, Transform tip, Vector3 target,
            Vector3 pole, float upperLength, float lowerLength)
        {
            if (upper == null || lower == null || tip == null) return;
            if (upperLength <= 0f || lowerLength <= 0f) return;

            var origin = upper.position;
            var offset = target - origin;
            var distance = offset.magnitude;

            if (distance < 1e-4f) return;

            var direction = offset / distance;

            // Both ends of the range are unreachable by construction: folded flat the chain
            // cannot be shorter than the difference, and straight it cannot be longer than the
            // sum. Clamping just inside keeps the cosine defined and the knee off the axis.
            var shortest = Mathf.Abs(upperLength - lowerLength) + 1e-3f;
            var longest = Mathf.Max(shortest, upperLength + lowerLength - 1e-3f);
            var reach = Mathf.Clamp(distance, shortest, longest);

            var bend = BendDirection(origin, direction, pole, lower.position);

            var cosine = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) /
                         (2f * upperLength * reach);
            var angle = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f));

            var knee = origin +
                       (direction * Mathf.Cos(angle) + bend * Mathf.Sin(angle)) * upperLength;

            Aim(upper, lower.position, knee);
            Aim(lower, tip.position, target);
        }

        public static void Aim(Transform bone, Vector3 childPosition, Vector3 desired)
        {
            if (bone == null) return;

            var from = childPosition - bone.position;
            var to = desired - bone.position;

            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;

            bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
        }

        private static Vector3 BendDirection(Vector3 origin, Vector3 direction, Vector3 pole,
            Vector3 knee)
        {
            var candidate = Vector3.ProjectOnPlane(pole - origin, direction);

            // A pole sitting on the line through the target says nothing about which way the
            // knee folds. The chain's own rest bend is the next best answer, and a chain that is
            // straight at rest gets an arbitrary one — arbitrary but stable, which is what
            // stops the knee flipping sides between frames.
            if (candidate.sqrMagnitude < 1e-6f)
                candidate = Vector3.ProjectOnPlane(knee - origin, direction);

            if (candidate.sqrMagnitude < 1e-6f)
                candidate = Vector3.ProjectOnPlane(Vector3.up, direction);

            if (candidate.sqrMagnitude < 1e-6f)
                candidate = Vector3.ProjectOnPlane(Vector3.right, direction);

            return candidate.normalized;
        }
    }
}
