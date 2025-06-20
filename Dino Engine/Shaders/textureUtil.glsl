
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

vec4 lookupMaterialWrapped(vec2 coords, float index) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }

    coords = fract(coords); // wrap UVs here

    if (isMaterial) {
        return texture(materialMapTextureArray, vec3(coords, index));
    }
    else {
        return texture(materialMapModelTextureArray, vec3(coords, index));
    }
}