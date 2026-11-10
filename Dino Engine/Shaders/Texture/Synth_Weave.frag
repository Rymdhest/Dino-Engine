#version 430

// A plain weave (Canvas.Weave): threads running up the tile and across it, each going over one and under the next, as cloth and
// wicker are made. Each thread is round in section, and dips as it goes under: what's seen at a point is whichever thread is highest
// there. `threads` is how many of each cross the tile, whole numbers so that it tiles.
//
// result0: the height (0 to 1), the thread's id, 0 for a thread running up and 1 for one running across, and how far along its
//          current crossing it is (0 to 1).

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;

uniform ivec2 threads;
uniform float thickness;    // How much of the space between threads one fills, 0 to 1.
uniform float dip;          // How far a thread sinks going under another, as a share of the height.
uniform int seed;

void main(void)
{
    ivec2 count = max(threads, ivec2(1));
    vec2 p = uvAt() * vec2(count);
    ivec2 cell = ivec2(floor(p));
    vec2 f = p - vec2(cell);
    uint s = uint(seed);
    float halfWidth = 0.5 * clamp(thickness, 0.05, 1.0);

    // Up the tile: thread cell.x, at f.x across it. Over where cell.x + cell.y is even.
    float wavyUp = -cos(3.1415927 * (p.y + float(cell.x) + 0.5));
    float acrossUp = (f.x - 0.5) / halfWidth;
    float sectionUp = acrossUp * acrossUp < 1.0 ? sqrt(1.0 - acrossUp * acrossUp) : -1.0;
    float heightUp = sectionUp < 0.0 ? -1.0 : (1.0 - dip) * sectionUp + dip * (0.5 + 0.5 * wavyUp);

    // Across the tile: thread cell.y, at f.y across it, over where the other is under.
    float wavyAcross = cos(3.1415927 * (p.x + float(cell.y) + 0.5));
    float acrossAcross = (f.y - 0.5) / halfWidth;
    float sectionAcross = acrossAcross * acrossAcross < 1.0 ? sqrt(1.0 - acrossAcross * acrossAcross) : -1.0;
    float heightAcross = sectionAcross < 0.0 ? -1.0 : (1.0 - dip) * sectionAcross + dip * (0.5 + 0.5 * wavyAcross);

    bool up = heightUp >= heightAcross;
    float height = max(max(heightUp, heightAcross), 0.0);
    float id = up ? synthHash1(ivec2(wrapInt(cell, count).x, 0), s) : synthHash1(ivec2(0, wrapInt(cell, count).y), s ^ 0x7feb352du);
    result0 = vec4(height, id, up ? 0.0 : 1.0, up ? f.y : f.x);
}
