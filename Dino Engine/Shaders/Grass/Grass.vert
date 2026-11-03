#version 430

// Draws the blades Grass_Generate.comp left in the bound list: one instance per blade, a triangle strip of `segments`
// rows plus the tip, built from gl_VertexID alone (no vertex buffers).

#include Globals.glsl
#include Grass/GrassField.glsl
#include Grass/GrassBlade.glsl

layout(std430, binding = 1) readonly buffer GrassBlades {
    GrassBlade blades[];
};

uniform int segments;
uniform float minPixelWidth;    // blades are drawn at least this many pixels wide, at partial coverage (0 for off)

out vec3 fragNormal;
out vec3 fragAcross;
out vec3 fragGroundNormal;
out float fragAcrossCoord;
out float fragT;
out float fragViewDepth;
out float fragCoverage;
flat out vec3 fragColour;
flat out vec3 fragLook;
flat out uint fragCoverageSeed;

void main()
{
    GrassBladeData blade = unpackGrassBlade(blades[gl_InstanceID]);

    // Pixels a metre across makes, a metre from the eye.
    float pixelsPerMetre = 0.5 * resolution.y * projectionMatrix[1][1];
    float minHalfWidthPerMetre = 0.5 * minPixelWidth / pixelsPerMetre;

    vec3 position;
    vec3 normal;
    vec3 across;
    grassBladeVertex(blade, gl_VertexID, segments, vec4(viewPosWorld, 1.0), minHalfWidthPerMetre, position, normal, across, fragAcrossCoord, fragT, fragCoverage);
    // The blade's own, from where it grows: the same every frame, whichever slot of the list it lands in.
    fragCoverageSeed = grassHash(floatBitsToUint(blade.root.x) ^ grassHash(floatBitsToUint(blade.root.z)));

    mat3 viewRotation = mat3(viewMatrix);
    fragNormal = viewRotation * normal;
    fragAcross = viewRotation * across;
    vec2 ground = blade.groundNormal;
    fragGroundNormal = viewRotation * vec3(ground.x, sqrt(max(0.0, 1.0 - dot(ground, ground))), ground.y);
    fragColour = blade.colour;
    fragLook = vec3(blade.tipBleach, blade.rootAO, blade.gloss);

    vec4 positionView = viewMatrix * vec4(position, 1.0);
    fragViewDepth = -positionView.z;
    gl_Position = projectionMatrix * positionView;
}
