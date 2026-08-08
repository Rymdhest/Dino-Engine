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

const vec2 poissonDisk[16] = vec2[](
    vec2( -0.94201624, -0.39906216 ),
    vec2( 0.94558609, -0.76890725 ),
    vec2( -0.094184101, -0.92938870 ),
    vec2( 0.34495938, 0.29387760 ),
    vec2( -0.91588581, 0.45771432 ),
    vec2( -0.81544232, -0.87912464 ),
    vec2( -0.38277543, 0.27676845 ),
    vec2( 0.97484398, 0.75648379 ),
    vec2( 0.44323325, -0.97511554 ),
    vec2( 0.53742981, -0.47373420 ),
    vec2( -0.26496911, -0.41893023 ),
    vec2( 0.79197514, 0.19090188 ),
    vec2( -0.24188840, 0.99706507 ),
    vec2( -0.81409955, 0.91437590 ),
    vec2( 0.19984126, 0.78641367 ),
    vec2( 0.14383161, -0.14100790 )
);

float random(vec3 seed) {
    return fract(sin(dot(seed, vec3(12.9898, 78.233, 45.164))) * 43758.5453123);
}

float calcShadow(vec3 positionViewSpace) {
    float finalSunFactor = 1.0;
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
            const int numSamples = 16;

            // Generate a random rotation angle per pixel to prevent fixed grid patterns
            float randomAngle = random(vec3(gl_FragCoord.xy, float(i))) * 6.2831853;
            mat2 rotation = mat2(
                cos(randomAngle), -sin(randomAngle),
                sin(randomAngle),  cos(randomAngle)
            );

            vec2 texelSize = 1.0 / vec2(textureSize(shadowMaps[i], 0));
            float filterRadius = float(pcfRadius); // Scale spread using your existing pcfRadius uniform

            for (int j = 0; j < numSamples; j++) {
                vec2 rotatedOffset = rotation * poissonDisk[j];
                vec2 offsetCoord = rotatedOffset * texelSize * filterRadius;

                vec3 sampleCoord = vec3(
                    positionSunSpace.xy + offsetCoord,
                    positionSunSpace.z - bias
                );

                float sunFactor = texture(shadowMaps[i], sampleCoord);
                sunFactorSum += sunFactor;
            }
            
            finalSunFactor = min(finalSunFactor, sunFactorSum / float(numSamples));
        }
    }
    return finalSunFactor;
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
	float shadowResult = calcShadow(position);
	float sunFactor = clamp(shadowResult.x, 0.0, 1.0);
	vec3 viewDir = normalize(-position);
	vec3 color = getLightPBR(albedo.rgb, normal, roughness, metallic, lightColour, 1.0, ambientFactor*ambient, viewDir, LightDirectionViewSpace, sunFactor, materialTransparancy, materialHeight);

	out_Colour = vec4(color, 0.0);
	
}
