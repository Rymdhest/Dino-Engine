#ifndef DINO_WAY_NOISE_GLSL
#define DINO_WAY_NOISE_GLSL

// Value noise for a way, from hashes with no sines in them, so that a way open for hours, whose noise is read far from the origin, moves as smoothly as one just opened.

float wayHash(vec3 p)
{
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}

float wayHash1(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

// 0 to 1, smooth, a unit to a feature.
float wayNoise(vec3 x)
{
    vec3 i = floor(x);
    vec3 f = fract(x);
    vec3 u = f * f * (3.0 - 2.0 * f);
    float n000 = wayHash(i);
    float n100 = wayHash(i + vec3(1.0, 0.0, 0.0));
    float n010 = wayHash(i + vec3(0.0, 1.0, 0.0));
    float n110 = wayHash(i + vec3(1.0, 1.0, 0.0));
    float n001 = wayHash(i + vec3(0.0, 0.0, 1.0));
    float n101 = wayHash(i + vec3(1.0, 0.0, 1.0));
    float n011 = wayHash(i + vec3(0.0, 1.0, 1.0));
    float n111 = wayHash(i + vec3(1.0, 1.0, 1.0));
    return mix(mix(mix(n000, n100, u.x), mix(n010, n110, u.x), u.y), mix(mix(n001, n101, u.x), mix(n011, n111, u.x), u.y), u.z);
}

float wayNoise1(float x)
{
    float i = floor(x);
    float f = fract(x);
    float u = f * f * (3.0 - 2.0 * f);
    return mix(wayHash1(i), wayHash1(i + 1.0), u);
}

#endif
