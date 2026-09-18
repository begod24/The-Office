using UnityEngine;
using static Office.Editor.Level.TowerGeometry;

namespace Office.Editor.Level
{
    /// <summary>
    /// The two stair cores and the dead lift bank. These are the only things in the
    /// building that cross floors, so they are built once for the whole tower rather
    /// than per floor.
    ///
    /// Each stairwell is a dogleg: one flight up the far side, a half-landing, a second
    /// flight back down the near side, arriving on a strip of slab that stays solid on
    /// every floor. That arrival strip is why the floor plates have an L-shaped hole and
    /// not a rectangular one — walk off the top step and there is floor under you.
    /// </summary>
    internal static class TowerCores
    {
        private const float FlightWidth = 3f;
        private const float FlightRise = BlockoutKit.FloorToFloor * 0.5f;
        private const float FlightRun = 5f;
        private const float LandingDepth = 1.5f;

        internal static void Build(Transform root, int topFloor)
        {
            var group = BlockoutKit.Group(root, "VerticalCores");

            for (var floor = 5; floor < topFloor; floor++)
            {
                var baseY = FloorY(floor);

                StairwellA(BlockoutKit.Group(group, $"StairA_F{floor}_to_F{floor + 1}"), baseY);
                StairwellB(BlockoutKit.Group(group, $"StairB_F{floor}_to_F{floor + 1}"), baseY);
            }

            LiftBank(BlockoutKit.Group(group, "LiftShafts"), topFloor);
        }

        private static void StairwellA(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Core;

            // Up the west side...
            BlockoutKit.Stair(parent, "Flight_Lower",
                new Vector3(-21f, baseY, StairAArrival), Vector3.forward,
                FlightWidth, FlightRise, FlightRun, mat);

            // ...turn on the half-landing against the north wall...
            BlockoutKit.Box(parent, "HalfLanding",
                new Vector3(-18f, baseY + FlightRise - 0.1f, 16.75f),
                new Vector3(6f, 0.2f, LandingDepth), mat);

            // ...and back down the east side, arriving on the solid strip at z = 11.
            BlockoutKit.Stair(parent, "Flight_Upper",
                new Vector3(-15f, baseY + FlightRise, 16f), Vector3.back,
                FlightWidth, FlightRise, FlightRun, mat);
        }

        private static void StairwellB(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Core;

            BlockoutKit.Stair(parent, "Flight_Lower",
                new Vector3(21f, baseY, StairBArrival), Vector3.back,
                FlightWidth, FlightRise, FlightRun, mat);

            BlockoutKit.Box(parent, "HalfLanding",
                new Vector3(18f, baseY + FlightRise - 0.1f, -6.75f),
                new Vector3(6f, 0.2f, LandingDepth), mat);

            BlockoutKit.Stair(parent, "Flight_Upper",
                new Vector3(15f, baseY + FlightRise, -6f), Vector3.forward,
                FlightWidth, FlightRise, FlightRun, mat);
        }

        /// <summary>
        /// The lifts are dead on every floor (GDD §5.1), so they are a solid block with
        /// recessed door panels — a vertical connection the player can see and not use.
        /// </summary>
        private static void LiftBank(Transform parent, int topFloor)
        {
            var height = (topFloor - 5 + 1) * BlockoutKit.FloorToFloor;
            var centreY = FloorY(5) - BlockoutKit.SlabThickness + height * 0.5f;

            BlockoutKit.Box(parent, "LiftShaft_Block",
                new Vector3((LiftXMin + LiftXMax) * 0.5f, centreY, (LiftZMin + LiftZMax) * 0.5f),
                new Vector3(LiftXMax - LiftXMin, height, LiftZMax - LiftZMin),
                BlockoutPalette.Core);

            for (var floor = 5; floor <= topFloor; floor++)
            {
                var baseY = FloorY(floor);
                var doors = BlockoutKit.Group(parent, $"LiftDoors_F{floor}");

                foreach (var z in new[] { -2f, 2f })
                    BlockoutKit.Box(doors, $"Door_{(z < 0 ? "South" : "North")}",
                        new Vector3(LiftXMax + 0.06f, baseY + 1.05f, z),
                        new Vector3(0.12f, 2.1f, 1.8f), BlockoutPalette.Accent);
            }
        }
    }
}
