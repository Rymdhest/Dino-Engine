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

uniform vec4 albedo;
uniform vec4 material;
uniform vec2 frequenzy;
#define PI 3.1415926538

void main(void)
{   
	albedo_out = albedo;
	material_out = material;

	float x =cos(textureCoords.x*frequenzy.x*PI*2.0)*0.5+0.5;
	float y =cos(textureCoords.y*frequenzy.y*PI*2.0)*0.5+0.5;

	material_out.w = x*y;
}