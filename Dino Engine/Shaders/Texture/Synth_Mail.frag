#version 430

// Mail (Canvas.Mail): rows of round links, each row half a link along from the next and leaning the other way, every link through
// four others, as European four-in-one is made. Each link is a ring of wire, round in section, seen tilted: its near side stands
// higher than its far side, and where links cross, the one standing higher there shows. `links` is links across and rows down.
//
// result0: the height (0 to 1), the link's id, how far round it this is (0 to 1), and how much of a link this is.

#include Texture/SynthCommon.glsl
#include Texture/SynthNoise.glsl

layout (location = 0) out vec4 result0;

uniform ivec2 links;        // Links across the tile, and rows of them down it: the rows must be even, for the lean to tile.
uniform float ring;         // A link's radius, in the spacing of links across.
uniform float wire;         // The wire's radius, in the same.
uniform float lean;         // How far a link tilts, 0 to 1.
uniform int seed;

void main(void)
{
    ivec2 count = max(links, ivec2(1, 2));
    vec2 p = uvAt() * vec2(count);
    uint s = uint(seed);
    float bestHeight = -1.0;
    float bestId = 0.0;
    float bestRound = 0.0;

    ivec2 base = ivec2(floor(p));
    for (int j = -2; j <= 2; j++)
    {
        for (int i = -2; i <= 2; i++)
        {
            int row = base.y + j;
            float shift = (row & 1) == 0 ? 0.0 : 0.5;
            vec2 centre = vec2(float(base.x + i) + 0.5 + shift, float(row) + 0.5);
            vec2 d = p - centre;
            // Rows are closer together than links are wide, so a link is an ellipse once seen leaning.
            float tiltSign = (row & 1) == 0 ? 1.0 : -1.0;
            vec2 inPlane = vec2(d.x, d.y / max(1.0 - 0.35 * lean, 0.2));
            float out_ = length(inPlane);
            float fromWire = abs(out_ - ring);
            if (fromWire >= wire) continue;
            float section = sqrt(1.0 - (fromWire / wire) * (fromWire / wire));
            float standing = 0.5 + 0.5 * tiltSign * lean * (inPlane.x / max(ring, 1e-4));
            float height = 0.55 * section + 0.45 * clamp(standing, 0.0, 1.0);
            if (height > bestHeight)
            {
                bestHeight = height;
                ivec2 cell = wrapInt(ivec2(base.x + i, row), count);
                bestId = synthHash1(cell, s);
                bestRound = atan(inPlane.y, inPlane.x) / 6.2831853 + 0.5;
            }
        }
    }
    result0 = vec4(max(bestHeight, 0.0), bestId, bestRound, bestHeight > 0.0 ? 1.0 : 0.0);
}
