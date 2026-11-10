#version 430

// Broken rock (Canvas.Facets): every cell a flat face at a tilt of its own, standing a little proud of or behind its neighbours, with
// a crack where two meet. A Voronoi cell is level and only shows at its edge, which reads as dried mud: it's planes catching the
// light differently from each other that look like stone. The crack keeps its width round corners, by the true distance to the
// border.
//
// result0: the height, the face's id, the distance to its border in cells, and how far its plane leans there.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;

uniform ivec2 frequency;
uniform float tilt;         // How far a face leans, in heights per cell.
uniform float relief;       // How far faces stand from each other.
uniform float crack;        // How deep the cracks between them go.
uniform float width;        // How wide those cracks are, in cells.
uniform int seed;

vec2 siteOf(ivec2 cell, ivec2 period, uint s)
{
    return vec2(cell) + synthHash3(wrapInt(cell, period), s).xy;
}

void main(void)
{
    ivec2 period = max(frequency, ivec2(1));
    vec2 p = uvAt() * vec2(period);
    ivec2 base = ivec2(floor(p));
    uint s = uint(seed);

    float nearest = 1e9;
    ivec2 nearestCell = base;
    vec2 nearestSite = p;
    for (int j = -2; j <= 2; j++)
    {
        for (int i = -2; i <= 2; i++)
        {
            ivec2 cell = base + ivec2(i, j);
            vec2 site = siteOf(cell, period, s);
            float d = dot(site - p, site - p);
            if (d < nearest)
            {
                nearest = d;
                nearestCell = cell;
                nearestSite = site;
            }
        }
    }

    float border = 1e9;
    for (int j = -2; j <= 2; j++)
    {
        for (int i = -2; i <= 2; i++)
        {
            ivec2 cell = nearestCell + ivec2(i, j);
            if (cell == nearestCell) continue;
            vec2 site = siteOf(cell, period, s);
            vec2 between = site - nearestSite;
            float length2 = dot(between, between);
            if (length2 < 1e-10) continue;
            border = min(border, dot(0.5 * (nearestSite + site) - p, between * inversesqrt(length2)));
        }
    }

    vec3 own = synthHash3(wrapInt(nearestCell, period), s ^ 0x85ebca6bu);
    float turn = own.x * 6.2831853;
    vec2 lean = vec2(cos(turn), sin(turn)) * (0.35 + 0.65 * own.y);
    vec2 fromSite = p - nearestSite;
    float plane = dot(fromSite, lean) * tilt;
    float height = 0.5 + (own.z - 0.5) * relief + plane;
    height -= crack * (1.0 - smoothstep(0.0, width, border));
    result0 = vec4(height, synthHash1(wrapInt(nearestCell, period), s ^ 0xc2b2ae35u), max(border, 0.0), plane);
}
