#version 430

// Fractal noise (Canvas.Noise): standardised, so it comes out with mean 0.5 and a deviation of 1/6, nearly all of it in [0, 1],
// whatever its kind and octaves. A second, independent channel can go in g, for a warp's other axis. Optionally warped by a lower,
// slower noise of its own first, which is what makes it look grown rather than computed: it stays seamless, as the warp tiles too.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result;

uniform ivec2 frequency;
uniform int octaves;
uniform float gain;
uniform int lacunarity;
uniform int kind;
uniform int seed;
uniform float mean;             // One octave's of this kind (Canvas.NoiseStatistics).
uniform float deviation;
uniform float perlinDeviation;  // One octave of gradient noise's, for the warp.
uniform float warp;             // How far the warp moves a point, as a deviation in the tile: 0 for none.
uniform ivec2 warpFrequency;
uniform int channels;           // 1, or 2 for a second channel in g.

const float SPREAD = 1.0 / 6.0;

void main(void)
{
    vec2 uv = uvAt();
    uint s = uint(seed);

    if (warp > 0.0)
    {
        // Three octaves of gradient noise for each axis: gradient noise warped by itself stays smooth.
        float wx = synthFbm(uv, warpFrequency, 3, 0.5, 2, NOISE_PERLIN, s ^ 0x5bd1e995u, 0.0, perlinDeviation);
        float wy = synthFbm(uv, warpFrequency, 3, 0.5, 2, NOISE_PERLIN, s ^ 0x1b873593u, 0.0, perlinDeviation);
        uv += vec2(wx, wy) * warp;
    }

    float value = synthFbm(uv, frequency, octaves, gain, lacunarity, kind, s, mean, deviation);
    float other = 0.0;
    if (channels > 1) other = synthFbm(uv, frequency, octaves, gain, lacunarity, kind, s ^ 0xcc9e2d51u, mean, deviation);
    result = vec4(0.5 + value * SPREAD, 0.5 + other * SPREAD, 0.0, 1.0);
}
