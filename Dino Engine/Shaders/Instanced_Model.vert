#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 materials;
layout(location=3) in vec3 normal;
layout(location=4) in mat4 modelMatrix;

out vec3 fragColor;
out vec3 positionViewSpace_pass;
out vec3 fragMaterials;
out vec3 fragNormal;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

void main() {
	mat4 modelViewMatrix =transpose(modelMatrix)*viewMatrix;
	mat4 modelViewProjectionMatrix = modelViewMatrix*projectionMatrix;
	mat4 normalModelViewMatrix = (inverse(modelMatrix));
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	positionViewSpace_pass =  (vec4(position, 1.0)*modelViewMatrix).xyz;
	fragColor = color;
	fragMaterials = materials;
	fragNormal = (vec4(normal, 1.0f)*normalModelViewMatrix).xyz;
}