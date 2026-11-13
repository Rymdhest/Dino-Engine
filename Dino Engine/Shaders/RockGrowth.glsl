#ifndef DINO_ROCK_GROWTH_GLSL
#define DINO_ROCK_GROWTH_GLSL

// What grows on the world's rock and what lies on it, and the few things that needs: which bed a point is in and whether it's soft, and the rock's own hash and
// noise. Rock.glsl paints moss and snow by these, and the moss that stands on stone as shells (SHELLS.md) grows by the same, so the painted moss and the moss
// standing on it can never be in different places. Needs Geology.glsl.

const int ROCK_KIND_GRANITE = 0;    // World/Geology.cs's RockKind, as Rock.glsl's ROCK_GRANITE.

uint rockMix(uint h)
{
    h ^= h >> 16u;
    h *= 0x7FEB352Du;
    h ^= h >> 15u;
    h *= 0x846CA68Bu;
    h ^= h >> 16u;
    return h;
}

// Two mixes are enough to tell neighbours apart, and every rock pixel takes dozens of these.
uint rockHash(int a, int b, int c)
{
    return rockMix(uint(a) * 0x8DA6B343u + rockMix(uint(b) * 0xD8163841u ^ uint(c) * 0xCB1AB31Fu ^ 0x165667B1u));
}

float rockUnit(uint h)
{
    return float(h & 0xFFFFu) * (1.0 / 65535.0);
}

// Smooth noise, 0..1, for where joints wander and where things gather.
float rockNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    float a = rockUnit(rockMix(uint(c.x) * 0x8DA6B343u ^ uint(c.y) * 0xD8163841u ^ 0x27D4EB2Fu));
    float b = rockUnit(rockMix(uint(c.x + 1) * 0x8DA6B343u ^ uint(c.y) * 0xD8163841u ^ 0x27D4EB2Fu));
    float d = rockUnit(rockMix(uint(c.x) * 0x8DA6B343u ^ uint(c.y + 1) * 0xD8163841u ^ 0x27D4EB2Fu));
    float e = rockUnit(rockMix(uint(c.x + 1) * 0x8DA6B343u ^ uint(c.y + 1) * 0xD8163841u ^ 0x27D4EB2Fu));
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y);
}

// The layer of the world's rock a point is in, as its colours are read, wandering as its partings do: a bed's edge goes in and out with what's come off it.
// Granite has no beds, only sheets peeling off it, which follow the land far more loosely than a sea floor did.
float rockLayerAt(vec3 p)
{
    int kind = int(geologyFace.z + 0.5);
    vec3 bedNormal = normalize(vec3(geologyDip.x, 1.0, geologyDip.y));
    vec3 strike = vec3(geologyFrame.x, 0.0, geologyFrame.y);
    vec3 along = normalize(strike - bedNormal * dot(strike, bedNormal));
    vec3 across = cross(bedNormal, along);
    float a = dot(p, along);
    float c = dot(p, across);
    float sheeted = kind == ROCK_KIND_GRANITE ? 2.4 : 0.5;
    return geologyLayerAt(p) + (rockNoise(vec2(a, c) * 0.29 + p.y * 0.07) - 0.5) * sheeted;
}

// 1 in a bed's soft band, which wears back under the hard one over it.
float rockSoftOf(float layer)
{
    return 1.0 - smoothstep(0.4, 0.7, geologyToneAt(layer).a);
}

// The middling patches rock weathers in, which moss gathers by.
float rockMiddling(vec3 position)
{
    return rockNoise(position.xz * 0.19 - position.y * 0.13 + 4.1);
}

struct RockGrowth {
    float moss;         // How much of the rock here is under moss, 0..1.
    float snow;         // How much under snow, 0..1, where a land has snowfields.
    float patches;      // Where moss and snow gather, 0..1.
    float drifts;       // How deep snow has drifted, 0..1.
};

// `up` is how much the surface faces the sky, its normal's y; `soft` whether it's a soft bed (rockSoftOf), whose ledges hold more.
// What grows on rock: in the open on whatever faces the sky, and thickest on a soft bed's ledge. Snow lies on whatever faces the sky where a land has snowfields,
// drifted into patches, thinnest on edges where the wind takes it and deepest on ledges.
RockGrowth rockGrowthAt(vec3 position, float up, float soft, float middling)
{
    RockGrowth g;
    g.patches = rockNoise(position.xz * 0.43 + position.y * 0.1) * 0.65 + middling * 0.35;
    g.moss = geologyMoss.w * smoothstep(0.25, 0.85, up) * smoothstep(0.62 - 0.35 * geologyMoss.w, 0.86, g.patches + soft * 0.12);
    g.snow = 0.0;
    g.drifts = 0.0;
    if (geologyFace.w > 0.0)
    {
        g.drifts = rockNoise(position.xz * 0.21 + position.y * 0.11 + 5.3) * 0.7 + g.patches * 0.3;
        g.snow = geologyFace.w * smoothstep(0.5, 0.86, up + (g.drifts - 0.5) * 0.4 + soft * 0.1);
    }
    return g;
}

#endif
