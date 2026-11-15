#ifndef DINO_WATER_SHORE_GROWTH_GLSL
#define DINO_WATER_SHORE_GROWTH_GLSL

// What the sea grows on the world's rock where it reaches it (SEA.md), in the bands every rocky shore has. Below the troughs of the waves, a coat of weed, thickest on what
// faces the sky, and in a warm sea a pink crust of coralline on what the weed leaves bare; from there up as high as the waves wash, barnacles in a pale crust, most on
// the faces that take the waves; and above them, as high as the spray is thrown, the black band of the lichen that lives on salt. A land of lakes has no tide and no salt:
// only a dark ring where the water stands and a film of green below it. On stone alone (Model.frag), so every stone set down in the water and the cliffs the rock skin
// clads have it, and nothing that lives there does. Needs Water/ShoreData.glsl (shoreLife: x how warm the sea is, y salt).

const vec3 GROWTH_WEED_COLD = vec3(0.075, 0.062, 0.026);   // Olive-brown: wrack and the holdfasts of kelp.
const vec3 GROWTH_WEED_WARM = vec3(0.05, 0.085, 0.032);    // Green turf.
const vec3 GROWTH_CORALLINE = vec3(0.34, 0.2, 0.22);       // Pink, of the warm seas.
const vec3 GROWTH_BARNACLES = vec3(0.42, 0.41, 0.37);
const vec3 GROWTH_LICHEN = vec3(0.028, 0.027, 0.024);      // Verrucaria: black on the rock above the barnacles.
const vec3 GROWTH_FRESH_FILM = vec3(0.06, 0.075, 0.03);

// Lays the sea's growth on a stone at `position`, `s` being the sea there (shoreWaterAt), on its colour both before the weather (`dry`) and after it (`albedo`), so the
// water that soaks it afterwards soaks the growth too.
void shoreGrowth(ShoreWater s, vec3 position, vec3 geometryNormal, inout vec3 dry, inout vec3 albedo, inout float roughness)
{
    // Deep under the sea shoreWaterAt has nothing to work out and leaves the field unread, which is where a reef is: read it here.
    ShoreField field = s.field.inside ? s.field : shoreFieldAt(position.xz);
    if (shoreLevel.y < 0.5 || !field.inside) return;
    // Only by the sea, within a few waves' run of its shoreline: a rock up a valley at the same height has never been wet with it.
    float bySea = 1.0 - smoothstep(6.0, 14.0, -field.distance);
    if (bySea <= 0.0) return;
    float h = s.height;
    float up = geometryNormal.y;
    float wave = max(s.coast.waveHeight, 0.12);
    vec2 xz = position.xz;
    float patches = textureLod(shoreNoise, xz * (1.0 / 3.1) + position.y * 0.05 + vec2(0.29, 0.61), 0.0).b;
    float fine = textureLod(shoreNoise, xz * (1.0 / 0.53) + position.y * 0.7 + vec2(0.71, 0.13), 0.0).a;

    if (shoreLife.y < 0.5)
    {
        // Fresh water: a dark ring at its level, and a film of green on what faces up below it.
        float ring = (1.0 - smoothstep(0.0, 0.18, abs(h + 0.02))) * 0.45 * bySea;
        float film = (1.0 - smoothstep(-0.25, -0.05, h)) * smoothstep(0.1, 0.7, up) * smoothstep(0.35, 0.6, patches) * 0.75 * bySea;
        dry = mix(dry * (1.0 - ring), GROWTH_FRESH_FILM, film);
        albedo = mix(albedo * (1.0 - ring), GROWTH_FRESH_FILM, film);
        return;
    }

    // Weed below the troughs, in patches, most on what faces the sky and none on an overhang.
    float below = 1.0 - smoothstep(-0.35 * wave, -0.06, h);
    float weed = below * smoothstep(-0.2, 0.6, up) * smoothstep(0.3, 0.55, patches + 0.15 * fine);
    // In a warm sea, coralline crusts what the weed leaves bare.
    float coralline = below * smoothstep(0.55, 0.85, shoreLife.x) * (1.0 - weed) * smoothstep(0.4, 0.6, fine);
    // Barnacles from the troughs up as high as the waves wash, thickest on the faces that take the waves rather than on the tops.
    float washed = smoothstep(-0.55 * wave, -0.25 * wave, h) * (1.0 - smoothstep(0.45 * wave, 0.85 * wave + 0.08, h));
    float barnacles = washed * smoothstep(0.35, 0.6, fine + 0.25 * patches) * (1.0 - 0.5 * max(up, 0.0));
    // The black band the spray keeps alive, above.
    float spray = smoothstep(0.35 * wave, 0.7 * wave, h) * (1.0 - smoothstep(1.4 * wave + 0.25, 2.1 * wave + 0.5, h)) * smoothstep(0.25, 0.5, patches);

    vec3 weedColour = mix(GROWTH_WEED_COLD, GROWTH_WEED_WARM, smoothstep(0.45, 0.8, shoreLife.x)) * (0.8 + 0.4 * fine);
    vec3 crust = GROWTH_CORALLINE * (0.85 + 0.3 * patches);
    vec3 shells = GROWTH_BARNACLES * (0.85 + 0.3 * fine);
    spray *= 0.85 * bySea;
    barnacles *= 0.8 * bySea;
    coralline *= 0.7 * bySea;
    weed *= 0.9 * bySea;
    dry = mix(mix(mix(mix(dry, GROWTH_LICHEN, spray), shells, barnacles), crust, coralline), weedColour, weed);
    albedo = mix(mix(mix(mix(albedo, GROWTH_LICHEN, spray), shells, barnacles), crust, coralline), weedColour, weed);
    roughness = mix(roughness, 0.9, barnacles);
}

#endif
