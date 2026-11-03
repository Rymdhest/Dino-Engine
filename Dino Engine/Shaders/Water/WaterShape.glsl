#ifndef DINO_WATER_SHAPE_GLSL
#define DINO_WATER_SHAPE_GLSL

// Outline of a still water body. Physics/Water/WaterShape.cs is an exact CPU mirror of this file: change both together.

const int WATER_SHAPE_ELLIPSE = 0;
const int WATER_SHAPE_RECTANGLE = 1;
const int WATER_NOISE_CELL_OFFSET = 32768;

uint waterHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float waterLattice(ivec2 cell)
{
    uint h = waterHash(uint(cell.x) + waterHash(uint(cell.y) + 40503u));
    return float(h & 0xffffffu) / 16777215.0;
}

// Smooth value noise, 0..1.
float waterValueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    ivec2 cell = ivec2(i) + WATER_NOISE_CELL_OFFSET;
    float a = waterLattice(cell);
    float b = waterLattice(cell + ivec2(1, 0));
    float c = waterLattice(cell + ivec2(0, 1));
    float d = waterLattice(cell + ivec2(1, 1));
    return a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y;
}

float waterEdgeNoise(vec2 p)
{
    return 0.65 * waterValueNoise(p) + 0.35 * waterValueNoise(p * 2.03 + vec2(17.3));
}

// Signed distance in metres to the outline before edge noise; negative inside.
float waterShapeDistance(int shape, vec2 local, vec2 halfExtents)
{
    vec2 r = max(halfExtents, vec2(1e-3));
    if (shape == WATER_SHAPE_RECTANGLE)
    {
        vec2 d = abs(local) - r;
        return length(max(d, vec2(0.0))) + min(max(d.x, d.y), 0.0);
    }
    float k0 = length(local / r);
    float k1 = length(local / (r * r));
    return k1 > 1e-6 ? k0 * (k0 - 1.0) / k1 : -min(r.x, r.y);
}

// 0..1, how much of this point the body covers: 1 inside, fading to 0 over edgeSoftness metres inside the outline.
float waterShapeCoverage(int shape, vec2 local, vec2 halfExtents, float edgeSoftness, float edgeNoise, float invNoiseScale, vec2 worldXZ)
{
    float distance = waterShapeDistance(shape, local, halfExtents);
    if (edgeNoise > 0.0) distance += edgeNoise * (2.0 * waterEdgeNoise(worldXZ * invNoiseScale) - 1.0);
    if (edgeSoftness <= 1e-4) return distance < 0.0 ? 1.0 : 0.0;
    return smoothstep(0.0, edgeSoftness, -distance);
}

#endif
