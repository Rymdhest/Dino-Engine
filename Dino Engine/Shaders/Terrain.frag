#version 330


in vec2 fragUV;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 normalTBN;
in vec3 worldNormal;
in vec3 COLOR_TEST;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;

uniform float groundID;
uniform float rockID;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

uniform bool DEBUG_VIEW;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

#include textureUtil.glsl
#include gBufferUtil.glsl
#include procedural/fastHash.glsl


void main() {
	vec3 viewDir = normalize(TangentViewPos - TangentFragPos);
    float steepness = dot(vec3(0.0, 1.0, 0.0), worldNormal);

    vec2 parallaxedCoordsGround = fragUV;
    vec2 parallaxedCoordsRock = fragUV;
    if (parallaxDepth > 0.001) {
        parallaxedCoordsGround = ParallaxMapping(fragUV,  viewDir, groundID, parallaxDepth, parallaxLayers);
        parallaxedCoordsRock = ParallaxMapping(fragUV,  viewDir, rockID, parallaxDepth, parallaxLayers);
    }

    float rockWeight = lookupMaterial(parallaxedCoordsRock, rockID).a*((1.0-steepness)*1.0);
    float groundWeight = lookupMaterial(parallaxedCoordsGround, groundID).a*(steepness*1.0);
    
    float textureIndex = groundID;
    vec2 parallaxedCoords = parallaxedCoordsGround;
    if (rockWeight > groundWeight) {
        textureIndex = rockID;
        parallaxedCoords = parallaxedCoordsRock;
    }

    MaterialProps material = LookupAllMaterialProps(parallaxedCoords, textureIndex);
	gAlbedo.rgb = material.albedo;
    if (DEBUG_VIEW) {
        gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
        gAlbedo.rgb *= COLOR_TEST;
    }

    if (material.alphaBit == 0) discard;

    gAlbedo.a = material.subSurface;

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