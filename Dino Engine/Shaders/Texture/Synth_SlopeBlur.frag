#version 430

// Smears an image down the slopes of another (Field.SlopeBlur), as water and wear do: each texel follows the second field's
// gradient for `distance` of the tile, in `samples` steps, and takes the average of what it passes over, or the least or the most of
// it. Run on a height down its own slopes it rounds crests and fills hollows the way weather does; run with min it eats away edges.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;      // What's smeared.
uniform int channelA;
uniform vec4 constantA;
uniform sampler2D inputB;      // Whose slopes it follows.
uniform int channelB;
uniform vec4 constantB;
uniform float distance;        // In the tile. Negative goes uphill.
uniform int samples;
uniform int mode;              // 0 average, 1 least, 2 most.

vec2 gradientAt(vec2 uv)
{
    vec2 texel = 1.0 / targetSize;
    float l = sampleInput(inputB, channelB, constantB, uv - vec2(texel.x, 0.0)).x;
    float r = sampleInput(inputB, channelB, constantB, uv + vec2(texel.x, 0.0)).x;
    float d = sampleInput(inputB, channelB, constantB, uv - vec2(0.0, texel.y)).x;
    float u = sampleInput(inputB, channelB, constantB, uv + vec2(0.0, texel.y)).x;
    return vec2(r - l, u - d);
}

void main(void)
{
    vec2 uv = uvAt();
    int count = max(samples, 1);
    float step_ = distance / float(count);
    vec4 first = sampleInput(inputA, channelA, constantA, uv);
    vec4 sum = first;
    vec4 least = first;
    vec4 most = first;
    for (int i = 1; i <= count; i++)
    {
        vec2 g = gradientAt(uv);
        float length_ = length(g);
        if (length_ < 1e-8) break;
        // Down the slope: against the gradient.
        uv -= g / length_ * step_;
        vec4 v = sampleInput(inputA, channelA, constantA, uv);
        sum += v;
        least = min(least, v);
        most = max(most, v);
    }
    result = mode == 1 ? least : (mode == 2 ? most : sum / float(count + 1));
}
