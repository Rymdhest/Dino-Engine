#version 430

// Cells (Canvas.Cells): a Voronoi diagram of one random site in each of `frequency` cells across the tile, wrapping. Everything is
// measured in cells, so a pattern of stretched cells is stretched as a whole. The distance to the border is the true one, from each
// point to the plane halfway between its site and the nearest other (Quilez, "Voronoi Edges", 2012): unlike F2 - F1 it keeps its
// width all the way round a corner, which is what makes a joint between stones look laid rather than computed.
//
// result0: distance to the border, distance to the site, the cell's id and a second random of its own, all in [0, 1).
// result1: the site from here (x, y, in cells), the distance to the second nearest site, and the id of the cell across the border.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;
layout (location = 1) out vec4 result1;

uniform ivec2 frequency;
uniform vec2 jitter;        // Across and down: 0 for a grid, 1 for sites anywhere in their cells. Little down and much across lays stones in rough courses.
uniform int seed;

vec2 siteOf(ivec2 cell, ivec2 period, uint s)
{
    vec3 h = synthHash3(wrapInt(cell, period), s);
    return vec2(cell) + 0.5 + (h.xy - 0.5) * jitter;
}

void main(void)
{
    ivec2 period = max(frequency, ivec2(1));
    vec2 p = uvAt() * vec2(period);
    ivec2 base = ivec2(floor(p));
    uint s = uint(seed);

    // The nearest site and the next. Sites can lie anywhere in their cells, so the search goes two cells out to be sure.
    float nearest = 1e9;
    float second = 1e9;
    ivec2 nearestCell = base;
    vec2 nearestSite = p;
    for (int j = -2; j <= 2; j++)
    {
        for (int i = -2; i <= 2; i++)
        {
            ivec2 cell = base + ivec2(i, j);
            vec2 site = siteOf(cell, period, s);
            vec2 to = site - p;
            float d = dot(to, to);
            if (d < nearest)
            {
                second = nearest;
                nearest = d;
                nearestCell = cell;
                nearestSite = site;
            }
            else if (d < second)
            {
                second = d;
            }
        }
    }

    // The distance to each plane halfway between the nearest site and another, and the least of them.
    float border = 1e9;
    ivec2 across = nearestCell;
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
            float d = dot(0.5 * (nearestSite + site) - p, between * inversesqrt(length2));
            if (d < border)
            {
                border = d;
                across = cell;
            }
        }
    }

    vec3 own = synthHash3(wrapInt(nearestCell, period), s ^ 0x68e31da4u);
    float other = synthHash3(wrapInt(across, period), s ^ 0x68e31da4u).x;
    result0 = vec4(max(border, 0.0), sqrt(nearest), own.x, own.y);
    result1 = vec4(nearestSite - p, sqrt(second), other);
}
