#version 330

// A body, flat, for the line round it to be found from. Nothing about how it's lit matters here.

layout (location = 0) out vec4 out_Colour;

uniform vec4 colour;

in float textureIndex;
in vec2 fragUV;

void main(void)
{
	out_Colour = colour;
}
