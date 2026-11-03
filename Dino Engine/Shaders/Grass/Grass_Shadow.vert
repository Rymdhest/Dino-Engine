#version 430

// Grass blades into a shadow map: the same blades as Grass.vert, widened where the light sees them edge-on so their
// shadows keep their width.

#include Grass/GrassField.glsl
#include Grass/GrassBlade.glsl

layout(std430, binding = 1) readonly buffer GrassBlades {
    GrassBlade blades[];
};

uniform int segments;
uniform mat4 shadowViewProjectionMatrix;
uniform vec4 shadowEye;

void main()
{
    GrassBladeData blade = unpackGrassBlade(blades[gl_InstanceID]);

    vec3 position;
    vec3 normal;
    vec3 across;
    float acrossCoord;
    float t;
    float coverage;
    grassBladeVertex(blade, gl_VertexID, segments, shadowEye, 0.0, position, normal, across, acrossCoord, t, coverage);

    gl_Position = shadowViewProjectionMatrix * vec4(position, 1.0);
}
