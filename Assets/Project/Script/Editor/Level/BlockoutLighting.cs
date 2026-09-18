using UnityEngine;
using UnityEngine.Rendering;
using static Office.Editor.Level.TowerGeometry;

namespace Office.Editor.Level
{
    /// <summary>
    /// Night lighting for the blockout. There is no sun: the building is lit only by what
    /// is still switched on inside it, which is the whole point of a floor where the power
    /// is the objective.
    ///
    /// Three kinds of source, and nothing else. Cold fluorescents over the rooms that
    /// still have power, red emergency units on the escape routes, and a green exit sign
    /// at each stair door. Everything between them is meant to be black — the darkness is
    /// the level's main occluder, not an accident of under-lighting.
    /// </summary>
    internal static class BlockoutLighting
    {
        private static readonly Color FluorescentTint = new(0.74f, 0.82f, 0.92f);
        private static readonly Color EmergencyTint = new(1f, 0.16f, 0.10f);
        private static readonly Color ExitSignTint = new(0.25f, 1f, 0.42f);

        internal static void Build(Transform root)
        {
            var group = BlockoutKit.Group(root, "Lighting");
            var baseY = FloorY(5);

            Ambient();
            Fluorescents(BlockoutKit.Group(group, "Fluorescent"), baseY);
            EmergencyLights(BlockoutKit.Group(group, "Emergency"), baseY);
            ExitSigns(BlockoutKit.Group(group, "ExitSigns"), baseY);
        }

        private static void Ambient()
        {
            // Almost nothing. Anything brighter and the unlit half of the floor stops
            // being genuinely dark, which is the only tool the level has for tension.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.074f, 0.084f, 0.106f);
            RenderSettings.ambientEquatorColor = new Color(0.054f, 0.062f, 0.080f);
            RenderSettings.ambientGroundColor = new Color(0.030f, 0.033f, 0.041f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.028f, 0.032f, 0.040f);
            RenderSettings.fogDensity = 0.012f;

            RenderSettings.reflectionIntensity = 0.35f;
        }

        private static void Fluorescents(Transform parent, float baseY)
        {
            // Rooms that still have power. The ring is deliberately lit in patches rather
            // than evenly, so a player crossing it moves through light and dark.
            Fitting(parent, "Dev_A", new Vector3(-11f, baseY, 15f));
            Fitting(parent, "Dev_B", new Vector3(-5f, baseY, 15f));
            Fitting(parent, "Dev_C", new Vector3(-11f, baseY, 10f));

            Fitting(parent, "Conference_A", new Vector3(0f, baseY, 13f), 62f);
            Fitting(parent, "Conference_B", new Vector3(5f, baseY, 16f));

            Fitting(parent, "Admin_A", new Vector3(12f, baseY, 10f));
            Fitting(parent, "Admin_B", new Vector3(19f, baseY, 16f));

            Fitting(parent, "Ring_North", new Vector3(-4f, baseY, 7f));
            Fitting(parent, "Ring_East", new Vector3(9f, baseY, -3f));
            Fitting(parent, "Ring_South", new Vector3(2f, baseY, -7f));

            Fitting(parent, "Cubicles_A", new Vector3(-8f, baseY, -12f), 68f);
            Fitting(parent, "Cubicles_B", new Vector3(0f, baseY, -16f));
            Fitting(parent, "Cubicles_C", new Vector3(6f, baseY, -11f));

            Fitting(parent, "BreakRoom", new Vector3(-17f, baseY, -13f), 62f);
            Fitting(parent, "LiftLobby_A", new Vector3(-14f, baseY, 4f));
            Fitting(parent, "LiftLobby_B", new Vector3(-14f, baseY, -4f));

            Fitting(parent, "MeetingRoom", new Vector3(12f, baseY, 4f));
            Fitting(parent, "StorageSouth", new Vector3(13f, baseY, -13f), 28f);
        }

        private static void EmergencyLights(Transform parent, float baseY)
        {
            // Escape routes only: both stair approaches, the lift lobby, and the two ring
            // corners furthest from a fluorescent.
            EmergencyUnit(parent, "Stair_A", new Vector3(-18f, baseY, 9.5f));
            EmergencyUnit(parent, "Stair_B", new Vector3(15.5f, baseY, 0f));
            EmergencyUnit(parent, "Ring_NorthWest", new Vector3(-9f, baseY, 7f));
            EmergencyUnit(parent, "Ring_SouthEast", new Vector3(9f, baseY, -7f));
            EmergencyUnit(parent, "Core_South", new Vector3(5f, baseY, -7f));
        }

        private static void ExitSigns(Transform parent, float baseY)
        {
            Sign(parent, "Exit_StairA", new Vector3(-16f, baseY + 2.4f, 8.3f));
            Sign(parent, "Exit_StairB", new Vector3(13.7f, baseY + 2.4f, 0f));
        }

        /// <summary>
        /// A ceiling fitting: a wide spot aimed straight down, not a point light. A panel
        /// in a suspended ceiling throws light at the floor, not into the plenum above it —
        /// and a spot costs one shadow map where a point costs six, which is the difference
        /// between eighteen fittings fitting in the shadow atlas and not.
        /// </summary>
        private static void Fitting(Transform parent, string name, Vector3 position,
            float intensity = 45f)
        {
            var host = BlockoutKit.Marker(parent, $"Fitting_{name}",
                position + Vector3.up * (BlockoutKit.WallHeight - 0.25f));

            host.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var light = host.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 120f;
            light.innerSpotAngle = 58f;
            light.color = FluorescentTint;
            light.intensity = intensity;
            light.range = 11f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.renderMode = LightRenderMode.ForcePixel;

            BlockoutKit.Box(host.transform, "Housing", Vector3.forward * -0.12f,
                new Vector3(1.2f, 0.3f, 0.06f), BlockoutPalette.Ceiling);
        }

        private static void EmergencyUnit(Transform parent, string name, Vector3 position)
        {
            var host = BlockoutKit.Marker(parent, $"Emergency_{name}",
                position + Vector3.up * 2.7f);

            var light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = EmergencyTint;
            light.intensity = 18f;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }

        private static void Sign(Transform parent, string name, Vector3 position)
        {
            var host = BlockoutKit.Marker(parent, $"Sign_{name}", position);

            var light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ExitSignTint;
            light.intensity = 9f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;
        }
    }
}
