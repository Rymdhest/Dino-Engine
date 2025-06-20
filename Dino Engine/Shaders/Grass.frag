#version 330


in vec3 fragColor;
in vec3 fragNormal;
in vec3 fragMaterials;
in float valid;
in float tipFactor;
layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;



void main() {
	if (valid < 0.5f) discard;
	gAlbedo = vec4(fragColor, 1.0);
	gNormal = vec4(normalize(fragNormal), 0.5f+tipFactor*0.5f);
	gMaterials = vec4(fragMaterials, 0.0f);
}