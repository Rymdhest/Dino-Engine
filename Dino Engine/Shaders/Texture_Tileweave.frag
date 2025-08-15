#version 330

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl
#include procedural/patterns.glsl


in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform vec2 scale;
uniform int count;
uniform float width;
uniform float smoothness;

uniform vec4 albedo;
uniform vec4 material;

void main(void)
{   


    float value = tileWeave(textureCoords, scale, count, width, smoothness).x;
    value = value*0.5+0.5;

    albedo_out = albedo;
    material_out = material;
    height_out = 1.0-value;

}