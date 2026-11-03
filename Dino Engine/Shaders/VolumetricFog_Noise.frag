#version 420

// Bakes one z-slice of the tileable 3D fog noise. Every channel tiles with period 1 in all axes.
// r: Perlin-Worley billows (base shape)
// g: Worley fBm, medium frequency (detail erosion)
// b: Worley fBm, high frequency
// a: smooth Perlin fBm, low frequency (domain warp and top-of-layer heave)

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform float slice;

uvec3 pcg3d(uvec3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v;
}

vec3 hash33(ivec3 cell, int period, uint seed)
{
    // Cells are never below -1 here, and % is undefined for negative operands in GLSL.
    ivec3 wrapped = (cell + period) % period;
    return vec3(pcg3d(uvec3(wrapped) + seed * 7919u)) * (1.0 / 4294967295.0);
}

float perlin(vec3 p, int period, uint seed)
{
    ivec3 i = ivec3(floor(p));
    vec3 f = fract(p);
    vec3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float corners[8];
    for (int c = 0; c < 8; c++)
    {
        ivec3 offset = ivec3(c & 1, (c >> 1) & 1, (c >> 2) & 1);
        vec3 gradient = normalize(hash33(i + offset, period, seed) * 2.0 - 1.0);
        corners[c] = dot(gradient, f - vec3(offset));
    }
    float x00 = mix(corners[0], corners[1], u.x);
    float x10 = mix(corners[2], corners[3], u.x);
    float x01 = mix(corners[4], corners[5], u.x);
    float x11 = mix(corners[6], corners[7], u.x);
    return mix(mix(x00, x10, u.y), mix(x01, x11, u.y), u.z);
}

// Distance to the closest feature point, about 0..1.
float worley(vec3 p, int period, uint seed)
{
    ivec3 i = ivec3(floor(p));
    vec3 f = fract(p);
    float closest = 1.0;
    for (int z = -1; z <= 1; z++)
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        ivec3 offset = ivec3(x, y, z);
        vec3 featurePoint = vec3(offset) + hash33(i + offset, period, seed);
        closest = min(closest, length(featurePoint - f));
    }
    return closest;
}

// 0..1, 1 at the centre of the cells (cauliflower-like billows).
float worleyFbm(vec3 p, int period, uint seed)
{
    float a = 1.0 - worley(p * float(period), period, seed);
    float b = 1.0 - worley(p * float(period * 2), period * 2, seed + 1u);
    float c = 1.0 - worley(p * float(period * 4), period * 4, seed + 2u);
    return a * 0.625 + b * 0.25 + c * 0.125;
}

// About 0..1.
float perlinFbm(vec3 p, int period, int octaves, uint seed)
{
    float sum = 0.0;
    float amplitude = 1.0;
    float total = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        sum += perlin(p * float(period), period, seed + uint(o)) * amplitude;
        total += amplitude;
        period *= 2;
        amplitude *= 0.5;
    }
    return clamp(sum / total * 0.85 + 0.5, 0.0, 1.0);
}

float remap(float value, float oldMin, float oldMax, float newMin, float newMax)
{
    return newMin + (value - oldMin) / (oldMax - oldMin) * (newMax - newMin);
}

void main(void)
{
    vec3 p = vec3(textureCoords, slice);

    float billowPerlin = perlinFbm(p, 4, 5, 11u);
    float billowWorley = worleyFbm(p, 4, 23u);
    float perlinWorley = clamp(remap(billowPerlin, 0.0, 1.0, billowWorley, 1.0), 0.0, 1.0);

    float detailMedium = worleyFbm(p, 8, 37u);
    float detailHigh = worleyFbm(p, 16, 53u);
    float smoothLow = perlinFbm(p, 2, 4, 71u);

    out_Colour = vec4(perlinWorley, detailMedium, detailHigh, smoothLow);
}
