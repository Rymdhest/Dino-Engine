#version 330

#include procedural/hash.glsl
#include procedural/bricks.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform vec4 albedo;
uniform vec4 material;

uniform vec2 numBricks;
uniform float spacing;
uniform float smoothness;
uniform int returnMode;

void main(void)
{   
    vec3 value = bricks(textureCoords, numBricks, smoothness, spacing);
    
    albedo_out = albedo;
    material_out = material;

    if (returnMode == 0) {
        height_out = value.x;
    }
    if (returnMode == 1) {
        height_out = hash1D(value.yz);
    }

}