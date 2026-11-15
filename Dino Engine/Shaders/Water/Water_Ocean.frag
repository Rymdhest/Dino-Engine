#version 420

// The sea. The quads sit at the top of the slab the waves stay within; each pixel walks its view ray down to the moving surface (a few fixed-point steps,
// exact enough because a camera looking down sees the surface steeply), so no dense mesh is needed and the waterline moves up and down the beach by itself.
//
// Near the coast the waves are Water/ShoreData.glsl's, the same the ground reads: they run in towards the nearest land, shoal, break, and the bore runs on up
// the beach as the swash. The swash, the wet sand it leaves and the foam on it are the ground's to draw (Water/ShoreGround.glsl), as the beach is ground:
// lit as the ground is, reflecting what's round it, and where the ground comes before the sea this pass leaves it be. Out at sea a wind-raised swell takes
// over. Wind ripples, ripples from things wading through it, refraction, absorption, scattering, reflections and caustics come from WaterCommon.glsl, and
// the foam is Water/ShoreFoam.glsl's, in layers: the white water a breaking crest spills down its face runs in with the wave, what it leaves behind stays
// where it fell, and what lingers between the waves drifts with the wind. Read SHORE.md.

#include Globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Wind.glsl
#include Water/WaterCommon.glsl
#include Water/ShoreData.glsl
#include Water/ShoreFoam.glsl

in vec3 fragWorldPosition;

layout (location = 0) out vec4 out_Colour;

// The surface lies further along every view ray than the quad, so depth only grows; declaring it keeps early depth
// rejection against the ground working although gl_FragDepth is written.
layout (depth_greater) out float gl_FragDepth;

uniform vec2 oceanSlab;
uniform vec4 swellAmplitudes[2];
uniform vec4 swellPhases[2];
uniform vec2 whitecaps;

// oceanSlab:   x highest, y lowest world y the surface can reach.
// whitecaps:   x crest height above which whitecaps show (swell standard deviations), y 1 / swell standard deviation (1/m).

const vec3 SURF_TURBIDITY = vec3(0.8, 0.75, 0.6);   // Scattering per metre of sand stirred up by breaking waves.
const vec3 SEABED_ALBEDO = vec3(0.26, 0.23, 0.17);  // Wet sand, where the bottom isn't loaded.
const int SURFACE_STEPS = 4;

// The open-sea swell. Must match WaterWaves.SwellDirections / SwellWavelengths (directions as (sin, cos) of the angle).
const int SWELL_COUNT = 8;
const vec2 SWELL_DIRECTIONS[8] = vec2[](
    vec2(0.0, 1.0), vec2(0.7313537, 0.6819984), vec2(0.9993908, 0.0348995), vec2(0.6946584, -0.7193398),
    vec2(0.0348995, -0.9993908), vec2(-0.6819984, -0.7313537), vec2(-0.9998477, 0.0174524), vec2(-0.6946584, 0.7193398));
const float SWELL_WAVENUMBERS[8] = float[](
    0.2416610, 0.3695991, 0.1903996, 0.5235988, 0.2991993, 0.4188790, 0.2166628, 0.6283185);

// ---------------------------------------------------------------- a point of the sea

struct SeaPoint
{
    ShoreField field;
    float depth;        // metres of water down to the ground's mesh, or a long way where none is known
    Coast coast;
};

// Everything about the coast at one point of the sea. A pixel's view ray moves a metre or two while it looks for the surface, and each point it looks at is
// asked about itself, so the surface is the world's and not the pixel's: taken once a pixel from where its quad was, a crest swam as the camera moved.
SeaPoint seaAt(vec2 xz)
{
    SeaPoint p;
    p.field = shoreFieldAt(xz);
    float ground;
    p.depth = waterTerrainHeight(xz, ground) ? shoreLevel.x - ground : 1e4;
    p.coast = coastAt(xz, p.field.openness);
    return p;
}

// ---------------------------------------------------------------- waves

struct OceanWave
{
    float elevation;    // metres above sea level
    vec2 slope;         // d elevation / d xz
    ShoreWave shore;    // the waves running in to the coast
    float whitecaps;    // amount of whitecap foam on the swell
};

// The surface at xz. withSwell = false leaves out the open-sea swell: it has faded out wherever the surface meets the ground, so finding where a view ray
// meets the surface doesn't need it.
OceanWave oceanWave(vec2 xz, SeaPoint p, bool withSwell)
{
    OceanWave wave;
    wave.shore = shoreWaveAt(p.field, p.depth, p.coast);
    wave.elevation = wave.shore.elevation;
    wave.slope = wave.shore.slope;
    wave.whitecaps = 0.0;

    // Out at sea the wind's swell; it gives way to the shore waves as the water shoals, and calms in sheltered water.
    float swellFade = p.depth > 1e3 ? 1.0
        : smoothstep(0.0, max(3.0 * shoreWave.x / WATER_BREAKING_RATIO, 0.5), p.depth) * p.coast.shelter;
    if (withSwell && swellFade > 0.0)
    {
        float elevation = 0.0;
        vec2 slope = vec2(0.0);
        for (int i = 0; i < SWELL_COUNT; i++)
        {
            float amplitude = swellAmplitudes[i / 4][i % 4];
            if (amplitude <= 0.0) continue;
            float phase = SWELL_WAVENUMBERS[i] * dot(SWELL_DIRECTIONS[i], xz) - SHORE_TAU * swellPhases[i / 4][i % 4];
            elevation += amplitude * sin(phase);
            slope += (amplitude * SWELL_WAVENUMBERS[i] * cos(phase)) * SWELL_DIRECTIONS[i];
        }
        wave.elevation += elevation * swellFade;
        wave.slope += slope * swellFade;
        // Whitecaps on the highest crests, covering the fraction of the sea the wind calls for.
        wave.whitecaps = smoothstep(whitecaps.x, whitecaps.x + 0.8, elevation * whitecaps.y) * swellFade;
    }
    return wave;
}

// ---------------------------------------------------------------- foam

// The foam on the sea at a point: the white water a breaking crest spills down its own face, which runs in with it; what it leaves behind, which stays where
// it fell, a pattern of its own for every wave; and what lingers in the surf and the whitecaps out at sea, which drift with the wind. `shown` is how much of
// the water here is the sea's to draw: towards the beach its foam thins out into lace as the swash's, which the ground draws, comes in
// (Water/ShoreGround.glsl), as a ring of it where the water met the sand was a line round every shore. None of it is drawn out into streaks: the way the
// water runs turns along a curved shore, and a pattern turned by it about the world's far-off origin was sheared into hatching.
Foam seaFoam(vec2 xz, SeaPoint p, OceanWave wave, float shown, float lod)
{
    ShoreWave s = wave.shore;
    // Small waves in sheltered water churn up less white water than the open coast's breakers.
    float multiplier = max(shoreWet.x, 0.0) * shown * mix(0.35, 1.0, clamp(p.coast.waveHeight / max(shoreWave.x, 1e-3), 0.0, 1.0));
    float gradient = length(p.field.gradient);
    vec2 seaward = gradient > 1e-4 ? p.field.gradient / gradient : vec2(0.0, 1.0);
    float wavelength = 1.0 / max(shoreWave.y, 1e-3);

    // Which wave's face or crest this is, and how far through it: 0 where its face starts, s.front at its crest.
    float faceWave = floor(s.psi + s.front);
    float through = s.psi + s.front - faceWave;
    float behind = max(through - s.front, 0.0);
    // Everything about a crest runs in with its wave, carried a wavelength towards the shore over each wave's cycle, from the foot of its face, where it
    // starts as a pattern of its own and there's none of it to see. Carried from anywhere farther off, it was sheared wherever the shore curves, by as far
    // as the crest was from the shore, as the way to the sea turns with it.
    vec2 onCrest = xz + seaward * (through * wavelength) + shoreFoamShift(faceWave);
    // The roller: white water from the crest part way down the face, reaching further down in some places than others and heaped in lumps along its length,
    // thinning out fast behind the crest. Taken at the crest alone it was a white line a few centimetres wide drawn along every wave.
    vec2 rough = textureLod(shoreNoise, onCrest * (1.0 / 3.1) + vec2(0.41, 0.07), 0.0).ba;
    float toe = 0.75 - 0.5 * rough.x;
    float roller = through < s.front ? smoothstep(toe - 0.25, toe + 0.15, through / s.front) : exp(-behind * 12.0);
    // And a crest breaks in sections along its length, each wave in sections of its own, some of it white and some hardly at all: breaking all along the shore
    // at once, a steep shore, where the water deepens within a stride and the surf is a narrow strip, was ringed in a white line.
    float sections = smoothstep(0.3, 0.7, textureLod(shoreNoise, onCrest * (1.0 / 19.0) + vec2(0.17, 0.53), 0.0).a);
    float crestAmount = s.breaking * s.coherence * roller * (0.5 + 0.9 * rough.y) * (0.2 + 0.8 * sections);
    Foam foam = shoreFoamAt(crestAmount * multiplier, onCrest, lod);

    // Left behind, where it fell, by the wave that has just passed, taking over from the roller as it thins and bursting in turn, in sections as the roller's.
    vec2 onTrail = xz + shoreFoamShift(faceWave + 0.5) * 0.7;
    float trailSections = smoothstep(0.3, 0.7, textureLod(shoreNoise, onTrail * (1.0 / 19.0) + vec2(0.71, 0.29), 0.0).a);
    float trailAmount = s.breaking * s.coherence * (exp(-behind * 4.5) - exp(-behind * 12.0)) * 1.3 * (0.2 + 0.8 * trailSections);
    if (through < s.front) trailAmount = 0.0;
    foam = shoreFoamOver(foam, shoreFoamAt(trailAmount * multiplier, onTrail, lod));

    // What lingers in the surf, and the whitecaps: drifting with the wind.
    float lingering = s.breaking * 0.16 + wave.whitecaps;
    foam = shoreFoamOver(foam, shoreFoamAt(lingering * multiplier, xz + shoreFoamFlow.xy, lod));
    return foam;
}

// ---------------------------------------------------------------- main

float windowDepth(vec3 world)
{
    vec4 clip = projectionMatrix * viewMatrix * vec4(world, 1.0);
    return clip.z / clip.w * 0.5 + 0.5;
}

void main(void)
{
    vec3 quadPoint = fragWorldPosition;
    // Derivatives up front, in uniform control flow; every lookup below takes explicit gradients or levels.
    vec2 xzDx = dFdx(quadPoint.xz);
    vec2 xzDy = dFdy(quadPoint.xz);

    vec3 origin = viewPosWorld;
    vec3 ray = quadPoint - origin;
    float quadDistance = length(ray);
    ray /= max(quadDistance, 1e-5);
    // Only rays going down meet the surface from above.
    if (ray.y > -1e-3) discard;
    // --render shoredebug=7 is the bed's (Water/ShoreGround.glsl): the sea stands aside for it.
    if (shoreLevel.w > 6.5) discard;

    // Ground well above anything the waves reach (the margin covers the field's blur and parallax relief).
    float quadGround;
    if (waterTerrainHeight(quadPoint.xz, quadGround) && quadGround - shoreLevel.x > shoreWave.x * WATER_ALONGSHORE_MAX + 0.6) discard;

    ivec2 pixel = ivec2(gl_FragCoord.xy);
    vec2 uv = gl_FragCoord.xy / resolution;
    float sceneZ = texelFetch(sceneDepth, pixel, 0).r;
    bool sceneOpen = sceneZ >= 1.0;
    vec3 scenePoint = sceneOpen ? origin + ray * 1e5 : ReconstructWorldSpacePosition(vec2(pixel) + 0.5, sceneZ, invProjectionMatrix, invViewMatrix, resolution);
    float sceneDistance = dot(scenePoint - origin, ray);

    // Walk down to the surface, from where the ray meets the calm sea: drop to the height of the waves below the current point. A few steps are plenty for a
    // camera looking down; the step is clamped to the slab the surface lies in.
    float tMin = quadDistance;
    float tMax = max((oceanSlab.y - origin.y) / ray.y, tMin);
    float t = clamp((shoreLevel.x - origin.y) / ray.y, tMin, tMax);
    for (int i = 0; i < SURFACE_STEPS; i++)
    {
        vec3 p = origin + ray * t;
        SeaPoint at = seaAt(p.xz);
        float surfaceHeight = shoreLevel.x + shoreWaveAt(at.field, at.depth, at.coast).elevation;
        t = clamp(t + (p.y - surfaceHeight) / -ray.y, tMin, tMax);
    }
    vec3 surface = origin + ray * t;

    // The ground comes first, or stands above the surface's mesh (parallax relief dipping below it isn't under water): the beach, with whatever the swash is
    // doing on it, is the ground's to draw (Water/ShoreGround.glsl).
    float groundHeight;
    bool hasGround = waterTerrainHeight(surface.xz, groundHeight);
    if (!(t < sceneDistance - 0.002) || (hasGround && surface.y <= groundHeight)) discard;

    SeaPoint sea = seaAt(surface.xz);
    OceanWave wave = oceanWave(surface.xz, sea, true);
    float windSpeed = length(sampleWindMap(windMap, surface.xz).xz);
    float rippleSlope;
    vec3 ripples = waterRipples(surface.xz, xzDx, xzDy, windSpeed, rippleSlope);
    vec2 interaction = waterInteractionSlope(surface.xz);
    vec2 slope = wave.slope + ripples.xy + interaction;
    vec3 N = normalize(vec3(-slope.x, 1.0, -slope.y));
    float alphaSquared = WATER_BASE_ROUGHNESS2 + ripples.z;
    vec3 V = -ray;

    WaterBed straight = waterSceneBehind(uv, surface);
    WaterBed bed = waterRefractedBed(surface, N, straight);
    if (bed.open && sea.depth > 0.0 && sea.depth < 1e3)
    {
        // At the edge of the loaded terrain the view can leave it before reaching the bottom. The capture still knows how deep the sea is there, so put a
        // sandy bottom at that depth, lit by the same sun and sky, instead of an endless deep.
        bed.open = false;
        bed.depth = sea.depth;
        bed.pathLength = sea.depth / max(-ray.y, 0.1);
        bed.position = surface + ray * bed.pathLength;
        vec3 light = skyColour * WATER_SKY_AMBIENT;
        if (directionalLightCount > 0) light += directionalColours[0] * (max(directionalDirections[0].y, 0.0) / WATER_PI);
        bed.colour = SEABED_ALBEDO * light;
    }
    float causticLod = waterPatternLod(xzDx, xzDy, causticParameters.x);
    float caustics = waterCausticModulation(bed, rippleSlope + dot(wave.slope, wave.slope) * 0.5, causticLod);

    // The water's edge. Over the beach the sea fades in across SHORE_SHALLOWS of water by the mesh's depth, as the sheet the ground draws fades out under it
    // by the same measure (shoreDrowned), so there's no line where one ends and the other begins; round anything standing in the water, a post or a rock, it
    // fades in over the first few centimetres of the scene's depth.
    float edge = smoothstep(0.0, max(edgeFade, 1e-3), straight.depth);
    if (hasGround) edge = min(edge, shoreDrowned(surface.y - groundHeight));

    // Foam first, as its own relief is what the light falls across.
    float pixelMetres = max(length(xzDx), length(xzDy));
    Foam foam = seaFoam(surface.xz, sea, wave, edge, shoreFoamLod(pixelMetres));
    vec3 foamNormal = normalize(N + vec3(-foam.slope.x, 0.0, -foam.slope.y));

    // Breaking stirs sand up off the bottom, and makes the surf milky (scattering per metre, sand coloured), most just behind the crest.
    ShoreWave s = wave.shore;
    float behindCrest = fract(s.psi);
    float stirred = s.breaking * (0.4 + 0.6 * exp(-behindCrest * 7.0));
    vec3 turbidity = SURF_TURBIDITY * stirred * shoreWet.x;
    WaterShading shading = waterShade(surface, N, alphaSquared, V, bed, caustics, turbidity, foamNormal);

    vec3 colour = mix(straight.colour, shading.water, edge);
    // Thin foam is a film the light goes through, darker than a heap of it.
    colour = mix(colour, shading.foam * (0.8 + 0.2 * foam.thick), foam.cover);
    // --render shoredebug=5: the sea's foam green (and the beach's magenta, Water/ShoreGround.glsl). 6: red how much of the water is the sea's to draw, green
    // its foam, blue the depth over the terrain's mesh (of a metre).
    if (shoreLevel.w > 4.5 && shoreLevel.w < 5.5) colour = mix(colour, vec3(0.0, 0.7, 0.0), foam.cover);
    if (shoreLevel.w > 5.5 && shoreLevel.w < 6.5) colour = vec3(edge, foam.cover, clamp(hasGround ? surface.y - groundHeight : 1.0, 0.0, 1.0)) * 0.6;

    out_Colour = vec4(colour, 1.0);
    gl_FragDepth = max(gl_FragCoord.z, windowDepth(surface));
}
