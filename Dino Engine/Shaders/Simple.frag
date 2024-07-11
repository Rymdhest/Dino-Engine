#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D blitTexture;

void main(void){
	out_Colour =  texture(blitTexture, textureCoords);
}