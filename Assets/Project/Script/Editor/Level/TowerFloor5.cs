using UnityEngine;
using static Office.Editor.Level.TowerGeometry;
using static Office.Editor.Level.TowerWalls;

namespace Office.Editor.Level
{
    /// <summary>
    /// Floor 5 — Normal Operations. The only fully detailed floor: it is the one the
    /// playtest walks, so it carries every room, every door and every marker.
    ///
    /// Read the plan as three layers. A service core in the middle (restrooms, riser,
    /// storage). A 2 m corridor ring around it, which is the loop. Four perimeter bands
    /// holding the named zones from the concept art — Dev north-west, Conference north,
    /// Admin north-east, Cubicles south, Break Room south-west, Storage south-east.
    /// </summary>
    internal static class TowerFloor5
    {
        internal static void Build(Transform root)
        {
            var baseY = FloorY(5);

            var structure = BlockoutKit.Group(root, "Structure");
            var interior = BlockoutKit.Group(root, "InteriorWalls");

            BuildShell(structure, baseY);
            BuildCore(interior, baseY);
            BuildNorthBand(interior, baseY);
            BuildWestBand(interior, baseY);
            BuildEastBand(interior, baseY);
            BuildSouthBand(interior, baseY);
            BuildRingEdges(interior, baseY);
        }

        private static void BuildShell(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Shell;

            Shell(parent, "Shell_South", new Vector3(PlateXMin, 0f, PlateZMin),
                new Vector3(PlateXMax, 0f, PlateZMin), mat, baseY);

            Shell(parent, "Shell_North", new Vector3(PlateXMin, 0f, PlateZMax),
                new Vector3(PlateXMax, 0f, PlateZMax), mat, baseY);

            Shell(parent, "Shell_West", new Vector3(PlateXMin, 0f, PlateZMin),
                new Vector3(PlateXMin, 0f, PlateZMax), mat, baseY);

            Shell(parent, "Shell_East", new Vector3(PlateXMax, 0f, PlateZMin),
                new Vector3(PlateXMax, 0f, PlateZMax), mat, baseY);
        }

        private static void BuildCore(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Core;

            // Restrooms open north onto the ring, storage opens south. One door each, on
            // opposite sides, so crossing the core costs a half lap of the ring.
            Horizontal(parent, "Core_North", CoreZMax, CoreXMin, CoreXMax, mat, baseY, D(-5f));
            Horizontal(parent, "Core_South", CoreZMin, CoreXMin, CoreXMax, mat, baseY, D(5f));

            Vertical(parent, "Core_West", CoreXMin, CoreZMin, CoreZMax, mat, baseY);
            Vertical(parent, "Core_East", CoreXMax, CoreZMin, CoreZMax, mat, baseY);

            // The riser is sealed on both sides — it is plant, not playable space.
            Vertical(parent, "Core_RiserWest", -2f, CoreZMin, CoreZMax, mat, baseY);
            Vertical(parent, "Core_RiserEast", 2f, CoreZMin, CoreZMax, mat, baseY);
        }

        private static void BuildRingEdges(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            // The ring's west and east faces. One wide threshold each, dead centre, so the
            // lift lobby and the east offices both read as part of the loop.
            Vertical(parent, "Ring_WestFace", RingXMin, RingZMin, RingZMax, mat, baseY, A(0f, 3f));
            Vertical(parent, "Ring_EastFace", RingXMax, RingZMin, RingZMax, mat, baseY, A(0f, 3f));
        }

        private static void BuildNorthBand(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            // The band's south face, full width. Everything north of the ring hangs off it.
            Horizontal(parent, "North_BandFace", RingZMax, PlateXMin, PlateXMax, mat, baseY,
                D(-16f),      // Stairwell A -> lift lobby
                A(-12f, 2f),  // Dev open space -> lift lobby
                A(-6f, 3f),   // Dev open space -> ring
                D(3f),        // Conference room -> ring
                A(9f, 2f),    // Admin open -> ring, north-east corner
                D(12f),       // Admin open -> meeting room
                D(18f));      // Admin open -> east storage

            Vertical(parent, "North_StairAFace", StairAXMax, StairAZMin, PlateZMax, mat, baseY);
            Vertical(parent, "North_DevConference", -2f, RingZMax, PlateZMax, mat, baseY, D(13f));

            // Conference glazes onto the glass office — the concept's "Glass Office" plate.
            Vertical(parent, "North_ConferenceAdmin", CoreXMax, RingZMax, PlateZMax, mat, baseY,
                D(10f), W(15f, 4f));

            Horizontal(parent, "North_AdminGlass", 13f, CoreXMax, PlateXMax, mat, baseY,
                D(11f), D(18f));

            Vertical(parent, "North_GlassSplit", 15f, 13f, PlateZMax, mat, baseY);
        }

        private static void BuildWestBand(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            // Supply closet, dead lift shafts, server closet — all opening east into the
            // lift lobby, which is the band's circulation space.
            Vertical(parent, "West_ClosetFace", LiftXMax, RingZMin, RingZMax, mat, baseY,
                D(6f), D(-6f));

            Horizontal(parent, "West_LiftTop", LiftZMax, PlateXMin, LiftXMax, mat, baseY);
            Horizontal(parent, "West_LiftBottom", LiftZMin, PlateXMin, LiftXMax, mat, baseY);
        }

        private static void BuildEastBand(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            // Meeting room over office, both fronting the ring; storage and stairwell B
            // behind them against the east wall.
            Horizontal(parent, "East_MeetingOffice", 0f, RingXMax, StairBXMin, mat, baseY, D(12f));

            Vertical(parent, "East_StorageFace", StairBXMin, StairBZMax, RingZMax, mat, baseY,
                D(5f));

            // The door sits at z = 1, not z = 0. At z = 0 it would land exactly on the
            // T-junction with the meeting/office wall, and a doorway in a wall junction
            // is a doorway the pathfinder walks around: the reachability check measured
            // 94 m to this stairwell against 78 m on the shell floors above, which have
            // no such junction. It must also stay inside the arrival strip (z -1 to 2) —
            // anywhere further south and the door opens onto the open shaft.
            Vertical(parent, "East_StairBFace", StairBXMin, RingZMin, StairBZMax, mat, baseY,
                D(1f));

            Horizontal(parent, "East_StairBTop", StairBZMax, StairBXMin, PlateXMax, mat, baseY);
        }

        private static void BuildSouthBand(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            // The band's north face. Two wide thresholds into the cubicle floor keep the
            // open space reading as part of the ring rather than a room off it.
            Horizontal(parent, "South_BandFace", RingZMin, PlateXMin, PlateXMax, mat, baseY,
                D(-17f),      // Break room -> lift lobby
                A(-6f, 3f),   // Cubicles -> ring
                A(4f, 3f),    // Cubicles -> ring
                D(12f));      // South storage -> east office

            Vertical(parent, "South_BreakCubicles", -12f, PlateZMin, RingZMin, mat, baseY, D(-13f));
            Vertical(parent, "South_CubiclesStorage", RingXMax, PlateZMin, RingZMin, mat, baseY,
                D(-13f));

            Vertical(parent, "South_StorageService", 16f, PlateZMin, RingZMin, mat, baseY, D(-11f));
            Horizontal(parent, "South_ServiceSplit", -13f, 16f, PlateXMax, mat, baseY, D(19f));
        }
    }
}
