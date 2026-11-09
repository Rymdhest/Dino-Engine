#version 430

// Objects into voxels (GI_Voxelize.frag): a model as it stands, in the world, with no wind on it.

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in mat4 modelMatrix;

out VS_OUT
{
    vec3 world;
    vec3 colour;
    vec2 uv;
    float material;
} o;

uniform int plant;

// Plant.glsl's plantTint, which can't be included here without the wind that comes with it.
vec3 giPlantTint(vec2 at)
{
    float bright = fract(sin(dot(at, vec2(12.9898, 78.233))) * 43758.5453);
    float warm = fract(sin(dot(at, vec2(39.3462, 11.1351))) * 24634.6345) - 0.5;
    return (0.9 + 0.2 * bright) * vec3(1.0 + 0.16 * warm, 1.0, 1.0 - 0.22 * warm);
}

void main()
{
    vec4 world = modelMatrix * vec4(position, 1.0);
    o.world = world.xyz;
    o.colour = plant == 1 ? color * giPlantTint(modelMatrix[3].xz) : color;
    o.uv = uv;
    o.material = materialIndex;
    gl_Position = world;
}
