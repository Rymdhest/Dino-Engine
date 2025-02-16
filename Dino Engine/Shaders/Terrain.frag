#version 330


in vec3 fragColor;
in vec2 fragUV;
in vec3 positionViewSpace_pass;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 normalTBN;
in vec3 worldNormal;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;


uniform float groundID;
uniform float rockID;

uniform vec3 viewPos;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gPosition;
layout (location = 3) out vec4 gMaterials;  

#include textureUtil.glsl
#include procedural/fastHash.glsl

vec2 ParallaxMapping(vec2 texCoords, vec3 viewDir, float textureIndex)
{ 
    // calculate the size of each layer
    float layerDepth = 1.0 / parallaxLayers;
    // depth of current layer
    float currentLayerDepth = 0.0;
    // the amount to shift the texture coordinates per layer (from vector P)
    //vec2 P = viewDir.xy/-viewDir.z * parallaxDepth; 
    vec2 P = viewDir.xy * parallaxDepth;
    //vec2 P = viewDir.xy / max(viewDir.z, 0.1) * parallaxDepth;
    vec2 deltaTexCoords = P / parallaxLayers;
  
    vec2  currentTexCoords     = texCoords;
    float currentDepthMapValue =1.0-lookupMaterial(currentTexCoords, textureIndex).a;
  
    while(currentLayerDepth < currentDepthMapValue)
    {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = 1.0-lookupMaterial(currentTexCoords, textureIndex).a;
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }
    
    // get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // get depth after and before collision for linear interpolation
    float afterDepth  = currentDepthMapValue - currentLayerDepth;
    float beforeDepth =(1.0-lookupMaterial(prevTexCoords, textureIndex).a) - currentLayerDepth + layerDepth;
 
    // interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}  

void main() {

	vec3 viewDir   = normalize((TangentViewPos - TangentFragPos)*vec3(1.0, 1.0, 1.0));
    //vec2 parallaxedCoords = fragUV;
    


    float steepness = dot(vec3(0.0, 1.0, 0.0), worldNormal);

    vec2 parallaxedCoordsGround = ParallaxMapping(fragUV,  viewDir, groundID);
    vec2 parallaxedCoordsRock = ParallaxMapping(fragUV,  viewDir, rockID);

    float rockWeight = lookupMaterial(parallaxedCoordsRock, rockID).a*((1.0-steepness)*1.35);
    float groundWeight = lookupMaterial(parallaxedCoordsGround, groundID).a*(steepness*0.3);
    
    float textureIndex = groundID;
    vec2 parallaxedCoords = parallaxedCoordsGround;
    if (rockWeight > groundWeight) {
        textureIndex = rockID;
        parallaxedCoords = parallaxedCoordsRock;
    }
    //if(parallaxedCoords.x > 1.0 || parallaxedCoords.y > 1.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;

	gAlbedo = lookupAlbedo(parallaxedCoords, textureIndex);
	gAlbedo.rgb *= fragColor;
    //gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
    if (gAlbedo.a < 0.5f) discard;


    //gAlbedo.rgb = vec3((fragUV), 0f);
	vec4 normalTangentSpace = lookupNorma(parallaxedCoords, textureIndex);
	normalTangentSpace.xyz = normalTangentSpace.xyz*2-1;
	gNormal.xyz = normalTangentSpace.xyz*normalTBN;
	gPosition = vec4(positionViewSpace_pass, 0.0f);

	gNormal.a = normalTangentSpace.a;

	gMaterials = lookupMaterial(parallaxedCoords, textureIndex).rgba;
}