#version 430

// Bricks, blocks, boards and tiles (Canvas.Bricks): `rows` courses across the tile, each of `perRow` pieces, every course shifted
// by `bond` of a piece from the last and by a random share of one more, and the pieces of a course of random lengths if asked. A
// course's lengths and shift are hashed from its number once wrapped, so the pattern tiles both ways. Distances are in the tile,
// whose sides are the same length in the world, so a joint is the same width along a course and across it.
//
// result0: distance to the edge of the piece (negative in the joint), the piece's id, how far along it and across it (0 to 1).
// result1: the piece's length and height in the tile, its course's id, and its place in the course.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;
layout (location = 1) out vec4 result1;

uniform int rows;
uniform int perRow;
uniform int axis;           // 0: courses run along u, as in a wall. 1: along v, as boards of a floor running up the tile.
uniform float bond;         // How far each course is shifted from the last, in pieces: a half for a running bond.
uniform float rowJitter;    // A random shift of each course besides, in pieces.
uniform float lengthJitter; // 0 for pieces all the same length, towards 1 for pieces of very different lengths.
uniform float gap;          // The width of a joint, in the tile.
uniform int seed;

const int MOST_PER_ROW = 64;

float boxDistance(vec2 p, vec2 halfSize)
{
    vec2 d = abs(p) - halfSize;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

void main(void)
{
    vec2 uv = uvAt();
    if (axis == 1) uv = uv.yx;
    int courses = max(rows, 1);
    int pieces = clamp(perRow, 1, MOST_PER_ROW);
    uint s = uint(seed);

    float rowPosition = uv.y * float(courses);
    int row = int(floor(rowPosition));
    int course = wrapInt(ivec2(row, 0), ivec2(courses, 1)).x;
    float across = rowPosition - float(row);
    vec3 courseRandom = synthHash3(ivec2(course, 0), s);

    float shift = (bond * float(course) + rowJitter * courseRandom.x) / float(pieces);
    float along = fract(uv.x - shift);

    // Where this course's joints fall, from 0 to 1.
    float left = 0.0;
    float right = 1.0;
    int piece = 0;
    if (lengthJitter <= 0.0)
    {
        piece = min(int(floor(along * float(pieces))), pieces - 1);
        left = float(piece) / float(pieces);
        right = float(piece + 1) / float(pieces);
    }
    else
    {
        float total = 0.0;
        for (int k = 0; k < pieces; k++) total += 1.0 + lengthJitter * (synthHash1(ivec2(course, k + 1), s ^ 0x2545F491u) * 2.0 - 1.0);
        float start = 0.0;
        for (int k = 0; k < pieces; k++)
        {
            float size = (1.0 + lengthJitter * (synthHash1(ivec2(course, k + 1), s ^ 0x2545F491u) * 2.0 - 1.0)) / total;
            if (along < start + size || k == pieces - 1)
            {
                piece = k;
                left = start;
                right = start + size;
                break;
            }
            start += size;
        }
    }

    float width = right - left;
    float height = 1.0 / float(courses);
    vec2 local = vec2(along - 0.5 * (left + right), (across - 0.5) * height);
    float edge = -boxDistance(local, 0.5 * vec2(width, height) - 0.5 * gap);

    float id = synthHash1(ivec2(course, piece), s ^ 0x9E3779B9u);
    result0 = vec4(edge, id, (along - left) / width, across);
    result1 = vec4(width, height, courseRandom.y, float(piece) / float(pieces));
}
