using System.Collections.Generic;
using UnityEngine;
using static Office.Editor.Level.TowerGeometry;

namespace Office.Editor.Level
{
    /// <summary>
    /// Floor plates and dropped ceilings. Floors above F5 are cut open over both stair
    /// shafts and the lift block, which is what makes the tower read as one volume
    /// instead of four unrelated rooms stacked on top of each other.
    /// </summary>
    internal static class TowerPlates
    {
        internal static void Build(Transform root, int floor)
        {
            var baseY = FloorY(floor);
            var group = BlockoutKit.Group(root, "Plates");

            var voids = floor == 5
                ? new List<Rect> { LiftVoid }
                : new List<Rect> { StairAVoid, StairBVoid, LiftVoid };

            BlockoutKit.SlabWithVoids(group, "Slab", Plate, voids, baseY, BlockoutPalette.Floor);

            // The ceiling is cut over both shafts on every floor, so a player standing at
            // the bottom of a stairwell can see the flights above them.
            var ceilingVoids = new List<Rect> { StairAVoid, StairBVoid, LiftVoid };

            CeilingWithVoids(group, "Ceiling", Plate, ceilingVoids,
                baseY + BlockoutKit.WallHeight, BlockoutPalette.Ceiling);
        }

        /// <summary>The roof cap. Solid — nothing climbs above the top floor.</summary>
        internal static void BuildRoof(Transform root, int topFloor)
        {
            var group = BlockoutKit.Group(root, "Roof");

            BlockoutKit.Slab(group, "Roof_Slab", Plate,
                FloorY(topFloor) + BlockoutKit.FloorToFloor, BlockoutPalette.Shell);
        }

        private static void CeilingWithVoids(Transform parent, string name, Rect plate,
            IReadOnlyList<Rect> voids, float underY, Material material)
        {
            // A ceiling is a slab whose underside, not its top, is the height that
            // matters — so pass a top one thickness higher and the plane lands with its
            // face at underY.
            BlockoutKit.SlabWithVoids(parent, name, plate, voids,
                underY + BlockoutKit.SlabThickness, material);
        }
    }
}
