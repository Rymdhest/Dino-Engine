#version 330


in vec3 fragColor;
in vec2 fragUV;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 normalTBN;
in float textureIndex;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;
uniform vec3 viewPos;

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

void main() {

	vec3 viewDir   = normalize((TangentViewPos - TangentFragPos));
    vec2 parallaxedCoords = ParallaxMapping(fragUV,  viewDir, textureIndex, parallaxDepth, parallaxLayers);
    //vec2 parallaxedCoords = fragUV;
    //if(parallaxedCoords.x > 1.0 || parallaxedCoords.y > 1.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;

	gAlbedo = lookupAlbedo(parallaxedCoords, textureIndex);
	gAlbedo.rgb *= fragColor;


    //gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
    if (gAlbedo.a < 0.5f) discard;
    
    //gAlbedo.rgb = vec3(fract(fragUV), 0f);
	vec4 normalTangentSpace = lookupNorma(parallaxedCoords, textureIndex).xyzw;
    gNormal.a = normalTangentSpace.a;
	normalTangentSpace.xyz = normalTangentSpace.xyz*2-1;
	gNormal.xyz = normalize(normalTBN*normalTangentSpace.xyz);

	gMaterials = lookupMaterial(parallaxedCoords, textureIndex).rgba;
}