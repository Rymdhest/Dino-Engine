


vec4 lookupAlbedoGrad(vec2 coords, float index, vec2 dx, vec2 dy) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }
    if (isMaterial) {
        return textureGrad(albedoMapTextureArray, vec3(coords, index), dx, dy);
    }
    else {
        return textureGrad(albedoMapModelTextureArray, vec3(coords, index), dx, dy);
    }
}

vec4 lookupAlbedo(vec2 coords, float index) {
    return lookupAlbedoGrad(coords, index, dFdx(coords), dFdy(coords));
}

struct NormalLookupResult {
    vec3 normal;
    float ambient;
    float SSS;
};

NormalLookupResult lookupNormaGrad(vec2 coords, float index, vec2 dx, vec2 dy) {
    bool isMaterial = true;
    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }
    vec4 normal;
    if (isMaterial) {
        normal = textureGrad(normalMapTextureArray, vec3(coords, index), dx, dy);
    }
    else {
        normal = textureGrad(normalMapModelTextureArray, vec3(coords, index), dx, dy);
    }
    normal.xy = normal.xy * 2.0 - 1.0;
    float nZ = sqrt(1.0 - clamp(dot(normal.xy, normal.xy), 0.0, 1.0));

    NormalLookupResult result;
    result.normal = vec3(normal.xy, nZ);
    result.SSS = normal.z;
    result.ambient = normal.a;
    return result;
}

NormalLookupResult lookupNorma(vec2 coords, float index) {
    return lookupNormaGrad(coords, index, dFdx(coords), dFdy(coords));
}

vec4 lookupMaterialGrad(vec2 coords, float index, vec2 dx, vec2 dy) {
    bool isMaterial = true;

    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }

    if (isMaterial) {
        return textureGrad(materialMapTextureArray, vec3(coords, index), dx, dy);
    }
    else {
        return textureGrad(materialMapModelTextureArray, vec3(coords, index), dx, dy);
    }
}

vec4 lookupMaterial(vec2 coords, float index) {
    return lookupMaterialGrad(coords, index, dFdx(coords), dFdy(coords));
}

struct MaterialProps {
    vec3 albedo;
    int alphaBit;
    vec3 normal;
    float ambient;
    float roughness;
    float emission;
    float metalic;
    float subSurface;
    float height;
};

// dx/dy are the screen-space derivatives to sample with. Pass the derivatives of the un-displaced UV when coords
// come from parallax mapping, whose output jumps at occlusion edges and would otherwise select the wrong mip.
MaterialProps LookupAllMaterialPropsGrad(vec2 coords, float index, vec2 dx, vec2 dy) {
    vec4 albedoRead = lookupAlbedoGrad(coords, index, dx, dy);
    NormalLookupResult normalResult = lookupNormaGrad(coords, index, dx, dy);
    vec4 materialRead = lookupMaterialGrad(coords, index, dx, dy);

    int alphaBit= 1;
    if (albedoRead.a < 0.5) {
        alphaBit = 0; //discard;
    }

    MaterialProps props;

    props.albedo = albedoRead.rgb;
    props.alphaBit = alphaBit;
    props.subSurface = normalResult.SSS;
    props.normal = normalResult.normal;
    props.ambient = normalResult.ambient;
    props.roughness = materialRead.r;
    props.emission = materialRead.g;
    props.metalic = materialRead.b;
    props.height = materialRead.a;

    return props;
}

MaterialProps LookupAllMaterialProps(vec2 coords, float index) {
    return LookupAllMaterialPropsGrad(coords, index, dFdx(coords), dFdy(coords));
}


// Parallax occlusion mapping in tangent space.
// viewDir is the tangent-space direction towards the camera, built per pixel (see Model.frag).
// dx/dy are the screen-space derivatives of baseUV, taken before the march since implicit derivatives
// are undefined inside its data-dependent loop.
vec2 ParallaxMapping(vec2 baseUV, vec3 viewDir, float materialIndex, float depthScale, float layers, vec2 dx, vec2 dy)
{
    if (viewDir.z <= 1e-4) return baseUV;

    float layerDepth = 1.0 / layers;

    vec2 P = (viewDir.xy / max(viewDir.z, 0.05)) * (depthScale / 5.0);
    vec2 deltaUV = P / layers;

    vec2 currentUV = baseUV;
    float currentLayerDepth = 0.0;
    float currentMapDepth = 1.0 - lookupMaterialGrad(currentUV, materialIndex, dx, dy).a;
    if (currentMapDepth <= 0.0) return baseUV;

    vec2 prevUV = currentUV;
    float prevLayerDepth = 0.0;
    float prevMapDepth = currentMapDepth;

    int maxSteps = int(ceil(layers));
    for (int i = 1; i <= maxSteps; i++)
    {
        currentUV = baseUV - deltaUV * float(i);
        currentLayerDepth = float(i) * layerDepth;
        currentMapDepth = 1.0 - lookupMaterialGrad(currentUV, materialIndex, dx, dy).a;
        if (currentLayerDepth >= currentMapDepth) break;
        prevUV = currentUV;
        prevLayerDepth = currentLayerDepth;
        prevMapDepth = currentMapDepth;
    }

    // Intersect the ray with the height field linearly interpolated between the last two layers
    float before = prevMapDepth - prevLayerDepth;
    float after = currentMapDepth - currentLayerDepth;
    float weight = clamp(before / max(before - after, 1e-6), 0.0, 1.0);
    return mix(prevUV, currentUV, weight);
}
