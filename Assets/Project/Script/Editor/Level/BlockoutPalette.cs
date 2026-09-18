using UnityEditor;
using UnityEngine;

namespace Office.Editor.Level
{
    /// <summary>
    /// The blockout's material set. Greybox values only — brightness reads as surface
    /// role (floor / wall / ceiling / accent), never as final art. Smoothness is where
    /// the wet-corridor look starts, so the floor is deliberately glossy.
    /// </summary>
    internal static class BlockoutPalette
    {
        private const string Folder = "Assets/Project/Art/Materials";

        internal static Material Floor { get; private set; }
        internal static Material FloorWet { get; private set; }
        internal static Material Wall { get; private set; }
        internal static Material Ceiling { get; private set; }
        internal static Material Core { get; private set; }
        internal static Material Glass { get; private set; }
        internal static Material Accent { get; private set; }
        internal static Material Shell { get; private set; }

        internal static void Load()
        {
            Floor = Make("M_BO_Floor", new Color(0.205f, 0.210f, 0.222f), 0.45f);
            FloorWet = Make("M_BO_FloorWet", new Color(0.125f, 0.135f, 0.150f), 0.82f);
            Wall = Make("M_BO_Wall", new Color(0.330f, 0.325f, 0.310f), 0.08f);
            Ceiling = Make("M_BO_Ceiling", new Color(0.265f, 0.265f, 0.262f), 0.05f);
            Core = Make("M_BO_Core", new Color(0.240f, 0.243f, 0.248f), 0.12f);
            Glass = Make("M_BO_Glass", new Color(0.180f, 0.205f, 0.215f), 0.92f);
            Accent = Make("M_BO_Accent", new Color(0.420f, 0.105f, 0.075f), 0.30f);
            Shell = Make("M_BO_Shell", new Color(0.175f, 0.175f, 0.178f), 0.05f);
        }

        private static Material Make(string assetName, Color colour, float smoothness)
        {
            var path = $"{Folder}/{assetName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                EnsureFolder(Folder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
