#version 420

#include globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Halls.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 lightPositionViewSpace;
uniform vec3 lightDirectionViewSpace;
uniform vec3 lightColor;
uniform float lightRadius;
uniform float softness;
uniform float lightFill;        // As in Point_Light.frag.
uniform float cutoffCosine;

uniform mat4 lightSpaceMatrix;
uniform int pcfRadius;
uniform sampler2DShadow shadowMap;
uniform bool isShadow;
uniform vec2 shadowResolution;

float calcShadow(vec3 positionViewSpace) {
	vec2 pixelSize = 1.0 / shadowResolution; // move to CPU

	// Project into light's clip space
	vec4 positionLightSpace = lightSpaceMatrix * vec4(positionViewSpace, 1.0);
	positionLightSpace /= positionLightSpace.w;         // Required for perspective projections
	positionLightSpace = positionLightSpace * 0.5 + 0.5; // Transform from [-1,1] to [0,1]

	float shadow = 0.0;
	float totalWeight = 0.0;

	for (int x = -pcfRadius; x <= pcfRadius; x++) {
		for (int y = -pcfRadius; y <= pcfRadius; y++) {
			vec3 offset = vec3(x * pixelSize.x, y * pixelSize.y, 0);
			shadow += 1.0-texture(shadowMap, positionLightSpace.xyz + offset);
			totalWeight += 1.0;
		}
	}

	shadow /= totalWeight;
	return shadow; // Convert to shadow amount: 0 = lit, 1 = full shadow
}

float calcSoftEdge(vec3 lightDir, vec3 lightDirectionViewSpace, float cutoff)
{
    float theta = dot(lightDir, normalize(-lightDirectionViewSpace));
    if (theta < cutoff) return 0.0;
    float epsilon = max(softness * (1.0 - cutoff), 1e-6);
    return smoothstep(0.0, 1.0, clamp((theta - cutoff) / epsilon, 0.0, 1.0));
}

void main(void){
	GBufferSample g = locateGBufferSample();
	vec3 toLight = lightPositionViewSpace - g.position;
	float distanceToLight = length(toLight);
	vec3 lightDir = toLight / max(distanceToLight, 1e-4);

	float strength = lightFalloff(distanceToLight, lightRadius) * calcSoftEdge(lightDir, lightDirectionViewSpace, cutoffCosine);
	if (strength <= 0.0) discard;
	// A hall keeps its own light in, and everyone else's out (Halls.glsl).
	if (hallsLetThrough((invViewMatrix * vec4(lightPositionViewSpace, 1.0)).xyz, (invViewMatrix * vec4(g.position, 1.0)).xyz) <= 0.0) discard;

	finishGBufferSample(g);
	Surface s = surfaceOf(g);
	vec3 radiance = lightColor * strength;
	float visibility = isShadow ? clamp(1.0 - calcShadow(g.position), 0.0, 1.0) : 1.0;

	vec3 colour = lightSurface(s, lightDir, radiance * visibility);
	colour += s.albedo * radiance * (lightFill * g.ambient / LIGHT_PI);
	out_Colour = vec4(colour, 0.0);
}
