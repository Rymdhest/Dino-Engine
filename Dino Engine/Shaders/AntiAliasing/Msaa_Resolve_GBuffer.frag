#version 420

// Resolves the multisampled G-buffer into the single-sample one that the light passes and the screen-space passes read
// (ambient occlusion, reflections, water, fog, bloom, depth of field), and sorts the pixels into two kinds.
// Where a pixel's samples lie on one surface, or on surfaces that face much the same way at much the same depth, as the
// blades of a field and the ground under them do, lighting them one by one and averaging the results comes to the same as
// lighting their average, because light is linear in what it falls on. Those pixels get the average, and are shaded once.
// A pixel whose samples are on different things is an edge pixel: it takes its farthest sample, as thin foreground detail
// only covers part of a pixel and water, fog and depth of field want what lies behind it there, and is flagged for
// MsaaRenderer to mark in the stencil buffer, where the passes that shade from the G-buffer run once per sample. A field
// is nearly all thin detail, so telling these two apart is the difference between shading a field once and four times.

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

// Relative difference in view depth beyond which two samples are on different things.
const float DEPTH_TOLERANCE = 0.05;
// The cosine of the angle between two samples' normals past which light can't treat them alike.
const float NORMAL_TOLERANCE = 0.5;
// Difference in stored emission past which one sample glows and the other doesn't, which an average would smear.
const float EMISSION_TOLERANCE = 0.03;

float viewDepth(float depth)
{
    vec4 view = invProjectionMatrix * vec4(0.0, 0.0, depth * 2.0 - 1.0, 1.0);
    return -view.z / view.w;
}

void main(void)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int count = msaaSampleCount();

    vec4 albedo = texelFetch(gAlbedoSamples, pixel, 0);
    vec4 normal = texelFetch(gNormalSamples, pixel, 0);
    vec4 materials = texelFetch(gMaterialsSamples, pixel, 0);
    float depth0 = texelFetch(gDepthSamples, pixel, 0).r;
    float viewDepth0 = viewDepth(depth0);
    vec3 normal0 = normal.xyz * 2.0 - 1.0;
    float emission0 = materials.g;

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

        vec4 sampleNormal = texelFetch(gNormalSamples, pixel, i);
        vec4 sampleMaterials = texelFetch(gMaterialsSamples, pixel, i);
        albedo += texelFetch(gAlbedoSamples, pixel, i);
        normal += sampleNormal;
        materials += sampleMaterials;

        float sampleViewDepth = viewDepth(depth);
        edge = edge
            || dot(sampleNormal.xyz * 2.0 - 1.0, normal0) < NORMAL_TOLERANCE
            || abs(sampleMaterials.g - emission0) > EMISSION_TOLERANCE
            || abs(sampleViewDepth - viewDepth0) > DEPTH_TOLERANCE * min(sampleViewDepth, viewDepth0);
    }

    if (edge)
    {
        gAlbedo = texelFetch(gAlbedoSamples, pixel, farthest);
        gNormal = texelFetch(gNormalSamples, pixel, farthest);
        gMaterials = texelFetch(gMaterialsSamples, pixel, farthest);
    }
    else
    {
        float share = 1.0 / float(count);
        gAlbedo = albedo * share;
        gNormal = normal * share;
        gMaterials = materials * share;
    }
    gEdge = vec4(edge ? 1.0 : 0.0);
    gl_FragDepth = farthestDepth;
}
