#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 normal;

uniform sampler2D heightMap;
uniform vec2 texelSize;
uniform float normalFlatness;

void main(void)
{
	float heightL = texture(heightMap, textureCoords + vec2(-texelSize.x, 0.0)).a;
    float heightR = texture(heightMap, textureCoords + vec2(texelSize.x, 0.0)).a;
    float heightD = texture(heightMap, textureCoords + vec2(0.0, -texelSize.y)).a;
    float heightU = texture(heightMap, textureCoords + vec2(0.0, texelSize.y)).a;
    float height = texture(heightMap, textureCoords).a;
    float dx = (heightR - heightL);
    float dy = (heightU - heightD);
    vec3 normalTemp = vec3(-dx, -dy, normalFlatness);
    //normalTemp.y *= -1;
    normalTemp = normalize(normalTemp);
    normal.rgb = normalTemp*0.5f+0.5f;


    normal.a = 0.2f+height*0.8f;
    
}