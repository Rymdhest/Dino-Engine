

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
struct NormalLookupResult {
    vec3 normal;
    float ambient;
    float SSS;
};

NormalLookupResult lookupNorma(vec2 coords, float index) {
    bool isMaterial = true;
    if (int(index + 0.5) >= numberOfMaterials) {
        isMaterial = false;
        index -= numberOfMaterials;
    }
    vec4 normal;
    if (isMaterial) {
        normal = texture(normalMapTextureArray, vec3(coords, index));
    }
    else {
        normal = texture(normalMapModelTextureArray, vec3(coords, index));
    }
    normal.xy = normal.xy * 2.0 - 1.0;
    float nZ = sqrt(1.0 - clamp(dot(normal.xy, normal.xy), 0.0, 1.0));

    NormalLookupResult result;
    result.normal = vec3(normal.xy, nZ);
    result.SSS = normal.z;
    result.ambient = normal.a;
    return result;
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

MaterialProps LookupAllMaterialProps(vec2 coords, float index) {
    vec4 albedoRead = lookupAlbedo(coords, index);
    NormalLookupResult normalResult = lookupNorma(coords, index);
    vec4 materialRead = lookupMaterial(coords, index);

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


float getSampledHeight(vec2 uv, float materialIndex)
{
    // Uses your existing lookup abstraction
    return lookupMaterial(uv, materialIndex).a; 
}

vec2 ParallaxMapping(vec2 baseUV, vec3 viewDir, float materialIndex, float depthScale, float layers)
{
    float layerDepth = 1.0 / layers;
    float currentLayerDepth = 0.0;

    vec2 P = (viewDir.xy / max(viewDir.z, 0.05)) * (depthScale / 5.0);
    vec2 deltaUV = P / layers;

    vec2 currentUV = baseUV;
    float currentMapHeight = 1.0 - getSampledHeight(currentUV, materialIndex);

    while (currentLayerDepth < currentMapHeight)
    {
        currentUV -= deltaUV;
        currentMapHeight = 1.0 - getSampledHeight(currentUV, materialIndex);
        currentLayerDepth += layerDepth;
    }

    vec2 prevUV = currentUV + deltaUV;
    float afterDepth = currentMapHeight - currentLayerDepth;
    float beforeDepth = (1.0 - getSampledHeight(prevUV, materialIndex)) - currentLayerDepth + layerDepth;

    float weight = afterDepth / (afterDepth - beforeDepth);
    return mix(currentUV, prevUV, weight);
}
