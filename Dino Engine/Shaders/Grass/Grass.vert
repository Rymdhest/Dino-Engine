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

out vec3 fragNormal;
out vec3 fragAcross;
out vec3 fragGroundNormal;
out float fragAcrossCoord;
out float fragT;
out float fragViewDepth;
flat out vec3 fragColour;
flat out vec3 fragLook;

void main()
{
    GrassBladeData blade = unpackGrassBlade(blades[gl_InstanceID]);

    vec3 position;
    vec3 normal;
    vec3 across;
    grassBladeVertex(blade, gl_VertexID, segments, vec4(viewPosWorld, 1.0), position, normal, across, fragAcrossCoord, fragT);

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
