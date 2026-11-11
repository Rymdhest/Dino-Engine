#ifndef DINO_SNOW_TRAIL_GLSL
#define DINO_SNOW_TRAIL_GLSL

// What's been pressed into the snow around the camera (SnowTrailRenderer), for whatever draws snowy ground. Read SNOW.md.
// r how deep a print's floor goes, of SNOW_TRAIL_DEEPEST; g how much of the snow's own surface that floor replaces, 1 in a footprint and less in a churned furrow;
// b snow heaped round it, of SNOW_TRAIL_HIGHEST; a clumps of what was kicked up, lying on top.

uniform sampler2D snowTrail;
uniform vec4 snowTrailRegion;

const float SNOW_TRAIL_DEEPEST = 0.6;   // As SnowTrailRenderer.Deepest.
const float SNOW_TRAIL_HIGHEST = 0.25;  // As SnowTrailRenderer.Highest.
const float SNOW_TRAIL_FADE_FROM = 0.40;    // Of the region's width out from its centre, where prints start to fade, and are gone,
const float SNOW_TRAIL_FADE_TO = 0.47;      // before its edge, where the texture comes round again.
const float SNOW_TRAIL_NEAR_LOD = 4.0;  // As SnowTrailRenderer.Levels: 0.75 m a texel, fine enough that eight bits still keep a lone clump.
const float SNOW_LUMP_SIZE = 0.07;      // Metres across a lump of loose snow,
const float SNOW_CHUNK_SIZE = 0.19;     // and across a chunk of what a body churned up.

// Whether there's a trail to read at all here: anything pressed in since it was last emptied, this inside it, and anything near, at the pixel or at `reach`,
// the far end of what its ray can go through, at two looks.
bool snowTrailCovers(vec2 xz, vec2 reach)
{
    if (snowTrailRegion.w < 0.5) return false;
    vec2 off = abs(xz - snowTrailRegion.xy) / snowTrailRegion.z;
    if (max(off.x, off.y) >= SNOW_TRAIL_FADE_TO) return false;
    vec4 near = textureLod(snowTrail, xz / snowTrailRegion.z, SNOW_TRAIL_NEAR_LOD) + textureLod(snowTrail, reach / snowTrailRegion.z, SNOW_TRAIL_NEAR_LOD);
    return near.r + near.g + near.b + near.a > 0.0;
}

vec4 snowTrailAt(vec2 xz)
{
    vec2 off = abs(xz - snowTrailRegion.xy) / snowTrailRegion.z;
    float edge = 1.0 - smoothstep(SNOW_TRAIL_FADE_FROM, SNOW_TRAIL_FADE_TO, max(off.x, off.y));
    if (edge <= 0.0) return vec4(0.0);
    return textureLod(snowTrail, xz / snowTrailRegion.z, 0.0) * edge;
}

float snowTrailHash(vec2 p)
{
    p = fract(p * vec2(0.1031, 0.1030));
    p += dot(p, p + vec2(19.19, 19.19));
    p = fract(vec2(p.x * p.y, p.y * p.x) * 19.19);
    return fract(p.x + p.y);
}

float snowValueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(snowTrailHash(i), snowTrailHash(i + vec2(1.0, 0.0)), u.x), mix(snowTrailHash(i + vec2(0.0, 1.0)), snowTrailHash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Lumps of loose snow, 0..1, smooth, fixed to the ground.
float snowLumps(vec2 xz) { return snowValueNoise(xz / SNOW_LUMP_SIZE); }

// Chunks of churned snow, 0..1, with lumps on them.
float snowChunks(vec2 xz) { return 0.7 * snowValueNoise(xz / SNOW_CHUNK_SIZE + 31.7) + 0.3 * snowLumps(xz); }

#endif
