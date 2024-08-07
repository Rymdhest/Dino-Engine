#version 330

layout(location=0) in vec3 position;

uniform mat4 modelViewProjectionMatrix;

void main(void){
	gl_Position = vec4(position, 1.0)*modelViewProjectionMatrix;
}