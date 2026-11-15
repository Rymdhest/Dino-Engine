#ifndef DINO_WATER_SHORE_FOAM_GLSL
#define DINO_WATER_SHORE_FOAM_GLSL

// Foam, as the sea (Water_Ocean.frag) and the beach (Water/ShoreGround.glsl) both draw it, from one texture (WaterTextureBaker.BakeFoam): r the lace thinning
// foam breaks into, g bubbles, b patches, a streaks. A little foam shows only the strongest strands of the lace, more fills in round them, and a lot is a sheet
// of bubbles with holes in it where its patches thin. It has a body, a couple of centimetres of it, which the light falls across, so it reads as a froth and
// not as white paint. Read SHORE.md. Needs Water/ShoreData.glsl.

uniform sampler2D shoreFoam;

const float FOAM_RESOLUTION = 512.0;                // WaterTextureBaker.FoamResolution
const vec3 SHORE_FOAM_ALBEDO = vec3(0.8, 0.84, 0.86);
const float SHORE_FOAM_RELIEF = 0.045;              // Metres thick foam stands, for the light falling across it.

struct Foam
{
    float cover;    // 0..1 of the surface it hides
    float thick;    // 0..1, how much of a body it has: thin foam is a film the light goes through
    vec2 slope;     // d height / d xz of its own relief, in metres per metre
};

// The mip of the foam texture for a pixel spanning `pixelMetres` of the surface.
float shoreFoamLod(float pixelMetres)
{
    return max(log2(max(pixelMetres * FOAM_RESOLUTION * shoreFoamFlow.z, 1e-6)), 0.0);
}

// How much foam a texel holds, about 0..1: its lace and bubbles, gathered into patches. It's never drawn out into streaks along the way the water runs: that
// turns along a curved shore, and a pattern turned by it about the world's far-off origin is sheared into hatching.
float shoreFoamBody(vec4 t)
{
    // Bubbles weigh nearly as much as the lace, so what's left of thinning foam is clusters of them along its strands and never the bare crossings of its net.
    return t.r * 0.5 + t.g * 0.36 + (t.b - 0.5) * 0.5 + 0.1;
}

// `amount` of foam (0 none, 1 thick, a little more thicker still) at `at`, in metres in the foam's own frame: whoever calls says how that's moving.
Foam shoreFoamAt(float amount, vec2 at, float lod)
{
    Foam f = Foam(0.0, 0.0, vec2(0.0));
    if (amount <= 0.004) return f;
    // Thin foam is read softer, a mip or so coarser: its last few bubbles would otherwise be single pixels that sparkle as anything moves.
    lod += 1.2 * (1.0 - clamp(amount, 0.0, 1.0));
    vec2 uv = at * shoreFoamFlow.z;
    float body = shoreFoamBody(textureLod(shoreFoam, uv, lod));
    float threshold = 1.0 - clamp(amount, 0.0, 1.35);
    f.cover = smoothstep(threshold, threshold + 0.24, body) * clamp(amount * 5.0, 0.0, 1.0);
    if (f.cover <= 0.002) return f;
    f.thick = clamp((body - threshold) * 2.2, 0.0, 1.0) * clamp(amount, 0.0, 1.0);
    // Its relief, from two more looks a couple of texels over at this level: the size of its bubbles.
    float texel = 2.0 * exp2(lod) / FOAM_RESOLUTION;
    float right = shoreFoamBody(textureLod(shoreFoam, uv + vec2(texel, 0.0), lod));
    float ahead = shoreFoamBody(textureLod(shoreFoam, uv + vec2(0.0, texel), lod));
    f.slope = (vec2(right, ahead) - body) * (SHORE_FOAM_RELIEF * shoreFoamFlow.z / texel) * f.thick;
    return f;
}

// One layer of foam over another: what either covers.
Foam shoreFoamOver(Foam under, Foam over)
{
    Foam f;
    f.cover = 1.0 - (1.0 - under.cover) * (1.0 - over.cover);
    f.thick = max(under.thick, over.thick);
    float share = over.cover / max(over.cover + under.cover, 1e-4);
    f.slope = mix(under.slope, over.slope, share);
    return f;
}

// Turns a point into a frame whose u runs along `along` (unit), so the streaks, long along u, lie along it. Only for a fixed `along`: one that changes from
// place to place shears the pattern by as much as the world's origin is far off.
vec2 shoreFoamFrame(vec2 at, vec2 along)
{
    return vec2(dot(at, along), dot(at, vec2(-along.y, along.x)));
}

// Where a wave's foam pattern sits, of its own: a long way off in the texture, from the wave's own random numbers.
vec2 shoreFoamShift(float wave)
{
    return shoreWaveRandom(wave + 0.5 * 977.0).zw * 61.0;
}

#endif
