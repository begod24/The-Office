using UnityEngine;
using static Office.Editor.Level.TowerGeometry;
using static Office.Editor.Level.TowerWalls;

namespace Office.Editor.Level
{
    /// <summary>
    /// Floors 6 to 8 as structural shells: envelope, service core, corridor ring, band
    /// faces and the stair cores. No room subdivision — these exist so the vertical
    /// structure is real and walkable while F5 is the floor under playtest.
    ///
    /// Each floor carries a thin layer of its own character so the shells are not
    /// interchangeable grey boxes: F6 gets the repeated partitions that sell the loop,
    /// F7 gets standing water, F8 gets security checkpoints.
    /// </summary>
    internal static class TowerShellFloors
    {
        internal static void Build(Transform root, int floor)
        {
            var baseY = FloorY(floor);
            var structure = BlockoutKit.Group(root, "Structure");
            var interior = BlockoutKit.Group(root, "InteriorWalls");

            BuildShell(structure, baseY);
            BuildCoreAndRing(interior, baseY);
            BuildBandFaces(interior, baseY);
            BuildVerticalCoreWalls(interior, baseY);

            switch (floor)
            {
                case 6:
                    RealityBreaks(BlockoutKit.Group(root, "Anomalies"), baseY);
                    break;

                case 7:
                    Flooded(BlockoutKit.Group(root, "Hazards"), baseY);
                    break;

                case 8:
                    Surveillance(BlockoutKit.Group(root, "Security"), baseY);
                    break;
            }
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

        private static void BuildCoreAndRing(Transform parent, float baseY)
        {
            var core = BlockoutPalette.Core;
            var wall = BlockoutPalette.Wall;

            Horizontal(parent, "Core_North", CoreZMax, CoreXMin, CoreXMax, core, baseY, D(-5f));
            Horizontal(parent, "Core_South", CoreZMin, CoreXMin, CoreXMax, core, baseY, D(5f));
            Vertical(parent, "Core_West", CoreXMin, CoreZMin, CoreZMax, core, baseY);
            Vertical(parent, "Core_East", CoreXMax, CoreZMin, CoreZMax, core, baseY);

            Vertical(parent, "Ring_WestFace", RingXMin, RingZMin, RingZMax, wall, baseY, A(0f, 3f));
            Vertical(parent, "Ring_EastFace", RingXMax, RingZMin, RingZMax, wall, baseY, A(0f, 3f));
        }

        private static void BuildBandFaces(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            Horizontal(parent, "North_BandFace", RingZMax, PlateXMin, PlateXMax, mat, baseY,
                D(-16f), A(-12f, 2f), A(-6f, 3f), D(3f), A(9f, 2f), D(18f));

            Horizontal(parent, "South_BandFace", RingZMin, PlateXMin, PlateXMax, mat, baseY,
                D(-17f), A(-6f, 3f), A(4f, 3f), D(12f));
        }

        private static void BuildVerticalCoreWalls(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            Vertical(parent, "StairA_EastFace", StairAXMax, StairAZMin, PlateZMax, mat, baseY);
            Vertical(parent, "StairB_WestFace", StairBXMin, RingZMin, StairBZMax, mat, baseY, D(0f));
            Horizontal(parent, "StairB_NorthFace", StairBZMax, StairBXMin, PlateXMax, mat, baseY);
            Vertical(parent, "Lift_ClosetFace", LiftXMax, RingZMin, RingZMax, mat, baseY,
                D(6f), D(-6f));
        }

        /// <summary>
        /// F6 — the same office section repeated four times down the north band. The
        /// geometry alone does not loop; it sets up the recognisable section that the
        /// loop scripting will later swap behind the player.
        /// </summary>
        private static void RealityBreaks(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Wall;

            for (var i = 0; i < 4; i++)
            {
                var x = -14f + i * 8f;

                Vertical(parent, $"RepeatedSection_{i}", x, RingZMax, PlateZMax, mat, baseY,
                    D(13f));

                BlockoutKit.Marker(parent, $"LoopTrigger_{i}", new Vector3(x + 4f, baseY, 13f));
            }

            BlockoutKit.Marker(parent, "Anomaly_EndlessCorridor",
                new Vector3(0f, baseY, RingZMax - 1f));

            BlockoutKit.Marker(parent, "Anomaly_RoomBiggerInside",
                new Vector3(16f, baseY, -4f));
        }

        /// <summary>F7 — standing water over the ring and the south band.</summary>
        private static void Flooded(Transform parent, float baseY)
        {
            var water = BlockoutPalette.FloorWet;

            BlockoutKit.Box(parent, "Water_SouthBand",
                new Vector3(0f, baseY + 0.16f, (PlateZMin + RingZMin) * 0.5f),
                new Vector3(PlateXMax - PlateXMin - 1f, 0.32f, RingZMin - PlateZMin),
                water, default, Office.Data.PhysicsLayers.Prop);

            BlockoutKit.Box(parent, "Water_RingSouth",
                new Vector3(0f, baseY + 0.16f, RingZMin + 1f),
                new Vector3(RingXMax - RingXMin, 0.32f, 2f),
                water, default, Office.Data.PhysicsLayers.Prop);

            BlockoutKit.Marker(parent, "Hazard_LeakingCeiling", new Vector3(-6f, baseY, -12f));
            BlockoutKit.Marker(parent, "Hazard_ExposedElectrics", new Vector3(6f, baseY, -7f));
            BlockoutKit.Marker(parent, "Zone_DryOffices", new Vector3(0f, baseY, 13f));
            BlockoutKit.Marker(parent, "Zone_ITRoom", new Vector3(12f, baseY, 4f));
        }

        /// <summary>F8 — checkpoints on both stair approaches, cameras on the ring.</summary>
        private static void Surveillance(Transform parent, float baseY)
        {
            var mat = BlockoutPalette.Accent;

            BlockoutKit.Box(parent, "Checkpoint_StairA",
                new Vector3(-16f, baseY + 0.55f, RingZMax - 1.2f),
                new Vector3(3f, 1.1f, 0.3f), mat);

            BlockoutKit.Box(parent, "Checkpoint_StairB",
                new Vector3(StairBXMin - 1.2f, baseY + 0.55f, 0f),
                new Vector3(0.3f, 1.1f, 3f), mat);

            foreach (var (x, z) in new[] { (-9f, 7f), (9f, 7f), (-9f, -7f), (9f, -7f) })
                BlockoutKit.Box(parent, $"Camera_{x}_{z}",
                    new Vector3(x, baseY + BlockoutKit.WallHeight - 0.4f, z),
                    new Vector3(0.3f, 0.3f, 0.5f), mat);

            BlockoutKit.Marker(parent, "Zone_ServerRoom", new Vector3(5f, baseY, 0f));
            BlockoutKit.Marker(parent, "Zone_SurveillanceHub", new Vector3(-5f, baseY, 0f));
            BlockoutKit.Marker(parent, "Zone_RestrictedArchive", new Vector3(18f, baseY, 14f));
        }
    }
}
