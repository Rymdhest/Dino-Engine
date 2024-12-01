#version 330

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl
#include procedural/gradientNoise.glsl
#include procedural/voronoi.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;

uniform vec4 albedo;
uniform vec4 material;

uniform vec2 scale;
uniform float seed;
uniform float jitter;
uniform float phase;
uniform int returnMode;

void main(void)
{   
    vec3 value = voronoi(textureCoords, scale, jitter, phase, seed)*2.0;

    if (returnMode == 0) {
        albedo_out = albedo;
        material_out = vec4(material.xyz, value.x*material.w);
    }
    if (returnMode == 1) {
        albedo_out = albedo;
        material_out = vec4(material.xyz, hash1D(value.yz)*material.w);
    }

}