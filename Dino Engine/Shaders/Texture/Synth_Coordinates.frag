#version 430

// Where each texel is in the tile, u and v from 0 to 1 (Canvas.U and Canvas.V). These jump from 1 back to 0 at the edge, so they only
// tile once put through something that repeats a whole number of times across it: a sine of whole frequency, a fract of a whole multiple.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

void main(void)
{
    result = vec4(uvAt(), 0.0, 1.0);
}
