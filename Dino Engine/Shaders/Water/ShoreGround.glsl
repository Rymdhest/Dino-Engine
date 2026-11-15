#ifndef DINO_WATER_SHORE_GROUND_GLSL
#define DINO_WATER_SHORE_GROUND_GLSL

// The beach, as the ground draws it (Terrain.frag): wet as Water/ShoreWet.glsl leaves it, the swash's foam, and the wrack (SHORE.md).
// The foam of the swash is the bore's, piled up at the front of the water as it climbs; it's left behind on the sheet as lace that thins as it bursts, carried up
// the beach and back with the water; and what's still there when the water runs off stays on the sand, most of it in a line where the water stopped, until it
// bursts too. Each wave's foam is a pattern of its own, so no two run up alike. Above the reach of the biggest waves lies the wrack of the highest seas: weed
// dark in clumps along a broken line, the odd pale stick, and shells. Wet sand glitters, as the film on each grain is a mirror of its own. Needs
// procedural/fastHash.glsl, Water/ShoreData.glsl, Water/ShoreFoam.glsl and Water/ShoreWet.glsl.

const vec3 SHORE_WEED_DARK = vec3(0.045, 0.04, 0.022);
const vec3 SHORE_WEED_LIGHT = vec3(0.11, 0.095, 0.05);
const vec3 SHORE_WEED_DRY = vec3(0.2, 0.18, 0.13);
const vec3 SHORE_STICK = vec3(0.4, 0.36, 0.3);
const vec3 SHORE_SHELL = vec3(0.46, 0.43, 0.38);

// Wet sand's glitter: a few tiny facets on fixed spots, each tipped its own way and smooth, which the light pass only lights while one is nearly a mirror
// between the sun and the eye, as snow's crystals are (Terrain.frag's SnowGlitter). So they twinkle as the camera moves, wherever the sun is, and go out as
// the film drains off the grains, and they never flicker in place.
const float SHORE_GLINT_CELL = 0.07;       // Metres of sand to a facet at most.
const float SHORE_GLINT_SHARE = 0.12;      // How many cells have one.
const float SHORE_GLINT_TILT = 0.95;       // How far one may be tipped, as a slope: grains lie every way, so some catch a sun low behind the camera.
const float SHORE_GLINT_ROUGHNESS = 0.16;

void shoreGlitter(vec2 xz, vec3 geometryNormal, float share, inout vec3 normal, inout float roughness)
{
    if (share < 0.05) return;
    vec2 cell = floor(xz / SHORE_GLINT_CELL);
    if (hash21(cell + 913.0) < 1.0 - SHORE_GLINT_SHARE) return;
    vec2 centre = (hash22(cell + 37.0) - 0.5) * 0.4;
    float facet = (1.0 - smoothstep(0.2, 0.3, length(fract(xz / SHORE_GLINT_CELL) - 0.5 - centre))) * share;
    if (facet <= 0.0) return;
    vec2 tip = hash22(cell + 71.0) * 2.0 - 1.0;
    normal = normalize(mix(normal, normalize(geometryNormal + vec3(tip.x, 0.0, tip.y) * SHORE_GLINT_TILT), facet));
    roughness = mix(roughness, SHORE_GLINT_ROUGHNESS, facet);
}

// The swash's foam at a point: the bore's at the front of the water, lace on the sheet behind it, and what the water left on the sand as it ran off.
Foam shoreSwashFoam(ShoreWater s, vec2 xz, float lod)
{
    Swash w = s.swash;
    // Under the sea the foam is the sea's, on its own surface: any of the ground's would show through the clear water as a ghost of it. It hands over as the sea
    // pass fades in, so the foam of the one thins out where the other's comes in, and neither ends in a line.
    float surf = clamp(s.coast.waveHeight / max(shoreWave.x, 1e-3), 0.0, 1.0) * max(shoreWet.x, 0.0) * (1.0 - shoreDrowned(s.under));
    // On the sheet, carried up the beach and back with the water: the bore's front, heaped in lumps along its length and thinner as it runs back, and the lace
    // left behind it.
    float front = 0.0;
    if (w.edge > 0.001 && surf > 0.0)
    {
        float lumps = textureLod(shoreNoise, xz * (1.0 / 3.7) + vec2(0.83, 0.47), 0.0).a;
        front = w.edge * mix(0.45, 1.1, w.climbing) * (0.5 + 0.8 * lumps);
    }
    float lace = w.cover * exp(-w.coveredFor / SHORE_SHEET_FOAM) * 0.85;
    vec2 onSheet = xz + s.seaward * w.sheetFlow + shoreFoamShift(w.sheetWave);
    Foam f = shoreFoamAt(max(front, lace) * surf, onSheet, lod);
    // On the sand, where the water left it.
    if (w.foamLeft > 0.0 && w.cover < 0.999)
    {
        float left = w.foamLeft * exp(-w.sinceWet / SHORE_FOAM_LEFT) * (1.0 - w.cover) * 0.95;
        vec2 onSand = xz + s.seaward * w.leftFlow + shoreFoamShift(w.leftWave);
        f = shoreFoamOver(f, shoreFoamAt(left * surf, onSand, lod));
    }
    return f;
}

// Lays the beach over the ground at `position` (its x and z where the ground was hit, and y its mesh), after the weather: `dry` its colour before any water,
// `geometryNormal` the mesh's, `relief` how high its material stands there (0..1), `pixelMetres` how much a pixel spans.
void shoreGround(vec3 position, vec3 geometryNormal, float porosity, float metallic, float relief, vec3 dry, float pixelMetres,
                 inout vec3 albedo, inout float roughness, inout vec3 normal, inout float ambient)
{
    float height = position.y - shoreLevel.x;
    float slope = sqrt(max(1.0 - geometryNormal.y * geometryNormal.y, 0.0)) / max(geometryNormal.y, 0.05);
    ShoreWater s = shoreWaterAt(position.xz, height, slope, pixelMetres);
    // --render shoredebug=7: the water running over the bed under the waves, which sways what grows there (Wind_Vegetation.frag's shoreSurgeBend): red shorewards,
    // blue seawards, full at 0.6 m/s.
    if (shoreLevel.w > 6.5)
    {
        ShoreField f = shoreFieldAt(position.xz);
        if (!f.inside || height > 0.5) return;
        vec2 way;
        float column;
        float speed = shoreSurgeAt(f, -height, coastAt(position.xz, f.openness), way, column);
        albedo = vec3(max(speed, 0.0), 0.05, max(-speed, 0.0)) / 0.6 * 0.6;
        roughness = 0.95;
        normal = geometryNormal;
        return;
    }
    // --render shoredebug=4: metres inland (red ramps each metre), height (green ramps each quarter metre), and the mean wave's need (blue, of 3), everywhere.
    if (shoreLevel.w > 3.5 && shoreLevel.w < 4.5)
    {
        ShoreField f = shoreFieldAt(position.xz);
        if (!f.inside) return;
        vec2 meanRunUp = shoreRunUp(coastAt(position.xz, f.openness).waveHeight * (1.0 - 0.5 * shoreShape.y));
        albedo = vec3(fract(max(-f.distance, 0.0)), fract(height * 4.0), clamp(shoreNeed(max(-f.distance, 0.0), height, meanRunUp) / 3.0, 0.0, 1.0)) * 0.6;
        roughness = 0.95;
        normal = geometryNormal;
        return;
    }
    if (!s.near) return;

    ShoreWetness w = shoreWetness(s, relief, weatherPooling(position.xz));
    shoreWetten(s, w, position, geometryNormal, porosity, metallic, dry, pixelMetres, albedo, roughness, normal);
    float lod = shoreFoamLod(pixelMetres);

    // The water's own ripples focus the light onto the sand under it, in a net that moves with the water (the caustics the sea's bottom has, WaterCommon.glsl),
    // giving way to the sea's own as the sea pass fades in over it.
    float causticShare = s.cover * (1.0 - shoreDrowned(s.under)) * smoothstep(0.003, 0.02, max(s.swash.sheet, s.under));
    if (causticShare > 0.01)
    {
        vec2 drag = position.xz + s.seaward * s.swash.sheetFlow;
        float causticLod = max(log2(max(pixelMetres * 512.0 / 2.7, 1e-6)), 0.0);
        // Each net turned off the world's axes and off the other, so the two never repeat together in a lattice (as the sheet's ripples, Water/ShoreWet.glsl).
        float focused = min(textureLod(shoreNoise, mat2(0.8, 0.6, -0.6, 0.8) * drag / 2.7 + vec2(0.11, 0.43), causticLod).g,
                            textureLod(shoreNoise, mat2(0.39, -0.92, 0.92, 0.39) * drag / 1.9 + vec2(0.67, 0.29), causticLod + log2(2.7 / 1.9)).g) * 3.0;
        albedo *= max(1.0 + 0.35 * (focused / max(shoreFoamFlow.w, 0.05) - 1.0) * causticShare, 0.0);
    }

    // Freshly wet sand glitters, never under the water itself, and less as it drains.
    shoreGlitter(position.xz, geometryNormal, max(w.film * 0.9, smoothstep(0.6, 1.0, w.wet) * 0.35) * (1.0 - s.cover), normal, roughness);

    // The wrack: clumps of weed strewn along a broken, wandering line at the top of the beach where only the highest seas reach, thickest at the line and
    // thinning out below it, with sticks and shells among them. It's laid out in metres up the beach (a metre's share of the waves' reach, which the swash's
    // own edge is softened by), so it's as wide on a steep beach as on a flat one: laid out as a share of the reach alone, it was a dashed line a few
    // centimetres wide wherever the beach was steep.
    vec2 meanRunUp = shoreRunUp(s.coast.waveHeight * (1.0 - 0.5 * shoreShape.y));
    float perMetre = max(1.0 / max(meanRunUp.x, 0.2), slope / max(meanRunUp.y, 0.02));
    float aboveLine = (s.swash.need - SHORE_WRACK) / perMetre;
    // Only on the beach itself: deep under the sea nothing about the swash is worked out, and its need stands at a marker that falls among the wrack.
    if (s.height > 0.0 && s.under <= 0.0 && s.coast.waveHeight > 1e-4 && aboveLine > -3.0 && aboveLine < 1.5)
    {
        aboveLine += (textureLod(shoreNoise, position.xz * (1.0 / 13.0) + vec2(0.53, 0.17), 0.0).b - 0.5) * 1.8;
        float band = smoothstep(-1.8, -0.25, aboveLine) * (1.0 - smoothstep(0.0, 0.4, aboveLine)) * smoothstep(1.2, 1.6, s.swash.need);
        if (band > 0.01)
        {
            float broken = smoothstep(0.3, 0.6, textureLod(shoreNoise, position.xz * (1.0 / 7.3) + vec2(0.21, 0.83), 0.0).a);
            // Clumps a hand to a stride across, where a smooth noise stands highest: more of them where the line is thickest, none in its gaps.
            float lumps = textureLod(shoreNoise, position.xz * (1.0 / 2.2) + vec2(0.31, 0.77), 0.0).b;
            float cut = 0.97 - 0.36 * band * broken;
            float weed = smoothstep(cut - 0.04, cut + 0.03, lumps);
            float heap = clamp((lumps - cut) * 8.0, 0.0, 1.0);
            vec4 fronds = textureLod(shoreFoam, position.xz * shoreFoamFlow.z * 1.7 + vec2(0.31, 0.77), lod + log2(1.7));
            // Sticks lie every which way, so two sets of them at an angle; streaks turned by anything that changes from place to place would shear.
            vec4 along = textureLod(shoreFoam, shoreFoamFrame(position.xz, vec2(0.8, 0.6)) * shoreFoamFlow.z * 3.1 + vec2(0.67, 0.11), lod + log2(3.1));
            vec4 across = textureLod(shoreFoam, shoreFoamFrame(position.xz, vec2(-0.28, 0.96)) * shoreFoamFlow.z * 2.9 + vec2(0.13, 0.59), lod + log2(2.9));
            float stick = band * broken * smoothstep(0.89, 0.94, max(along.a, across.a)) * (1.0 - weed);
            float shell = band * 0.7 * smoothstep(0.88, 0.95, fronds.r * 0.4 + along.g * 0.75) * (1.0 - weed);
            // Darkest where it's heaped, its fronds paler at the edges, and the oldest of it, highest up, dried pale.
            vec3 weedColour = mix(SHORE_WEED_LIGHT, SHORE_WEED_DARK, clamp(heap * 0.7 + fronds.r * 0.4, 0.0, 1.0));
            weedColour = mix(weedColour, SHORE_WEED_DRY, smoothstep(-0.2, 0.35, aboveLine) * 0.6);
            albedo = mix(albedo, weedColour, weed * 0.94);
            albedo = mix(albedo, SHORE_STICK, stick * 0.85);
            albedo = mix(albedo, SHORE_SHELL, shell * 0.8);
            // Weed lies in heaps, damp still, and casts its hollows into shadow.
            roughness = mix(roughness, 0.5, weed);
            ambient *= 1.0 - 0.3 * weed * (1.0 - 0.5 * heap);
            normal = normalize(normal + vec3(fronds.g - 0.5, 0.0, fronds.b - 0.5) * 0.6 * weed);
        }
    }

    // The swash's foam, over everything but a wave, which the sea pass draws with its own.
    Foam foam = shoreSwashFoam(s, position.xz, lod);
    if (foam.cover > 0.001)
    {
        float lit = 0.84 + 0.16 * foam.thick;
        // --render shoredebug=5 draws it magenta, and the sea's green.
        albedo = mix(albedo, shoreLevel.w > 4.5 && shoreLevel.w < 5.5 ? vec3(0.7, 0.0, 0.7) : SHORE_FOAM_ALBEDO * lit, foam.cover);
        roughness = mix(roughness, 0.42, foam.cover);
        normal = normalize(mix(normal, normalize(geometryNormal + vec3(-foam.slope.x, 0.0, -foam.slope.y)), foam.cover));
        ambient *= mix(1.0, 0.82 + 0.18 * foam.thick, foam.cover);
    }

    // --render shoredebug=N (LightingSettings.ShoreDebug); 5 is the sea's (Water_Ocean.frag).
    if (shoreLevel.w > 0.5 && shoreLevel.w < 4.5)
    {
        albedo = shoreLevel.w < 1.5 ? vec3(w.wet, w.film, s.cover) * 0.6
            : shoreLevel.w < 2.5 ? vec3(fract(s.swash.need), step(1.0, s.swash.need) * 0.4, clamp(s.sinceWet / 40.0, 0.0, 1.0)) * 0.6
            : vec3(s.coast.waveHeight / max(shoreWave.x * WATER_ALONGSHORE_MAX, 1e-3), s.field.openness, clamp(-s.field.distance / 10.0, 0.0, 1.0)) * 0.6;
        roughness = 0.95;
        normal = geometryNormal;
    }
}

#endif
