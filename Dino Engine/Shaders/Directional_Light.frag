#version 420

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sun_Visibility.glsl
#include Lighting/SunShadow.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;
uniform vec3 LightDirectionViewSpace;
uniform vec3 lightColour;

void main(void){
	GBufferSample g = readGBufferSample();
	if (g.depth >= 1.0) discard;

	vec3 radiance = lightColour * clamp(calcShadow(g.position), 0.0, 1.0);
	// A light with cascades is the sun, and has clouds to get past. Lightning is under them.
	if (numberOfCascades > 0) radiance *= weatherSunVisibility((invViewMatrix * vec4(g.position, 1.0)).xz);
	out_Colour = vec4(lightSurface(surfaceOf(g), normalize(LightDirectionViewSpace), radiance), 0.0);
}
