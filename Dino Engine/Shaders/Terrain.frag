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
uniform bool TEST_FRAG;
uniform float TEST_CLamp;

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
#include procedural/fastHash.glsl

vec2 ParallaxOcclusionMapping(vec2 texCoords, vec3 viewDir, float textureIndex)
{
    // Adjust layer count based on view angle (optional enhancement)
    float numLayers = mix(parallaxLayers, parallaxLayers * 2.0, 1.0 - abs(dot(vec3(0.0, 0.0, 1.0), viewDir)));
    float layerDepth = 1.0 / numLayers;
    vec2 P = viewDir.xy / max(viewDir.z, TEST_CLamp) * parallaxDepth;
    if (TEST_FRAG) P = viewDir.xy / (abs(viewDir.z) + TEST_CLamp) * parallaxDepth;
    P = viewDir.xy * parallaxDepth;
    vec2  deltaTexCoords = P / numLayers;
        
    // Initialize
    vec2 currentTexCoords = texCoords;
    float currentLayerDepth = 0.0;
    float currentDepthMapValue = 1.0 - lookupMaterial(currentTexCoords, textureIndex).a;

    // Step through depth layers
    while (currentLayerDepth < currentDepthMapValue)
    {
        currentTexCoords -= deltaTexCoords;
        currentDepthMapValue = 1.0 - lookupMaterial(currentTexCoords, textureIndex).a;
        currentLayerDepth += layerDepth;
    }

    // Back up one step
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;
    float afterDepth = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = (1.0 - lookupMaterial(prevTexCoords, textureIndex).a) - (currentLayerDepth - layerDepth);

    // Linear interpolation between the last two steps
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = mix(currentTexCoords, prevTexCoords, weight);

    return finalTexCoords;
}

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
	vec3 viewDir = normalize(TangentViewPos - TangentFragPos);
    float steepness = dot(vec3(0.0, 1.0, 0.0), worldNormal);

    vec2 parallaxedCoordsGround = ParallaxMapping(fragUV,  viewDir, groundID);
    vec2 parallaxedCoordsRock = ParallaxMapping(fragUV,  viewDir, rockID);


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
    vec3 fragColor = vec3(1.0 , 1.0, 1.0);

	gAlbedo.rgb *= fragColor;
    if (DEBUG_VIEW) {
        gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
        gAlbedo.rgb *= COLOR_TEST;
    }

    if (gAlbedo.a < 0.5f) discard;
	vec4 normalTangentSpace = lookupNorma(parallaxedCoords, textureIndex).xyzw;
    gNormal.a = normalTangentSpace.a;
	normalTangentSpace.xyz = normalTangentSpace.xyz*2-1;
	gNormal.xyz = normalize( normalTBN*normalTangentSpace.xyz);


	gMaterials = lookupMaterial(parallaxedCoords, textureIndex).rgba;
    
}