#version 430

// Long cells (Canvas.LongCells): Synth_Cells.frag's diagram for what cracks into long pieces, as bark does up a trunk and dried wood
// along its grain. One random site in each of `frequency` cells across the tile, wrapping, far more of them one way than the other.
//
// `metric` is how long a cell's sides are as they lie in the tile, the shorter as 1: (1, 4) for cells four times as tall as wide.
// The diagram is drawn in cells, so its pieces are stretched as their cells are, long and leaning into each other as a bark's ridges
// do, but the distance to the border is then how far off it really is, in the cell's shorter side, so a border is as wide whichever
// way it runs. Left in cells, a border across a tall cell is four times the width of one along it. A diagram of sites as they lie,
// measured the tile's way throughout, has no long pieces at all: sites strewn one to a cell are strewn evenly, however long the
// cells, and it's the measure that stretches a diagram, not where its sites are.
//
// With a `pull` the whole diagram is pulled about by slow noise before it's drawn, by about that many cells each way, so no border
// runs straight. Its distances are those of the diagram before it was pulled, so a border is wider where it was stretched.
//
// This is a shader of its own, and Synth_Cells.frag was left exactly as it was: folded into that one, with a measure of (1, 1) and
// no pull, the same sums came out of the compiler a last bit different, and a few texels of every material made of cells, cobbles,
// stone walls, leather, moved by a shade.
//
// result0: distance to the border, distance to the site (in cells), the cell's id and a second random of its own, all in [0, 1).
// result1: the site from here (x, y, in cells), the distance to the second nearest site, and the id of the cell across the border.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;
layout (location = 1) out vec4 result1;

uniform ivec2 frequency;
uniform vec2 jitter;        // Across and down: 0 for a grid, 1 for sites anywhere in their cells.
uniform vec2 metric;        // How long a cell's width and height are as they lie, the shorter as 1.
uniform vec2 pull;          // How far the diagram is pulled about, in cells, across and down: 0 leaves it be.
uniform ivec2 pullFrequency; // Lumps of that pull across the tile.
uniform float pullDeviation; // One octave of Perlin noise's deviation (Canvas.OctaveStatistics), to make the pull's size mean what it says.
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
    uint s = uint(seed);
    if (pull.x > 0.0 || pull.y > 0.0)
    {
        ivec2 lumps = max(pullFrequency, ivec2(1));
        float across = synthFbm(uvAt(), lumps, 3, 0.5, 2, NOISE_PERLIN, s ^ 0x51ed270bu, 0.0, pullDeviation);
        float down = synthFbm(uvAt(), lumps, 3, 0.5, 2, NOISE_PERLIN, s ^ 0x2f6a1c39u, 0.0, pullDeviation);
        p += vec2(across, down) * pull;
    }
    ivec2 base = ivec2(floor(p));

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

    // The distance to each plane halfway between the nearest site and another, and the least of them. A plane is a line here, and a
    // line is still a line once the cells are laid out as long as they are: how far off it then is, is how far off it is in cells
    // over how long its unit normal is once each part of it is divided by how long a cell is that way.
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
            vec2 normal = between * inversesqrt(length2);
            float d = dot(0.5 * (nearestSite + site) - p, normal) / length(normal / metric);
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
