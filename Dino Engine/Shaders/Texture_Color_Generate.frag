#version 330

in vec2 textureCoords;
uniform sampler2D heightMap;

layout (location = 0) out vec4 color;

void main(void)
{
	float height = texture(heightMap, textureCoords).a;
	height = height*0.5f+0.5f;
	color = vec4(height, height, height, 1.0f);
}