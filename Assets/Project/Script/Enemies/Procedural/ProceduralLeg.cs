using System;
using UnityEngine;

namespace Office.Enemies
{
    [Serializable]
    public sealed class ProceduralLeg
    {
        [Tooltip("Optional yaw bone above the thigh — Leg_*_Hip in the office rigs. Turned " +
                 "toward the foot it carries, so a step to the side swings the whole leg " +
                 "instead of twisting it at the knee. Legs without one simply skip it.")]
        public Transform Hip;

        public Transform Thigh;

        public Transform Shin;

        [Tooltip("Optional fourth joint — Leg_*_Tarsus on the bird legs. The chain is solved as " +
                 "thigh plus shin to the ankle, then this one is aimed at the foot: a three " +
                 "segment solve with a free joint has more answers than the art has shapes.")]
        public Transform Tarsus;

        public Transform Foot;

        [Tooltip("IK_Leg_* from the rig. Its rest position is where this foot stands when the " +
                 "body is still, and it is moved onto the planted foot every frame so the solve " +
                 "can be read in the scene view.")]
        public Transform Target;

        [Tooltip("Pole_Leg_* from the rig. The knee folds toward it.")]
        public Transform Pole;

        [Tooltip("Legs sharing a number swing together and never while another group is " +
                 "swinging. A quadruped walks on diagonals: FL+BR is one group, FR+BL the other.")]
        [Min(0)] public int Group;
    }
}
