#ifndef DINO_WEATHER_WETNESS_GLSL
#define DINO_WEATHER_WETNESS_GLSL

// What weather does to surfaces, for every material, in the geometry pass. After Lagarde's "Water drop" series. Something that gets wet darkens, because water fills its pores and light
// that would have come back out is caught in there, and gets glossier, because the water smooths it: how much of each is how thirsty the material is. Metal and glass take in nothing and
// only carry a film. Past that water stands: first in the cracks of whatever's rough, then in the ground's hollows as puddles, flat and mirror-smooth, and rain rings run across all of it.
// Written before lighting, so that sun, sky, lamps and reflections all see the same wet surface. Needs Globals.glsl, Weather/Weather.glsl, Weather/SurfaceMap.glsl and Weather/RainShadow.glsl.

uniform sampler2D rainRipples;      // Rain rings (Rain_Ripples.frag): slopes on the xz plane.
uniform sampler2D dripSurface;      // What drips do to the ground (Drip_Surface.frag): xy slopes on the xz plane, z how wet they keep it.

const float BEAD_COLUMNS = 7.0;     // Paths for water to run down, to a metre of wall.
const float BEAD_SPACING = 1.3;     // Metres between one bead on a path and the next.
const float BEAD_SHARE = 0.4;       // How many of those paths have water on them.
const float BEAD_RUNOFF = 0.6;      // How strongly they show where only what runs down from above reaches.

vec2 rainRingsAt(vec2 xz)
{
    return textureLod(rainRipples, xz * wetLook.w, 0.0).xy;
}

vec3 dripSurfaceAt(vec2 xz)
{
    return textureLod(dripSurface, (xz - dripRegion.xy) / dripRegion.zw, 0.0).xyz;
}

// Beads of water making their way down something steep, each dragging a wet trail behind it. Water doesn't sheet down a wall in a shower (Lagarde, "Water drop 2b"): most of it stays where it lands, and
// what shows is the few beads that have grown heavy enough to go. Each path keeps to its own speed, slower on what drinks it, in whole half cycles a second so that the clock can wrap.
float weatherBead(vec3 position, vec3 normal, float thirst)
{
    float across = (abs(normal.x) > abs(normal.z) ? position.z : position.x) * BEAD_COLUMNS + 0.35 * sin(position.y * 1.9);
    float column = floor(across);
    uint seed = uint(int(column)) * 2654435761u;
    seed ^= seed >> 15u;
    seed *= 0x7feb352du;
    seed ^= seed >> 13u;
    if (float(seed >> 8u) * (1.0 / 16777216.0) > BEAD_SHARE) return 0.0;

    float speed = 0.5 * (1.0 + float(seed & 1u) + round((1.0 - thirst) * 2.0));
    float along = fract(position.y / BEAD_SPACING + dripLook.x * speed + float((seed >> 4u) & 255u) * (1.0 / 256.0));
    float side = (across - column - 0.5) * 2.0;
    float tail = exp(-along * 4.0) * 0.6 + exp(-along * 60.0) * 0.4;
    return exp(-side * side * 7.0) * tail;
}

// Geometric specular anti-aliasing (Tokuyoshi and Kaplanyan 2019), as the grass has. A smooth wet surface over a bumpy normal map shows a different highlight in every pixel, and they crawl as
// the view moves: where the normal turns a lot within a pixel, the highlight widens to what the pixel would average to. Call it in uniform control flow, as it takes derivatives.
float weatherFilterRoughness(float roughness, vec3 normal)
{
    vec3 normalDx = dFdx(normal);
    vec3 normalDy = dFdy(normal);
    float kernel = min(0.5 * (dot(normalDx, normalDx) + dot(normalDy, normalDy)), 0.18);
    float alpha = roughness * roughness;
    return sqrt(sqrt(min(alpha * alpha + kernel, 1.0)));
}

// Makes a surface wet, as far as the weather here has. `geometryNormal` is the surface's own, `normal` what the G-buffer will hold, both in world space. `hollow` is how much of a dip this
// is, 0..1, and `relief` how high the material stands at this point, 0..1: water finds the low places first.
void weatherWetten(vec3 worldPosition, vec3 geometryNormal, float porosity, float metallic, float hollow, float relief,
                   inout vec3 albedo, inout float roughness, inout vec3 normal)
{
    if (weatherKind.w < 0.5) return;

    vec4 here = weatherSurfaceAt(worldPosition.xz);
    if (here.x + here.y + here.z + here.w < 0.004) return;

    float reach = rainReach(worldPosition);
    // What's turned to the rain catches it; what's turned from it stays dry, however hard it comes down.
    float catching = smoothstep(-0.3, 0.3, dot(geometryNormal, -weatherFalling.xyz));
    float upward = smoothstep(0.55, 0.92, geometryNormal.y);

    // Water coming off what's above keeps the ground under an edge wet, even where no rain gets in.
    vec3 dripped = dripSurfaceAt(worldPosition.xz);
    float dripWet = min(dripped.z, 1.0) * upward;

    float wet = max(here.x * reach * catching, dripWet);
    float thirst = porosity * (1.0 - metallic);

    // Water in the pores: a dark, rich colour, and less scattered light, so a smoother look (Lagarde, "Water drop 3b").
    float soaked = mix(1.0, wetLook.x, thirst);
    albedo *= mix(1.0, soaked, wet);
    albedo = mix(albedo, albedo * sqrt(albedo) * 1.6, wet * thirst * 0.5);
    float smoothness = 1.0 - roughness;
    float wetSmoothness = mix(1.0, smoothness, mix(1.0, soaked, 0.5 * wet * wetLook.y));

    // A film over all of it, once it's had time to form: thirsty things drink it, and it stands on the ones that can't.
    float film = smoothstep(0.45, 1.0, wet) * (1.0 - 0.55 * thirst);
    wetSmoothness = max(wetSmoothness, mix(smoothness, 0.86, film * wetLook.y));

    // Water standing in what's low. Rough material holds it between its stones first.
    float standing = 0.0;
    if (upward > 0.0)
    {
        float level = here.y * wetLook.z * max(reach, dripWet);
        float low = max(hollow, (1.0 - relief) * 0.45 * step(0.05, level));
        standing = upward * smoothstep(0.0, 0.2, level * 1.2 - (1.0 - low));
        // Still water hides what's under it, and shows the sky.
        albedo *= mix(1.0, 0.72, standing);
        wetSmoothness = mix(wetSmoothness, 0.97, standing);
        normal = normalize(mix(normal, geometryNormal, standing));
    }

    // Rain rings, on whatever's wet enough to carry them, and the rings of drops coming off things, which go on after the rain.
    float carrying = max(standing, film * upward * 0.6);
    float ringing = carrying * here.w;
    vec2 slope = (dripped.xy * carrying) * rippleLook.x;
    if (ringing > 0.01) slope += rainRingsAt(worldPosition.xz) * (rippleLook.x * ringing);
    normal = normalize(normal + vec3(-slope.x, 0.0, -slope.y));

    // Beads, on what's too steep for water to stay on: they catch the sky where the wall around them can't. Some come down from what's above even on a wall the rain can't reach, or which walls have
    // them would depend on where the wind blows from, and this camera only sees the one side of anything. Leaves are cards, with no way down to run.
    float steep = 1.0 - smoothstep(0.3, 0.65, abs(geometryNormal.y));
    float running = steep * smoothstep(0.1, 0.6, here.x) * max(smoothstep(0.35, 0.85, wet), BEAD_RUNOFF);
    if (running > 0.01 && porosity != WEATHER_FOLIAGE_POROSITY)
    {
        // What's seen of one on plaster or wood is mostly the dark trail it soaks into them. On what can't drink, it's only the shine.
        float bead = min(weatherBead(worldPosition, geometryNormal, thirst) * running * 1.8, 1.0);
        wetSmoothness = mix(wetSmoothness, 0.9, bead);
        albedo *= mix(1.0, mix(0.85, 0.5, thirst), bead);
        normal = normalize(normal + vec3(0.0, bead * 0.5, 0.0));
    }

    roughness = 1.0 - wetSmoothness;

    // Snow settles on what faces up, and in what's low before what stands proud.
    float settled = here.z * reach * smoothstep(0.1, 0.65, geometryNormal.y);
    settled = smoothstep(0.0, 0.5, settled * (1.25 - 0.5 * relief));
    albedo = mix(albedo, vec3(0.82, 0.85, 0.9), settled);
    roughness = mix(roughness, 0.72, settled);
    normal = normalize(mix(normal, geometryNormal, settled * 0.7));
}

#endif
