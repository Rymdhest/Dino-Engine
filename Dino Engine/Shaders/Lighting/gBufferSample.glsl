#ifndef DINO_GBUFFER_SAMPLE_GLSL
#define DINO_GBUFFER_SAMPLE_GLSL

// The multisampled G-buffer as the light passes read it: one sample per invocation, sample 0 in the per-pixel variant
// and gl_SampleID in the PER_SAMPLE one (see AntiAliasing/Msaa.glsl). MsaaRenderer.BindGeometrySamples binds the
// textures. Needs Globals.glsl, gBufferUtil.glsl, Lighting/lightingCalc.glsl and AntiAliasing/Msaa.glsl included first.

uniform sampler2DMS gAlbedo;
uniform sampler2DMS gNormal;
uniform sampler2DMS gMaterials;
uniform sampler2DMS gDepth;
uniform sampler2D ambientOcclusion;

struct GBufferSample
{
    float depth;        // 1 where nothing was drawn
    vec3 position;      // view space
    vec4 albedo;        // rgb albedo, a subsurface (light let through)
    vec3 normal;        // view space
    float ambient;      // the material's ambient occlusion and the screen-space occlusion together
    vec4 materials;     // roughness, emission, metallic, height
};

GBufferSample readGBufferSample()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int index = msaaShadingSample();

    GBufferSample g;
    g.depth = texelFetch(gDepth, pixel, index).r;
    g.position = ReconstructViewSpacePosition(msaaShadingCoord(), g.depth, invProjectionMatrix, resolution);
    g.albedo = texelFetch(gAlbedo, pixel, index);
    vec4 normal = texelFetch(gNormal, pixel, index);
    g.normal = normalize(unCompressNormal(normal.xyz));
    // The screen-space occlusion is per pixel; it darkens each sample's own.
    g.ambient = normal.a * texture(ambientOcclusion, (vec2(pixel) + 0.5) / resolution).r;
    g.materials = texelFetch(gMaterials, pixel, index);
    return g;
}

Surface surfaceOf(GBufferSample g)
{
    Surface s;
    s.albedo = g.albedo.rgb;
    s.normal = g.normal;
    s.view = normalize(-g.position);
    s.roughness = g.materials.r;
    s.metallic = g.materials.b;
    s.translucency = g.albedo.a;
    s.thickness = g.materials.a;
    return s;
}

#endif
