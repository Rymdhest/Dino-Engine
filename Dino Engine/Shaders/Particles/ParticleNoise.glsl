#ifndef DINO_PARTICLE_NOISE_GLSL
#define DINO_PARTICLE_NOISE_GLSL

// Small noises for what particles paint: value noise, its sum over octaves, and the distances to the nearest two points of a jittered grid (Worley).

uint particleHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float particleHash21(vec2 cell)
{
    ivec2 c = ivec2(floor(cell));
    return float(particleHash(uint(c.x) * 1973u + particleHash(uint(c.y) * 9277u + 26699u)) >> 8u) * (1.0 / 16777216.0);
}

vec2 particleHash22(vec2 cell)
{
    ivec2 c = ivec2(floor(cell));
    uint h = particleHash(uint(c.x) * 1973u + particleHash(uint(c.y) * 9277u + 26699u));
    return vec2(float(h & 0xFFFFu), float(h >> 16u)) * (1.0 / 65535.0);
}

float particleValueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = particleHash21(i);
    float b = particleHash21(i + vec2(1.0, 0.0));
    float c = particleHash21(i + vec2(0.0, 1.0));
    float d = particleHash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float particleFbm(vec2 p, int octaves)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        sum += amplitude * particleValueNoise(p);
        p = p * 2.03 + vec2(17.3, -9.1);
        amplitude *= 0.5;
    }
    return sum;
}

vec3 particleGradient3(vec3 cell)
{
    ivec3 c = ivec3(floor(cell));
    uint h = particleHash(uint(c.x) * 73856093u ^ particleHash(uint(c.y) * 19349663u ^ particleHash(uint(c.z) * 83492791u)));
    vec3 g = vec3(float(h & 1023u), float((h >> 10u) & 1023u), float((h >> 20u) & 1023u)) * (2.0 / 1023.0) - 1.0;
    return g / max(length(g), 1e-4);
}

// Gradient noise in three dimensions, about -1 to 1.
float particleNoise3(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = p - i;
    vec3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = dot(particleGradient3(i), f);
    float b = dot(particleGradient3(i + vec3(1.0, 0.0, 0.0)), f - vec3(1.0, 0.0, 0.0));
    float c = dot(particleGradient3(i + vec3(0.0, 1.0, 0.0)), f - vec3(0.0, 1.0, 0.0));
    float d = dot(particleGradient3(i + vec3(1.0, 1.0, 0.0)), f - vec3(1.0, 1.0, 0.0));
    float e = dot(particleGradient3(i + vec3(0.0, 0.0, 1.0)), f - vec3(0.0, 0.0, 1.0));
    float g = dot(particleGradient3(i + vec3(1.0, 0.0, 1.0)), f - vec3(1.0, 0.0, 1.0));
    float h = dot(particleGradient3(i + vec3(0.0, 1.0, 1.0)), f - vec3(0.0, 1.0, 1.0));
    float k = dot(particleGradient3(i + vec3(1.0, 1.0, 1.0)), f - vec3(1.0, 1.0, 1.0));
    return mix(mix(mix(a, b, u.x), mix(c, d, u.x), u.y), mix(mix(e, g, u.x), mix(h, k, u.x), u.y), u.z) * 1.6;
}

// x the distance to the nearest point, y to the second nearest, z a random number for the nearest's cell.
vec3 particleWorley(vec2 p)
{
    vec2 cell = floor(p);
    vec2 f = p - cell;
    float nearest = 8.0;
    float second = 8.0;
    float id = 0.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec2 offset = vec2(float(x), float(y));
            vec2 point = offset + particleHash22(cell + offset);
            float d = length(point - f);
            if (d < nearest)
            {
                second = nearest;
                nearest = d;
                id = particleHash21(cell + offset + vec2(71.0, 13.0));
            }
            else if (d < second)
            {
                second = d;
            }
        }
    }
    return vec3(nearest, second, id);
}

#endif
