#version 330

in vec3 position;
uniform mat4 modelViewProjectionMatrix;

layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;

out float textureIndex;
out vec2 fragUV;

void main(void){
	gl_Position = vec4(position, 1.0)*modelViewProjectionMatrix;
	fragUV = uv;
	textureIndex = materialIndex;
}