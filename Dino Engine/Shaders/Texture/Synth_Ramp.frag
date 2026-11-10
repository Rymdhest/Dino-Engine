#version 430

// A gradient map (Canvas.Ramp): a field read as a place along a row of colours, each stop at a place of its own, blended between in
// linear light. Nature's colour comes in ramps far more often than in mixes of two, which is why this is the main way a material
// gets its colour.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;
uniform int channelA;
uniform vec4 constantA;
uniform int stopCount;
uniform vec4 stops[8];      // rgb a colour, a where it stands, in increasing order.

void main(void)
{
    float t = readInput(inputA, channelA, constantA, texelAt()).x;
    vec3 colour = stops[0].rgb;
    for (int i = 1; i < stopCount; i++)
    {
        float from = stops[i - 1].a;
        float to = stops[i].a;
        float share = clamp((t - from) / max(to - from, 1e-6), 0.0, 1.0);
        colour = mix(colour, stops[i].rgb, share);
    }
    result = vec4(colour, 1.0);
}
