#version 330

in vec3 fragColor;
in vec2 fragUV;
in vec3 positionViewSpace_pass;
in mat4 TBM;
uniform sampler2D albedoMap;
uniform sampler2D normalMap;
uniform sampler2D materialMap;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gPosition;
layout (location = 3) out vec4 gMaterials;

void main() {

	gAlbedo = texture(albedoMap, fragUV).rgba;
	gAlbedo *= vec4(fragColor, 1.0f);

	vec3 normalTangentSpace = texture(normalMap, fragUV).xyz;
	gNormal.xyz = (vec4(normalTangentSpace, 1f)*TBM).xyz;
	gPosition = vec4(positionViewSpace_pass, 0.0f);

	//gNormal.xyz = normalTangentSpace;

	gMaterials = texture(materialMap, fragUV).rgba;
}