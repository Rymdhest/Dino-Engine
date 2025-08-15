#version 420


in vec3 fragColor;
in vec2 fragUV;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 normalTBN;
in float textureIndex;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

#include textureUtil.glsl
#include procedural/fastHash.glsl
#include gBufferUtil.glsl

void main() {

	vec3 viewDir   = normalize((TangentViewPos - TangentFragPos));
    vec2 parallaxedCoords = ParallaxMapping(fragUV,  viewDir, textureIndex, parallaxDepth, parallaxLayers);
    //vec2 parallaxedCoords = fragUV;
    //if(parallaxedCoords.x > 1.0 || parallaxedCoords.y > 1.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;
    MaterialProps material = LookupAllMaterialProps(parallaxedCoords, textureIndex);
	gAlbedo.rgb = material.albedo;
	gAlbedo.rgb *= fragColor;


    if (material.alphaBit == 0) discard;

    gAlbedo.a = material.subSurface;

    //gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
    
    //gAlbedo.rgb = vec3(fract(fragUV), 0f);
	vec3 normalTangentSpace = material.normal;
    gNormal.a = material.ambient;
    if (!gl_FrontFacing) normalTangentSpace.z *= -1.0;
    normalTangentSpace.xyz = normalize(normalTangentSpace.xyz);
    vec3 normal = normalize(normalTBN*normalTangentSpace.xyz);
	gNormal.xyz = compressNormal(normal);

	gMaterials.r = material.roughness;
	gMaterials.g = material.emission;
	gMaterials.b = material.metalic;
    gMaterials.a = material.height;
}