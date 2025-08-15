#version 330

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl
#include procedural/gradientNoise.glsl
#include procedural/metric.glsl
#include procedural/cellularNoise.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform vec4 albedo;
uniform vec4 material;
uniform float height;


void main(void)
{   
	albedo_out = albedo;
	material_out = material;
	height_out = height;
}