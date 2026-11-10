#version 430

// Scattered stones (Canvas.Scatter): up to four in each of `frequency` cells across the tile, each an ellipse of its own size,
// shape and turn, standing as a dome whose top is its own height. Where they overlap the higher one shows, as with real stones lying
// on each other. A stone is never more than a cell across from its middle, so it's found from the cells around.
//
// result0: the height of the highest stone here (0 where there's none), its id, how far out from its middle this is (0 to 1, past 1
//          off it), and how much of a stone this is, softened over the outermost rim.
// result1: where this is across the stone and along it (each -1 to 1), turned with it, and a second random of its own.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;
layout (location = 1) out vec4 result1;

uniform ivec2 frequency;
uniform int count;          // Stones in each cell, 1 to 4.
uniform float radius;       // Of an average stone, in cells.
uniform float radiusJitter; // 0 for stones all one size, 1 for anything from nothing to twice the average.
uniform float elongation;   // How much longer than wide a stone can be: 0 for round ones.
uniform float heightJitter; // 0 for all as high as each other.
uniform float flatness;     // 0 for a round dome, towards 1 for a flat top with a rounded rim, as a worn cobble.
uniform float turn;         // Which way a stone's long axis lies, in radians from +u,
uniform float turnJitter;   // and how much of a whole turn either way it may stray from that: 1 for any way at all.
uniform int seed;

void main(void)
{
    ivec2 period = max(frequency, ivec2(1));
    vec2 p = uvAt() * vec2(period);
    ivec2 base = ivec2(floor(p));
    uint s = uint(seed);
    int perCell = clamp(count, 1, 4);

    float bestHeight = 0.0;
    float bestId = 0.0;
    float bestOut = 2.0;
    vec2 bestLocal = vec2(0.0);
    float bestOther = 0.0;

    for (int j = -1; j <= 1; j++)
    {
        for (int i = -1; i <= 1; i++)
        {
            ivec2 cell = base + ivec2(i, j);
            ivec2 wrapped = wrapInt(cell, period);
            for (int k = 0; k < perCell; k++)
            {
                uint stoneSeed = s + uint(k) * 0x27d4eb2du;
                vec3 a = synthHash3(wrapped, stoneSeed);
                vec3 b = synthHash3(wrapped, stoneSeed ^ 0x165667b1u);
                vec2 centre = vec2(cell) + a.xy;
                float stretch = 1.0 + elongation * b.y;
                // Its long axis can't reach past the cells searched.
                float size = min(radius * max(1.0 + radiusJitter * (a.z * 2.0 - 1.0), 0.05), 1.0 / sqrt(stretch));
                float angle = turn + (b.x - 0.5) * 6.2831853 * turnJitter;
                vec2 axisA = vec2(cos(angle), sin(angle));
                vec2 axisB = vec2(-axisA.y, axisA.x);
                vec2 offset = p - centre;
                vec2 local = vec2(dot(offset, axisA) / (size * sqrt(stretch)), dot(offset, axisB) * sqrt(stretch) / size);
                float out_ = length(local);
                if (out_ >= 1.0) continue;

                // A dome, flattened on top as far as asked: the rim stays round however flat the top.
                float rim = clamp((1.0 - out_) / max(1.0 - flatness, 1e-3), 0.0, 1.0);
                float dome = sqrt(1.0 - (1.0 - rim) * (1.0 - rim));
                float top = 1.0 - heightJitter * b.z;
                float height = dome * top;
                if (height > bestHeight)
                {
                    bestHeight = height;
                    bestId = synthHash1(wrapped, stoneSeed ^ 0x3c6ef372u);
                    bestOut = out_;
                    bestLocal = local;
                    bestOther = synthHash1(wrapped, stoneSeed ^ 0x7f4a7c15u);
                }
            }
        }
    }

    float covered = bestOut < 1.0 ? 1.0 - smoothstep(0.92, 1.0, bestOut) : 0.0;
    result0 = vec4(bestHeight, bestId, min(bestOut, 1.0), covered);
    result1 = vec4(bestLocal, bestOther, 0.0);
}
