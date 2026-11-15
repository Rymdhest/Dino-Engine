#ifndef DINO_WATER_SHORE_WET_GLSL
#define DINO_WATER_SHORE_WET_GLSL

// What the sea does to whatever it reaches, in the geometry pass, after Weather/Wetness.glsl has had its say, so that sun, sky, lamps and reflections all see
// the same wet beach (SHORE.md). The darker the ground holds water, the richer its colour; a film left by the water as it runs off is glassy and drains off the
// grains first, standing longest in the hollows of the sand, so the last of it lies in lines along the ripples; low down, where the beach's own water comes out,
// they stay full; the sheet running over it is a mirror with the sand's ripples drowned under it; and as the sea deepens over it, it goes over to the sea bed,
// rough, as the sea pass draws the wave's own surface over it and nothing of the ground's own shine should show through, by the same measure the sea fades in by
// (shoreDrowned), so there's no line where the sea ends. Needs Water/ShoreData.glsl and Weather/Wetness.glsl.

const float SHORE_SHEET_ROUGHNESS = 0.05;
const float SHORE_FILM_ROUGHNESS = 0.08;
const float SHORE_SOAKED_ROUGHNESS = 0.26;
const float SHORE_DAMP_ROUGHNESS = 0.62;
const float SHORE_UNDER_ROUGHNESS = 0.75;

struct ShoreWetness
{
    float wet;          // 0..1, how much water the ground holds
    float film;         // 0..1, a film of water standing on it
    float standing;     // 0..1, water standing in its hollows
};

// How wet the shore has left a point, `relief` being how high the material stands there (0 in its hollows, 1 at the mesh), and `pooling` how deep water lies
// there (Weather/Wetness.glsl's weatherPooling, or a half on what moves): a ripple's hollow holds the water longest, and some patches of a beach hold it longer than
// others, or the last of it lies in the same hollows of every tile, a lattice across the whole shore.
ShoreWetness shoreWetness(ShoreWater s, float relief, float pooling)
{
    ShoreWetness w = ShoreWetness(0.0, 0.0, 0.0);
    if (!s.near) return w;
    float hollow = clamp((1.0 - clamp(relief, 0.0, 1.0)) * mix(0.45, 1.45, pooling), 0.0, 1.0);
    float period = max(shoreShape.w, 0.5);
    // Water drains off the grains first and out of the hollows last.
    float filmTime = max(shoreWet.w, 0.1) * mix(0.6, 3.5, hollow * hollow);
    float film = exp(-s.sinceWet / filmTime);
    // Past the waves looked back over, nothing's known of when it was wet but what the damp says, which takes over before then.
    float soaked = exp(-s.sinceWet / max(shoreWet.z, 0.5)) * (1.0 - smoothstep(0.6, 1.0, s.sinceWet / (float(SHORE_LOOKBACK) * period)));
    // What the seas of the last few hours reached is damp, the more so the lower it lies, thinning out to the dry sand.
    float damp = 1.0 - smoothstep(SHORE_DAMP_FROM, SHORE_DAMP_TO, s.swash.need);
    damp *= damp;
    // And fine sand draws the sea up through it, however far the waves come: low on a beach it's damp, the more so the nearer the water it lies.
    float fringe = (1.0 - smoothstep(0.15, SHORE_FRINGE_TOP, s.height + 0.12 * s.swash.wander)) * (1.0 - smoothstep(SHORE_FRINGE_REACH * 0.3, SHORE_FRINGE_REACH, s.inland));
    damp = max(damp, fringe * fringe * 0.85);
    // Low on the beach its water comes out of the sand, and the ripples' hollows hold it.
    float seep = 1.0 - smoothstep(SHORE_SEEP * 0.55, SHORE_SEEP, s.swash.need);
    if (s.sinceWet <= 0.0) seep = 1.0;

    // The edge of a wave's reach, where its water was thinnest, takes less of it in, so the wet sand fades into the damp rather than ending in a line.
    soaked *= mix(0.6, 1.0, smoothstep(0.0, 0.35, s.swash.leftShare));
    w.wet = max(max(soaked, damp * 0.75), max(seep, s.cover));
    w.standing = max(seep * smoothstep(0.45, 0.8, hollow), film * smoothstep(0.55, 0.9, hollow));
    w.film = max(max(film, w.standing), max(seep * 0.55, s.cover));
    return w;
}

// Lays the shore's water on a surface: `dry` its colour before any water, the rest as Weather/Wetness.glsl took them. `pixelMetres` is how much a pixel spans,
// for the ripples the sheet drags across it.
void shoreWetten(ShoreWater s, ShoreWetness w, vec3 position, vec3 geometryNormal, float porosity, float metallic, vec3 dry, float pixelMetres,
                 inout vec3 albedo, inout float roughness, inout vec3 normal)
{
    if (!s.near || (w.wet <= 0.001 && w.film <= 0.001)) return;

    // Darker and richer for the water in its pores, as rain makes it, and never lighter than the rain left it. Under the shallowest of the sea it's still the
    // sheet, as the sea pass has hardly begun to draw anything over it there; the deeper the water, the more it's the sea bed.
    float drowned = shoreDrowned(s.under);
    float thirst = clamp(porosity, 0.0, 1.0) * (1.0 - metallic);
    float soak = w.wet * thirst;
    // A film over it darkens it further, as light that gets into the water under the film mostly stays there. The sea bed is only a little darker: the sea
    // pass darkens and tints what it sees through the water on the way down and back (WaterCommon.glsl), and pale sand under it is what makes the shallows glow.
    float darkening = mix(clamp(shoreWet.y * (1.0 + 0.3 * w.film), 0.0, 0.95), 0.15, drowned);
    vec3 soaked = dry * (1.0 - darkening * soak);
    soaked = mix(soaked, soaked * sqrt(max(soaked, vec3(0.0))) * 1.6, 0.8 * soak * (1.0 - drowned));
    albedo = min(albedo, soaked);

    // Satin when it's soaked, glassy under a film, a mirror where water stands or runs.
    float wetRoughness = mix(roughness, min(roughness, mix(SHORE_DAMP_ROUGHNESS, SHORE_SOAKED_ROUGHNESS, smoothstep(0.55, 0.95, w.wet))), smoothstep(0.1, 0.5, w.wet));
    wetRoughness = mix(wetRoughness, min(wetRoughness, SHORE_FILM_ROUGHNESS), w.film);
    wetRoughness = mix(wetRoughness, SHORE_SHEET_ROUGHNESS, max(w.standing, s.cover));

    // A film smooths over the finest of the relief, standing water and the sheet all of it.
    vec3 smoothed = normalize(mix(normal, geometryNormal, max(max(w.standing, s.cover * 0.88), 0.5 * w.film)));

    // The sheet drags shallow ripples up the beach and back with it.
    if (s.swash.cover > 0.001)
    {
        // Each layer is read turned off the world's axes and off the other: the pattern tiles, and read square to the world both layers repeated in a square
        // lattice, which the sun's highlight on the sheet picked out as a grid across the whole beach. Turned, they share no period.
        vec2 drag = position.xz + s.seaward * s.swash.sheetFlow;
        float lod = max(log2(max(pixelMetres * 512.0 / 1.9, 1e-6)), 0.0);
        vec2 ripple = textureLod(shoreNoise, mat2(0.6, 0.8, -0.8, 0.6) * drag / 1.9 + vec2(0.71, 0.23), lod).ba - 0.5;
        vec2 fine = textureLod(shoreNoise, mat2(0.92, -0.39, 0.39, 0.92) * drag / 0.83 + vec2(0.19, 0.57), lod + log2(1.9 / 0.83)).ba - 0.5;
        smoothed = normalize(smoothed + vec3(-ripple.x - fine.x * 0.6, 0.0, -ripple.y - fine.y * 0.6) * 0.22 * s.swash.cover);
    }
#ifndef WEATHER_NOTHING_STANDS
    // Rain rings, on whatever water stands or runs.
    if (weatherKind.w > 0.5)
    {
        float carrying = max(max(w.standing, s.cover), 0.6 * w.film) * weatherSurfaceAt(position.xz).w;
        if (carrying > 0.01)
        {
            vec3 rings = rainRingsAt(position.xz, position, geometryNormal);
            float strength = rippleLook.x * carrying;
            smoothed = normalize(smoothed + vec3(-rings.x, 0.0, -rings.y) * strength);
            float alpha = wetRoughness * wetRoughness;
            wetRoughness = sqrt(sqrt(alpha * alpha + rings.z * strength * strength));
        }
    }
#endif
    // Under the sea the sea pass draws the water's own surface, and the ground's shine would show through it.
    roughness = mix(wetRoughness, SHORE_UNDER_ROUGHNESS, drowned);
    normal = normalize(mix(smoothed, geometryNormal, drowned));
}

#endif
