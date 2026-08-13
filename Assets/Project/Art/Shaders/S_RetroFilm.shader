// The whole PS1-through-a-worn-VHS look, as one fullscreen pass.
//
// GDD §12.1 asks for low internal resolution, limited colour depth with ordered dithering, and
// a tape layer over the top. Two of those three are cheaper and more honest outside a shader —
// the pipeline's own render scale with a point upscale gives real low-resolution rendering
// rather than an imitation of it, and costs frame time instead of spending it. What is left
// here is everything that has to happen per output pixel: the palette, the dither that makes a
// small palette readable, and the tape.
//
// Ordered here on purpose, and the order is the whole difference between "retro" and "broken":
// the tape damages the picture (wobble, tracking, colour bleed) before the palette quantises it,
// because a real VHS deck recorded an already-degraded signal. Dithering after quantisation
// would sit on top of the bands instead of hiding them.

Shader "Office/RetroFilm"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RetroFilm"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelHeight;
            float _ColourSteps;
            float _DitherStrength;
            float _ScanlineStrength;
            float _ScanlineCount;
            float _ChromaticOffset;
            float _WobbleAmplitude;
            float _WobbleSpeed;
            float _TrackingStrength;
            float _TrackingSpeed;
            float _NoiseStrength;
            float _Desaturation;

            // Deliberately not a texture lookup. One hash is cheaper than a sampler at this
            // rate, and the grain has to change every frame or it reads as dirt on the lens
            // rather than as noise in the signal.
            // Small multiplier on purpose. The usual form of this hash scales by a few hundred
            // before the first frac(), which throws away most of a 32-bit float's mantissa
            // once the input reaches the hundreds — and this one is fed pixel coordinates plus
            // a clock, so it gets there. Scaling *down* first keeps the working value near 1
            // where the precision is, and the pattern is just as unstructured.
            //
            // Inputs are still expected to be bounded; see the note on `time` in Frag.
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

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

                // Row index in the picture the tape thinks it is playing, not in the output.
                // Deriving the tape's scale from _PixelHeight rather than from the screen is
                // what keeps the wobble and the scanlines locked to the pixels: tie them to
                // the output and every artefact changes size with the player's resolution.
                float row = floor(uv.y * _PixelHeight);

                // Wrapped, and this is not tidiness. Everything below hashes on time, and a
                // hash built on frac(p * 443.897) has no precision left once p reaches the
                // hundreds — adjacent pixels collapse onto the same value and the grain
                // degenerates into fixed vertical bars. _Time.y is seconds since the
                // application started, so at 32-bit float that arrives a few minutes into a
                // run: the effect would look correct in every short test and slowly fall apart
                // over a session, which is the hardest kind of bug to be told about.
                //
                // The period is deliberately not round. A wrap at 60 seconds would put every
                // repeat on the same beat as the clock and the timer.
                float time = fmod(_Time.y, 97.0);

                // --------------------------------------------------------------- tape
                // Two frequencies. The slow one is the tape breathing; the fast one is a head
                // that never quite tracks. One alone reads as a sine wave, which is a shader
                // effect. Two read as a machine.
                float wobble =
                    sin(row * 0.55 + time * _WobbleSpeed) * 0.6 +
                    sin(row * 3.10 + time * _WobbleSpeed * 2.3) * 0.4;

                // A band of bad tracking, drifting up the picture. Everything inside it is
                // displaced sideways and lit wrong — the moment a real tape reminds you it is
                // a tape.
                //
                // Narrow, and gated so that most of the time there is no band at all. Both
                // matter more than they look: a permanent tear is wallpaper, and the player
                // stops seeing it within a minute while still paying its cost in legibility
                // for the rest of a thirty-minute run. Something that happens every twenty
                // seconds or so keeps working, because it is an event.
                float bandPosition = frac(uv.y + time * _TrackingSpeed);
                float bandShape = pow(saturate(1.0 - abs(bandPosition - 0.5) * 26.0), 4.0);

                // The gate changes once every few seconds and is open for a small part of its
                // range, so a fault arrives, lasts about a second, and goes.
                float gate = smoothstep(0.86, 1.0, Hash(float2(floor(time * 0.45), 17.0)));

                float band = bandShape * gate;

                // Stepped at 24 frames a second, wrapped to keep the hash input small. A tear
                // that changed every rendered frame would flicker at whatever the machine
                // happens to run at, so the fault would look different on a fast PC — the
                // whole point of a fixed rate is that the tape does not care.
                float jitter = Hash(float2(row, fmod(floor(time * 24.0), 128.0))) - 0.5;

                uv.x += wobble * _WobbleAmplitude;
                uv.x += band * jitter * _TrackingStrength;

                // Clamped rather than wrapped. A wrapped tape displacement wraps the *picture*,
                // so a corridor's left wall appears on the right — legible as a bug, not as
                // damage. A real deck smears the edge pixel instead.
                uv = saturate(uv);

                // ------------------------------------------------------- chromatic bleed
                // Colour on VHS carries at a lower bandwidth than luma, so it smears sideways.
                // Widened inside the tracking band, which is where the two failures share a
                // cause.
                float bleed = _ChromaticOffset * (1.0 + band * 3.0);

                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    saturate(uv + float2(bleed, 0.0))).r;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    saturate(uv - float2(bleed, 0.0))).b;

                half3 colour = half3(r, source.g, b);

                // Tape stock never held saturation well, and GDD §12.2 wants the office grey
                // so the red emergency light and the green CRT glow are the only colours that
                // survive.
                float luma = dot(colour, half3(0.299, 0.587, 0.114));
                colour = lerp(colour, luma.xxx, _Desaturation);

                // The band burns slightly, the way an over-driven head does. Scaled by what is
                // already lit: a flat addition turns an unlit corridor into a grey stripe, and
                // this office is meant to be dark enough that the flashlight matters.
                colour += band * 0.10 * (luma + 0.15);

                // ------------------------------------------------------------- palette
                //
                // **In gamma space, not linear.** This is the difference between a retro
                // palette and a ruined one. The colour arriving here is linear, where the
                // darkest tenth of what the eye can distinguish occupies about one percent of
                // the range — so evenly spaced steps put almost the entire palette in the
                // highlights and leave an unlit office to be described by two or three of
                // them. The dither then has nothing to blend between and turns into a visible
                // plaid across the whole picture. A PS1 stored five bits per channel of
                // *gamma* values, and the conversion is what makes 32 steps look like 32
                // steps everywhere instead of only where it is already bright.
                float2 pixel = input.texcoord * _ScreenParams.xy;
                float steps = max(2.0, _ColourSteps);

                colour = LinearToSRGB(saturate(colour));

                // Dither, then quantise. The offset is smaller than one palette step, so it
                // cannot shift a colour further than the banding it is hiding — it only
                // decides which side of a step a pixel falls on, which is what turns a hard
                // band into a texture the eye reads as a gradient.
                colour += DitherOffset(pixel) * (_DitherStrength / steps);
                colour = floor(saturate(colour) * steps + 0.5) / steps;

                // ----------------------------------------------------------- scanlines
                // Applied in gamma space with the rest of the tape, so a scanline darkens the
                // picture by as much as it looks like it does.
                float scan = sin(uv.y * _ScanlineCount * PI);
                colour *= 1.0 - _ScanlineStrength * scan * scan;

                // Signal noise, resolved per picture pixel and per frame.
                //
                // Scaled by brightness, which is the difference between a tape and a broken
                // aerial. Added flat, it is loudest exactly where there is no picture — and
                // this office is dark by design (GDD §12.1, §14: the flashlight is what buys
                // information back), so a flat grain turns every unlit corridor into a wall of
                // static and hides the one thing the player is straining to see. The floor
                // keeps a little life in the blacks so they do not read as a dead pixel.
                float grain = Hash(pixel + time * 61.0) - 0.5;
                colour += grain * _NoiseStrength * (luma * 0.85 + 0.15);

                // Back to linear for whatever composites this. Everything from the palette
                // down was authored against gamma values, so the round trip has to close here
                // rather than anywhere earlier.
                colour = SRGBToLinear(saturate(colour));

                return half4(colour, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
