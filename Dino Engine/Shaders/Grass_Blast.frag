#version 330
layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;
uniform sampler2D lastTexture;
uniform float delta;
uniform float test;
uniform vec2 center;
uniform float radius;
uniform vec2 grassPatchSize;
uniform float power;
uniform float exponent;

void main(void){
	float dist = distance(center, textureCoords*grassPatchSize);
	float bend = clamp((radius-dist)/radius, 0f, 1f);
	vec2 direction =normalize(textureCoords-center/grassPatchSize);
	out_Colour.xy = direction*bend*power;
}
