#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D colorTexture;
uniform sampler2D blurTexture;
uniform sampler2D positionTexture;

uniform float range;
uniform float focusDistance;

void main(void){

	vec3 blur = texture(blurTexture, textureCoords).rgb;
	vec3 sharp = texture(colorTexture, textureCoords).rgb;
	float depth = -texture(positionTexture, textureCoords).z;


	float bluryness = clamp(abs(depth-focusDistance)*range, 0f, 1f);

	out_Colour.rgb = mix(sharp, blur, bluryness);
}