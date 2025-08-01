#version 420


in vec3 fragColor;
in vec3 fragNormal;
in float valid;
in float tipFactor;
in vec3 terrainNormal;
in float depth;

uniform vec4 grassMaterial;
uniform float fakeAmbientOcclusionStrength;
uniform float fakeColorAmbientOcclusionStrength;
uniform float groundNormalStrength;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

#include gBufferUtil.glsl

void main() {
	if (valid < 0.5f) discard;

	vec3 color = (fragColor-fakeColorAmbientOcclusionStrength*fragColor)+tipFactor*fragColor*fakeColorAmbientOcclusionStrength;

	gAlbedo = vec4(color, 1.0);
	gNormal = vec4(normalize(fragNormal), (1.0-fakeAmbientOcclusionStrength)+tipFactor*fakeAmbientOcclusionStrength);
	if (!gl_FrontFacing) gNormal.xyz = -gNormal.xyz;
	gNormal.xyz = compressNormal(normalize(gNormal.xyz +terrainNormal*1.5+terrainNormal*groundNormalStrength*depth*0.01f));
	//gNormal.xyz = compressNormal(normalize(gNormal.xyz + terrainNormal*groundNormalStrength));

	gMaterials = vec4(grassMaterial);
}