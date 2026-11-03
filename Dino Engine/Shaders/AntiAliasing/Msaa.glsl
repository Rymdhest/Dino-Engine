#ifndef DINO_MSAA_GLSL
#define DINO_MSAA_GLSL

// Multisample anti-aliasing, shared by everything that renders into or reads the multisampled G-buffer and scene. The
// block is written by MsaaRenderer (binding MsaaRenderer.UniformBinding) whenever its buffers are built; the layout
// must match MsaaRenderer.UploadUniforms.
layout(std140, binding = 8) uniform MsaaData {
    vec4 msaaInfo;               // x samples per pixel, y 1 when alpha-tested cutouts are resolved per sample, zw unused
    vec4 msaaSamplePositions[4]; // xy and zw: where samples 2i and 2i + 1 lie within their pixel (0..1)
};

int msaaSampleCount()
{
    return int(msaaInfo.x);
}

vec2 msaaSamplePosition(int index)
{
    vec4 pair = msaaSamplePositions[index >> 1];
    return (index & 1) == 0 ? pair.xy : pair.zw;
}

// Passes that shade from the multisampled G-buffer are built twice and drawn by MsaaRenderer.DrawSplit: once per pixel
// where all of a pixel's samples show the same surface, shading sample 0 for all of them, and with PER_SAMPLE defined
// once per sample on the edge pixels, whose samples show different surfaces.
int msaaShadingSample()
{
#ifdef PER_SAMPLE
    return gl_SampleID;
#else
    return 0;
#endif
}

// Window coordinates of the sample being shaded, for reconstructing its position from its depth.
vec2 msaaShadingCoord()
{
#ifdef PER_SAMPLE
    return floor(gl_FragCoord.xy) + gl_SamplePosition;
#else
    return floor(gl_FragCoord.xy) + msaaSamplePosition(0);
#endif
}

// The sample mask a fragment shader writes, limited to the samples the triangle covers. The mask written to
// gl_SampleMask should only ever narrow the triangle's coverage, but some drivers (AMD's, at least) take it as the
// whole coverage, which fills every sample of a triangle's edge pixels and undoes the anti-aliasing of its edges.
int msaaCoverage(int mask)
{
    return mask & gl_SampleMaskIn[0];
}

// The samples an alpha-tested surface covers. Each sample passes where the alpha, carried along its screen-space
// gradient from the pixel centre out to the sample, reaches the cutoff, so a cutout's edge falls between the samples
// as precisely as a polygon's edge would (analytic alpha to coverage): leaves get smooth, steady outlines instead of
// stair steps that crawl as they sway. Call it in uniform control flow (it takes derivatives) and write the result to
// gl_SampleMask[0]; 0 means nothing is covered. With per-sample cutouts off it is the plain alpha test.
int alphaTestCoverage(float alpha, float cutoff)
{
    vec2 gradient = vec2(dFdx(alpha), dFdy(alpha));
    int count = msaaSampleCount();
    if (msaaInfo.y < 0.5) return msaaCoverage(alpha >= cutoff ? (1 << count) - 1 : 0);
    int mask = 0;
    for (int i = 0; i < count; i++)
    {
        if (alpha + dot(gradient, msaaSamplePosition(i) - 0.5) >= cutoff) mask |= 1 << i;
    }
    return msaaCoverage(mask);
}

#endif
