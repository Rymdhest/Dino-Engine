#version 420


in vec2 fragUV;
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

void main() {

	MaterialProps material = LookupAllMaterialProps(fragUV, textureIndex);
    if (material.alphaBit == 0) discard;

	gAlbedo.rgb = material.albedo;
	vec3 normal = material.normal;
    if (!gl_FrontFacing) normal.z *= -1.0;
	normal = normalize(viewTBN * normal);
	normal = compressNormal(normal);
	gNormal.xyz = normal;

    gAlbedo.a = material.subSurface;
	//gAlbedo.a = 0.0;
    gNormal.a = material.ambient;
	gMaterials.r = material.roughness;
	gMaterials.g = material.emission;
	gMaterials.b = material.metalic;
    gMaterials.a = material.height;
}