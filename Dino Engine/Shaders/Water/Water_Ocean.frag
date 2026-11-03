#version 420

// The sea. The quads sit at the top of the slab the waves stay within; each pixel walks its view ray down to the
// moving surface (a few fixed-point steps, exact enough because a camera looking down sees the surface steeply), so no
// dense mesh is needed and the waterline moves up and down the beach by itself.
//
// Waves near the coast run in towards the nearest land, following the shore field (WaterShoreField): a signed distance
// to the shoreline, the depth, and how open the water is. Offshore they shoal (Green's law, height ~ depth^-1/4), then
// break once they are 0.78 times as high as the water is deep, and the bore runs on to the beach as a thin sheet (the
// swash) that slides up and back, leaving wet sand that dries. How far it runs follows Hunt's formula: the height it
// climbs is the beach slope times sqrt(wave height x deep-water wavelength), so it runs about that far inland on any
// beach and only climbs high where the shore is steep. Sheltered water (lagoons, inlets) gets smaller waves. Out at sea
// a wind-raised swell takes over. Wind ripples, ripples from things wading through it, refraction, absorption,
// scattering, reflections and caustics come from WaterCommon.glsl.

#include Globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Wind.glsl
#include Water/WaterCommon.glsl

in vec3 fragWorldPosition;

layout (location = 0) out vec4 out_Colour;

// The surface lies further along every view ray than the quad, so depth only grows; declaring it keeps early depth
// rejection against the ground working although gl_FragDepth is written.
layout (depth_greater) out float gl_FragDepth;

uniform sampler2D shoreField;
uniform sampler2D shoreOpenness;
uniform sampler2D gNormal;

uniform vec4 shoreRegion;
uniform float opennessLod;
uniform float seaLevel;
uniform vec2 oceanSlab;
uniform vec4 shoreWave;
uniform vec4 shoreShape;
uniform vec4 swellAmplitudes[2];
uniform vec4 swellPhases[2];
uniform vec2 whitecaps;
uniform vec3 shoreFoam;

// shoreRegion: xy world xz corner, zw size of the shore field (z = 0: no field, open sea everywhere).
// oceanSlab:   x highest, y lowest world y the surface can reach.
// shoreWave:   x breaking wave height (m), y 1 / wavelength (1/m), z phase (cycles, wrapping at WATER_SET_LENGTH), w run-up multiplier.
// shoreShape:  x reach out to sea (m), y set variation (0..1), z deep-water wavelength of the waves (m), w wave period (s).
// whitecaps:   x crest height above which whitecaps show (swell standard deviations), y 1 / swell standard deviation (1/m).
// shoreFoam:   x foam multiplier, y wet sand darkening (0..1), z drying time (s).

const float WATER_BREAKING_RATIO = 0.78;   // WaterWaves.BreakingRatio
const float WATER_SET_LENGTH = 7.0;        // WaterWaves.SetLength
const float WATER_ALONGSHORE_MAX = 1.25;   // WaterWaves.AlongShoreMax
const float WATER_RUNUP_VERTICAL = 1.2;    // WaterWaves.MaxRunUpHeight: the swash climbs at most this many wave heights.
const float UPRUSH_FRACTION = 0.25;        // Part of each wave's cycle the swash spends climbing the beach.
const float SHEET_THICKNESS = 0.4;         // Swash sheet depth relative to the vertical run-up left beyond the point.
const vec3 SURF_TURBIDITY = vec3(0.8, 0.75, 0.6);  // Scattering per metre of sand stirred up by breaking waves.
const vec3 SEABED_ALBEDO = vec3(0.45, 0.4, 0.3);   // Sand assumed where the bottom isn't loaded.
const float TAU = 6.28318530718;

// The open-sea swell. Must match WaterWaves.SwellDirections / SwellWavelengths (directions as (sin, cos) of the angle).
const int SWELL_COUNT = 8;
const vec2 SWELL_DIRECTIONS[8] = vec2[](
    vec2(0.0, 1.0), vec2(0.7313537, 0.6819984), vec2(0.9993908, 0.0348995), vec2(0.6946584, -0.7193398),
    vec2(0.0348995, -0.9993908), vec2(-0.6819984, -0.7313537), vec2(-0.9998477, 0.0174524), vec2(-0.6946584, 0.7193398));
const float SWELL_WAVENUMBERS[8] = float[](
    0.2416610, 0.3695991, 0.1903996, 0.5235988, 0.2991993, 0.4188790, 0.2166628, 0.6283185);

// ---------------------------------------------------------------- shore field

struct Shore
{
    float distance;   // signed metres to the shoreline, positive out at sea
    float depth;      // metres below sea level (negative on land)
    vec2 gradient;    // of the distance; shorter than 1 where shorelines meet (bays, the middle of channels)
};

Shore sampleShore(vec2 xz)
{
    Shore s = Shore(1e4, 1e4, vec2(0.0));
    if (shoreRegion.z <= 0.0) return s;
    vec2 uv = (xz - shoreRegion.xy) / shoreRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return s;
    vec4 field = textureLod(shoreField, uv, 0.0);
    s.distance = field.r;
    s.depth = field.g;
    s.gradient = field.ba;
    return s;
}

// What changes only over tens of metres along the coast. A pixel's view ray moves a metre or two while it looks for the
// surface, so this is looked up once per pixel.
struct Coast
{
    float waveHeight;  // breaking height of the waves reaching here before sets: less in sheltered water, varying along the shore
    float jitter;      // crests lag and lead along the shore by up to 0.6 of a wave
    float shelter;     // 0..1, how open the water is (1 = open sea)
};

Coast sampleCoast(vec2 xz)
{
    float openness = 1.0;
    if (shoreRegion.z > 0.0)
    {
        vec2 uv = (xz - shoreRegion.xy) / shoreRegion.zw;
        if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0))))
        {
            vec2 open = textureLod(shoreOpenness, uv, opennessLod).rg;
            openness = open.g > 1e-3 ? open.r / open.g : 1.0;
        }
    }
    // Two independent smooth noises from one lookup.
    vec2 noise = textureLod(patternTexture, xz * (1.0 / 53.0), 0.0).ba;
    Coast c;
    c.shelter = smoothstep(0.15, 0.6, openness);
    c.waveHeight = shoreWave.x * c.shelter * (0.75 + 0.5 * noise.y);
    c.jitter = (noise.x - 0.5) * 1.2;
    return c;
}

// Waves come in sets: the height rises and falls over WATER_SET_LENGTH waves.
float setFactor(float cycle)
{
    return 1.0 - shoreShape.y * (0.5 + 0.5 * cos(cycle * (TAU / WATER_SET_LENGTH)));
}

// Profile of a shore wave over one cycle, 0 (trough) to 1 (crest at u = 0): a gentle back behind the crest and a front
// that steepens as the wave nears breaking. slope = d profile / du.
float waveProfile(float u, float front, out float slope)
{
    float back = 1.0 - front;
    if (u < back)
    {
        float x = 1.0 - u / back;
        slope = -2.0 * x / back;
        return x * x;
    }
    float x = (u - back) / front;
    slope = 6.0 * x * (1.0 - x) / front;
    return x * x * (3.0 - 2.0 * x);
}

// ---------------------------------------------------------------- waves

struct OceanWave
{
    float elevation;  // metres above sea level
    vec2 slope;       // d elevation / d xz
    float foam;       // amount of foam (before the lace pattern)
    float stirred;    // 0..1, sand the breaking waves have stirred up into the water
};

// The surface at xz. withSwell = false leaves out the open-sea swell: it has faded out wherever the surface meets the
// ground, so finding where a view ray meets the surface doesn't need it.
OceanWave oceanWave(vec2 xz, Shore s, Coast coast, bool withSwell)
{
    OceanWave wave = OceanWave(0.0, vec2(0.0), 0.0, 0.0);

    float height = coast.waveHeight;
    if (height > 1e-4 && s.distance < shoreShape.x)
    {
        // Phase: crests sit where psi is whole and move shorewards as the phase grows.
        float psi = max(s.distance, 0.0) * shoreWave.y + shoreWave.z + coast.jitter;
        float u = fract(psi);
        height *= setFactor(psi);

        float breakingDepth = max(height / WATER_BREAKING_RATIO, 0.05);
        float shoaling = pow(clamp(breakingDepth / max(s.depth, breakingDepth), 0.0, 1.0), 0.25);
        float surf = smoothstep(-0.05, breakingDepth, s.depth);
        float reach = 1.0 - smoothstep(shoreShape.x * 0.5, shoreShape.x, s.distance);
        float coherence = smoothstep(0.35, 0.85, length(s.gradient));
        float amplitude = height * shoaling * mix(0.35, 1.0, surf) * reach * coherence;
        float breaking = smoothstep(breakingDepth * 1.5, breakingDepth * 0.8, s.depth) * smoothstep(-0.1, 0.0, s.depth) * reach;

        float front = mix(0.45, 0.12, breaking);
        float profileSlope;
        float profile = waveProfile(u, front, profileSlope);
        float mean = (1.0 - front) / 3.0 + front * 0.5;
        wave.elevation += amplitude * (profile - mean);
        if (s.distance > 0.0) wave.slope += amplitude * profileSlope * shoreWave.y * s.gradient;

        // Breaking churns foam up on the face and crest; it trails off behind. The churning also lifts sand off the bottom.
        float trail = u < 1.0 - front ? exp(-u * 7.0) : pow((u - (1.0 - front)) / front, 3.0);
        wave.foam += breaking * (trail * coherence + 0.25);
        wave.stirred = breaking * (0.4 + 0.6 * trail);
    }

    // Out at sea the wind's swell; it gives way to the shore waves as the water shoals, and calms in sheltered water.
    float swellFade = s.depth > 1e3 ? 1.0
        : smoothstep(0.0, max(3.0 * shoreWave.x / WATER_BREAKING_RATIO, 0.5), s.depth) * coast.shelter;
    if (withSwell && swellFade > 0.0)
    {
        float elevation = 0.0;
        vec2 slope = vec2(0.0);
        for (int i = 0; i < SWELL_COUNT; i++)
        {
            float amplitude = swellAmplitudes[i / 4][i % 4];
            if (amplitude <= 0.0) continue;
            float phase = SWELL_WAVENUMBERS[i] * dot(SWELL_DIRECTIONS[i], xz) - TAU * swellPhases[i / 4][i % 4];
            elevation += amplitude * sin(phase);
            slope += (amplitude * SWELL_WAVENUMBERS[i] * cos(phase)) * SWELL_DIRECTIONS[i];
        }
        wave.elevation += elevation * swellFade;
        wave.slope += slope * swellFade;
        // Whitecaps on the highest crests, covering the fraction of the sea the wind calls for.
        wave.foam += smoothstep(whitecaps.x, whitecaps.x + 0.8, elevation * whitecaps.y) * swellFade;
    }
    return wave;
}

// ---------------------------------------------------------------- swash and wet sand

struct Swash
{
    float tau;    // 0..1 through the current wave's swash cycle, 0 when the wave reaches the shoreline
    float reach;  // 0..1 of its full run-up the swash has climbed so far: a fast climb that slows to a stop, then a slide back
    float need;   // of the current wave's full run-up, the fraction it takes to reach this point (above 1: out of reach)
    float previousNeed;  // the same for the wave before
    float sheetDepth;    // metres of vertical run-up the current wave has beyond this point, at its highest
};

// How much of a run-up by waves `waveHeight` high it takes to reach a point `inland` metres beyond the shoreline and
// `height` metres above the sea: Hunt's horizontal excursion sqrt(H L0), capped at WATER_RUNUP_VERTICAL wave heights up.
float runUpNeed(float inland, float height, float waveHeight)
{
    float runUp = max(shoreWave.w, 0.0);
    if (runUp * waveHeight <= 1e-4) return 2.0;
    float horizontal = runUp * sqrt(waveHeight * max(shoreShape.z, 1.0));
    float vertical = runUp * WATER_RUNUP_VERTICAL * waveHeight;
    return max(inland / horizontal, height / vertical);
}

Swash swashAt(Shore s, Coast coast, float height)
{
    float psi = shoreWave.z + coast.jitter;
    Swash swash;
    swash.tau = fract(psi);
    float cycle = psi - swash.tau;
    float x = swash.tau < UPRUSH_FRACTION ? 1.0 - swash.tau / UPRUSH_FRACTION : (swash.tau - UPRUSH_FRACTION) / (1.0 - UPRUSH_FRACTION);
    swash.reach = 1.0 - x * x;

    float inland = max(-s.distance, 0.0);
    float current = coast.waveHeight * setFactor(cycle);
    swash.need = runUpNeed(inland, height, current);
    swash.previousNeed = runUpNeed(inland, height, coast.waveHeight * setFactor(cycle - 1.0));
    swash.sheetDepth = WATER_RUNUP_VERTICAL * max(shoreWave.w, 0.0) * current;
    return swash;
}

// 0..1, how wet the sand still is: since the backwash uncovered it, it has been drying.
float sandWetness(Swash swash)
{
    float period = shoreShape.w;
    float elapsed = -1.0;
    if (swash.need < 1.0)
    {
        float uncovered = UPRUSH_FRACTION + (1.0 - UPRUSH_FRACTION) * sqrt(1.0 - swash.need);
        if (swash.tau >= uncovered) elapsed = (swash.tau - uncovered) * period;
    }
    if (elapsed < 0.0 && swash.previousNeed < 1.0)
    {
        float uncovered = UPRUSH_FRACTION + (1.0 - UPRUSH_FRACTION) * sqrt(1.0 - swash.previousNeed);
        elapsed = (swash.tau + 1.0 - uncovered) * period;
    }
    return elapsed < 0.0 ? 0.0 : exp(-elapsed / max(shoreFoam.z, 0.1));
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

    // Ground well above anything the sea reaches (the margin covers the blur of the field and parallax relief).
    Shore here = sampleShore(quadPoint.xz);
    if (here.depth < -(shoreWave.x * WATER_ALONGSHORE_MAX * shoreWave.w * WATER_RUNUP_VERTICAL + 0.6)) discard;

    ivec2 pixel = ivec2(gl_FragCoord.xy);
    vec2 uv = gl_FragCoord.xy / resolution;
    float sceneZ = texelFetch(sceneDepth, pixel, 0).r;
    bool sceneOpen = sceneZ >= 1.0;
    vec3 scenePoint = sceneOpen ? origin + ray * 1e5 : ReconstructWorldSpacePosition(vec2(pixel) + 0.5, sceneZ, invProjectionMatrix, invViewMatrix, resolution);
    float sceneDistance = dot(scenePoint - origin, ray);

    Coast coast = sampleCoast(quadPoint.xz);

    // Walk down to the surface: from the current point, drop to the height of the waves below it. Three steps are plenty
    // for a camera looking down; the step is clamped to the slab the surface lies in.
    float tMin = quadDistance;
    float tMax = max((oceanSlab.y - origin.y) / ray.y, tMin);
    float t = tMin;
    Shore shore = here;
    for (int i = 0; i < 3; i++)
    {
        vec3 p = origin + ray * t;
        if (i > 0) shore = sampleShore(p.xz);
        float surfaceHeight = seaLevel + oceanWave(p.xz, shore, coast, false).elevation;
        t = clamp(t + (p.y - surfaceHeight) / -ray.y, tMin, tMax);
    }
    vec3 surface = origin + ray * t;

    // Open water: the surface comes before whatever the pixel shows, and lies above the ground's mesh (parallax relief
    // dipping below it isn't under water). It shades with the field from the last step, centimetres from the surface.
    bool sheet = false;
    float swashFoam = 0.0;
    vec3 groundNormal = vec3(0.0, 1.0, 0.0);
    float groundHeight;
    bool hasGround = waterTerrainHeight(surface.xz, groundHeight);
    if (!(t < sceneDistance - 0.002) || (hasGround && surface.y <= groundHeight))
    {
        // The ground comes first. The swash may cover it with a thin sheet of water, or it may be sand the sea has wetted.
        if (sceneOpen) discard;
        shore = sampleShore(scenePoint.xz);
        float pointGround;
        float height = (waterTerrainHeight(scenePoint.xz, pointGround) ? max(scenePoint.y, pointGround) : scenePoint.y) - seaLevel;
        Swash swash = swashAt(shore, coast, height);
        groundNormal = normalize(mat3(invViewMatrix) * unCompressNormal(texelFetch(gNormal, pixel, 0).xyz));
        if (swash.need < swash.reach)
        {
            float beyond = swash.reach - swash.need;
            float thickness = clamp(beyond * swash.sheetDepth * SHEET_THICKNESS, 0.004, 0.12);
            t = max(sceneDistance - thickness / -ray.y, tMin);
            surface = origin + ray * t;
            sheet = true;
            // Foam rides the leading edge of the sheet and lingers on it in lace as it drains.
            swashFoam = (1.0 - smoothstep(0.0, 0.15, beyond)) * 0.9 + 0.35;
        }
        else
        {
            float wetness = sandWetness(swash);
            if (wetness < 0.01) discard;

            // Wet sand: darker, with a faint sheen of the sky and the sun.
            vec3 V = -ray;
            vec3 colour = texelFetch(sceneColour, pixel, 0).rgb * (1.0 - shoreFoam.y * wetness);
            float sheen = waterFresnel(dot(groundNormal, V)) * wetness * 0.5;
            colour += sheen * waterSkyRadiance(reflect(ray, groundNormal));
            colour += wetness * 0.25 * waterDirectionalSpecular(scenePoint, groundNormal, V, 0.45);
            out_Colour = vec4(colour, 1.0);
            // Just in front of the ground, so the depth test against it passes.
            gl_FragDepth = max(gl_FragCoord.z, sceneZ - 2e-7);
            return;
        }
    }

    OceanWave wave = oceanWave(surface.xz, shore, coast, true);
    float windSpeed = length(sampleWindMap(windMap, surface.xz).xz);
    float rippleSlope;
    vec3 ripples = waterRipples(surface.xz, xzDx, xzDy, windSpeed, rippleSlope);
    vec2 interaction = waterInteractionSlope(surface.xz);
    vec3 N;
    if (sheet)
    {
        // The sheet follows the slope of the beach but fills its small ripples, so it is mostly flat.
        N = normalize(mix(vec3(0.0, 1.0, 0.0), groundNormal, 0.25) - vec3(ripples.x + interaction.x, 0.0, ripples.y + interaction.y) * 0.3);
    }
    else
    {
        vec2 slope = wave.slope + ripples.xy + interaction;
        N = normalize(vec3(-slope.x, 1.0, -slope.y));
    }
    float alphaSquared = WATER_BASE_ROUGHNESS2 + ripples.z;
    vec3 V = -ray;

    WaterBed straight = waterSceneBehind(uv, surface);
    WaterBed bed = waterRefractedBed(surface, N, straight);
    if (bed.open && shore.depth > 0.0 && shore.depth < 1e3)
    {
        // At the edge of the loaded terrain the view can leave it before reaching the bottom. The field still knows how
        // deep the sea is there, so put a sandy bottom at that depth, lit by the same sun and sky, instead of an endless deep.
        bed.open = false;
        bed.depth = shore.depth;
        bed.pathLength = shore.depth / max(-ray.y, 0.1);
        bed.position = surface + ray * bed.pathLength;
        vec3 light = skyColour * WATER_SKY_AMBIENT;
        if (directionalLightCount > 0) light += directionalColours[0] * (max(directionalDirections[0].y, 0.0) / WATER_PI);
        bed.colour = SEABED_ALBEDO * light;
    }
    float causticLod = waterPatternLod(xzDx, xzDy, causticParameters.x);
    float caustics = waterCausticModulation(bed, rippleSlope + dot(wave.slope, wave.slope) * 0.5, causticLod);
    // Stirred-up sand makes the surf milky (scattering per metre, sand coloured).
    vec3 turbidity = SURF_TURBIDITY * wave.stirred * shoreFoam.x;
    WaterShading shading = waterShade(surface, N, alphaSquared, V, bed, caustics, turbidity);

    // Shoreline fade: the mesh's depth gives a smooth waterline, the scene's fades around things standing in the water.
    float edgeDepth = sheet || !hasGround ? straight.depth : min(straight.depth, surface.y - groundHeight);
    float edge = smoothstep(0.0, max(edgeFade, 1e-3), edgeDepth);
    vec3 colour = mix(straight.colour, shading.water, edge);

    // Foam: breaking waves and whitecaps, a line at the waterline on coasts with surf, and the swash.
    float surf = clamp(coast.waveHeight / max(shoreWave.x, 1e-3), 0.0, 1.0);
    float waterline = (1.0 - smoothstep(0.0, 0.15, edgeDepth)) * 0.6 * surf;
    float foamAmount = (wave.foam + waterline + swashFoam) * shoreFoam.x;
    float foam = waterFoamCoverage(foamAmount, surface.xz, waterPatternLod(xzDx, xzDy, foamPattern.z));
    colour = mix(colour, shading.foam, foam);

    out_Colour = vec4(colour, 1.0);
    gl_FragDepth = max(gl_FragCoord.z, windowDepth(surface));
}
