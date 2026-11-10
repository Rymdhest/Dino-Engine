#version 430

// The least or the most of an image within a disc (Field.Erode, Field.Dilate), wrapping round the tile: eroding a height eats its
// crests back and chips its edges, dilating it spreads them. The radius is in texels.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;
uniform int channelA;
uniform vec4 constantA;
uniform float radius;
uniform int mode;              // 0 least, 1 most.

void main(void)
{
    ivec2 at = texelAt();
    int reach = int(ceil(radius));
    float radiusSquared = radius * radius;
    vec4 v0 = fetchWrapped(inputA, at);
    vec4 best = channelA >= 4 ? v0 : vec4(v0[max(channelA, 0)]);
    for (int j = -reach; j <= reach; j++)
    {
        for (int i = -reach; i <= reach; i++)
        {
            if (float(i * i + j * j) > radiusSquared) continue;
            vec4 v = fetchWrapped(inputA, at + ivec2(i, j));
            v = channelA >= 4 ? v : vec4(v[max(channelA, 0)]);
            best = mode == 0 ? min(best, v) : max(best, v);
        }
    }
    result = channelA < 0 ? constantA : best;
}
