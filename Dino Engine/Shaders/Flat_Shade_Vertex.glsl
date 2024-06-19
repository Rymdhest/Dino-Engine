#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 materials;

out vec3 geoColor;
out vec3 geoMaterials;
out vec3 positionViewSpace;

uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
void main() {
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	positionViewSpace =  (vec4(position, 1.0)*modelViewMatrix).xyz;
	geoColor = color;
	geoMaterials = materials;
}