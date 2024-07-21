#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 normal;

uniform sampler2D heightMap;
uniform vec2 texelSize;

void main(void)
{
	float heightL = texture(heightMap, textureCoords + vec2(-texelSize.x, 0.0)).a;
    float heightR = texture(heightMap, textureCoords + vec2(texelSize.x, 0.0)).a;
    float heightD = texture(heightMap, textureCoords + vec2(0.0, -texelSize.y)).a;
    float heightU = texture(heightMap, textureCoords + vec2(0.0, texelSize.y)).a;

    float dx = (heightR - heightL);
    float dy = (heightU - heightD);

    normal.xyz = vec3(-dx, -dy, 1.0);
    normal.y *= -1f;
    normal.xyz = normalize(normal.xyz);
    
}