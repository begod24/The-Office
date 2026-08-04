using Office.Data;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    /// <summary>
    /// The throwaway test space. Everything sits on the 2 m module and 0.25 m sub-grid from the
    /// locked kit contract (GDD §12.3), so walking it answers real questions: does a 1 m doorway
    /// feel wide enough at 0.32 m player radius, does a 3 m ceiling read as an office, is
    /// 3.2 m/s the right walk speed for a 2 m corridor.
    ///
    /// This is deleted the moment B's modular kit lands. Nothing here is art.
    /// </summary>
    internal static class GreyboxSandbox
    {
        private const float Module = 2f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.25f;
        private const float DoorWidth = 1f;

        private static Material floorMaterial;
        private static Material wallMaterial;
        private static Material accentMaterial;

        public static void Build()
        {
            floorMaterial = Material("M_Greybox_Floor", new Color(0.30f, 0.30f, 0.32f));
            wallMaterial = Material("M_Greybox_Wall", new Color(0.55f, 0.54f, 0.52f));
            accentMaterial = Material("M_Greybox_Accent", new Color(0.78f, 0.29f, 0.22f));

            var root = new GameObject("Greybox").transform;

            BuildFloor(root, 24f);
            BuildOuterWalls(root, 24f);
            BuildInteriorPartition(root);
            BuildCorridor(root);
            BuildScaleReferences(root);
        }

        private static void BuildFloor(Transform parent, float size)
        {
            var floor = Box(parent, "Floor",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(size, 0.2f, size),
                floorMaterial);

            floor.isStatic = true;
        }

        private static void BuildOuterWalls(Transform parent, float size)
        {
            var half = size * 0.5f;
            var group = new GameObject("OuterWalls").transform;
            group.SetParent(parent, false);

            Box(group, "Wall_North", new Vector3(0f, WallHeight * 0.5f, half),
                new Vector3(size, WallHeight, WallThickness), wallMaterial);

            Box(group, "Wall_South", new Vector3(0f, WallHeight * 0.5f, -half),
                new Vector3(size, WallHeight, WallThickness), wallMaterial);

            Box(group, "Wall_East", new Vector3(half, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, size), wallMaterial);

            Box(group, "Wall_West", new Vector3(-half, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, size), wallMaterial);
        }

        /// <summary>
        /// A wall across the room with a single 1 m doorway. The one measurement worth testing
        /// before twenty rooms are built: whether a doorway that narrow is passable at speed
        /// with a teammate coming the other way.
        /// </summary>
        private static void BuildInteriorPartition(Transform parent)
        {
            var group = new GameObject("Partition").transform;
            group.SetParent(parent, false);

            const float z = 6f;
            const float span = 24f;
            var sideLength = (span - DoorWidth) * 0.5f;
            var sideOffset = (DoorWidth + sideLength) * 0.5f;

            Box(group, "Partition_Left",
                new Vector3(-sideOffset, WallHeight * 0.5f, z),
                new Vector3(sideLength, WallHeight, WallThickness), wallMaterial);

            Box(group, "Partition_Right",
                new Vector3(sideOffset, WallHeight * 0.5f, z),
                new Vector3(sideLength, WallHeight, WallThickness), wallMaterial);

            // Header above the doorway, so the opening reads as a door rather than a gap.
            Box(group, "Partition_Header",
                new Vector3(0f, 2.5f, z),
                new Vector3(DoorWidth, 1f, WallThickness), wallMaterial);

            Box(group, "DoorFrame_Marker",
                new Vector3(0f, 0.02f, z),
                new Vector3(DoorWidth, 0.04f, WallThickness), accentMaterial);
        }

        /// <summary>A 2 m wide dead-end corridor — the width the whole kit is built around.</summary>
        private static void BuildCorridor(Transform parent)
        {
            var group = new GameObject("Corridor").transform;
            group.SetParent(parent, false);

            const float length = 10f;
            const float centreX = -7f;
            var startZ = -11f + length * 0.5f;

            Box(group, "Corridor_WallA",
                new Vector3(centreX - Module * 0.5f, WallHeight * 0.5f, startZ),
                new Vector3(WallThickness, WallHeight, length), wallMaterial);

            Box(group, "Corridor_WallB",
                new Vector3(centreX + Module * 0.5f, WallHeight * 0.5f, startZ),
                new Vector3(WallThickness, WallHeight, length), wallMaterial);

            Box(group, "Corridor_Ceiling",
                new Vector3(centreX, WallHeight, startZ),
                new Vector3(Module, 0.2f, length), wallMaterial);
        }

        /// <summary>
        /// Obstacles at the heights that decide the controller's step offset and crouch height:
        /// a desk to crouch under, a 0.3 m step to walk over, a 0.6 m crate to be blocked by,
        /// and a ramp to check slope limit.
        /// </summary>
        private static void BuildScaleReferences(Transform parent)
        {
            var group = new GameObject("ScaleReferences").transform;
            group.SetParent(parent, false);

            Box(group, "Step_030", new Vector3(4f, 0.15f, -4f),
                new Vector3(4f, 0.3f, 2f), accentMaterial);

            Box(group, "Crate_060", new Vector3(8f, 0.3f, -4f),
                new Vector3(1f, 0.6f, 1f), wallMaterial);

            // Underside at 1.2 m: too low to walk under, high enough to crouch under.
            Box(group, "DeskUnderpass_Top", new Vector3(4f, 1.3f, 2f),
                new Vector3(4f, 0.2f, 2f), wallMaterial);

            Box(group, "DeskUnderpass_LegA", new Vector3(2.1f, 0.6f, 2f),
                new Vector3(0.2f, 1.2f, 2f), wallMaterial);

            Box(group, "DeskUnderpass_LegB", new Vector3(5.9f, 0.6f, 2f),
                new Vector3(0.2f, 1.2f, 2f), wallMaterial);

            var ramp = Box(group, "Ramp_30deg", new Vector3(-5f, 0.6f, -6f),
                new Vector3(3f, 0.2f, 5f), accentMaterial);
            ramp.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);

            var pillar = Box(group, "Pillar_Column", new Vector3(-4f, 1.5f, 3f),
                new Vector3(0.5f, WallHeight, 0.5f), wallMaterial);
            pillar.isStatic = true;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = PhysicsLayers.LevelGeometry;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.isStatic = true;
            return box;
        }

        private static Material Material(string assetName, Color color)
        {
            const string folder = "Assets/Project/Art/Materials";
            var path = $"{folder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Project/Art", "Materials");

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };
            material.SetFloat("_Smoothness", 0.05f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
