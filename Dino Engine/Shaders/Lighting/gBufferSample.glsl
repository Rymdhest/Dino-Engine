#ifndef DINO_GBUFFER_SAMPLE_GLSL
#define DINO_GBUFFER_SAMPLE_GLSL

// The multisampled G-buffer as the light passes read it: one sample per invocation, sample 0 in the per-pixel variant
// and gl_SampleID in the PER_SAMPLE one (see AntiAliasing/Msaa.glsl). MsaaRenderer.BindGeometrySamples binds the
// textures. Needs Globals.glsl, gBufferUtil.glsl and AntiAliasing/Msaa.glsl included first.

uniform sampler2DMS gAlbedo;
uniform sampler2DMS gNormal;
uniform sampler2DMS gMaterials;
uniform sampler2DMS gDepth;
uniform sampler2D ambientOcclusion;

struct GBufferSample
{
    vec3 position;      // view space
    vec4 albedo;        // rgb albedo, a subsurface (light let through)
    vec3 normal;        // view space
    float ambient;      // the material's ambient occlusion, less the screen-space occlusion
    vec4 materials;     // roughness, emission, metallic, height
};

GBufferSample readGBufferSample()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int index = msaaShadingSample();

    GBufferSample g;
    float depth = texelFetch(gDepth, pixel, index).r;
    g.position = ReconstructViewSpacePosition(msaaShadingCoord(), depth, invProjectionMatrix, resolution);
    g.albedo = texelFetch(gAlbedo, pixel, index);
    vec4 normal = texelFetch(gNormal, pixel, index);
    g.normal = unCompressNormal(normal.xyz);
    // The screen-space occlusion is per pixel; it takes away from each sample's own.
    float occlusion = texture(ambientOcclusion, (vec2(pixel) + 0.5) / resolution).r;
    g.ambient = clamp(normal.a - (1.0 - occlusion), 0.0, 1.0);
    g.materials = texelFetch(gMaterials, pixel, index);
    return g;
}

#endif
