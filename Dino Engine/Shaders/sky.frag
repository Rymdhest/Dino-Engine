#version 420
#include globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

void main(void){

	vec2 uv = ((textureCoords*2)-1);
	uv = (uv*resolution)/resolution.y;

	vec3 viewDir = normalize((viewMatrix*vec4(uv, -1, 1.0f)).xyz);
	vec3 upNormalViewSpace = vec3(0, 1, 0.0f);

	out_Colour.rgb = skyColor;

	out_Colour.rgb *= 1-pow(max( dot( viewDir, upNormalViewSpace), 0.0 ), 1)*0.9f;

	
	out_Colour.a = 1.0f;
}