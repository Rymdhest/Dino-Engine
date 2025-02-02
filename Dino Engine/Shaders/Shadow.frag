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
	
	float opacity = lookupAlbedo(fragUV, textureIndex).a;

	if (opacity < 0.5) discard;
}