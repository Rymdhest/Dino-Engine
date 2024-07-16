#version 330
layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;
uniform sampler2D lastTexture;
uniform float delta;
uniform float test;
uniform vec2 center;
uniform float radius;
uniform float power;
uniform float exponent;

void main(void){
	float dist = clamp( distance(center, textureCoords)/radius, 0f, 1f);
	float bend = pow(1f-dist, exponent);
	vec2 direction =normalize(textureCoords-center);
	out_Colour.xy = direction*bend*power;
}
