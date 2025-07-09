#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D gNormal;
uniform sampler2D SSAO;

void main(void){
	out_Colour.r = texture(gNormal, textureCoords).a*texture(SSAO, textureCoords).r;
}