#ifndef DINO_WIND_GLSL
#define DINO_WIND_GLSL

// The engine's single wind model. Everything that moves with the wind reads it from here:
// - windVelocityAt(): the wind field itself (ambient gusts and turbulence plus local emitters). Wind_Field.frag
//   bakes it into the wind map each frame; Wind/WindField.cs is an exact CPU mirror of it for gameplay code.
// - sampleWindMap(): the baked wind map, which is what shaders should normally read.
// - windCrest(): the waves rolling down the wind.
// - vegetationBend(): the swaying state of vegetation from Wind_Vegetation.frag.
// - canopyState(): the same for trees, from Wind_Canopy.frag, on a slower spring.
// - vegetationTouch(): what's walking through plants, from Wind_Vegetation.frag. Nothing up in a tree reads this.
//
// Like the wind of Ghost of Tsushima, it is a steady vector with slow noise on it. What moves with the wind must only ever
// read phases WindSystem has accumulated (never time multiplied by a live speed), and what plants read has to stay broad and
// slow: quick noise on a field of plants reads as a fault, not as a breeze.
//
// Velocities are in m/s in world space (x, y = up, z), near the ground. The wind map holds
// (velocity.x, velocity.y, velocity.z, gust intensity 0..1).

// Written each frame by WindRenderer.UploadWindData (binding WindRenderer.UniformBinding); the layout must match it.
layout(std140, binding = 6) uniform WindData {
    vec4 windMean;          // xy mean wind velocity on the xz plane (m/s), z mean speed, w number of emitters
    vec4 windHeading;       // xy unit vector the live wind blows towards, zw unit vector of the prevailing wind
    vec4 windShape;         // x gustiness, y turbulence, z 1 / gust scale, w 1 / turbulence scale
    vec4 windGust;          // xy gust pattern offset (noise units), z gust evolution phase, w unused
    vec4 windTurbulence;    // xy turbulence pattern offset (noise units), z turbulence evolution phase, w unused
    vec4 windMapRegion;     // xy world xz of the wind map's corner, zw its size in metres
    vec4 vegetationRegion;  // xy world xz of the vegetation map's corner, zw its size in metres
    vec4 vegetationParams;  // x max bend (radians), y reference speed (m/s), z flutter (radians), w flutter phase (cycles)
    vec4 windWave;          // x lean of a crest at this speed (radians), y strength of the waves (0..1), z wavering of their fronts (cycles), w unused
    vec4 canopyRegion;      // xy world xz of the canopy map's corner, zw its size in metres
    vec4 windWaves[4];      // A train each: xy wave vector (cycles per metre), z phase (cycles), w height
    vec4 windEmitters[96];  // 3 per emitter: (position, radius), (direction, strength), (type, age, duration, unused)
};

const float WIND_NOISE_PERIOD = 256.0;
const int WIND_GUST_OCTAVES = 2;
const int WIND_MAX_EMITTERS = 32;
const int WIND_WAVE_TRAINS = 4;
const float WIND_TAU = 6.28318530718;
const float WIND_EMITTER_BLAST = 0.0;
const float WIND_EMITTER_RADIAL = 1.0;
const float WIND_EMITTER_DIRECTIONAL = 2.0;
const float VEGETATION_MAX_TOTAL_BEND = 1.5;

uint windHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

// Unit gradient of a lattice corner. The lattice repeats every `period` cells, so the noise tiles and the
// pattern offsets can be wrapped without a seam.
vec3 windLatticeGradient(vec3 cell, float period)
{
    vec3 wrapped = cell - period * floor(cell / period);
    uvec3 c = uvec3(wrapped);
    uint h = windHash(c.x + windHash(c.y + windHash(c.z)));
    vec3 g = vec3(uvec3(h, h >> 10u, h >> 20u) & 1023u) * (2.0 / 1023.0) - 1.0;
    return g / sqrt(max(dot(g, g), 1e-6));
}

// Gradient noise with analytic derivatives: (value, d/dx, d/dy, d/dz). After Inigo Quilez.
vec4 windNoised(vec3 x, float period)
{
    vec3 i = floor(x);
    vec3 f = x - i;
    vec3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec3 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

    vec3 ga = windLatticeGradient(i + vec3(0.0, 0.0, 0.0), period);
    vec3 gb = windLatticeGradient(i + vec3(1.0, 0.0, 0.0), period);
    vec3 gc = windLatticeGradient(i + vec3(0.0, 1.0, 0.0), period);
    vec3 gd = windLatticeGradient(i + vec3(1.0, 1.0, 0.0), period);
    vec3 ge = windLatticeGradient(i + vec3(0.0, 0.0, 1.0), period);
    vec3 gf = windLatticeGradient(i + vec3(1.0, 0.0, 1.0), period);
    vec3 gg = windLatticeGradient(i + vec3(0.0, 1.0, 1.0), period);
    vec3 gh = windLatticeGradient(i + vec3(1.0, 1.0, 1.0), period);

    float va = dot(ga, f - vec3(0.0, 0.0, 0.0));
    float vb = dot(gb, f - vec3(1.0, 0.0, 0.0));
    float vc = dot(gc, f - vec3(0.0, 1.0, 0.0));
    float vd = dot(gd, f - vec3(1.0, 1.0, 0.0));
    float ve = dot(ge, f - vec3(0.0, 0.0, 1.0));
    float vf = dot(gf, f - vec3(1.0, 0.0, 1.0));
    float vg = dot(gg, f - vec3(0.0, 1.0, 1.0));
    float vh = dot(gh, f - vec3(1.0, 1.0, 1.0));

    float value = va + u.x * (vb - va) + u.y * (vc - va) + u.z * (ve - va)
                + u.x * u.y * (va - vb - vc + vd) + u.y * u.z * (va - vc - ve + vg) + u.z * u.x * (va - vb - ve + vf)
                + u.x * u.y * u.z * (-va + vb + vc - vd + ve - vf - vg + vh);

    vec3 derivative = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.z * (ge - ga)
                    + u.x * u.y * (ga - gb - gc + gd) + u.y * u.z * (ga - gc - ge + gg) + u.z * u.x * (ga - gb - ge + gf)
                    + u.x * u.y * u.z * (-ga + gb + gc - gd + ge - gf - gg + gh)
                    + du * (vec3(vb - va, vc - va, ve - va)
                          + u.yzx * vec3(va - vb - vc + vd, va - vc - ve + vg, va - vb - ve + vf)
                          + u.zxy * vec3(va - vb - ve + vf, va - vb - vc + vd, va - vc - ve + vg)
                          + u.yzx * u.zxy * (-va + vb + vc - vd + ve - vf - vg + vh));

    return vec4(value, derivative);
}

// Gust pattern, -1..1. A frozen-turbulence field: it is carried along by the mean wind (windGust.xy)
// and slowly changes shape as it travels (windGust.z).
float windGustNoise(vec2 xz)
{
    vec2 q = xz * windShape.z - windGust.xy;
    float sum = 0.0;
    float amplitude = 1.0;
    float total = 0.0;
    float frequency = 1.0;
    for (int o = 0; o < WIND_GUST_OCTAVES; o++)
    {
        sum += amplitude * windNoised(vec3(q, windGust.z) * frequency, WIND_NOISE_PERIOD * frequency).x;
        total += amplitude;
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    float gust = clamp(sum / total * 1.8, -1.0, 1.0);
    return gust * (1.5 - 0.5 * gust * gust);
}

// Swirling turbulence: the curl of a noise potential, so the flow it adds is divergence free (it swirls
// instead of piling up or draining away). In noise units, about -1.5..1.5.
vec2 windTurbulenceCurl(vec2 xz)
{
    vec2 q = xz * windShape.w - windTurbulence.xy;
    vec2 gradient = windNoised(vec3(q, windTurbulence.z), WIND_NOISE_PERIOD).yz;
    return vec2(gradient.y, -gradient.x);
}

// The fronts of the waves waver as they come: a few long, slow sines, cheap enough for every texel of the simulation.
float windFrontWander(vec2 xz)
{
    float slow = WIND_TAU * windWave.z;
    return 0.5 * sin(xz.x * 0.083 + xz.y * 0.047 + slow)
         + 0.35 * sin(xz.x * -0.061 + xz.y * 0.109 - slow)
         + 0.25 * sin(xz.x * 0.137 + xz.y * -0.094 + 2.0 * slow);
}

// How much of a wave is passing here, 0..1. Two families of trains run down the wind at slightly different angles and
// lengths, and meet in crests and part in lulls. Their directions are fixed, and a wind that turns fades from one to the
// next (WindField.WaveTrain): turned as a whole, the fronts would slide past faster the further from the world's origin.
float windCrest(vec2 xz)
{
    float wander = windFrontWander(xz);
    float sum = 0.0;
    for (int i = 0; i < WIND_WAVE_TRAINS; i++)
    {
        vec4 train = windWaves[i];
        float shifted = i < 2 ? wander : -0.7 * wander;
        sum += train.w * sin(WIND_TAU * (dot(xz, train.xy) + shifted - train.z));
    }
    float wave = clamp(0.5 + sum, 0.0, 1.0);
    return wave * wave * windWave.y;
}

// Ambient wind without local emitters: (velocity, gust intensity 0..1).
vec4 windAmbient(vec2 xz)
{
    float gust = windGustNoise(xz);
    float speedScale = max(1.0 + windShape.x * gust, 0.0);
    vec2 velocity = windMean.xy * speedScale + windTurbulenceCurl(xz) * (windMean.z * windShape.y * 0.5);
    return vec4(velocity.x, 0.0, velocity.y, gust * 0.5 + 0.5);
}

// Velocity added by emitter `index`. Emitters act as vertical columns: only the horizontal distance counts.
vec3 windEmitterVelocity(int index, vec2 xz)
{
    vec4 positionRadius = windEmitters[index * 3];
    vec4 directionStrength = windEmitters[index * 3 + 1];
    vec4 typeAgeDuration = windEmitters[index * 3 + 2];

    float radius = positionRadius.w;
    float strength = directionStrength.w;
    if (radius <= 0.0) return vec3(0.0);

    vec2 offset = xz - positionRadius.xz;
    float dist = length(offset);
    vec2 outward = dist > 1e-4 ? offset / dist : vec2(0.0);
    float type = typeAgeDuration.x;

    if (type < WIND_EMITTER_BLAST + 0.5)
    {
        // A blast wave: a front that races out and slows (Sedov-Taylor, radius ~ t^0.4) while it weakens,
        // a gentler outflow behind it and an updraft over the centre.
        float duration = typeAgeDuration.z;
        if (duration <= 0.0) return vec3(0.0);
        float t = clamp(typeAgeDuration.y / duration, 0.0, 1.0);
        float decay = (1.0 - t) * (1.0 - t);
        float front = radius * pow(t, 0.4);
        float width = max(radius * 0.2, 0.5);
        float frontProfile = (dist - front) / width;
        float outflow = exp(-frontProfile * frontProfile) + (dist < front ? 0.25 * dist / max(front, 1e-4) : 0.0);
        float centre = dist / width;
        return vec3(outward.x * outflow, 0.4 * exp(-centre * centre), outward.y * outflow) * (strength * decay);
    }
    if (type < WIND_EMITTER_RADIAL + 0.5)
    {
        // Downwash (a rotor or anything blowing down onto the ground): air spreads out along the ground,
        // fastest half way out, and falls in the middle.
        float x = dist / radius;
        if (x >= 1.0) return vec3(0.0);
        float spread = sin(3.14159265 * x);
        return vec3(outward.x * spread, -(1.0 - x) * (1.0 - x), outward.y * spread) * strength;
    }
    // Directional (a fan, a vehicle's wake): blows along its direction, fading out towards its radius.
    float x = dist / radius;
    if (x >= 1.0) return vec3(0.0);
    float falloff = (1.0 - x * x) * (1.0 - x * x);
    return directionStrength.xyz * (strength * falloff);
}

// The full wind field at a point on the ground: (velocity, gust intensity 0..1).
vec4 windVelocityAt(vec2 xz)
{
    vec4 wind = windAmbient(xz);
    int emitters = min(int(windMean.w + 0.5), WIND_MAX_EMITTERS);
    for (int i = 0; i < emitters; i++)
    {
        wind.xyz += windEmitterVelocity(i, xz);
    }
    return wind;
}

// The baked wind field: (velocity, gust intensity 0..1). Outside the map it falls back to the mean wind.
vec4 sampleWindMap(sampler2D windMap, vec2 xz)
{
    if (windMapRegion.z <= 0.0) return vec4(windMean.x, 0.0, windMean.y, 0.5);
    vec2 uv = (xz - windMapRegion.xy) / windMapRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return vec4(windMean.x, 0.0, windMean.y, 0.5);
    return textureLod(windMap, uv, 0.0);
}

// The bend vegetation settles at in a steady wind (radians, leaning downwind). Drag grows with the square of
// the wind speed, and the bend saturates because a blade can only lie down so far.
vec2 vegetationRestBend(vec2 windXZ)
{
    float speed = length(windXZ);
    if (speed <= 1e-4 || vegetationParams.y <= 0.0) return vec2(0.0);
    float relative = speed / vegetationParams.y;
    return windXZ / speed * (vegetationParams.x * (1.0 - exp(-relative * relative)));
}

// The bend a patch of plants is heading for at this moment: where the local wind pushes it, and the wave going by.
vec2 vegetationTargetBend(sampler2D windMap, vec2 xz)
{
    return vegetationRestBend(sampleWindMap(windMap, xz).xz) + windHeading.xy * (windWave.x * windCrest(xz));
}

// How far vegetation leans (radians) towards +x / +z at a point, and how fast that's changing. Inside the vegetation simulation this is the
// simulated, springy state; outside it falls back to what the spring would be heading for.
vec4 vegetationState(sampler2D bendMap, sampler2D windMap, vec2 xz)
{
    if (vegetationRegion.z > 0.0)
    {
        vec2 uv = (xz - vegetationRegion.xy) / vegetationRegion.zw;
        if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0))))
        {
            return textureLod(bendMap, uv, 0.0);
        }
    }
    return vec4(vegetationTargetBend(windMap, xz), 0.0, 0.0);
}

vec2 vegetationBend(sampler2D bendMap, sampler2D windMap, vec2 xz)
{
    return vegetationState(bendMap, windMap, xz).xy;
}

// The same for what has too much mass to answer a gust at once.
vec4 canopyState(sampler2D canopyMap, sampler2D windMap, vec2 xz)
{
    if (canopyRegion.z > 0.0)
    {
        vec2 uv = (xz - canopyRegion.xy) / canopyRegion.zw;
        if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0))))
        {
            return textureLod(canopyMap, uv, 0.0);
        }
    }
    return vec4(vegetationTargetBend(windMap, xz), 0.0, 0.0);
}

// What's walking through plants at a point: (lean towards +x / +z in radians, pressed flat in radians, how fast it's moving in rad/s). Further out than
// the simulation goes nobody's there to see it.
vec4 vegetationTouch(sampler2D touchMap, vec2 xz)
{
    if (vegetationRegion.z <= 0.0) return vec4(0.0);
    vec2 uv = (xz - vegetationRegion.xy) / vegetationRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return vec4(0.0);
    return textureLod(touchMap, uv, 0.0);
}

float vegetationCornerRandom(ivec2 cell)
{
    return float(windHash(uint(cell.x) + windHash(uint(cell.y))) & 0xffffu) / 65535.0;
}

// Smooth noise, 0..1, fixed to the world: how stiff the plants are here, in patches `patchSize` metres across.
float vegetationStiffness(vec2 xz, float patchSize)
{
    vec2 p = xz / patchSize;
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    return mix(mix(vegetationCornerRandom(c), vegetationCornerRandom(c + ivec2(1, 0)), u.x),
               mix(vegetationCornerRandom(c + ivec2(0, 1)), vegetationCornerRandom(c + ivec2(1, 1)), u.x), u.y);
}

#endif
