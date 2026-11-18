#ifndef DINO_WATER_SHORE_DATA_GLSL
#define DINO_WATER_SHORE_DATA_GLSL

// The coast, as the sea (Water_Ocean.frag) and the ground (Terrain.frag, Model.frag) both see it, so that the waves, the water running up the beach and the
// sand it leaves wet are worked out once, the same way, on either side of the waterline. Read SHORE.md. Needs Globals.glsl.
//
// Waves near the coast run in towards the nearest land, following the shore field (WaterShoreField): a signed distance to the shoreline, its gradient, and
// how open the water is. Offshore they shoal (Green's law, height ~ depth^-1/4), then break once they're 0.78 times as high as the water is deep, and the
// bore runs on up the beach as a thin sheet (the swash) that climbs fast, slows to a stop and slides back. How far it runs follows Hunt's formula: the height
// it climbs is the beach slope times sqrt(wave height x deep-water wavelength), so it runs about that far inland on any beach and only climbs high where the
// shore is steep. No two waves run up alike: each is its set's size and a size of its own, and wanders along the shore in lobes of its own, so the edge of
// the wet sand is the ragged edge of the last few waves and never a line. Sand the water has just left holds a film for a few seconds, glassy, longest in its
// hollows; then it's dark as it drains, and dries over a minute or so. The beach's own water comes out low down, where it stays glassy for good, what the
// biggest waves reach is damp.

layout(std140, binding = 15) uniform ShoreData
{
    vec4 shoreRegion;   // xy world corner, zw size of the shore field; z = 0 where there's none
    vec4 shoreWave;     // x breaking wave height (m), y 1 / wavelength (1/m), z phase (cycles, counted on over the sets), w run-up multiplier
    vec4 shoreShape;    // x reach out to sea (m), y set variation (0..1), z deep-water wavelength (m), w wave period (s)
    vec4 shoreWet;      // x foam multiplier, y how much darker sand is soaked (0..1), z seconds it takes to dry, w seconds a film of water stands on it
    vec4 shoreLevel;    // x sea level (m), y 1 with a sea at all, z how far an edge is softened (pixels), w the debug view (LightingSettings.ShoreDebug)
    vec4 shoreFoamFlow; // xy how far the wind has carried foam (m, wrapped), z 1 / foam tile (1/m), w the mean of two caustic layers crossed (WaterTextureBaker)
    vec4 shoreLife;     // x how warm the sea is (0..1, as a biome's warmth), y 1 for the salt sea and 0 for a land of lakes, zw unused (SEA.md)
};

uniform sampler2D shoreField;   // x signed distance to the shoreline (m, + at sea), yz its gradient, w how open the water is (WaterShoreField)
uniform sampler2D shoreNoise;   // The water's pattern (WaterTextureBaker.BakePattern): b and a, two smooth noises.

const float WATER_BREAKING_RATIO = 0.78;    // WaterWaves.BreakingRatio
const float WATER_SET_LENGTH = 7.0;         // WaterWaves.SetLength
const float WATER_ALONGSHORE_MAX = 1.25;    // WaterWaves.AlongShoreMax
const float WATER_RUNUP_VERTICAL = 2.0;     // WaterWaves.MaxRunUpHeight: the swash climbs at most this many wave heights.
const float SHORE_TAU = 6.28318530718;

const float SHORE_UPRUSH = 0.25;            // Of a wave's cycle, the share its swash spends climbing.
const float SHORE_SHEET_THICKNESS = 0.4;    // How deep the sheet is, of the vertical run-up left beyond a point.
const int SHORE_LOOKBACK = 7;               // Waves looked back over for how long ago the water left a point: a set.
const float SHORE_WAVE_SPREAD = 0.2;        // How much bigger or smaller than its set says a wave can be.
const float SHORE_LOBES = 0.2;              // How far a run-up wanders, of itself, along the shore.
const float SHORE_FRONT = 0.06;             // Of a run-up, how deep the band of foam at the front of the water is.
const float SHORE_SHEET_FOAM = 1.5;         // Seconds the bore's foam lasts on the sheet behind it.
const float SHORE_FOAM_LEFT = 2.6;          // Seconds the foam the water leaves on the sand takes to burst.
const float SHORE_FLOW = 0.3;               // Of how far the front has climbed, how far the water behind it has carried what floats on it.
const float SHORE_SEEP = 0.3;               // Of the mean wave's run-up, how far up the beach its own water comes out.
const float SHORE_DAMP_FROM = 1.0;          // ... where the damp starts thinning, past what the waves of the last minute reached,
const float SHORE_DAMP_TO = 2.3;            // and where the sand's dry: what the seas of the last few hours left damp.
const float SHORE_TOP = 2.6;                // ... and how high anything about the shore goes.
const float SHORE_FRINGE_TOP = 1.1;         // Metres above the sea fine sand draws the sea up to, through it (its capillary fringe):
const float SHORE_FRINGE_REACH = 14.0;      // as far inland as the water under a beach stands at the sea's level.
const float SHORE_SHALLOWS = 0.3;           // Metres of water over which the sea's own surface takes over from the ground's sheet (shoreDrowned).

// ---------------------------------------------------------------- the field

struct ShoreField
{
    bool inside;        // the field covers this point
    float distance;     // signed metres to the shoreline, + at sea
    vec2 gradient;      // of the distance, seaward: shorter than 1 where shorelines meet (bays, the middle of channels)
    float openness;     // 0..1, how much of what's round is sea
};

ShoreField shoreFieldAt(vec2 xz)
{
    ShoreField s = ShoreField(false, 1e4, vec2(0.0), 1.0);
    if (shoreRegion.z <= 0.0) return s;
    vec2 uv = (xz - shoreRegion.xy) / shoreRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return s;
    vec4 field = textureLod(shoreField, uv, 0.0);
    s.inside = true;
    s.distance = field.r;
    s.gradient = field.gb;
    s.openness = field.a;
    return s;
}

// What changes only over tens of metres along the coast.
struct Coast
{
    float waveHeight;   // breaking height of the biggest waves of a set here: less in sheltered water, varying along the shore
    float jitter;       // crests lag and lead along the shore by up to 0.6 of a wave
    float shelter;      // 0..1, how open the water is (1 = open sea)
};

Coast coastAt(vec2 xz, float openness)
{
    // Two independent smooth noises from one lookup.
    vec2 noise = textureLod(shoreNoise, xz * (1.0 / 53.0), 0.0).ba;
    Coast c;
    c.shelter = smoothstep(0.15, 0.6, openness);
    c.waveHeight = shoreWave.x * c.shelter * (0.75 + 0.5 * noise.y);
    c.jitter = (noise.x - 0.5) * 1.2;
    return c;
}

// Waves come in sets: the height rises and falls over WATER_SET_LENGTH waves.
float shoreSetFactor(float cycle)
{
    return 1.0 - shoreShape.y * (0.5 + 0.5 * cos(cycle * (SHORE_TAU / WATER_SET_LENGTH)));
}

// Profile of a shore wave over one cycle, 0 (trough) to 1 (crest at u = 0): a gentle back behind the crest and a front that steepens as the wave nears
// breaking. slope = d profile / du.
float shoreWaveProfile(float u, float front, out float slope)
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

// ---------------------------------------------------------------- the waves near the coast

struct ShoreWave
{
    float elevation;    // metres above sea level
    vec2 slope;         // d elevation / d xz
    float breaking;     // 0..1, how much it's breaking here
    float psi;          // the phase here: crests where it's whole, moving shorewards as it grows
    float front;        // share of the cycle the front face takes
    float coherence;    // 0..1, how much one wave front this is (not where fronts from two shores meet)
};

// The waves running in to the shore at a point of the field, `depth` metres below sea level (negative on land). The open sea's swell is the sea's own.
ShoreWave shoreWaveAt(ShoreField s, float depth, Coast coast)
{
    ShoreWave w = ShoreWave(0.0, vec2(0.0), 0.0, 0.0, 0.45, 0.0);
    float height = coast.waveHeight;
    if (height <= 1e-4 || s.distance >= shoreShape.x) return w;

    float psi = max(s.distance, 0.0) * shoreWave.y + shoreWave.z + coast.jitter;
    float u = fract(psi);
    height *= shoreSetFactor(psi);

    float breakingDepth = max(height / WATER_BREAKING_RATIO, 0.05);
    float shoaling = pow(clamp(breakingDepth / max(depth, breakingDepth), 0.0, 1.0), 0.25);
    float surf = smoothstep(-0.05, breakingDepth, depth);
    float reach = 1.0 - smoothstep(shoreShape.x * 0.5, shoreShape.x, s.distance);
    float coherence = smoothstep(0.35, 0.85, length(s.gradient));
    float amplitude = height * shoaling * mix(0.35, 1.0, surf) * reach * coherence;
    float breaking = smoothstep(breakingDepth * 1.5, breakingDepth * 0.8, depth) * smoothstep(-0.1, 0.0, depth) * reach;

    float front = mix(0.45, 0.12, breaking);
    float profileSlope;
    float profile = shoreWaveProfile(u, front, profileSlope);
    float mean = (1.0 - front) / 3.0 + front * 0.5;
    w.elevation = amplitude * (profile - mean);
    if (s.distance > 0.0) w.slope = amplitude * profileSlope * shoreWave.y * s.gradient;
    w.breaking = breaking;
    w.psi = psi;
    w.front = front;
    w.coherence = coherence;
    return w;
}

// ---------------------------------------------------------------- the water over the bed

// How fast the water runs over the bed `depth` metres down, as fast as the surface stands up over it times sqrt(g / depth): shorewards under a crest of the waves coming in
// and back under a trough. It's what sways what grows there (Wind_Vegetation.frag, SEA.md). Within the shore waves' reach it's the sea's own surface (shoreWaveAt); past it
// the same train still rocks the bed, a long low swell too faint to see from up here, shoaled less and dying away over a few reaches, in step with the waves it becomes.
// Negative runs seawards; `shoreward` is which way that is, and `column` how deep the water stands over the bed now.
float shoreSurgeAt(ShoreField field, float depth, Coast coast, out vec2 shoreward, out float column)
{
    shoreward = vec2(0.0);
    ShoreWave wave = shoreWaveAt(field, depth, coast);
    float elevation = wave.elevation;
    float gradient = length(field.gradient);
    float reach = shoreShape.x;
    float beyond = smoothstep(reach * 0.5, reach, field.distance) * (1.0 - smoothstep(reach, reach * 4.0, field.distance));
    if (beyond > 0.0 && coast.waveHeight > 1e-4)
    {
        // Crests where the shore waves have theirs (whole psi), so the two meet in step.
        float psi = max(field.distance, 0.0) * shoreWave.y + shoreWave.z + coast.jitter;
        float height = coast.waveHeight * shoreSetFactor(psi);
        float shoaling = pow(clamp(max(height / WATER_BREAKING_RATIO, 0.05) / max(depth, 0.05), 0.0, 1.0), 0.25);
        elevation += 0.5 * height * shoaling * cos(SHORE_TAU * psi) * smoothstep(0.35, 0.85, gradient) * beyond;
    }
    column = depth + elevation;
    if (column < 0.03 || gradient < 1e-4) return 0.0;
    shoreward = -field.gradient / gradient;
    return elevation * sqrt(9.81 / max(column, 0.3));
}

// ---------------------------------------------------------------- the swash

// Inland and up, how far a wave `size` metres high runs: Hunt's sqrt(H L0) inland, and at most WATER_RUNUP_VERTICAL wave heights up.
vec2 shoreRunUp(float size)
{
    float r = max(shoreWave.w, 0.0);
    return vec2(r * sqrt(max(size, 0.0) * max(shoreShape.z, 1.0)), r * WATER_RUNUP_VERTICAL * max(size, 0.0));
}

// Of a run-up, how much it takes to get `inland` metres past the shoreline and `height` metres above the sea: above 1, out of its reach.
float shoreNeed(float inland, float height, vec2 runUp)
{
    if (runUp.y <= 1e-4) return 4.0;
    return max(inland / max(runUp.x, 1e-4), height / runUp.y);
}

// Of its whole run-up, how far a swash has climbed `a` cycles after it reached the shoreline: a fast climb that slows to a stop, then a slide back.
float shoreReach(float a)
{
    if (a < SHORE_UPRUSH)
    {
        float x = 1.0 - a / SHORE_UPRUSH;
        return 1.0 - x * x;
    }
    float x = (a - SHORE_UPRUSH) / (1.0 - SHORE_UPRUSH);
    return 1.0 - x * x;
}

// Four random numbers of a wave of its own, by which wave it is counting from the start of the session (PCG4D, Jarzynski and Olano).
vec4 shoreWaveRandom(float wave)
{
    uvec4 v = uvec4(uint(int(wave) + 1048576)) * uvec4(1u, 3u, 5u, 7u) + uvec4(0x9e3779b9u, 0x7f4a7c15u, 0x85ebca6bu, 0xc2b2ae35u);
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    v ^= v >> 16u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    return vec4(v >> 8u) * (1.0 / 16777216.0);
}

// Four smooth noises, -1..1, at the sizes a run-up's front wanders by: a few metres and a stride or two.
vec4 shoreLobeNoise(vec2 xz)
{
    vec2 broad = textureLod(shoreNoise, xz * (1.0 / 23.0) + vec2(0.37, 0.61), 0.0).ba;
    vec2 fine = textureLod(shoreNoise, xz * (1.0 / 9.7) + vec2(0.13, 0.29), 0.0).ba;
    return vec4(broad, fine) * 2.0 - 1.0;
}

// How much farther or shorter than its size says a wave runs here: lobes of its own, from a mix of its own of the four noises (about a unit's worth of them).
float shoreLobe(vec4 noise, vec4 random)
{
    return 1.0 + SHORE_LOBES * dot(noise, (random.yzwx * 2.0 - 1.0) * 0.866);
}

struct Swash
{
    float need;         // of the mean wave's run-up, how much it takes to get here: what lies still on the beach goes by it
    float cover;        // 0..1 of the pixel under the current wave's sheet, softened across its edge
    float sheet;        // metres of water running over the point now
    float edge;         // 0..1, how near the edge of the water: its front while it climbs, then the edge it leaves behind as it runs back
    float climbing;     // 1 while the water's climbing, 0 while it runs back
    float coveredFor;   // seconds the sheet has been over the point
    float sheetWave;    // which wave the sheet is, counting from the start of the session, for its foam's own pattern
    float sheetFlow;    // metres that sheet has carried what floats on it up the beach
    float sinceWet;     // seconds since the water last ran off the point: 0 under water, past SHORE_LOOKBACK waves if none of them reached it
    float foamLeft;     // 0..1 of foam the water left as it ran off, before it bursts
    float leftWave;     // which wave left it
    float leftFlow;     // where its foam was when it was left
    float leftShare;    // of that wave's run-up, how much was left to go past here: little at the edge of its reach, where the water was thinnest
    float wander;       // -1..1, a smooth noise a few metres across, for edges that shouldn't follow the field exactly
};

// The swash at a point `inland` metres beyond the shoreline (less than nothing seaward of it) and `height` metres above the sea, as the waves of the last while
// have run up and back over it.
// `slope` is the ground's there, and `pixelMetres` how much ground a pixel spans, so the edge of the water is softened over a pixel and never steps.
Swash shoreSwash(vec2 xz, float inland, float height, Coast coast, float slope, float pixelMetres)
{
    Swash w = Swash(4.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1e4, 0.0, 0.0, 0.0, 0.0, 0.0);
    vec4 lobes = shoreLobeNoise(xz);
    w.wander = lobes.y * 0.7 + lobes.w * 0.3;
    vec2 mean = shoreRunUp(coast.waveHeight * (1.0 - 0.5 * shoreShape.y));
    w.need = shoreNeed(inland, height, mean * (1.0 + SHORE_LOBES * 0.5 * (lobes.x + lobes.z)));
    // Out of reach of the biggest wave there could be.
    vec2 biggest = shoreRunUp(coast.waveHeight * (1.0 + SHORE_WAVE_SPREAD)) * (1.0 + 2.0 * SHORE_LOBES);
    if (shoreNeed(inland, height, biggest) >= 1.0 + 0.1) return w;

    float psi = shoreWave.z + coast.jitter;
    float current = floor(psi);
    float since = psi - current;
    float period = max(shoreShape.w, 0.5);
    // Where each wave is in its set, stepped back a wave at a time by turning, rather than a cosine each (shoreSetFactor).
    const float SET_STEP = SHORE_TAU / WATER_SET_LENGTH;
    const vec2 BACK = vec2(cos(SET_STEP), sin(SET_STEP));
    vec2 set = vec2(cos(current * SET_STEP), sin(current * SET_STEP));
    for (int k = 0; k < SHORE_LOOKBACK; k++)
    {
        float n = current - float(k);
        float a = since + float(k);     // cycles since wave n reached the shoreline
        vec4 random = shoreWaveRandom(n);
        float setFactor = 1.0 - shoreShape.y * (0.5 + 0.5 * set.x);
        set = vec2(set.x * BACK.x + set.y * BACK.y, set.y * BACK.x - set.x * BACK.y);
        float size = coast.waveHeight * setFactor * (1.0 + SHORE_WAVE_SPREAD * (2.0 * random.x - 1.0));
        vec2 plain = shoreRunUp(size);
        vec2 runUp = plain * shoreLobe(lobes, random);
        float need = shoreNeed(inland, height, runUp);
        if (k == 0)
        {
            // The water of this wave, where it is now.
            float reach = shoreReach(a);
            float beyond = reach - need;
            float soft = shoreLevel.z * pixelMetres * max(1.0 / max(runUp.x, 0.2), slope / max(runUp.y, 0.02));
            w.cover = smoothstep(-soft, soft, beyond);
            w.edge = w.cover * (1.0 - smoothstep(0.0, SHORE_FRONT, beyond));
            w.climbing = a < SHORE_UPRUSH ? 1.0 : 0.0;
            w.sheet = w.cover * clamp(max(beyond, 0.0) * runUp.y * SHORE_SHEET_THICKNESS, 0.003, 0.12);
            w.sheetWave = n;
            w.sheetFlow = SHORE_FLOW * reach * plain.x;
        }
        if (need >= 1.0) continue;
        float rest = sqrt(1.0 - need);
        float reached = SHORE_UPRUSH * (1.0 - rest);
        // On its way and not here yet: only ever the current wave.
        if (a < reached) continue;
        float uncovered = SHORE_UPRUSH + (1.0 - SHORE_UPRUSH) * rest;
        if (a < uncovered)
        {
            w.coveredFor = (a - reached) * period;
            w.sinceWet = 0.0;
            w.leftShare = 1.0 - need;
            break;
        }
        // Waves are looked at newest first, and a newer one always ran off later than an older one did, so this is the last time water left here.
        w.sinceWet = (a - uncovered) * period;
        // The longer the water stood here, the more of the bore's foam had burst before it ran off: what's left lies in a line where it stopped.
        w.foamLeft = exp(-rest * period / SHORE_SHEET_FOAM);
        w.leftWave = n;
        w.leftFlow = SHORE_FLOW * need * plain.x;
        w.leftShare = 1.0 - need;
        break;
    }
    return w;
}

// ---------------------------------------------------------------- what the ground needs

// Of the water over ground `under` metres beneath the sea's surface, how much is the sea's to draw rather than the ground's. The sea pass fades in by it and
// the ground goes from the sheet's look to the sea bed's by it (Water/ShoreWet.glsl), and the foam and the caustics of each hand over by it, so the sea and the
// sheet meet with nothing between them: the water darkens the sand as a sheet, and the shallows' colour and the sea's own glints come in as it deepens. Both
// read it at the same depth, the sea from the terrain capture and the ground from its own mesh, over the same waves.
float shoreDrowned(float under)
{
    return smoothstep(0.0, SHORE_SHALLOWS, under);
}

// The highest anything about the shore reaches, in metres above the sea: above this nothing need look.
float shoreHighest()
{
    return max(SHORE_TOP * max(shoreWave.w, 0.0) * WATER_RUNUP_VERTICAL * shoreWave.x * WATER_ALONGSHORE_MAX * (1.0 - 0.5 * shoreShape.y) * (1.0 + SHORE_LOBES), SHORE_FRINGE_TOP + 0.2) + 0.05;
}

struct ShoreWater
{
    bool near;          // the sea comes anywhere near: nothing else here means anything otherwise
    float height;       // metres above the sea
    float inland;       // metres past the shoreline
    float under;        // metres a wave stands over the ground right now
    float cover;        // 0..1 of the pixel under water now: a wave, or the swash's sheet
    float sinceWet;     // seconds since the water last left it
    vec2 seaward;       // unit, the way to the sea
    Swash swash;
    Coast coast;
    ShoreField field;
};

// The sea at a point of the ground `height` metres above sea level, `slope` steep, a pixel there spanning `pixelMetres`.
ShoreWater shoreWaterAt(vec2 xz, float height, float slope, float pixelMetres)
{
    ShoreWater s;
    s.near = false;
    s.height = height;
    s.inland = 0.0;
    s.under = 0.0;
    s.cover = 0.0;
    s.sinceWet = 1e4;
    s.seaward = vec2(0.0, 1.0);
    s.swash = Swash(4.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1e4, 0.0, 0.0, 0.0, 0.0, 0.0);
    s.coast = Coast(0.0, 0.0, 1.0);
    s.field = ShoreField(false, 1e4, vec2(0.0), 1.0);
    if (shoreLevel.y < 0.5 || shoreRegion.z <= 0.0 || height > shoreHighest()) return s;
    // Deeper than the troughs of the biggest waves go: under the sea whatever the waves are doing, with nothing to work out.
    if (height < -(shoreWave.x * WATER_ALONGSHORE_MAX + 0.25))
    {
        s.near = true;
        s.under = -height;
        s.cover = 1.0;
        s.sinceWet = 0.0;
        s.swash.leftShare = 1.0;
        return s;
    }
    s.field = shoreFieldAt(xz);
    if (!s.field.inside) return s;
    // Farther inland than the biggest of waves could run on the flattest of beaches, before anything is looked up for it.
    if (-s.field.distance > max(SHORE_TOP * shoreRunUp(shoreWave.x * WATER_ALONGSHORE_MAX * (1.0 + SHORE_WAVE_SPREAD)).x * (1.0 + 2.0 * SHORE_LOBES), SHORE_FRINGE_REACH)) return s;

    s.inland = max(-s.field.distance, 0.0);
    s.coast = coastAt(xz, s.field.openness);
    float gradient = length(s.field.gradient);
    s.seaward = gradient > 1e-4 ? s.field.gradient / gradient : vec2(0.0, 1.0);
    ShoreWave wave = shoreWaveAt(s.field, -height, s.coast);
    s.under = max(wave.elevation - height, 0.0);
    // Seaward of the shoreline, less than nothing: what's out there is always under the swash, and only the shoreline itself is ever its edge, or as the water
    // ran back to it at the end of each wave the whole of the shallows was the edge of the water at once, foam and all.
    s.swash = shoreSwash(xz, -s.field.distance, height, s.coast, slope, pixelMetres);
    s.sinceWet = s.swash.sinceWet;
    // Below the sea or seaward of the shoreline, the water has only ever just gone, whatever the waves are doing.
    if (height < 0.0 || s.field.distance > 0.0)
    {
        s.sinceWet = 0.0;
        s.swash.leftShare = 1.0;
    }
    s.cover = max(s.swash.cover, smoothstep(0.0, 0.004, s.under));
    s.near = s.swash.need < SHORE_TOP || s.sinceWet < 1e3 || s.under > 0.0 || (height < SHORE_FRINGE_TOP + 0.2 && s.inland < SHORE_FRINGE_REACH);
    return s;
}

#endif
