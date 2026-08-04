#version 330


layout(location=0) in vec3 position;
layout(location=4) in mat4 modelMatrix;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

void main(void){
	gl_Position = vec4(position, 1.0)*transpose(modelMatrix)*viewMatrix*projectionMatrix;
}