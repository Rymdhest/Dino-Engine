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
	gAlbedo = lookupAlbedo(parallaxedCoords, textureIndex);
    
    //if(parallaxedCoords.x > 10.0 || parallaxedCoords.y > 10.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;

    if (DEBUG_VIEW) {
        gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
        gAlbedo.rgb *= COLOR_TEST;
    }

    if (gAlbedo.a < 0.5f) discard;
	vec4 normalTangentSpace = lookupNorma(parallaxedCoords, textureIndex).xyzw;
    gNormal.a = normalTangentSpace.a;
	gNormal.xyz = compressNormal(normalize( normalTBN*normalTangentSpace.xyz));


	gMaterials = lookupMaterial(parallaxedCoords, textureIndex).rgba;
    gMaterials.a = 0.0;
}