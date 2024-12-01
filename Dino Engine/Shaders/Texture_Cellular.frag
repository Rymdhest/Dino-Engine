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

uniform vec2 scale;
uniform float seed;
uniform float jitter;
uniform float phase;
uniform int metric;
uniform bool rigged;

void main(void)
{   

    uint metricUint = 3u;
    if (metric == 0) {
        metricUint = 0u;
    }
        if (metric == 1) {
        metricUint = 1u;
    }
        if (metric == 2) {
        metricUint = 2u;
    }

    vec2 value = cellularNoise(textureCoords, scale, jitter, phase, metricUint, seed);

    albedo_out = albedo;

    if (rigged) {
        material_out = vec4(material.xyz, value.y*material.w);
    } else {
        material_out = vec4(material.xyz, value.x*material.w);
    }


}