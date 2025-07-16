

vec4 lookupAlbedo(vec2 coords, float index) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }
    if (isMaterial) {
        return texture(albedoMapTextureArray, vec3(coords, index));
    }
    else {
        return texture(albedoMapModelTextureArray, vec3(coords, index));
    }
}

vec4 lookupNorma(vec2 coords, float index) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }
    if (isMaterial) {
        return texture(normalMapTextureArray, vec3(coords, index));
    }
    else {
        return texture(normalMapModelTextureArray, vec3(coords, index));
    }
}

vec4 lookupMaterial(vec2 coords, float index) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }

    if (isMaterial) {
        return texture(materialMapTextureArray, vec3(coords, index));
    }
    else {
        return texture(materialMapModelTextureArray, vec3(coords, index));
    }
}

vec2 ParallaxMapping(vec2 texCoords, vec3 viewDir, float textureIndex,float parallaxDepth, float parallaxLayers)
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

    vec2  currentTexCoords = texCoords;
    float currentDepthMapValue = 1.0 - lookupMaterial(currentTexCoords, textureIndex).a;

    while (currentLayerDepth < currentDepthMapValue)
    {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = 1.0 - lookupMaterial(currentTexCoords, textureIndex).a;
        // get depth of next layer
        currentLayerDepth += layerDepth;
    }

    // get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // get depth after and before collision for linear interpolation
    float afterDepth = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = (1.0 - lookupMaterial(prevTexCoords, textureIndex).a) - currentLayerDepth + layerDepth;

    // interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}
