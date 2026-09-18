using System.Collections.Generic;
using UnityEngine;

namespace Office.Editor.Level
{
    /// <summary>
    /// Authoring shorthand for wall runs. Openings are written in world coordinates —
    /// "a door at x = 3" — because that is how the plan is read off the drawing. The
    /// conversion to run-relative offsets happens here, once, instead of in every call.
    /// </summary>
    internal static class TowerWalls
    {
        internal enum Cut
        {
            Door,
            Arch,
            Window
        }

        internal readonly struct Hole
        {
            internal readonly Cut Kind;
            internal readonly float At;
            internal readonly float Width;

            internal Hole(Cut kind, float at, float width)
            {
                Kind = kind;
                At = at;
                Width = width;
            }
        }

        /// <summary>A 1 m doorway centred on the given world coordinate.</summary>
        internal static Hole D(float at) => new(Cut.Door, at, BlockoutKit.DoorWidth);

        /// <summary>A full-height threshold — no header. Corridor mouths use these.</summary>
        internal static Hole A(float at, float width) => new(Cut.Arch, at, width);

        /// <summary>An interior glazing band.</summary>
        internal static Hole W(float at, float width) => new(Cut.Window, at, width);

        /// <summary>A wall run at constant Z, from xMin to xMax.</summary>
        internal static void Horizontal(Transform parent, string name, float z, float xMin,
            float xMax, Material material, float baseY, params Hole[] holes) =>
            BlockoutKit.WallRun(parent, name,
                new Vector3(xMin, 0f, z), new Vector3(xMax, 0f, z),
                material, Convert(holes, xMin), BlockoutKit.WallHeight,
                BlockoutKit.WallThickness, baseY);

        /// <summary>A wall run at constant X, from zMin to zMax.</summary>
        internal static void Vertical(Transform parent, string name, float x, float zMin,
            float zMax, Material material, float baseY, params Hole[] holes) =>
            BlockoutKit.WallRun(parent, name,
                new Vector3(x, 0f, zMin), new Vector3(x, 0f, zMax),
                material, Convert(holes, zMin), BlockoutKit.WallHeight,
                BlockoutKit.WallThickness, baseY);

        /// <summary>The building envelope — thicker, and never opened on a blockout floor.</summary>
        internal static void Shell(Transform parent, string name, Vector3 from, Vector3 to,
            Material material, float baseY) =>
            BlockoutKit.WallRun(parent, name, from, to, material, null,
                BlockoutKit.WallHeight, BlockoutKit.ShellThickness, baseY);

        private static IEnumerable<Opening> Convert(IReadOnlyList<Hole> holes, float runStart)
        {
            if (holes == null || holes.Count == 0) yield break;

            foreach (var hole in holes)
            {
                var centre = hole.At - runStart;

                yield return hole.Kind switch
                {
                    Cut.Door => Opening.Door(centre, hole.Width),
                    Cut.Arch => Opening.Arch(centre, hole.Width),
                    _ => Opening.Window(centre, hole.Width)
                };
            }
        }
    }
}
