#version 430

// Grass blades into the multisampled G-buffer. No discard and no textures; the sample mask decides which samples a blade
// widened to the least width covers (Grass.vert).

#include gBufferUtil.glsl
#include Grass/GrassField.glsl
#include AntiAliasing/Msaa.glsl

in vec3 fragNormal;
in vec3 fragAcross;
in vec3 fragGroundNormal;
in float fragAcrossCoord;
in float fragT;
in float fragViewDepth;
in float fragCoverage;
flat in vec3 fragColour;
flat in vec3 fragLook;   // x tip bleach, y root AO, z gloss
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

    // Rounded normals: tilted out towards both edges, on whichever face is seen.
    vec3 face = normalize(fragNormal) * (gl_FrontFacing ? 1.0 : -1.0);
    vec3 normal = normalize(face + normalize(fragAcross) * (u * grassBlade.w));
    // Lower down the blade, and further away, the normal follows the ground's.
    normal = normalize(mix(normal, normalize(fragGroundNormal), mix(0.25 * (1.0 - t), 0.6, far)));

    // Shaded root, the blade's own colour along its body, a bleached tip, a slightly lighter midrib.
    vec3 colour = mix(grassRootColour.rgb, fragColour, smoothstep(0.0, 0.55, t));
    colour = mix(colour, mix(grassTipColour.rgb, grassDryColour.rgb, 0.35), fragLook.x * smoothstep(0.55, 1.0, t));
    colour *= 1.0 + 0.08 * (1.0 - smoothstep(0.0, 0.35, abs(u))) * (1.0 - far);

    float ao = mix(fragLook.y, 1.0, smoothstep(0.0, 0.8, t));
    ao = mix(ao, 1.0, far * 0.5);

    // A glossy streak stretched along the blade's middle.
    float streak = fragLook.z * smoothstep(0.1, 0.45, t) * (1.0 - smoothstep(0.7, 1.0, t)) * (1.0 - abs(u));
    float roughness = grassDryColour.w * mix(1.0, 0.65, streak * (1.0 - far));

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
    int covered = clamp(int(fragCoverage * float(sampleCount) + threshold), 0, sampleCount);
    int rotation = int((fragCoverageSeed >> 16u) % uint(sampleCount));
    int coveredMask = (1 << covered) - 1;
    gl_SampleMask[0] = msaaCoverage((coveredMask << rotation) | (coveredMask >> (sampleCount - rotation)));

    gAlbedo = vec4(colour, grassTipColour.w * mix(0.5, 1.0, t));
    gNormal = vec4(compressNormal(normal), ao);
    // Roughness, emission, metallic, thickness (thin blades let more light through towards the tip).
    gMaterials = vec4(roughness, 0.0, 0.0, mix(0.35, 0.05, t));
}
