#version 330

in vec3 fragColor;
in vec3 fragNormal;
in vec3 fragMaterials;
in vec3 positionViewSpace_pass;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gPosition;
layout (location = 3) out vec4 gMaterials;

void main() {
	gAlbedo = vec4(fragColor, 1.0);
	gNormal = vec4(normalize(fragNormal), 0.0f);
	gPosition = vec4(positionViewSpace_pass, 0.0f);
	gMaterials = vec4(fragMaterials, 0.0f);
}