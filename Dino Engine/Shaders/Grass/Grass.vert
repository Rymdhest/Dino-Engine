#version 430

// Draws the blades Grass_Generate.comp left in the bound list: one instance per blade, a triangle strip of `segments`
// rows plus the tip, built from gl_VertexID alone (no vertex buffers). Every blade is drawn twice, by Grass_Depth.frag and
// then Grass.frag, and the second time has to land on exactly the same depth: hence invariant.

#include Globals.glsl
#include Grass/GrassField.glsl
#include Grass/GrassBlade.glsl

layout(std430, binding = 1) readonly buffer GrassBlades {
    GrassBlade blades[];
};

uniform int segments;
uniform float minPixelWidth;    // blades are drawn at least this many pixels wide, at partial coverage (0 for off)

invariant gl_Position;

out vec3 fragNormal;
out vec3 fragAcross;
out float fragAcrossCoord;
out float fragT;
out float fragViewDepth;
out float fragCoverage;
out float fragCanopy;
flat out vec3 fragFieldNormal;
flat out vec3 fragColour;
flat out vec3 fragTipColour;
flat out vec3 fragHeadColour;
flat out vec4 fragLook;
flat out int fragKind;
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
    vec2 field = blade.fieldNormal;
    fragFieldNormal = viewRotation * vec3(field.x, sqrt(max(0.0, 1.0 - dot(field, field))), field.y);
    fragColour = blade.colour;
    fragTipColour = blade.tipColour;
    fragHeadColour = blade.headColour;
    fragLook = vec4(blade.tipBleach, blade.rootAO, blade.gloss, blade.sheen);
    fragKind = blade.kind;
    // How far up through the field this point stands: 0 on the ground, 1 at the top of the field around it.
    fragCanopy = (position.y - blade.root.y) / max(blade.canopy, 0.05);

    vec4 positionView = viewMatrix * vec4(position, 1.0);
    fragViewDepth = -positionView.z;
    gl_Position = projectionMatrix * positionView;
}
