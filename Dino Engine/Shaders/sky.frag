#version 420
#include globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

void main(void){

	vec2 ndc = textureCoords * 2.0 - 1.0;
	vec4 clipPos = vec4(ndc, -1.0, 1.0);

	vec4 viewPos = invProjectionMatrix * clipPos;
	viewPos /= viewPos.w;

	vec3 viewDirViewSpace = normalize(viewPos.xyz);

	vec3 viewDirWorldSpace = normalize((invViewMatrix*vec4(viewDirViewSpace, 0.0)).xyz);
	vec3 upNormalWorldSpace = vec3(0, 1, 0);

	out_Colour.rgb = skyColour;

	out_Colour.rgb *= 1-pow(max( dot( viewDirWorldSpace, upNormalWorldSpace), 0.0 ), 0.7)*0.9f;

	
	out_Colour.a = 1.0f;
}