#version 420


in vec3 fragColor;
in vec3 fragNormal;
in float valid;
in float tipFactor;
in vec3 terrainNormal;
in float depth;
in vec2 fragUV;

uniform vec4 grassMaterial;
uniform float fakeAmbientOcclusionStrength;
uniform float fakeColorAmbientOcclusionStrength;
uniform float groundNormalStrength;
uniform float groundNormalStrengthFlat;


uniform int numberOfMaterials;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

uniform int textureIndex;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

#include gBufferUtil.glsl
#include textureUtil.glsl

void main() {
	if (valid < 0.5f) discard;

	vec3 color = (fragColor-fakeColorAmbientOcclusionStrength*fragColor)+tipFactor*fragColor*fakeColorAmbientOcclusionStrength;
	gAlbedo = vec4(color, 1.0);
	gNormal = vec4(normalize(fragNormal), (1.0-fakeAmbientOcclusionStrength)+tipFactor*fakeAmbientOcclusionStrength);
	if (!gl_FrontFacing) gNormal.xyz = -gNormal.xyz;
	gNormal.xyz = compressNormal(normalize(gNormal.xyz +terrainNormal*groundNormalStrengthFlat+terrainNormal*groundNormalStrength*depth*0.01f));
	//gNormal.xyz = compressNormal(normalize(gNormal.xyz + terrainNormal*groundNormalStrength));

	gMaterials = vec4(grassMaterial);
	
	gAlbedo = gAlbedo*lookupAlbedo(fragUV, textureIndex);
}