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
uniform vec3 lightColor;
uniform vec3 attenuation;
uniform float lightAmbient;


void main(void){
    vec2 textureCoords = gl_FragCoord.xy / resolution;
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution);
	vec4 normalBuffer = texture(gNormal, textureCoords).xyzw;
	vec3 albedo = texture(gAlbedo, textureCoords).rgb;
	vec4 materialBuffer = texture(gMaterials, textureCoords).rgba;
	vec3 normal = unCompressNormal(normalBuffer.xyz);
	float ambient = normalBuffer.w;
	float roughness = materialBuffer.r;
	float metallic = materialBuffer.b;
    float materialTransparancy = materialBuffer.a;
	
	float lightFactor = 1.0;
	float geometricDepth = 1.3;
	float lightFactorEntry = 1.0;

	vec3 viewDir = normalize(-position);
    vec3 lightDir = normalize(lightPositionViewSpace - position);  
	float attenuationFactor = calcAttunuation(lightPositionViewSpace, position, attenuation);

    vec3 color = getLightPBR(albedo, normal, roughness, metallic, lightColor, attenuationFactor, lightAmbient*ambient, viewDir, lightDir, lightFactor, lightFactorEntry, materialTransparancy, geometricDepth);

	//color = color / (color + vec3(1.0));
    //color = pow(color, vec3(1.0/2.2));  
	//lighting = applyFog(lighting, -position.z, -viewDir);
	out_Colour = vec4(color, 0.0);
	//out_Colour =  vec4(1f, 0f, 10f, 1.0f);
	//out_Colour =  vec4(positionSunSpace.xyz, 1.0f);
	//out_Colour =  vec4(vec3(emission), 1.0f);

	
}
