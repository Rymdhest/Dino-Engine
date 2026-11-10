#version 430

// One direction of a Gaussian blur (Field.Blur, run twice, across and then down), wrapping round the tile. sigma is in texels, and
// the kernel reaches three of them each way.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;
uniform int channelA;
uniform vec4 constantA;
uniform ivec2 direction;
uniform float sigma;

void main(void)
{
    ivec2 at = texelAt();
    int reach = int(ceil(sigma * 3.0));
    float twoSigmaSquared = 2.0 * sigma * sigma;
    vec4 sum = vec4(0.0);
    float total = 0.0;
    for (int i = -reach; i <= reach; i++)
    {
        float w = exp(-float(i * i) / twoSigmaSquared);
        vec4 v = fetchWrapped(inputA, at + direction * i);
        sum += w * (channelA >= 4 ? v : vec4(v[max(channelA, 0)]));
        total += w;
    }
    result = channelA < 0 ? constantA : sum / total;
}
