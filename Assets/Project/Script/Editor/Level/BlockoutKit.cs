using System.Collections.Generic;
using Office.Data;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Office.Editor.Level
{
    /// <summary>A rectangular hole punched through a wall run, measured along the run.</summary>
    internal readonly struct Opening
    {
        internal readonly float Start;
        internal readonly float End;
        internal readonly float Sill;
        internal readonly float Head;

        private Opening(float start, float end, float sill, float head)
        {
            Start = start;
            End = end;
            Sill = sill;
            Head = head;
        }

        /// <summary>A standard 1.0 x 2.1 m doorway centred on <paramref name="centre"/>.</summary>
        internal static Opening Door(float centre, float width = BlockoutKit.DoorWidth) =>
            new(centre - width * 0.5f, centre + width * 0.5f, 0f, BlockoutKit.DoorHeight);

        /// <summary>A full-height gap — no header. Used for open thresholds into corridors.</summary>
        internal static Opening Arch(float centre, float width) =>
            new(centre - width * 0.5f, centre + width * 0.5f, 0f, BlockoutKit.WallHeight);

        /// <summary>An interior glazing band: waist-height sill, header above.</summary>
        internal static Opening Window(float centre, float width) =>
            new(centre - width * 0.5f, centre + width * 0.5f, 0.9f, 2.4f);
    }

    /// <summary>
    /// Every piece of blockout geometry is built here, as a real ProBuilder mesh, so the
    /// level designer can keep editing it by hand (extrude, bevel, UV) after generation.
    /// Walls are composed from box segments rather than boolean-cut, which keeps the quad
    /// topology clean — a CSG hole is far harder to edit afterwards than three boxes.
    /// </summary>
    internal static class BlockoutKit
    {
        internal const float Module = 2f;
        internal const float WallHeight = 3.2f;
        internal const float FloorToFloor = 4f;
        internal const float SlabThickness = 0.4f;
        internal const float WallThickness = 0.2f;
        internal const float ShellThickness = 0.4f;
        internal const float DoorWidth = 1f;
        internal const float DoorHeight = 2.1f;

        private static readonly List<Opening> Sorted = new(8);

        internal static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        internal static ProBuilderMesh Box(Transform parent, string name, Vector3 centre,
            Vector3 size, Material material, Quaternion rotation = default,
            int layer = PhysicsLayers.LevelGeometry)
        {
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            var go = mesh.gameObject;

            go.name = name;
            go.layer = layer;
            go.isStatic = true;

            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation == default ? Quaternion.identity : rotation;

            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            mesh.ToMesh();
            mesh.Refresh();

            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;

            return mesh;
        }

        /// <summary>
        /// A wall between two points on the XZ plane, with openings punched along it.
        /// Emits one box per solid piece: piers between openings, headers above doors,
        /// aprons below windows.
        /// </summary>
        internal static void WallRun(Transform parent, string name, Vector3 from, Vector3 to,
            Material material, IEnumerable<Opening> openings = null,
            float height = WallHeight, float thickness = WallThickness, float baseY = 0f)
        {
            var delta = to - from;
            var length = delta.magnitude;

            if (length < 0.01f) return;

            var direction = delta / length;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var index = 0;

            void Segment(float start, float end, float yMin, float yMax)
            {
                var run = end - start;
                var rise = yMax - yMin;

                if (run < 0.005f || rise < 0.005f) return;

                var centre = from
                             + direction * (start + run * 0.5f)
                             + Vector3.up * (baseY + yMin + rise * 0.5f);

                Box(parent, $"{name}_{index++}", centre,
                    new Vector3(thickness, rise, run), material, rotation);
            }

            Sorted.Clear();
            if (openings != null) Sorted.AddRange(openings);
            Sorted.Sort((a, b) => a.Start.CompareTo(b.Start));

            var cursor = 0f;

            foreach (var opening in Sorted)
            {
                var start = Mathf.Clamp(opening.Start, 0f, length);
                var end = Mathf.Clamp(opening.End, 0f, length);

                Segment(cursor, start, 0f, height);

                if (opening.Sill > 0f) Segment(start, end, 0f, opening.Sill);
                if (opening.Head < height) Segment(start, end, opening.Head, height);

                cursor = Mathf.Max(cursor, end);
            }

            Segment(cursor, length, 0f, height);
        }

        /// <summary>A floor slab. <paramref name="topY"/> is the walkable surface height.</summary>
        internal static ProBuilderMesh Slab(Transform parent, string name, Rect footprint,
            float topY, Material material) =>
            Box(parent, name,
                new Vector3(footprint.center.x, topY - SlabThickness * 0.5f, footprint.center.y),
                new Vector3(footprint.width, SlabThickness, footprint.height), material);

        /// <summary>
        /// A floor plate with rectangular holes cut out of it — stair shafts, elevator
        /// shafts, light wells. Decomposes into axis-aligned strips rather than boolean
        /// subtraction, so every piece stays a clean editable box.
        /// </summary>
        internal static void SlabWithVoids(Transform parent, string name, Rect plate,
            IReadOnlyList<Rect> voids, float topY, Material material)
        {
            var cuts = new List<float> { plate.xMin, plate.xMax };

            foreach (var hole in voids)
            {
                if (hole.xMin > plate.xMin && hole.xMin < plate.xMax) cuts.Add(hole.xMin);
                if (hole.xMax > plate.xMin && hole.xMax < plate.xMax) cuts.Add(hole.xMax);
            }

            cuts.Sort();

            var gaps = new List<(float Min, float Max)>(4);
            var index = 0;

            for (var i = 0; i < cuts.Count - 1; i++)
            {
                var x0 = cuts[i];
                var x1 = cuts[i + 1];

                if (x1 - x0 < 0.01f) continue;

                gaps.Clear();

                foreach (var hole in voids)
                {
                    if (hole.xMin > x0 + 0.01f || hole.xMax < x1 - 0.01f) continue;

                    gaps.Add((Mathf.Max(hole.yMin, plate.yMin), Mathf.Min(hole.yMax, plate.yMax)));
                }

                gaps.Sort((a, b) => a.Min.CompareTo(b.Min));

                var cursor = plate.yMin;

                foreach (var gap in gaps)
                {
                    if (gap.Min > cursor + 0.01f)
                        Slab(parent, $"{name}_{index++}",
                            Area(x0, cursor, x1, gap.Min), topY, material);

                    cursor = Mathf.Max(cursor, gap.Max);
                }

                if (plate.yMax > cursor + 0.01f)
                    Slab(parent, $"{name}_{index++}",
                        Area(x0, cursor, x1, plate.yMax), topY, material);
            }
        }

        /// <summary>A ceiling plane. <paramref name="underY"/> is the height it hangs at.</summary>
        internal static ProBuilderMesh Ceiling(Transform parent, string name, Rect footprint,
            float underY, Material material) =>
            Box(parent, name,
                new Vector3(footprint.center.x, underY + 0.1f, footprint.center.y),
                new Vector3(footprint.width, 0.2f, footprint.height), material);

        /// <summary>
        /// A straight flight. <paramref name="origin"/> is the bottom step's near-left corner;
        /// the flight climbs along +<paramref name="forward"/>.
        /// </summary>
        internal static void Stair(Transform parent, string name, Vector3 origin,
            Vector3 forward, float width, float rise, float run, Material material)
        {
            var steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.18f));
            var mesh = ShapeGenerator.GenerateStair(
                PivotLocation.FirstVertex, new Vector3(width, rise, run), steps, true);

            var go = mesh.gameObject;
            go.name = name;
            go.layer = PhysicsLayers.LevelGeometry;
            go.isStatic = true;

            go.transform.SetParent(parent, false);
            go.transform.localPosition = origin;
            go.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);

            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            mesh.ToMesh();
            mesh.Refresh();

            go.AddComponent<MeshCollider>();
        }

        /// <summary>A marker object — no geometry, used by the placement components.</summary>
        internal static GameObject Marker(Transform parent, string name, Vector3 position,
            float yaw = 0f)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return marker;
        }

        /// <summary>Builds a Rect from opposite corners on the XZ plane.</summary>
        internal static Rect Area(float xMin, float zMin, float xMax, float zMax) =>
            new(xMin, zMin, xMax - xMin, zMax - zMin);
    }
}
