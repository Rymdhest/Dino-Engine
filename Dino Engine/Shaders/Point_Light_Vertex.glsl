#version 330

layout(location=0) in vec3 positions;
layout(location=1) in vec3 color;
layout(location=2) in vec3 materials;

uniform mat4 TransformationMatrix;
uniform mat4 projectionMatrix;
uniform mat4 viewMatrix;

void main() {
	gl_Position =  vec4(positions, 1.0)*TransformationMatrix*viewMatrix*projectionMatrix;
}