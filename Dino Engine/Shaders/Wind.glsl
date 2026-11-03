#ifndef DINO_WIND_GLSL
#define DINO_WIND_GLSL

// The engine's single wind model. Everything that moves with the wind reads it from here:
// - windVelocityAt(): the wind field itself (ambient gusts and turbulence plus local emitters). Wind_Field.frag
//   bakes it into the wind map each frame; Wind/WindField.cs is an exact CPU mirror of it for gameplay code.
// - sampleWindMap(): the baked wind map, which is what shaders should normally read.
// - vegetationBend(): the swaying state of vegetation from Wind_Vegetation.frag.
//
// Velocities are in m/s in world space (x, y = up, z), near the ground. The wind map holds
// (velocity.x, velocity.y, velocity.z, gust intensity 0..1).

// Written each frame by WindRenderer.UploadWindData (binding WindRenderer.UniformBinding); the layout must match it.
layout(std140, binding = 6) uniform WindData {
    vec4 windMean;          // xy mean wind velocity on the xz plane (m/s), z mean speed, w number of emitters
    vec4 windShape;         // x gustiness, y turbulence, z 1 / gust scale, w 1 / turbulence scale
    vec4 windGust;          // xy gust pattern offset (noise units), z gust evolution phase, w unused
    vec4 windTurbulence;    // xy turbulence pattern offset (noise units), z turbulence evolution phase, w unused
    vec4 windMapRegion;     // xy world xz of the wind map's corner, zw its size in metres
    vec4 vegetationRegion;  // xy world xz of the vegetation map's corner, zw its size in metres
    vec4 vegetationParams;  // x max bend (radians), y reference speed (m/s), z flutter (radians), w unused
    vec4 windEmitters[96];  // 3 per emitter: (position, radius), (direction, strength), (type, age, duration, unused)
};

const float WIND_NOISE_PERIOD = 256.0;
const int WIND_GUST_OCTAVES = 3;
const int WIND_TURBULENCE_OCTAVES = 2;
const int WIND_MAX_EMITTERS = 32;
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
    return clamp(sum / total * 1.8, -1.0, 1.0);
}

// Swirling turbulence: the curl of a noise potential, so the flow it adds is divergence free (it swirls
// instead of piling up or draining away). In noise units, about -1.5..1.5.
vec2 windTurbulenceCurl(vec2 xz)
{
    vec2 q = xz * windShape.w - windTurbulence.xy;
    vec2 gradient = vec2(0.0);
    float amplitude = 1.0;
    float total = 0.0;
    float frequency = 1.0;
    for (int o = 0; o < WIND_TURBULENCE_OCTAVES; o++)
    {
        gradient += amplitude * frequency * windNoised(vec3(q, windTurbulence.z) * frequency, WIND_NOISE_PERIOD * frequency).yz;
        total += amplitude;
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    gradient /= total;
    return vec2(gradient.y, -gradient.x);
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

// How far vegetation leans (radians) towards +x / +z at a point. Inside the vegetation simulation this is the
// simulated, springy state; outside it falls back to the steady bend for the local wind.
vec2 vegetationBend(sampler2D bendMap, sampler2D windMap, vec2 xz)
{
    if (vegetationRegion.z > 0.0)
    {
        vec2 uv = (xz - vegetationRegion.xy) / vegetationRegion.zw;
        if (all(greaterThanEqual(uv, vec2(0.0))) && all(lessThanEqual(uv, vec2(1.0))))
        {
            return textureLod(bendMap, uv, 0.0).xy;
        }
    }
    return vegetationRestBend(sampleWindMap(windMap, xz).xz);
}

#endif
