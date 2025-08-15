#version 420

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;
uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D gAlbedo;
uniform sampler2D gMaterials;
uniform int numberOfCascades;


uniform vec3 LightDirectionViewSpace;
uniform vec3 lightColour;
uniform float ambientFactor;

uniform mat4 sunSpaceMatrices[5];
uniform sampler2DShadow shadowMaps[5];
uniform vec2 shadowMapResolutions[5];
uniform float cascadeProjectionSizes[5];


uniform int pcfRadius;


float calcShadow(vec3 positionViewSpace) {
	float maxShadow = 0.0;

	for (int i = 0; i < numberOfCascades; i++) {
		vec2 pixelSize = 1.0 / shadowMapResolutions[i];
		if (length(positionViewSpace) * 2.0 < cascadeProjectionSizes[i]) {
			vec4 positionSunSpace = sunSpaceMatrices[i] * vec4(positionViewSpace, 1.0);
			positionSunSpace = positionSunSpace * 0.5 + 0.5;

			// Discard if outside texture bounds
			if (any(lessThan(positionSunSpace.xyz, vec3(0.0))) ||
			    any(greaterThan(positionSunSpace.xyz, vec3(1.0)))) {
				continue;
			}

			float shadowFactor = 0.0;
			float totalWeight = 0.0;
			for (int x = -pcfRadius; x <= pcfRadius; x++) {
				for (int y = -pcfRadius; y <= pcfRadius; y++) {
					vec3 offset = vec3(x * pixelSize.x, y * pixelSize.y, 0);
					float depth = texture(shadowMaps[i], positionSunSpace.xyz + offset);
					shadowFactor += 1.0 - depth;
					totalWeight += 1.0;
				}
			}
			shadowFactor /= totalWeight;
			maxShadow = max(maxShadow, shadowFactor);
		}
	}

	return maxShadow;
}

void main(void){
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
	float sunFactor = clamp(1.0-calcShadow(position), 0.0, 1.0);
	vec3 viewDir = normalize(-position);

	vec3 color = getLightPBR(albedo.rgb, normal, roughness, metallic, lightColour, 1.0, ambientFactor*ambient, viewDir, LightDirectionViewSpace, sunFactor, lightFactorEntry, materialTransparancy, geometricDepth);

	out_Colour = vec4(color, 0.0);
	
}
