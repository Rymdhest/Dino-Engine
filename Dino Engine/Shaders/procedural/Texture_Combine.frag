#version 330


in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 materials_out;

uniform sampler2D albedoTexture1;
uniform sampler2D materialTexture1;

uniform sampler2D albedoTexture2;
uniform sampler2D materialTexture2;

void main(void)
{
	vec4 albedo1 = texture(albedoTexture1, textureCoords);
	vec4 material1 = texture(materialTexture1, textureCoords);

	vec4 albedo2 = texture(albedoTexture1, textureCoords);
	vec4 material2 = texture(materialTexture1, textureCoords);


	albedo_out = albedo1 + albedo2;
	materials_out = material1 + material2;
}