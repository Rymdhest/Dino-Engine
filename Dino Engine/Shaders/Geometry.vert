#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 materials;
layout(location=3) in vec3 normal;

out vec3 fragColor;
out vec3 positionViewSpace_pass;
out vec3 fragMaterials;
out vec3 fragNormal;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

void main() {
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	positionViewSpace_pass =  (vec4(position, 1.0)*modelViewMatrix).xyz;
	fragColor = color;
	fragMaterials = materials;
	fragNormal = (vec4(normal, 1.0f)*normalModelViewMatrix).xyz;
}