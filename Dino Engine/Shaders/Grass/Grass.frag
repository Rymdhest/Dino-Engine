#version 430

// Grass blades into the multisampled G-buffer. No discard and no textures; the sample mask decides which samples a blade
// widened to the least width covers (Grass.vert).
//
// A field is painted in masses, not blades. Each blade runs from a deep shaded root through its own colour to a lit tip,
// but what it shows also depends on how deep in the field it stands (fragCanopy), and its normal leans towards the top of
// the field as one soft surface, so that light falls across swells and waves instead of sparkling on single blades.

#include gBufferUtil.glsl
#include Grass/GrassField.glsl
#include Grass/GrassBlade.glsl
#include AntiAliasing/Msaa.glsl

in vec3 fragNormal;
in vec3 fragAcross;
in float fragAcrossCoord;
in float fragT;
in float fragViewDepth;
in float fragCoverage;
in float fragCanopy;
flat in vec3 fragFieldNormal;
flat in vec3 fragColour;
flat in vec3 fragTipColour;
flat in vec3 fragHeadColour;
flat in vec4 fragLook;   // x tip bleach, y root AO, z gloss, w sheen
flat in int fragKind;
flat in uint fragCoverageSeed;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

void main()
{
    float t = fragT;
    float u = fragAcrossCoord;
    // 0 close by, 1 far out: distant grass shades as one surface instead of sparkling.
    float far = smoothstep(grassField.z, grassField.y, fragViewDepth);
    bool flower = fragKind == GRASS_BLOOM && t > 1.0;

    // Rounded normals: tilted out towards both edges, on whichever face is seen.
    vec3 face = normalize(fragNormal) * (gl_FrontFacing || flower ? 1.0 : -1.0);
    vec3 normal = normalize(face + normalize(fragAcross) * (u * grassBlade.w));
    // Lower down the blade, and further away, the normal follows the top of the field.
    float along = min(t, 1.0);
    float follow = grassPaint.z * mix(1.0, 0.72, along);
    normal = normalize(mix(normal, normalize(fragFieldNormal), mix(follow, max(follow, 0.85), far)));

    // Shaded root, the blade's own colour along its body, a lit tip, a slightly lighter midrib.
    vec3 colour = mix(grassRootOf(fragColour), fragColour, smoothstep(0.0, 0.5, along));
    colour = mix(colour, fragTipColour, fragLook.x * smoothstep(0.38, 1.0, along));
    colour *= 1.0 + 0.1 * (1.0 - smoothstep(0.0, 0.35, abs(u))) * (1.0 - far);

    // A passing gust turns the blades' pale flat sides to the light, and a brightness runs through the field with it.
    float gustLight = fragLook.w * smoothstep(0.1, 0.8, along);
    colour = mix(colour, fragTipColour, gustLight * 0.5) * (1.0 + 0.75 * gustLight);

    // Deep in the field little light gets through, and what does has gone cool and green.
    float height = smoothstep(0.05, 0.95, fragCanopy);
    float shade = mix(1.0 - grassPaint.y, 1.0, height);
    colour *= shade * mix(vec3(0.78, 0.94, 1.0), vec3(1.0), height);

    float ao = mix(fragLook.y, 1.0, height);
    ao = mix(ao, 1.0, far * 0.3);

    // A glossy streak stretched along the blade's middle.
    float streak = fragLook.z * smoothstep(0.1, 0.45, t) * (1.0 - smoothstep(0.7, 1.0, t)) * (1.0 - abs(u));
    float roughness = grassDryColour.w * mix(1.0, 0.6, streak * (1.0 - far));
    float translucency = grassTipColour.w * mix(0.4, 1.0, along);
    float thickness = mix(0.35, 0.05, along);
    float coverage = fragCoverage;

    if (fragKind == GRASS_PLUME)
    {
        // A seed head: fine strands catching the light, warm where they leave the stem and pale at their ends, and thin
        // enough at the edge to see through.
        float head = smoothstep(GRASS_PLUME_START, GRASS_PLUME_START + 0.1, t);
        float strands = 0.8 + 0.2 * sin(t * 90.0 + abs(u) * 9.0) * (1.0 - far);
        vec3 seeds = mix(fragHeadColour * vec3(0.72, 0.56, 0.42), fragHeadColour, smoothstep(0.0, 0.7, abs(u) + (t - GRASS_PLUME_START)));
        colour = mix(colour, seeds * strands, head);
        translucency = mix(translucency, 0.9, head);
        thickness = mix(thickness, 0.03, head);
        roughness = mix(roughness, 0.8, head);
        ao = mix(ao, 1.0, head);
        coverage *= mix(1.0, 1.0 - 0.75 * smoothstep(0.25, 1.0, abs(u)) * (0.65 + 0.35 * sin(t * 90.0)), head);
    }
    else if (flower)
    {
        // Petals around a darker, warmer eye, deepening towards their tips. The disc is cut to their shape by the sample
        // mask, which needs no discard.
        vec2 disc = vec2(u, (t - 1.5) * 2.0);
        float radius = length(disc);
        float petals = 0.74 + 0.26 * abs(cos(atan(disc.y, disc.x) * 2.5 + float(fragCoverageSeed & 7u)));
        vec3 eye = mix(vec3(0.5, 0.26, 0.02), fragHeadColour * 0.4, 0.3);
        colour = mix(eye, fragHeadColour * mix(1.0, 0.75, radius * radius), smoothstep(0.18, 0.36, radius));
        coverage *= radius < petals ? 1.0 : 0.0;
        translucency = 0.35;
        thickness = 0.1;
        roughness = 0.6;
        ao = 1.0;
    }

    // Geometric specular anti-aliasing (Tokuyoshi and Kaplanyan 2019): where the normal turns a lot within a pixel, as
    // across a distant blade, the highlight widens to what the pixel would average to, instead of sparkling.
    vec3 normalDx = dFdx(normal);
    vec3 normalDy = dFdy(normal);
    float kernel = min(0.5 * (dot(normalDx, normalDx) + dot(normalDy, normalDy)), 0.18);
    float alpha = roughness * roughness;    // GGX alpha as lightingCalc.glsl squares it
    roughness = sqrt(sqrt(min(alpha * alpha + kernel, 1.0)));

    // Of the samples a blade's triangles touch, it covers the share fragCoverage says (all of them, unless it was
    // widened to the least width). The count is rounded up or down by the blade's own threshold, so the field comes out
    // as dense as it should on average while each blade looks the same from frame to frame, and the samples it takes
    // are a rotation of the set that differs from blade to blade, so overlapping thin blades don't hide one another.
    int sampleCount = msaaSampleCount();
    // Never quite 0: full coverage interpolates to a hair under 1 and must still round to every sample.
    float threshold = (float(fragCoverageSeed & 0xffffu) + 0.5) * (1.0 / 65536.0);
    int covered = clamp(int(coverage * float(sampleCount) + threshold), 0, sampleCount);
    int rotation = int((fragCoverageSeed >> 16u) % uint(sampleCount));
    int coveredMask = (1 << covered) - 1;
    gl_SampleMask[0] = msaaCoverage((coveredMask << rotation) | (coveredMask >> (sampleCount - rotation)));

    gAlbedo = vec4(colour, translucency);
    gNormal = vec4(compressNormal(normal), ao);
    // Roughness, emission, metallic, thickness (thin blades let more light through towards the tip).
    gMaterials = vec4(roughness, 0.0, 0.0, thickness);
}
