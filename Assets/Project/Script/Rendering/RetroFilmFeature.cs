using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Office.Rendering
{
    /// <summary>
    /// The PS1-through-a-worn-VHS layer: a limited palette with ordered dithering, and a tape
    /// that never quite tracks. GDD §12.1.
    /// </summary>
    /// <remarks>
    /// <b>This feature is only half of the look.</b> The other half is the pipeline asset's own
    /// render scale with a point upscale, which is what makes the picture genuinely low
    /// resolution instead of a full-resolution image pretending to be one — and buys frame time
    /// rather than spending it. <c>Office/Setup/Build Retro Render</c> sets both together,
    /// because they have to agree: <see cref="pixelHeight"/> tells this shader how tall the
    /// picture it is damaging actually is, and every artefact that should stay locked to the
    /// pixels — the wobble, the scanlines — is measured in those rows.
    /// <para>
    /// Runs at <see cref="RenderPassEvent.AfterRenderingPostProcessing"/>, so the volume profile's
    /// grade, bloom and vignette are all inside the signal being degraded, and the screen-space
    /// UI — composited after URP entirely — stays sharp and readable on top. GDD §14 wants a
    /// retro HUD eventually, but it should be drawn that way, not smeared into illegibility by
    /// a tape effect.
    /// </para>
    /// </remarks>
    [DisallowMultipleRendererFeature("Retro Film")]
    public sealed class RetroFilmFeature : ScriptableRendererFeature
    {
        [Tooltip("The shader that does the work. Office/RetroFilm.")]
        [SerializeField] private Shader shader;

        [Header("Picture")]
        [Tooltip("Rows in the internal picture — keep it equal to the height the pipeline's " +
                 "render scale actually produces, or the scanlines and the tape wobble stop " +
                 "lining up with the pixels. 594 is a 1080p screen at 0.55 scale.")]
        [Min(32f)]
        [SerializeField] private float pixelHeight = 594f;

        [Tooltip("Levels per colour channel. Five bits — 32 — is what the hardware did, and it " +
                 "is too few here: this office is mostly dark, where a coarse palette turns " +
                 "every soft light pool into rings. 48 keeps the banding as a texture rather " +
                 "than a feature.")]
        [Range(2f, 64f)]
        [SerializeField] private float colourSteps = 48f;

        [Tooltip("How hard the ordered dither works. It exists to hide the palette's steps, " +
                 "so it should be the thing nobody notices — turned up far enough to be seen " +
                 "as a pattern, it is louder than the banding it was hiding.")]
        [Range(0f, 2f)]
        [SerializeField] private float ditherStrength = 0.6f;

        [Tooltip("Grey the picture towards luma. Kept low on purpose: GDD §12.2 asks for a " +
                 "desaturated office, but that belongs to the lighting and the materials, " +
                 "which can still let a warm bulb read as warm. Draining it here drains the " +
                 "emergency red and the CRT green with it.")]
        [Range(0f, 1f)]
        [SerializeField] private float desaturation = 0.08f;

        [Header("Tape")]
        [Tooltip("How dark the line between each pair of picture rows is. Barely on: a CRT " +
                 "line structure this game never had is the fastest way to make the whole " +
                 "look read as a filter laid over the top rather than as the picture itself.")]
        [Range(0f, 1f)]
        [SerializeField] private float scanlineStrength = 0.05f;

        [Tooltip("Sideways colour smear, in UV. VHS carried colour at a lower bandwidth than " +
                 "brightness, which is why it bleeds horizontally and not vertically. This is " +
                 "the one artefact worth keeping clearly visible — it is what the eye actually " +
                 "reads as 'old tape', and it only shows on high-contrast edges, so it costs " +
                 "nothing anywhere else.")]
        [Range(0f, 0.02f)]
        [SerializeField] private float chromaticOffset = 0.0013f;

        [Tooltip("Constant horizontal unsteadiness, in UV. Should sit under a pixel — enough " +
                 "that the picture is never quite still, not enough to notice directly.")]
        [Range(0f, 0.02f)]
        [SerializeField] private float wobbleAmplitude = 0.00035f;

        [Range(0f, 20f)]
        [SerializeField] private float wobbleSpeed = 1.6f;

        [Tooltip("How far the tracking band tears the picture sideways. The band is narrow and " +
                 "only appears every twenty seconds or so, so this can afford to be visible " +
                 "when it happens — but it is the artefact that most easily reads as a broken " +
                 "game rather than a worn tape, so it stays small. Zero turns it off entirely.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float trackingStrength = 0.004f;

        [Tooltip("How fast the tracking band drifts up the screen. Slow is more unsettling — " +
                 "a band that races past reads as a glitch effect rather than as a bad tape.")]
        [Range(-1f, 1f)]
        [SerializeField] private float trackingSpeed = 0.08f;

        [Tooltip("Per-pixel signal noise, scaled by how lit the pixel already is. Tune it " +
                 "against a torch-lit wall rather than an empty scene — the shader keeps only " +
                 "a fraction of it in the blacks, so a value that looks tame in an unlit " +
                 "corridor is deafening once the flashlight is on something.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float noiseStrength = 0.015f;

        private Material material;
        private RetroFilmPass pass;

        public override void Create()
        {
            if (shader == null) return;

            material = CoreUtils.CreateEngineMaterial(shader);

            pass = new RetroFilmPass(material)
            {
                // After post-processing, so the grade is inside the signal being degraded.
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;

            // Game cameras only. A preview thumbnail with a tracking tear across it is noise,
            // and the scene view is where the level is built — dithering and a wobbling
            // picture would be actively in the way of the person placing walls, who needs to
            // see the geometry rather than the mood.
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            ApplySettings();
            renderer.EnqueuePass(pass);
        }

        private void ApplySettings()
        {
            material.SetFloat(ShaderIds.PixelHeight, pixelHeight);
            material.SetFloat(ShaderIds.ColourSteps, colourSteps);
            material.SetFloat(ShaderIds.DitherStrength, ditherStrength);
            material.SetFloat(ShaderIds.Desaturation, desaturation);
            material.SetFloat(ShaderIds.ScanlineStrength, scanlineStrength);

            // Half the picture height: one dark line per two rows, not per row. Derived from
            // the internal resolution rather than authored separately so the two cannot drift
            // — but halved, because at anything near a 1:1 line-per-row the pattern lands
            // close to the output's own pixel pitch and beats against it, and the moiré that
            // produces moves when the player does.
            material.SetFloat(ShaderIds.ScanlineCount, pixelHeight * 0.5f);

            material.SetFloat(ShaderIds.ChromaticOffset, chromaticOffset);
            material.SetFloat(ShaderIds.WobbleAmplitude, wobbleAmplitude);
            material.SetFloat(ShaderIds.WobbleSpeed, wobbleSpeed);
            material.SetFloat(ShaderIds.TrackingStrength, trackingStrength);
            material.SetFloat(ShaderIds.TrackingSpeed, trackingSpeed);
            material.SetFloat(ShaderIds.NoiseStrength, noiseStrength);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        private static class ShaderIds
        {
            public static readonly int PixelHeight = Shader.PropertyToID("_PixelHeight");
            public static readonly int ColourSteps = Shader.PropertyToID("_ColourSteps");
            public static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
            public static readonly int Desaturation = Shader.PropertyToID("_Desaturation");
            public static readonly int ScanlineStrength = Shader.PropertyToID("_ScanlineStrength");
            public static readonly int ScanlineCount = Shader.PropertyToID("_ScanlineCount");
            public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
            public static readonly int WobbleAmplitude = Shader.PropertyToID("_WobbleAmplitude");
            public static readonly int WobbleSpeed = Shader.PropertyToID("_WobbleSpeed");
            public static readonly int TrackingStrength = Shader.PropertyToID("_TrackingStrength");
            public static readonly int TrackingSpeed = Shader.PropertyToID("_TrackingSpeed");
            public static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        }

        /// <remarks>
        /// A blit into a scratch texture rather than in place: the source is also the
        /// destination otherwise, and reading a render target while writing it is undefined on
        /// every platform that does not silently resolve it first. Handing
        /// <c>resourceData.cameraColor</c> the result is what makes the swap free — nothing
        /// copies back.
        /// </remarks>
        private sealed class RetroFilmPass : ScriptableRenderPass
        {
            private readonly Material material;

            public RetroFilmPass(Material material)
            {
                this.material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                // Writing to the back buffer is not allowed here, and asking anyway is a
                // validation error rather than a wrong-looking frame.
                if (resourceData.isActiveTargetBackBuffer) return;

                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "RetroFilm";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = 0;

                var destination = renderGraph.CreateTexture(descriptor);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source, destination, material, 0);

                renderGraph.AddBlitPass(parameters, "Retro Film");

                resourceData.cameraColor = destination;
            }
        }
    }
}
