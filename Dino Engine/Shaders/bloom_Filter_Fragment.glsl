#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D shadedInput;
uniform sampler2D gMaterials;
uniform float bloomStrength;

void main(void){

	vec3 diffuse = texture(shadedInput, textureCoords).rgb;
	float emission = texture(gMaterials, textureCoords).y;

	float luminance = dot(diffuse, vec3(0.2126, 0.7152, 0.0722));
	vec3 globalBloom = diffuse*luminance*bloomStrength;
	out_Colour.rgb = globalBloom+diffuse*emission;
	out_Colour.a = 1.0f;
}