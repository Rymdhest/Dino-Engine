#version 420
#include globals.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl

// The sky itself, for the few views that see any: the map everything else reflects (Weather/Sky_Map.frag).

in vec2 textureCoords;

layout (location = 0) out vec4 out_Colour;

void main(void){

	vec2 ndc = textureCoords * 2.0 - 1.0;
	vec4 clipPos = vec4(ndc, -1.0, 1.0);

	vec4 viewPos = invProjectionMatrix * clipPos;
	viewPos /= viewPos.w;

	vec3 viewDirViewSpace = normalize(viewPos.xyz);

	vec3 viewDirWorldSpace = normalize((invViewMatrix*vec4(viewDirViewSpace, 0.0)).xyz);

	out_Colour.rgb = skyEnvironment(viewDirWorldSpace, 0.0, skyColour * weatherSunColour.w);
	out_Colour.a = 1.0f;
}