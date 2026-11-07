#version 430

// Which samples each blade owns, before anything is shaded (GrassRenderer). Writes depth and nothing else.
// Of the samples a blade's triangles touch, it covers the share fragCoverage says (all of them, unless it was widened to the
// least width). The count is rounded up or down by the blade's own threshold, so the field comes out as dense as it should
// on average while each blade looks the same from frame to frame, and the samples it takes are a rotation of the set that
// differs from blade to blade, so overlapping thin blades don't hide one another.

#include Grass/GrassField.glsl
#include Grass/GrassBlade.glsl
#include AntiAliasing/Msaa.glsl

in float fragAcrossCoord;
in float fragT;
in float fragCoverage;
flat in int fragKind;
flat in uint fragCoverageSeed;

void main()
{
    float t = fragT;
    float u = fragAcrossCoord;
    float coverage = fragCoverage;

    if (fragKind == GRASS_PLUME)
    {
        // A seed head is thin enough at the edge to see through.
        float head = smoothstep(GRASS_PLUME_START, GRASS_PLUME_START + 0.1, t);
        coverage *= mix(1.0, 1.0 - 0.75 * smoothstep(0.25, 1.0, abs(u)) * (0.65 + 0.35 * sin(t * 90.0)), head);
    }
    else if (fragKind == GRASS_BLOOM && t > 1.0)
    {
        // A flower's disc is cut to the shape of its petals, which needs no discard.
        vec2 disc = vec2(u, (t - 1.5) * 2.0);
        float petals = 0.74 + 0.26 * abs(cos(atan(disc.y, disc.x) * 2.5 + float(fragCoverageSeed & 7u)));
        coverage *= length(disc) < petals ? 1.0 : 0.0;
    }

    int sampleCount = msaaSampleCount();
    // Never quite 0: full coverage interpolates to a hair under 1 and must still round to every sample.
    float threshold = (float(fragCoverageSeed & 0xffffu) + 0.5) * (1.0 / 65536.0);
    int covered = clamp(int(coverage * float(sampleCount) + threshold), 0, sampleCount);
    int rotation = int((fragCoverageSeed >> 16u) % uint(sampleCount));
    int coveredMask = (1 << covered) - 1;
    gl_SampleMask[0] = msaaCoverage((coveredMask << rotation) | (coveredMask >> (sampleCount - rotation)));
}
