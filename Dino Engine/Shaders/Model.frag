#version 330
in vec3 fragColor;
in vec2 fragUV;
in vec3 positionViewSpace_pass;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in mat3 normalTBN;
in float textureIndex;
uniform float parallaxDepth;
uniform float parallaxLayers;
uniform vec3 viewPos;
uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gPosition;
layout (location = 3) out vec4 gMaterials;

vec2 ParallaxMapping(vec2 texCoords, vec3 viewDir)
{ 
    // calculate the size of each layer
    float layerDepth = 1.0 / parallaxLayers;
    // depth of current layer
    float currentLayerDepth = 0.0;
    // the amount to shift the texture coordinates per layer (from vector P)
    //vec2 P = viewDir.xy/-viewDir.z * parallaxDepth; 
    vec2 P = viewDir.xy * parallaxDepth; 
    vec2 deltaTexCoords = P / parallaxLayers;
  
    vec2  currentTexCoords     = texCoords;
    float currentDepthMapValue =1-texture(materialMapTextureArray, vec3(currentTexCoords, textureIndex)).a;
  
    while(currentLayerDepth < currentDepthMapValue)
    {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = 1-texture(materialMapTextureArray, vec3(currentTexCoords, textureIndex)).a;
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }
    
    // get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // get depth after and before collision for linear interpolation
    float afterDepth  = currentDepthMapValue - currentLayerDepth;
    float beforeDepth =(1-texture(materialMapTextureArray, vec3(prevTexCoords, textureIndex)).a) - currentLayerDepth + layerDepth;
 
    // interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}  

void main() {

	vec3 viewDir   = normalize(TangentViewPos - TangentFragPos);
    vec2 parallaxedCoords = ParallaxMapping(fragUV,  viewDir);
    //if(parallaxedCoords.x > 1.0 || parallaxedCoords.y > 1.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;

	gAlbedo = texture(albedoMapTextureArray,vec3(parallaxedCoords, textureIndex));
	gAlbedo *= vec4(fragColor, 1.0f);
    //gAlbedo.rgb = vec3((parallaxedCoords), 0f);
	vec4 normalTangentSpace = texture(normalMapTextureArray, vec3(parallaxedCoords, textureIndex));
	normalTangentSpace.xyz = normalTangentSpace.xyz*2-1;
	gNormal.xyz = normalTangentSpace.xyz*normalTBN;
	gPosition = vec4(positionViewSpace_pass, 0.0f);

	gNormal.a = normalTangentSpace.a;

	gMaterials = texture(materialMapTextureArray, vec3(parallaxedCoords, textureIndex)).rgba;
}