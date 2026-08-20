using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Office.Rendering
{
    /// <summary>
    /// The colour half of the PS1 look: a limited palette with ordered dithering, and an
    /// optional coarser pixel grid on top of the one the render scale already produces.
    /// GDD §12.1.
    /// </summary>
    /// <remarks>
    /// <b>This feature is only half of the look.</b> The other half is the pipeline asset's own
    /// render scale with a point upscale, which is what makes the picture genuinely low
    /// resolution instead of a full-resolution image pretending to be one — and buys frame time
    /// rather than spending it. <c>Office/Setup/Build Pixel Render</c> sets both together.
    /// <para>
    /// <b>It replaced a VHS layer, and the difference is the point.</b> The previous pass added
    /// tape wobble, a drifting tracking band, chromatic bleed, scanlines and per-frame grain.
    /// Every one of those moves, and a moving artefact is read as a filter laid over a picture
    /// rather than as the picture's own medium — which is exactly what pixel art cannot afford.
    /// Everything here is a function of the pixel and nothing else, so a still frame and a
    /// moving one are made of the same thing.
    /// </para>
    /// <para>
    /// Runs at <see cref="RenderPassEvent.AfterRenderingPostProcessing"/>, so the volume
    /// profile's grade, bloom and vignette are all inside the signal being quantised, and the
    /// screen-space UI — composited after URP entirely — stays sharp and readable on top.
    /// </para>
    /// </remarks>
    [DisallowMultipleRendererFeature("Pixel Art")]
    public sealed class PixelArtFeature : ScriptableRendererFeature
    {
        [Tooltip("The shader that does the work. Office/PixelArt.")]
        [SerializeField] private Shader shader;

        [Header("Grid")]
        [Tooltip("How many rendered pixels make one block, on top of the render scale. One " +
                 "leaves the render scale's own grid alone, which is the usual answer — the " +
                 "scale is the honest way to be low resolution and this only exists for when " +
                 "the grid has to be coarser than the frame time saved by lowering it further. " +
                 "Whole numbers only: a fractional block puts its edge halfway through a pixel " +
                 "and the seam crawls when the camera moves.")]
        [Range(1f, 8f)]
        [SerializeField] private float pixelSize = 1f;

        [Header("Palette")]
        [Tooltip("Levels per colour channel. Five bits — 32 — is what the hardware did, and it " +
                 "is close to right here: fewer turns every soft light pool into rings, and " +
                 "many more stops reading as a palette at all.")]
        [Range(2f, 64f)]
        [SerializeField] private float colourSteps = 32f;

        [Tooltip("How hard the ordered dither works. It exists to hide the palette's steps, " +
                 "so it should be the thing nobody notices — turned up far enough to be seen " +
                 "as a pattern, it is louder than the banding it was hiding.")]
        [Range(0f, 2f)]
        [SerializeField] private float ditherStrength = 0.9f;

        [Tooltip("Grey the picture towards luma. Kept low on purpose: GDD §12.2 asks for a " +
                 "desaturated office, but that belongs to the lighting and the materials, " +
                 "which can still let a warm bulb read as warm. Draining it here drains the " +
                 "emergency red and the CRT green with it.")]
        [Range(0f, 1f)]
        [SerializeField] private float desaturation = 0.06f;

        [Tooltip("Raises the floor before the palette is applied, so unlit geometry keeps a " +
                 "readable silhouette instead of falling off the first step into black. Tune " +
                 "this against an unlit corridor with the flashlight off, not against a lit " +
                 "room — it is invisible anywhere the picture already has light in it.")]
        [Range(0f, 0.25f)]
        [SerializeField] private float blackLift = 0.06f;

        private Material material;
        private PixelArtPass pass;

        public override void Create()
        {
            if (shader == null) return;

            material = CoreUtils.CreateEngineMaterial(shader);

            pass = new PixelArtPass(material)
            {
                // After post-processing, so the grade is inside the signal being quantised.
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;

            // Game cameras only. The scene view is where the level is built, and a quantised
            // picture would be actively in the way of the person placing walls, who needs to
            // see the geometry rather than the mood.
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            ApplySettings();
            renderer.EnqueuePass(pass);
        }

        private void ApplySettings()
        {
            material.SetFloat(ShaderIds.PixelSize, Mathf.Floor(Mathf.Max(1f, pixelSize)));
            material.SetFloat(ShaderIds.ColourSteps, colourSteps);
            material.SetFloat(ShaderIds.DitherStrength, ditherStrength);
            material.SetFloat(ShaderIds.Desaturation, desaturation);
            material.SetFloat(ShaderIds.BlackLift, blackLift);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        private static class ShaderIds
        {
            public static readonly int PixelSize = Shader.PropertyToID("_PixelSize");
            public static readonly int ColourSteps = Shader.PropertyToID("_ColourSteps");
            public static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
            public static readonly int Desaturation = Shader.PropertyToID("_Desaturation");
            public static readonly int BlackLift = Shader.PropertyToID("_BlackLift");
        }

        /// <remarks>
        /// A blit into a scratch texture rather than in place: the source is also the
        /// destination otherwise, and reading a render target while writing it is undefined on
        /// every platform that does not silently resolve it first. Handing
        /// <c>resourceData.cameraColor</c> the result is what makes the swap free — nothing
        /// copies back.
        /// </remarks>
        private sealed class PixelArtPass : ScriptableRenderPass
        {
            private readonly Material material;

            public PixelArtPass(Material material)
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
                descriptor.name = "PixelArt";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = 0;

                var destination = renderGraph.CreateTexture(descriptor);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source, destination, material, 0);

                renderGraph.AddBlitPass(parameters, "Pixel Art");

                resourceData.cameraColor = destination;
            }
        }
    }
}
