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
// The occlusion's depth, metres along the view and negative on a canopy (Occlusion/Occlusion.glsl), for which pixel's screen-space terms a sample takes.
uniform sampler2D occlusionDepth;

struct GBufferSample
{
    float depth;        // 1 where nothing was drawn
    vec3 position;      // view space
    vec4 albedo;        // rgb albedo, a its subsurface scattering, kind and amount (Lighting/SubsurfaceKinds.glsl)
    vec3 normal;        // view space
    float ambient;      // the material's ambient occlusion and the screen-space occlusion together
    vec4 materials;     // roughness, emission, metallic, and how much of the sun the surface's own relief lets through (Surface/Relief.glsl)
    ivec2 screenPixel;  // the pixel whose screen-space terms, the occlusion and the sun's shadows, this sample takes
    float screenApart;  // how far that pixel's depth is from this sample's, as a share of the sample's: 0 on a pixel whose samples agree
};

// Which pixel's screen-space terms a sample takes. They're worked out a pixel at a time from the resolved depth, which on an edge pixel is its farthest
// sample: a sample on something nearer takes them from whichever of its pixel and the eight round it is at its own depth, so that the ground's
// occlusion or shadow doesn't darken the edge of what stands over it. On a pixel whose samples agree, it's the pixel. apart is how far the one it
// takes is from the sample's depth, as a share of it.
ivec2 screenTermPixel(ivec2 pixel, float distance, out float apart)
{
#ifdef PER_SAMPLE
    ivec2 last = textureSize(occlusionDepth, 0) - 1;
    ivec2 best = pixel;
    float bestApart = abs(abs(texelFetch(occlusionDepth, pixel, 0).r) - distance);
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0) continue;
            ivec2 at = clamp(pixel + ivec2(x, y), ivec2(0), last);
            float away = abs(abs(texelFetch(occlusionDepth, at, 0).r) - distance);
            // The pixel itself keeps it unless a neighbour is clearly nearer the sample's depth.
            if (away < bestApart * 0.9 - 1e-4 * distance)
            {
                bestApart = away;
                best = at;
            }
        }
    }
    apart = bestApart / max(distance, 1e-4);
    return best;
#else
    apart = 0.0;
    return pixel;
#endif
}

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
    g.screenPixel = screenTermPixel(pixel, -g.position.z, g.screenApart);
    g.ambient = normal.a * texelFetch(ambientOcclusion, g.screenPixel, 0).r;
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
    s.scatterKind = subsurfaceKind(g.albedo.a);
    s.scatter = subsurfaceAmount(g.albedo.a);
    s.scaleBias = specularScaleBias(s.roughness, dot(s.normal, s.view));
    return s;
}

#endif
