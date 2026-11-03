#version 430

// Grass blades into the G-buffer. No discard and no textures, so early depth testing stays on.

#include gBufferUtil.glsl
#include Grass/GrassField.glsl

in vec3 fragNormal;
in vec3 fragAcross;
in vec3 fragGroundNormal;
in float fragAcrossCoord;
in float fragT;
in float fragViewDepth;
flat in vec3 fragColour;
flat in vec3 fragLook;   // x tip bleach, y root AO, z gloss

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

    gAlbedo = vec4(colour, grassTipColour.w * mix(0.5, 1.0, t));
    gNormal = vec4(compressNormal(normal), ao);
    // Roughness, emission, metallic, thickness (thin blades let more light through towards the tip).
    gMaterials = vec4(roughness, 0.0, 0.0, mix(0.35, 0.05, t));
}
