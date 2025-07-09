#version 330

#include gBufferUtil.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D colorTexture;
uniform sampler2D blurTexture;
uniform sampler2D gDepth;

uniform vec2 resolution;
uniform mat4 inverseProjection;

uniform float range;
uniform float focusDistance;

void main(void){

	vec3 blur = texture(blurTexture, textureCoords).rgb;
	vec3 sharp = texture(colorTexture, textureCoords).rgb;

	vec3 viewPosition = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, inverseProjection, resolution);

	float depth = -viewPosition.z;


	float bluryness = clamp(abs(depth-focusDistance)*range, 0, 1);

	out_Colour.rgb = mix(sharp, blur, bluryness);
}