#version 330

layout(location=0) in vec3 position;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;

uniform mat4 modelViewProjectionMatrix;
out float textureIndex;
out vec2 fragUV;

void main(void){
	gl_Position = modelViewProjectionMatrix*vec4(position, 1.0);
	fragUV = uv;
	textureIndex = materialIndex;
}