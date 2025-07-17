#version 420
#include globals.glsl
in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D HDRcolorTexture;

uniform float exposure;
uniform float gamma;
uniform float saturation;
uniform float brightness;
uniform float contrast;
uniform float dithering;

vec3 aces(vec3 x) {
  const float a = 2.51;
  const float b = 0.03;
  const float c = 2.43;
  const float d = 0.59;
  const float e = 0.14;
  return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}
vec3 reinhard(vec3 inputColor) {
	float key = 1.418f;
	return inputColor / (inputColor + vec3(1.0)) * (inputColor * key);
}
vec3 applyExposure(vec3 inputColor) {
	return vec3(1.0) - exp(-inputColor * exposure);
}

vec3 applyToneMap(vec3 inputColor) {
	//return aces(inputColor);
	return reinhard(inputColor);
}

vec3 applyGamma(vec3 inputColor) {
	return pow(inputColor, vec3(1.0 / gamma));
}

vec3 applyBrightness(vec3 inputColor) {
	return inputColor*vec3(brightness);
}
vec3 applyContrast(vec3 inputColor) {
    return mix(0.5 + (inputColor - 0.5) * contrast, inputColor, contrast);
}
vec3 applySaturation(vec3 inputColor) {
    float luminance = dot(inputColor, vec3(0.2126, 0.7152, 0.0722));
    vec3 desaturatedColor = vec3(luminance);
    return mix(desaturatedColor, inputColor, saturation);
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
	vec3 color =  texture(HDRcolorTexture, textureCoords).rgb;

	color = applyExposure(color);
	color = applyToneMap(color);
	color = applyContrast(color);
	color = applyBrightness(color);
	color = applySaturation(color);
	color = applyGamma(color);
	color = applyDithering(color);

	out_Colour =  vec4(color, 1.0f);
}