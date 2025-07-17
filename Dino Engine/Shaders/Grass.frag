#version 420


in vec3 fragColor;
in vec3 fragNormal;
in float valid;
in float tipFactor;

uniform vec4 grassMaterial;
uniform float fakeAmbientOcclusionStrength;
uniform float fakeColorAmbientOcclusionStrength;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

void main() {
	if (valid < 0.5f) discard;

	vec3 color = (fragColor-fakeColorAmbientOcclusionStrength*fragColor)+tipFactor*fragColor*fakeColorAmbientOcclusionStrength;

	gAlbedo = vec4(color, 1.0);
	gNormal = vec4(normalize(fragNormal), (1.0-fakeAmbientOcclusionStrength)+tipFactor*fakeAmbientOcclusionStrength);
	gMaterials = vec4(grassMaterial);
}