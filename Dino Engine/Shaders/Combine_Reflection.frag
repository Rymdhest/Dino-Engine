#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D sourceColorTexture;
uniform sampler2D reflectionTexture;
uniform sampler2D gMaterials;

void main(void){

	vec3 sourceColor = texture(sourceColorTexture, textureCoords).rgb;
	vec3 reflectionColor = texture(reflectionTexture, textureCoords).rgb;
	float metalicness = texture(gMaterials, textureCoords).b;
	out_Colour.rgb = mix(sourceColor, reflectionColor, metalicness);
	out_Colour.a = 1.0f;
}