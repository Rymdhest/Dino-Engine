#version 330
in vec3 fragColor;
in vec2 fragUV;
in mat3 normalTBN;
in float textureIndex;
in vec3 positionViewSpace_pass;

uniform int numberOfMaterials;
uniform float maxDepth;
uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

#include textureUtil.glsl

void main() {

	gAlbedo = lookupAlbedo(fragUV, textureIndex);
	if (gAlbedo.a < 0.5) discard;

	gAlbedo *= vec4(fragColor, 1.0);
	vec4 normalTangentSpace = lookupNorma(fragUV, textureIndex);
	normalTangentSpace.xyz =  (normalTangentSpace.xyz*2.0)-1.0;
	gNormal.xyz = normalTangentSpace.xyz*normalTBN;
	gNormal.xyz = (gNormal.xyz+1.0)/2.0;


	gNormal.a = normalTangentSpace.a;

	gMaterials = lookupMaterial(fragUV, textureIndex).rgba;
	gMaterials.a *= (positionViewSpace_pass.z)/maxDepth;
	//gMaterials.a = 1.0;

	//gAlbedo.rgb = vec3(gMaterials.a);

}