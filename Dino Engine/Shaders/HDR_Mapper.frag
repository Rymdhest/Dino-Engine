#version 420
#include gBufferUtil.glsl
#include globals.glsl
in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D HDRcolorTexture;

// The depth of field's mix, when it's left to this pass (DepthOfFieldRenderer.LeftToToneMap): Depth_Of_Field.frag's, with no landscape lens.
uniform int depthOfField;
uniform sampler2D blurTexture;
uniform sampler2D gDepth;
uniform float depthOfFieldRange;
uniform float focusDistance;

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

// Which curve takes the scene's light to the screen's (LightingSettings.ToneMapper): 0 the ACES fit above, 1 AgX, 2 Khronos PBR Neutral. Each of the other two is
// given its own exposure, so that a middle grey and sunlit ground come out where ACES puts them and only what's darker, brighter or more coloured differs.
uniform int toneMapper;

// AgX (Troy Sobotka's, as Blender 4 has it), by Benjamin Wrensch's fit of its curve: the light is taken into a space a little inside the primaries, its log
// encoded over sixteen and a half stops and put through one sigmoid, and brought out again. It goes to white the way film does, so a lamp's flame or a strong
// glow keeps its hue as it brightens rather than turning yellow or cyan in one channel's clip as ACES's does. Lower in contrast than ACES. The fit's small
// negative offset is left out, as ACES's is, so that black is exactly black and the edge of a pool of light doesn't clip.
const float AGX_EXPOSURE = 0.475;
const mat3 AGX_INSET = mat3(
	0.842479062253094, 0.0423282422610123, 0.0423756549057051,
	0.0784335999999992, 0.878468636469772, 0.0784336,
	0.0792237451477643, 0.0791661274605434, 0.879142973793104);
const mat3 AGX_OUTSET = mat3(
	1.19687900512017, -0.0528968517574562, -0.0529716355144438,
	-0.0980208811401368, 1.15190312990417, -0.0980434501171241,
	-0.0990297440797205, -0.0989611768448433, 1.15107367264116);

vec3 agx(vec3 colour) {
	const float lowest = -12.47393;
	const float highest = 4.026069;
	vec3 x = AGX_INSET * colour;
	x = (clamp(log2(max(x, vec3(1e-10))), lowest, highest) - lowest) / (highest - lowest);
	vec3 x2 = x * x;
	vec3 x4 = x2 * x2;
	x = 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x;
	// The curve gives what a display shows; taken back to light, for the gamma below to encode like the others.
	return pow(clamp(AGX_OUTSET * x, 0.0, 1.0), vec3(2.2));
}

// Khronos PBR Neutral (2024): the light as it is up to 0.76, so a material's colour on screen is its albedo lit, and only highlights are compressed, towards
// white as they pass the top, by a peak that keeps each colour's ratios. Made for showing materials true. Brighter than ACES in a highlight.
const float NEUTRAL_EXPOSURE = 0.824;

vec3 pbrNeutral(vec3 colour) {
	const float start = 0.8 - 0.04;
	const float desaturation = 0.15;
	float x = min(colour.r, min(colour.g, colour.b));
	float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
	colour -= offset;
	float peak = max(colour.r, max(colour.g, colour.b));
	if (peak < start) return colour;
	const float d = 1.0 - start;
	float newPeak = 1.0 - d * d / (peak + d - start);
	colour *= newPeak / peak;
	float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
	return mix(colour, vec3(newPeak), g);
}

vec3 toneMap(vec3 colour) {
	if (toneMapper == 1) return agx(colour * AGX_EXPOSURE);
	if (toneMapper == 2) return clamp(pbrNeutral(colour * NEUTRAL_EXPOSURE), 0.0, 1.0);
	return acesFitted(colour);
}

float random(vec2 coords) {
   return fract(sin(dot(coords.xy, vec2(12.9898,78.233))) * 43758.5453);
}

vec3 applyDithering(vec3 inputColor) {
	float NOISE_GRANULARITY = dithering/255.0;
	vec2 coordinates = gl_FragCoord.xy / resolution;
	// Black stays black: there is nothing to band, and noise would lift it.
	float visible = clamp(max(inputColor.r, max(inputColor.g, inputColor.b)) * 255.0, 0.0, 1.0);
	return inputColor + mix(-NOISE_GRANULARITY, NOISE_GRANULARITY, random(coordinates)) * visible;
}

void main(void){
	vec3 color = texture(HDRcolorTexture, textureCoords).rgb;
	if (depthOfField != 0)
	{
		float depth = -ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution).z;
		color = mix(color, texture(blurTexture, textureCoords).rgb, clamp(abs(depth - focusDistance) * depthOfFieldRange, 0.0, 1.0));
	}

	color = toneMap(color * exposure);
	color = pow(color, vec3(1.0 / gamma));
	color = applyDithering(color);

	out_Colour = vec4(color, 1.0);
}
