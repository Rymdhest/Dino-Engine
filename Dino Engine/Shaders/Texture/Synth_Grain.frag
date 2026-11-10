#version 430

// The grain of sawn wood (Canvas.Grain), board by board, from where each point is on its board (Canvas.Bricks with boards running up
// the tile). A board is a flat cut through a log, so its growth rings are where that plane crosses cylinders round the log's heart:
// close and straight near the edges, opening into arches where the heart comes nearest, and the heart never quite runs along the
// board, which is what makes the arches. Every board is cut from a log of its own, at a depth and a lean of its own.
//
// result0: the dark late wood of each ring (0 to 1), where in its ring this is (0 to 1), fine pores, and knots (0 to 1).

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;

uniform sampler2D inputA;      // The boards' first image: edge, id, along, across.
uniform sampler2D inputB;      // Their second: length, width, course id, place.
uniform float rings;           // Rings across a board's width, near its edge.
uniform float waviness;        // How far the rings wander, in rings.
uniform float knots;           // How many knots to a board, on average.
uniform int seed;

void main(void)
{
    ivec2 at = texelAt();
    vec4 board = texelFetch(inputA, at, 0);
    vec4 size = texelFetch(inputB, at, 0);
    float id = board.y;
    uint s = uint(seed);

    // Across the board from its middle, and along it, both in widths of the board.
    float width = max(size.y, 1e-5);
    float x = board.w - 0.5;
    float y = board.z * size.x / width;

    float depth = 0.15 + 0.9 * synthHashValue(id, s);                       // How far the cut is from the heart, in widths.
    float leanAlong = (synthHashValue(id, s ^ 0x1u) * 2.0 - 1.0) * 0.06;    // How the heart drifts towards and away from the face.
    float heartAcross = (synthHashValue(id, s ^ 0x2u) * 2.0 - 1.0) * 0.35;  // Where it lies across the board.
    float h = depth + leanAlong * y;

    // The rings wander along the board in long slow waves, as a tree grows.
    float wander = waviness * (synthPerlin(vec2(y * 0.6, id * 37.0), ivec2(1 << 20), s ^ 0x3u) + 0.5 * synthPerlin(vec2(y * 2.3, id * 91.0), ivec2(1 << 20), s ^ 0x4u));
    float radius = length(vec2(x - heartAcross, h));
    float ring = radius * rings + wander + synthHashValue(id, s ^ 0x5u) * 7.0;

    // Knots: a few to a board, each a small hard whorl the rings bend round.
    float knot = 0.0;
    int knotCount = int(floor(knots + synthHashValue(id, s ^ 0x6u)));
    float boardLength = size.x / width;
    for (int k = 0; k < 3; k++)
    {
        if (k >= knotCount) break;
        vec2 centre = vec2((synthHashValue(id, s ^ uint(17 + k)) - 0.5) * 0.7, synthHashValue(id, s ^ uint(31 + k)) * boardLength);
        vec2 d = vec2(x, y) - centre;
        float r = length(d * vec2(1.0, 0.45)) / (0.08 + 0.1 * synthHashValue(id, s ^ uint(47 + k)));
        float influence = exp(-r * r);
        ring += influence * 2.5 * (1.0 - r);
        knot = max(knot, 1.0 - smoothstep(0.55, 1.0, r));
    }

    float inRing = fract(ring);
    // Late wood: a thin dark band at the end of each year's growth, sharp on its outer side.
    float late = smoothstep(0.62, 0.9, inRing) * (1.0 - smoothstep(0.92, 1.0, inRing));
    float pores = synthPerlin(vec2(x * 60.0, y * 4.0) + id * 13.0, ivec2(1 << 20), s ^ 0x7u) * 0.5 + 0.5;
    result0 = vec4(late, inRing, pores, knot);
}
