#version 430

// Moves an image by a field of offsets (Field.Warp): each texel reads the image from where the offsets point, in the tile, filtered
// and wrapping. The offsets are two channels centred on 0.5, as Canvas.Noise makes them, so 0.5 moves nothing and a deviation of 1/6
// moves a point by a sixth of `amount`. Warping every channel of an image by the same offsets keeps them in step.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;      // What's moved.
uniform int channelA;
uniform vec4 constantA;
uniform sampler2D inputB;      // How far, in its first two channels.
uniform vec4 constantB;
uniform int channelB;          // 4 for an image with both, or the first of two consecutive channels.
uniform float amount;

void main(void)
{
    ivec2 at = texelAt();
    vec4 offsets = texelFetch(inputB, at, 0);
    vec2 offset = channelB >= 4 ? offsets.xy : (channelB < 0 ? constantB.xy : vec2(offsets[channelB], offsets[min(channelB + 1, 3)]));
    vec2 uv = uvAt() + (offset - 0.5) * 2.0 * amount;
    result = sampleInput(inputA, channelA, constantA, uv);
}
