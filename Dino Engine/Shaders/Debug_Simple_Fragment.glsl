#version 330

layout (location = 0) out vec4 out_Colour;
uniform vec3 color;

void main(void){
	out_Colour = vec4(color, 1.0f);
}