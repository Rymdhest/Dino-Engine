#version 420


layout(location=0) in vec3 position;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;

out float textureIndex;
out vec2 fragUV;
uniform mat4 viewpPojectionMatrix;

void main(void){
	gl_Position = viewpPojectionMatrix * modelMatrix*vec4(position, 1.0);
	fragUV = uv;
	textureIndex = materialIndex;
}