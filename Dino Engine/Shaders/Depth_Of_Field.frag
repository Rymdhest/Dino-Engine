#version 420

#include gBufferUtil.glsl
#include globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D colorTexture;
uniform sampler2D blurTexture;
uniform sampler2D gDepth;

uniform float range;
uniform float focusDistance;
uniform float landscape;    // Metres from the lens within which things are soft, with all past them sharp, for a view of a whole land (RenderEngine.LandscapeLens), or 0.

const float LANDSCAPE_SOFTEST = 0.85;   // How soft what's right at the lens is then.

void main(void){

	vec3 blur = texture(blurTexture, textureCoords).rgb;
	vec3 sharp = texture(colorTexture, textureCoords).rgb;

	vec3 viewPosition = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution);

	float depth = -viewPosition.z;


	float bluryness = clamp(abs(depth-focusDistance)*range, 0, 1);
	if (landscape > 0.0)
	{
		float near = clamp(1.0 - depth / landscape, 0.0, 1.0);
		bluryness = near * near * LANDSCAPE_SOFTEST;
	}

	out_Colour.rgb = mix(sharp, blur, bluryness);
}