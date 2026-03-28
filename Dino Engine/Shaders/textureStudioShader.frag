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
	int aInt = int(round(gAlbedo.a * 255.0));
	int alphaBit = aInt & 1;

	alphaBit = 1;
    if (gAlbedo.a < 0.5) {
        alphaBit = 0;
    }
	if (alphaBit == 0) discard;

	gAlbedo.rgb *= fragColor;
	NormalLookupResult normalLookup = lookupNorma(fragUV, textureIndex);
	gNormal.xyz = normalize(normalTBN*(normalLookup.normal));
	if (!gl_FrontFacing) gNormal.xyz *= -1.0;

	gNormal.xyz = (gNormal.xyz*0.5)+0.5;

	
	gNormal.z = normalLookup.SSS;
	gNormal.a = normalLookup.ambient;

	float depth = -positionViewSpace_pass.z/maxDepth;
	gMaterials = lookupMaterial(fragUV, textureIndex).rgba;
	gMaterials.a += 1.0-depth;
	gMaterials.a *= 0.5;
	//gMaterials.a = 1.0;

	
}