#ifndef DINO_GRASS_FIELD_GLSL
#define DINO_GRASS_FIELD_GLSL

// The grass field, shared by the grass shaders and Terrain.frag: its settings, where grass grows, how the top of the field
// rolls, and what colour it is. Terrain.frag tints the ground under the grass with grassGroundTint(), so the ground and the
// blades get their colour from the same functions here.

// Written each frame by GrassRenderer.UploadSettings (binding GrassRenderer.UniformBinding) from GrassSingleton; the
// layout must match it.
layout(std140, binding = 7) uniform GrassData {
    vec4 grassField;       // x blades per m², y draw distance (m), z thinning start (m), w min density (0..1)
    vec4 grassLod;         // x LOD distance (m), y shadow distance (m), z shadow density (0..1), w view-space thickening
    vec4 grassBlade;       // x length (m), y length variation, z root width (m), w normal roundness
    vec4 grassShape;       // x tilt (rad), y tilt variation (rad), z droop (rad), w unused
    vec4 grassClump;       // x tuft spacing (m), y pull to centre, z tuft height variation, w splay
    vec4 grassRootColour;  // rgb, w colour variation
    vec4 grassBodyColour;  // rgb, w dry patches
    vec4 grassTipColour;   // rgb, w translucency
    vec4 grassDryColour;   // rgb, w roughness
    vec4 grassGround;      // x ground tint (0 when grass is off), y bloom amount, zw unused
    vec4 grassRoll;        // x swell (0..1), y 1 / swell size (1/m), z comb (0..1), w 1 / comb size (1/m)
    vec4 grassWave;        // x wind gain, y wave strength (rad), z 1 / wave length (1/m), w wind sheen (0..1)
    vec4 grassPaint;       // x hue drift (0..1), y canopy shade (0..1), z field normal (0..1), w bloom size (m)
    vec4 grassPlume;       // rgb, w plume amount
};

uint grassHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

uint grassHash(ivec2 p, uint salt)
{
    return grassHash(uint(p.x) ^ grassHash(uint(p.y) ^ salt));
}

// Next number of a hash chain, 0..1.
float grassRandom(inout uint state)
{
    state = grassHash(state);
    return float(state >> 8u) * (1.0 / 16777216.0);
}

// Smooth value noise, 0..1.
float grassValueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    const uint salt = 0x2c1b3c6du;
    float a = float(grassHash(c, salt) >> 8u);
    float b = float(grassHash(c + ivec2(1, 0), salt) >> 8u);
    float d = float(grassHash(c + ivec2(0, 1), salt) >> 8u);
    float e = float(grassHash(c + ivec2(1, 1), salt) >> 8u);
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y) * (1.0 / 16777216.0);
}

// How well grass grows, 0..1: the terrain's grass map, less on slopes (normalY is the ground normal's y) and none on
// roads (the road weight from the height map).
float grassFertility(float grassMap, float normalY, float road)
{
    float steepness = 1.0 - normalY;
    return max((1.0 - steepness * 4.0) * grassMap * (1.0 - steepness) * (1.0 - min(road * 2.0, 1.0)), 0.0);
}

// How far the top of the field rises or sinks here, -1..1: long swells with shorter ones on them, so even flat ground
// rolls. What the eye reads as a field's shape from above is this, and the light lying across it.
float grassSwell(vec2 xz)
{
    vec2 p = xz * grassRoll.y;
    return (grassValueNoise(p) * 0.62 + grassValueNoise(p * 2.3 + 17.0) * 0.38) * 2.0 - 1.0;
}

// The way the field is combed here, radians on the xz plane: the prevailing wind, wandering in broad swirls. Long grass
// lies the way the weather has laid it, like the nap of velvet.
float grassCombAngle(vec2 xz, vec2 prevailing)
{
    vec2 p = xz * grassRoll.w;
    float swirl = (grassValueNoise(p + 41.0) - 0.5) * 3.6 + (grassValueNoise(p * 2.7 + 7.0) - 0.5) * 1.3;
    return atan(prevailing.y, prevailing.x + 1e-5) + swirl;
}

// 0 = lush, 1 = straw. Dry patches lie across the field at a scale of tens of metres, and grass that barely grows is dry.
// groundDryness is how dry the biome is here, from the terrain's grass map.
float grassDryness(vec2 xz, float fertility, float groundDryness)
{
    float patches = grassValueNoise(xz * (1.0 / 23.0)) * 0.65 + grassValueNoise(xz * (1.0 / 6.5)) * 0.35;
    float share = clamp(grassBodyColour.w + groundDryness, 0.0, 1.0);
    float threshold = 1.0 - share;
    float dry = share > 0.0 ? smoothstep(threshold - 0.06, threshold + 0.1, patches) : 0.0;
    return max(dry, 0.6 * (1.0 - smoothstep(0.03, 0.3, fertility)));
}

// A colour as a painter would mix it across a field: broad masses drifting from deep cool blue-greens to light warm
// yellow-greens, instead of one green shaded lighter and darker.
vec3 grassPainted(vec3 colour, vec2 xz)
{
    float drift = grassValueNoise(xz * (1.0 / 41.0)) * 0.6 + grassValueNoise(xz * (1.0 / 15.0) + 29.0) * 0.4;
    float warmth = smoothstep(0.3, 0.7, drift);
    vec3 shifted = colour * mix(vec3(0.56, 0.8, 1.02), vec3(1.5, 1.24, 0.68), warmth);
    return mix(colour, shifted, grassPaint.x);
}

// The shaded base of a blade of the given colour, leaning towards the field's root colour.
vec3 grassRootOf(vec3 bodyColour)
{
    return mix(grassRootColour.rgb, bodyColour * 0.3, 0.75);
}

// The sunlit tip of a blade of the given colour: lighter, and warmer than the body it grew from. Seen from above tips are
// most of what shows, so they keep the blade's own hue; bleach them far and the whole field goes pale.
vec3 grassTipOf(vec3 bodyColour)
{
    return mix(grassTipColour.rgb, bodyColour * vec3(2.1, 1.85, 1.0), 0.72);
}

// What a patch of blades averages to, seen from a distance: mostly body, some bleached tip.
vec3 grassFieldColour(vec2 xz, float fertility, vec3 bodyColour, float groundDryness)
{
    vec3 body = mix(grassPainted(bodyColour, xz), grassDryColour.rgb, grassDryness(xz, fertility, groundDryness));
    return mix(body, grassTipOf(body), 0.3);
}

// The ground under the grass takes on the field's colour (as in Ghost of Tsushima), so the gaps between blades read
// as more grass: the shade under the blades where they stand thick, the colour of the blades where the field thins
// away towards the draw distance.
vec3 grassGroundTint(vec3 ground, vec2 xz, float fertility, float viewDistance, vec3 bodyColour, float groundDryness)
{
    float strength = grassGround.x * smoothstep(0.02, 0.3, fertility);
    if (strength <= 0.0) return ground;
    vec3 field = grassFieldColour(xz, fertility, bodyColour, groundDryness);
    float far = smoothstep(grassField.y * 0.5, grassField.y, viewDistance);
    vec3 under = mix(mix(grassRootOf(bodyColour), field, 0.15), field, far);
    return mix(ground, under, strength);
}

#endif
