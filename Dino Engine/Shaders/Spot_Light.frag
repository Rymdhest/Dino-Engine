#version 420

#include globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl

layout (location = 0) out vec4 out_Colour;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D gAlbedo;
uniform sampler2D gMaterials;

uniform vec3 lightPositionViewSpace;
uniform vec3 lightDirectionViewSpace;
uniform vec3 lightColor;
uniform vec3 attenuation;
uniform float softness;
uniform float lightAmbient;
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
    if (theta < cutoff) discard;
    float epsilon = softness * (1.0 - cutoff); 
    float intensity = smoothstep(0, 1, clamp((theta - cutoff) / epsilon, 0.0f, 1.0f));
	return intensity;
}

void main(void){
    vec2 textureCoords = gl_FragCoord.xy / resolution;
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution);
	vec4 normalBuffer = texture(gNormal, textureCoords).xyzw;
	vec4 albedo = texture(gAlbedo, textureCoords).rgba;
	vec3 materialBuffer = texture(gMaterials, textureCoords).rgb;
	vec3 normal = unCompressNormal(normalBuffer.xyz);
	float ambient = normalBuffer.w;
	float roughness = materialBuffer.r;
	float metallic = materialBuffer.b;
    float materialTransparancy = albedo.a;
	float geometricDepth = 1.3;
	float lightFactorEntry = 1.0;
	vec3 viewDir = normalize(-position);
    vec3 lightDir = normalize(lightPositionViewSpace - position);
	float attenuationFactor = calcAttunuation(lightPositionViewSpace, position, attenuation);


	float lightFactor = 1.0;
	if (isShadow) lightFactor = clamp(1.0-calcShadow(position), 0.0, 1.0);
	 
    
    vec3 color = getLightPBR(albedo.rgb, normal, roughness, metallic, lightColor, attenuationFactor, lightAmbient*ambient, viewDir, lightDir, lightFactor, lightFactorEntry, materialTransparancy, geometricDepth);

	float intensity = calcSoftEdge(lightDir, lightDirectionViewSpace, cutoffCosine);

    color *= intensity;

	out_Colour = vec4(color, 0.0);

	
}