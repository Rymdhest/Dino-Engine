#ifndef DINO_GBUFFER_SAMPLE_GLSL
#define DINO_GBUFFER_SAMPLE_GLSL

// The G-buffer as the light passes read it. The PER_SAMPLE variant reads the multisampled one, a sample per invocation
// (gl_SampleID, see AntiAliasing/Msaa.glsl). The per-pixel variant reads what the resolve left for pixels whose samples
// can be lit as one (Msaa_Resolve_GBuffer.frag), which is much cheaper to read, and sample 0's depth, for where it is.
// MsaaRenderer.BindGeometrySamples binds both. Needs Globals.glsl, gBufferUtil.glsl, Lighting/lightingCalc.glsl and
// AntiAliasing/Msaa.glsl included first.

#ifdef PER_SAMPLE
uniform sampler2DMS gAlbedo;
uniform sampler2DMS gNormal;
uniform sampler2DMS gMaterials;
#else
uniform sampler2D gAlbedo;
uniform sampler2D gNormal;
uniform sampler2D gMaterials;
#endif
uniform sampler2DMS gDepth;
uniform sampler2D ambientOcclusion;

struct GBufferSample
{
    float depth;        // 1 where nothing was drawn
    vec3 position;      // view space
    vec4 albedo;        // rgb albedo, a subsurface (light let through)
    vec3 normal;        // view space
    float ambient;      // the material's ambient occlusion and the screen-space occlusion together
    vec4 materials;     // roughness, emission, metallic, and how much of the sun the surface's own relief lets through (Surface/Relief.glsl)
};

// Where this pixel is, which is all a local light needs to know if it reaches it. Finish the job with finishGBufferSample.
GBufferSample locateGBufferSample()
{
    GBufferSample g;
    g.depth = texelFetch(gDepth, ivec2(gl_FragCoord.xy), msaaShadingSample()).r;
    g.position = ReconstructViewSpacePosition(msaaShadingCoord(), g.depth, invProjectionMatrix, resolution);
    return g;
}

void finishGBufferSample(inout GBufferSample g)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int index = msaaShadingSample();

    g.albedo = texelFetch(gAlbedo, pixel, index);
    vec4 normal = texelFetch(gNormal, pixel, index);
    g.normal = normalize(unCompressNormal(normal.xyz));
    // The screen-space occlusion is per pixel; it darkens each sample's own.
    g.ambient = normal.a * texture(ambientOcclusion, (vec2(pixel) + 0.5) / resolution).r;
    g.materials = texelFetch(gMaterials, pixel, index);
}

GBufferSample readGBufferSample()
{
    GBufferSample g = locateGBufferSample();
    finishGBufferSample(g);
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
    return s;
}

#endif
