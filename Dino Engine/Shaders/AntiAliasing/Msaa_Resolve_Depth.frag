#version 420

// The multisampled depth into the single-sample G-buffer after the water has drawn into it: each pixel's farthest
// sample, as the G-buffer resolve picks. Colour writes are off; depth is written with the test set to always pass.

#include AntiAliasing/Msaa.glsl

uniform sampler2DMS depthSamples;

void main(void)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    float depth = texelFetch(depthSamples, pixel, 0).r;
    for (int i = 1; i < msaaSampleCount(); i++)
    {
        depth = max(depth, texelFetch(depthSamples, pixel, i).r);
    }
    gl_FragDepth = depth;
}
