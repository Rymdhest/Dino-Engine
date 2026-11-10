#version 330

in vec2 fragUV;
in float textureIndex;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;
 
uniform int numberOfMaterials;

#include textureUtil.glsl

void main(void){
	
	// Only a card has holes in it: a tiling material is solid everywhere, and reading it, in every cascade, would buy nothing.
	if (int(textureIndex + 0.5) < numberOfMaterials) return;
	float alpha = lookupAlbedo(fragUV, textureIndex).a;


	if (alpha < 0.5) discard;
}