#version 330

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl
#include procedural/perlinNoise.glsl
#include procedural/fbm.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;

uniform vec4 albedo;
uniform vec4 material;

uniform vec2 startFrequenzy;
uniform int octaves;
uniform bool rigged;
uniform float seed;
uniform float exponent;
uniform float amplitudePerOctave;
uniform int frequenzyPerOctave;

void main(void)
{   
    
    float shift = 1.5;
    float axialShift = 3.1415/2.0;
    float gain = amplitudePerOctave;
    float lacunarity = frequenzyPerOctave;
    uint mode = 2u;
    if (rigged) mode = 1u;
    float factor = exponent;
    float octaveFactor = 1.0;
    float offset = 0.0;
   
    float value = fbmPerlin(textureCoords, startFrequenzy, octaves, shift,axialShift, gain, lacunarity, mode, factor, offset, octaveFactor, seed);
    //float value = fbm(      textureCoords, startFrequenzy, octaves, shift, axialShift, gain, lacunarity, factor, seed);

    albedo_out = albedo;
    material_out = vec4(material.xyz, value*material.w);

}