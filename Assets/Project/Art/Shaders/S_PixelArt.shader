// The colour half of the PS1 look, as one fullscreen pass.
//
// GDD §12.1 asks for a low internal resolution and a limited palette. Only the second of those
// belongs in a shader: the first is the pipeline's own render scale with a point upscale, which
// produces genuinely low-resolution rendering rather than an imitation of it, and buys frame
// time instead of spending it. See PixelRenderBuilder, which sets both together.
//
// What was here before this was a VHS layer — wobble, a tracking band, chromatic bleed,
// scanlines and per-frame grain. All of it is gone deliberately. Every one of those artefacts
// moves, and movement is what stops an image reading as pixel art: the eye resolves a drifting
// grid as a filter laid over a picture, and a still one as the picture itself. What is left is
// static per pixel, so a frozen frame and a moving one look like the same medium.

Shader "Office/PixelArt"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelArt"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _ColourSteps;
            float _DitherStrength;
            float _Desaturation;
            float _BlackLift;

            // Bayer 4x4, the classic ordered matrix. Returns -0.5..0.5 so it can be added to a
            // colour before quantising without shifting the average brightness.
            float DitherOffset(float2 pixel)
            {
                const float bayer[16] =
                {
                     0.0,  8.0,  2.0, 10.0,
                    12.0,  4.0, 14.0,  6.0,
                     3.0, 11.0,  1.0,  9.0,
                    15.0,  7.0, 13.0,  5.0
                };

                int x = (int)fmod(pixel.x, 4.0);
                int y = (int)fmod(pixel.y, 4.0);

                return bayer[y * 4 + x] / 16.0 - 0.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // _ScreenParams is the buffer this pass is running in, which at a render scale
                // below one is already the small one. Blocks are therefore whole source pixels
                // and _PixelSize is a whole number of them — anything fractional puts a block
                // boundary halfway through a pixel, and the seam beats against the point
                // upscale in a pattern that crawls when the camera moves.
                float size = max(1.0, floor(_PixelSize));

                if (size > 1.0)
                {
                    float2 grid = _ScreenParams.xy / size;
                    uv = (floor(uv * grid) + 0.5) / grid;
                }

                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                half3 colour = source.rgb;

                // GDD §12.2 wants the office grey so the red emergency light and the green CRT
                // glow are the only colours that survive. Kept low: draining it here drains
                // those two with it, and the lighting is the better place for the rest.
                float luma = dot(colour, half3(0.299, 0.587, 0.114));
                colour = lerp(colour, luma.xxx, _Desaturation);

                // --------------------------------------------------------------- palette
                //
                // **In gamma space, not linear.** This is the difference between a retro
                // palette and a ruined one. The colour arriving here is linear, where the
                // darkest tenth of what the eye can distinguish occupies about one percent of
                // the range — so evenly spaced steps put almost the entire palette in the
                // highlights and leave an unlit office to be described by two or three of
                // them. The dither then has nothing to blend between and turns into a visible
                // plaid across the whole picture. A PS1 stored five bits per channel of
                // *gamma* values, and the conversion is what makes the steps land evenly.
                float steps = max(2.0, floor(_ColourSteps));

                colour = LinearToSRGB(saturate(colour));

                // Lifted before quantising, not after. This office is dark by design and the
                // first palette step is a cliff to pure black — a corridor the flashlight is
                // not pointed at loses its shape entirely without this, and a shape the player
                // cannot resolve cannot frighten them.
                colour = colour * (1.0 - _BlackLift) + _BlackLift;

                // The dither is measured against the block, not the output pixel, so a block
                // is one flat colour. Dithering per output pixel would put a 4x4 pattern
                // inside every block and undo the pixelation the pass exists to protect.
                float2 block = floor(input.texcoord * (_ScreenParams.xy / size));

                // Dither, then quantise. The offset is smaller than one palette step, so it
                // cannot shift a colour further than the banding it is hiding — it only
                // decides which side of a step a pixel falls on, which is what turns a hard
                // band into a texture the eye reads as a gradient.
                colour += DitherOffset(block) * (_DitherStrength / steps);
                colour = floor(saturate(colour) * steps + 0.5) / steps;

                // Back to linear for whatever composites this. Everything from the palette
                // down was authored against gamma values, so the round trip closes here.
                colour = SRGBToLinear(saturate(colour));

                return half4(colour, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
