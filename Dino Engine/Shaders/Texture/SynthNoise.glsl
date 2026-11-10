#ifndef DINO_SYNTH_NOISE_GLSL
#define DINO_SYNTH_NOISE_GLSL

// Seamless noise for texture synthesis. Every lattice here is a whole number of cells across the tile, and every cell is hashed by
// where it falls once wrapped round, so the noise at u = 1 is the noise at u = 0, to the bit. Hashes are on integers: a float hash
// truncates its input and gives two neighbouring cells the same value whenever the seed is large. Needs SynthCommon.glsl.

// PCG3D, from Jarzynski and Olano, "Hash Functions for GPU Rendering", JCGT 9(3), 2020: as good as anything that cheap gets.
uvec3 synthPcg3(uvec3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v;
}

// Three numbers in [0, 1) for a cell, which must already be wrapped.
vec3 synthHash3(ivec2 cell, uint seed)
{
    return vec3(synthPcg3(uvec3(uvec2(cell), seed))) * (1.0 / 4294967296.0);
}

float synthHash1(ivec2 cell, uint seed)
{
    return synthHash3(cell, seed).x;
}

// A number in [0, 1) for anything that's already a number in [0, 1), as a cell's id: the same id always gives the same answer.
float synthHashValue(float value, uint seed)
{
    uint bits = uint(clamp(value, 0.0, 1.0) * 16777215.0);
    return float(synthPcg3(uvec3(bits, seed, bits ^ 0x9E3779B9u)).x) * (1.0 / 4294967296.0);
}

// Perlin's gradient noise (2002), with a unit gradient at a random angle on every lattice point and his quintic fade, over a lattice
// of `period` cells that wraps. Mean 0; its spread is measured, not assumed (NoiseStatistics in Canvas.cs).
float synthPerlin(vec2 p, ivec2 period, uint seed)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    ivec2 c = ivec2(i);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float a00 = 6.2831853 * synthHash1(wrapInt(c, period), seed);
    float a10 = 6.2831853 * synthHash1(wrapInt(c + ivec2(1, 0), period), seed);
    float a01 = 6.2831853 * synthHash1(wrapInt(c + ivec2(0, 1), period), seed);
    float a11 = 6.2831853 * synthHash1(wrapInt(c + ivec2(1, 1), period), seed);

    float n00 = dot(vec2(cos(a00), sin(a00)), f);
    float n10 = dot(vec2(cos(a10), sin(a10)), f - vec2(1.0, 0.0));
    float n01 = dot(vec2(cos(a01), sin(a01)), f - vec2(0.0, 1.0));
    float n11 = dot(vec2(cos(a11), sin(a11)), f - vec2(1.0, 1.0));
    return mix(mix(n00, n10, u.x), mix(n01, n11, u.x), u.y);
}

// Value noise: a random number in [-1, 1] on every lattice point, faded across the cell. Blockier than gradient noise, which is what
// some things are.
float synthValue(vec2 p, ivec2 period, uint seed)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    ivec2 c = ivec2(i);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float v00 = synthHash1(wrapInt(c, period), seed);
    float v10 = synthHash1(wrapInt(c + ivec2(1, 0), period), seed);
    float v01 = synthHash1(wrapInt(c + ivec2(0, 1), period), seed);
    float v11 = synthHash1(wrapInt(c + ivec2(1, 1), period), seed);
    return mix(mix(v00, v10, u.x), mix(v01, v11, u.x), u.y) * 2.0 - 1.0;
}

const int NOISE_PERLIN = 0;     // Soft hills and hollows.
const int NOISE_VALUE = 1;      // The same, blockier.
const int NOISE_RIDGED = 2;     // Sharp crests where gradient noise crosses zero: veins, ridges, cracks seen from above.
const int NOISE_BILLOW = 3;     // Round lumps with creases between: clouds, cushions, heaped earth.

// One octave of a kind, before it's scaled to a common spread.
float synthOctave(int kind, vec2 p, ivec2 period, uint seed)
{
    if (kind == NOISE_VALUE) return synthValue(p, period, seed);
    float n = synthPerlin(p, period, seed);
    if (kind == NOISE_RIDGED)
    {
        float ridge = 1.0 - abs(n) * 1.6;
        return ridge * ridge;
    }
    if (kind == NOISE_BILLOW) return abs(n);
    return n;
}

// Fractal sums of octaves, each `lacunarity` (a whole number, or it wouldn't tile) times as fine and `gain` times as strong as the
// last, standardised: `mean` and `deviation` are one octave's, so the sum comes out with mean 0 and deviation 1 whatever the kind
// and however many octaves, as the octaves are independent. Every octave's lattice is shifted, so that they don't all meet at 0.
// An octave finer than the texels can hold fades out before it gets there, as it would only alias into grit, and the mip chain would
// then average it away to a different mean than it had: what can't be seen isn't made.
float synthFbm(vec2 uv, ivec2 frequency, int octaves, float gain, int lacunarity, int kind, uint seed, float mean, float deviation)
{
    float sum = 0.0;
    float weights = 0.0;
    float weightsSquared = 0.0;
    float amplitude = 1.0;
    ivec2 period = max(frequency, ivec2(1));
    for (int octave = 0; octave < octaves; octave++)
    {
        // Cells per texel along the finer axis: a lattice needs a few texels to a cell to be drawn at all.
        float fineness = float(max(period.x, period.y)) / targetSize.x;
        float shown = amplitude * (1.0 - smoothstep(0.125, 0.25, fineness));
        if (shown <= 0.0) break;
        uint octaveSeed = seed + uint(octave) * 0x9E3779B9u;
        vec2 shift = synthHash3(ivec2(octave, 7), seed).xy * vec2(period);
        sum += shown * synthOctave(kind, uv * vec2(period) + shift, period, octaveSeed);
        weights += shown;
        weightsSquared += shown * shown;
        amplitude *= gain;
        period *= lacunarity;
    }
    if (weightsSquared <= 0.0) return 0.0;
    return (sum - mean * weights) / (deviation * sqrt(weightsSquared));
}

#endif
