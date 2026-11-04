#version 420
#include globals.glsl
in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D HDRcolorTexture;

uniform float exposure;
uniform float gamma;
uniform float dithering;

// The ACES reference rendering and output transforms as Stephen Hill fitted them, with their colour space conversions.
// The fit's small negative offset is left out: it clips the dimmest light to black, and the edge of every pool of
// lamplight then shows as a hard, frayed line. Without it black is still exactly black.
const mat3 ACES_INPUT = mat3(
	0.59719, 0.07600, 0.02840,
	0.35458, 0.90834, 0.13383,
	0.04823, 0.01566, 0.83777);
const mat3 ACES_OUTPUT = mat3(
	 1.60475, -0.10208, -0.00327,
	-0.53108,  1.10813, -0.07276,
	-0.07367, -0.00605,  1.07602);

vec3 acesFitted(vec3 colour) {
	colour = ACES_INPUT * colour;
	vec3 a = colour * (colour + 0.0245786);
	vec3 b = colour * (0.983729 * colour + 0.4329510) + 0.238081;
	return clamp(ACES_OUTPUT * (a / b), 0.0, 1.0);
}

float random(vec2 coords) {
   return fract(sin(dot(coords.xy, vec2(12.9898,78.233))) * 43758.5453);
}

vec3 applyDithering(vec3 inputColor) {
	float NOISE_GRANULARITY = dithering/255.0;
	vec2 coordinates = gl_FragCoord.xy / resolution;
	return inputColor + mix(-NOISE_GRANULARITY, NOISE_GRANULARITY, random(coordinates));
}

void main(void){
	vec3 color = texture(HDRcolorTexture, textureCoords).rgb;

	color = acesFitted(color * exposure);
	color = pow(color, vec3(1.0 / gamma));
	color = applyDithering(color);

	out_Colour = vec4(color, 1.0);
}
