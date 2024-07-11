#version 330

layout (location = 0) out vec4 out_Colour;
uniform vec4 color;

void main(void){
	out_Colour =  color;
}