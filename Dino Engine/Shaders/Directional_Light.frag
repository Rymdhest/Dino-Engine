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
uniform sampler2D depthMaps[5];
uniform float cascadeProjectionSizes[5];


uniform int pcfRadius;


vec2 calcShadow(vec3 positionViewSpace) {
	float finalSunFactor = 1.0;
	float geometricDepth = 0.0;
	float bias = 0.0001;

	for (int i = 0; i < numberOfCascades; i++) {
		if (length(positionViewSpace) * 2.0 < cascadeProjectionSizes[i]) {
			vec4 positionSunSpace = sunSpaceMatrices[i] * vec4(positionViewSpace, 1.0);
			positionSunSpace = positionSunSpace * 0.5 + 0.5;

			// Discard if outside texture bounds
			
			if (any(lessThan(positionSunSpace.xyz, vec3(0.0))) ||
			    any(greaterThan(positionSunSpace.xyz, vec3(1.0)))) {
				continue;
			}
			
			float sunFactorSum = 0.0;
			float totalWeight = 0.0;

			float depthSum = 0.0;
			
			vec2 pixelSize = 1.0 / vec2(textureSize(shadowMaps[i], 0));
			for (int x = -pcfRadius; x <= pcfRadius; x++) {
				for (int y = -pcfRadius; y <= pcfRadius; y++) {
					vec3 offset = vec3(x * pixelSize.x, y * pixelSize.y, 0);

					
					float sunFactor = texture(shadowMaps[i], positionSunSpace.xyz + offset);
					sunFactorSum += sunFactor;
					totalWeight += 1.0;
					
					float depth = texture(depthMaps[i], positionSunSpace.xy + offset.xy).r;
					float fragDepth = positionSunSpace.z-bias;
					depthSum += max(0.0, fragDepth - depth);
				}
			}
			finalSunFactor = min(finalSunFactor, sunFactorSum/totalWeight);




			float cascadeRange = cascadeProjectionSizes[i];
			geometricDepth = max(geometricDepth, (depthSum/totalWeight)*cascadeRange);
		}
	}


	return vec2(finalSunFactor, geometricDepth);
}

void main(void){
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution);
	vec4 normalBuffer = texture(gNormal, textureCoords).xyzw;
	vec4 albedo = texture(gAlbedo, textureCoords).rgba;
	vec4 materialBuffer = texture(gMaterials, textureCoords).rgba;
	vec3 normal = unCompressNormal(normalBuffer.xyz);
	float ambient = normalBuffer.w;
	float roughness = materialBuffer.r;
	float metallic = materialBuffer.b;
	float materialHeight = materialBuffer.a;
	float materialTransparancy = albedo.a;
	vec2 shadowResult = calcShadow(position);
	float geometricDepth = shadowResult.y+materialHeight*0.5;
	float sunFactor = clamp(shadowResult.x, 0.0, 1.0);
	vec3 viewDir = normalize(-position);
	float sunFactorEntry = sunFactor;
	vec3 color = getLightPBR(albedo.rgb, normal, roughness, metallic, lightColour, 1.0, ambientFactor*ambient, viewDir, LightDirectionViewSpace, sunFactor, sunFactorEntry, materialTransparancy, geometricDepth);

	out_Colour = vec4(color, 0.0);
	
}
