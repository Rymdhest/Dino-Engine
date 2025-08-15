#version 330



in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform vec4 albedo;
uniform vec4 material;

uniform vec2 scale;
uniform float seed;
uniform float jitter;
uniform float phase;
uniform int returnMode;

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl
#include procedural/gradientNoise.glsl
#include procedural/voronoi.glsl

void main(void)
{   
    vec3 value = voronoi(textureCoords, scale, jitter, phase, seed)*2.0;
    
    albedo_out = albedo;
    material_out = material;

    if (returnMode == 0) {
        height_out = value.x;
    }
    if (returnMode == 1) {
        height_out = hash1D(value.yz);
    }

}