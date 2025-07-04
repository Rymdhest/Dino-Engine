#version 330

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
uniform vec2 resolution;
uniform mat4 invProjection;


void main(void){
    vec2 textureCoords = gl_FragCoord.xy / resolution;
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjection, resolution);
	vec4 normalBuffer = texture(gNormal, textureCoords).xyzw;
	vec3 albedo = texture(gAlbedo, textureCoords).rgb;
	vec3 materialBuffer = texture(gMaterials, textureCoords).rgb;
	vec3 normal = normalBuffer.xyz;
	float ambient = normalBuffer.w;
	float roughness = materialBuffer.r;
	float metallic = materialBuffer.b;
    
	vec3 viewDir = normalize(-position);
    vec3 lightDir = normalize(lightPositionViewSpace - position);  
	float attenuationFactor = calcAttunuation(lightPositionViewSpace, position, attenuation);

    vec3 color = getLightPBR(albedo, normal, roughness, metallic, lightColor, attenuationFactor, lightAmbient*ambient, viewDir, lightDir, 1.0);

	//color = color / (color + vec3(1.0));
    //color = pow(color, vec3(1.0/2.2));  
	//lighting = applyFog(lighting, -position.z, -viewDir);
	out_Colour = vec4(color, 0.0);
	//out_Colour =  vec4(1f, 0f, 10f, 1.0f);
	//out_Colour =  vec4(positionSunSpace.xyz, 1.0f);
	//out_Colour =  vec4(vec3(emission), 1.0f);

	
}
