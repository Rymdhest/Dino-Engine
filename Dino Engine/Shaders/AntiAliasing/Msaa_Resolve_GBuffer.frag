#version 420

// Resolves the multisampled G-buffer into the single-sample one that the screen-space passes read (ambient occlusion,
// reflections, water, fog, bloom, depth of field). Each pixel takes its farthest sample: thin foreground detail such as
// grass blades only covers part of a pixel, and water, fog and depth of field want what lies behind it there.
// It also flags the edge pixels, whose samples show different surfaces, for MsaaRenderer to mark in the stencil buffer,
// where the passes that shade from the G-buffer run once per sample.

#include Globals.glsl
#include AntiAliasing/Msaa.glsl

uniform sampler2DMS gAlbedoSamples;
uniform sampler2DMS gNormalSamples;
uniform sampler2DMS gMaterialsSamples;
uniform sampler2DMS gDepthSamples;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;
layout (location = 3) out vec4 gEdge;

// Samples of one surface come from the same fragment, so their G-buffer values match exactly; neighbouring triangles of
// one mesh differ by a little rounding. Anything beyond these differences (in stored 0..1 units) is another surface.
const vec4 ALBEDO_TOLERANCE = vec4(0.02);
const vec4 NORMAL_TOLERANCE = vec4(0.03, 0.03, 0.03, 0.05);
const vec4 MATERIAL_TOLERANCE = vec4(0.03);
// Relative difference in view depth beyond which two samples are different surfaces even where they look alike.
const float DEPTH_TOLERANCE = 0.05;

float viewDepth(float depth)
{
    vec4 view = invProjectionMatrix * vec4(0.0, 0.0, depth * 2.0 - 1.0, 1.0);
    return -view.z / view.w;
}

void main(void)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int count = msaaSampleCount();

    vec4 albedo0 = texelFetch(gAlbedoSamples, pixel, 0);
    vec4 normal0 = texelFetch(gNormalSamples, pixel, 0);
    vec4 materials0 = texelFetch(gMaterialsSamples, pixel, 0);
    float depth0 = texelFetch(gDepthSamples, pixel, 0).r;
    float viewDepth0 = viewDepth(depth0);

    int farthest = 0;
    float farthestDepth = depth0;
    bool edge = false;
    for (int i = 1; i < count; i++)
    {
        float depth = texelFetch(gDepthSamples, pixel, i).r;
        if (depth > farthestDepth)
        {
            farthestDepth = depth;
            farthest = i;
        }
        if (edge) continue;

        float sampleViewDepth = viewDepth(depth);
        edge = any(greaterThan(abs(texelFetch(gAlbedoSamples, pixel, i) - albedo0), ALBEDO_TOLERANCE))
            || any(greaterThan(abs(texelFetch(gNormalSamples, pixel, i) - normal0), NORMAL_TOLERANCE))
            || any(greaterThan(abs(texelFetch(gMaterialsSamples, pixel, i) - materials0), MATERIAL_TOLERANCE))
            || abs(sampleViewDepth - viewDepth0) > DEPTH_TOLERANCE * min(sampleViewDepth, viewDepth0);
    }

    gAlbedo = texelFetch(gAlbedoSamples, pixel, farthest);
    gNormal = texelFetch(gNormalSamples, pixel, farthest);
    gMaterials = texelFetch(gMaterialsSamples, pixel, farthest);
    gEdge = vec4(edge ? 1.0 : 0.0);
    gl_FragDepth = farthestDepth;
}
