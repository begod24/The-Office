using UnityEngine;

namespace Office.Editor.Level
{
    /// <summary>
    /// The building's dimensions, shared by every floor. All values are multiples of the
    /// 2 m module locked in GDD §0 decision 3, so the kit stays snappable later.
    ///
    /// The plate is organised as a service core, a corridor ring around it, and four
    /// perimeter bands. The ring is what makes the floor loop — the concept art lists
    /// "multiple routes and loops" as a key point for F5, and a ring delivers that
    /// without a single scripted trick.
    /// </summary>
    internal static class TowerGeometry
    {
        internal const float PlateXMin = -22f;
        internal const float PlateXMax = 22f;
        internal const float PlateZMin = -18f;
        internal const float PlateZMax = 18f;

        // Service core, dead centre.
        internal const float CoreXMin = -8f;
        internal const float CoreXMax = 8f;
        internal const float CoreZMin = -6f;
        internal const float CoreZMax = 6f;

        // Outer edge of the 2 m corridor ring that wraps the core.
        internal const float RingXMin = -10f;
        internal const float RingXMax = 10f;
        internal const float RingZMin = -8f;
        internal const float RingZMax = 8f;

        // Stairwell A — north-west corner. Arrival strip stays solid on every floor;
        // the rest of the shaft is open all the way up.
        internal const float StairAXMin = -22f;
        internal const float StairAXMax = -14f;
        internal const float StairAZMin = 8f;
        internal const float StairAArrival = 11f;
        internal const float StairAZMax = 18f;

        // Stairwell B — south-east. Mirrored, entered from its west wall.
        internal const float StairBXMin = 14f;
        internal const float StairBXMax = 22f;
        internal const float StairBZMin = -8f;
        internal const float StairBArrival = -1f;
        internal const float StairBZMax = 2f;

        // Elevator shafts — west wall. Dead on every floor (GDD §5.1), so they are solid
        // blocks with recessed doors, not a working vertical connection.
        internal const float LiftXMin = -22f;
        internal const float LiftXMax = -18f;
        internal const float LiftZMin = -4f;
        internal const float LiftZMax = 4f;

        internal static Rect Plate => BlockoutKit.Area(PlateXMin, PlateZMin, PlateXMax, PlateZMax);

        internal static Rect StairAVoid =>
            BlockoutKit.Area(StairAXMin, StairAArrival, StairAXMax, StairAZMax);

        internal static Rect StairBVoid =>
            BlockoutKit.Area(StairBXMin, StairBZMin, StairBXMax, StairBArrival);

        internal static Rect LiftVoid =>
            BlockoutKit.Area(LiftXMin, LiftZMin, LiftXMax, LiftZMax);

        internal static float FloorY(int floor) => (floor - 5) * BlockoutKit.FloorToFloor;

        internal static string FloorLabel(int floor) => floor switch
        {
            5 => "F5_NormalOperations",
            6 => "F6_RealityBreaks",
            7 => "F7_Flooded",
            8 => "F8_Surveillance",
            _ => $"F{floor}"
        };
    }
}
