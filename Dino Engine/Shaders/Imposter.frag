#version 420

#include Globals.glsl

in vec2 fragUV;
in vec3 fragWorldPos;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 viewTBN;
in float textureIndex;


uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;


uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

int numberOfMaterials = 0;


#include textureUtil.glsl
#include procedural/fastHash.glsl
#include gBufferUtil.glsl
#include AntiAliasing/Msaa.glsl
#include FocusCutout.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Wetness.glsl

void main() {

	applyFocusCutout(fragWorldPos);

	MaterialProps material = LookupAllMaterialProps(fragUV, textureIndex);
    // Cutouts end between the MSAA samples where the alpha crosses the cutoff.
    int coverage = alphaTestCoverage(material.alpha, 0.5);
    if (coverage == 0) discard;
    gl_SampleMask[0] = coverage;

	vec3 normal = material.normal;
    if (!gl_FrontFacing) normal.z *= -1.0;
	normal = normalize(viewTBN * normal);

	// Far off, a tree in the rain. Imposters are all crowns.
	vec3 albedo = material.albedo;
	float roughness = material.roughness;
	mat3 viewToWorld = mat3(invViewMatrix);
	vec3 worldNormal = viewToWorld * normal;
	weatherWetten(fragWorldPos, worldNormal, WEATHER_FOLIAGE_POROSITY, material.metalic, 0.0, material.height, albedo, roughness, worldNormal);
	normal = mat3(viewMatrix) * worldNormal;

	gAlbedo.rgb = albedo;
	gNormal.xyz = compressNormal(normal);

    gAlbedo.a = material.subSurface;
	//gAlbedo.a = 0.0;
    gNormal.a = material.ambient;
	gMaterials.r = roughness;
	gMaterials.g = encodeEmission(material.emission * EMISSION_GAIN);
	gMaterials.b = material.metalic;
    gMaterials.a = material.height;
}