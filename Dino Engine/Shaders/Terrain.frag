#version 330 core

in vec2 fragUV;
in vec2 fragInstanceChunkPos;
in vec2 fragInstanceChunkSizeXZ;
in float fragInstanceHeightMapID;
in vec3 fragWorldPos;
in vec3 fragWorldNormal;
in vec3 fragWorldTangent;
in vec3 COLOR_TEST;

uniform vec3 viewPos;
uniform mat4 invViewMatrix;
uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;
uniform float textureTileSize;
uniform float textureMapOffset;

uniform float groundID;
uniform float rockID;
uniform float roadID;
uniform float grassID;
uniform float beachID;
uniform float transitionID;

uniform sampler2DArray normalHeightTextureArray;
uniform sampler2DArray grassTextureArray;
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

void ConsiderMaterial(float matID, float weight, inout vec3 bestIDs, inout vec3 bestWeights) {
    if (weight <= 0.0) return;

    if (weight > bestWeights.x) {
        bestIDs.z = bestIDs.y;     bestWeights.z = bestWeights.y;
        bestIDs.y = bestIDs.x;     bestWeights.y = bestWeights.x;
        bestIDs.x = matID;         bestWeights.x = weight;
    } else if (weight > bestWeights.y) {
        bestIDs.z = bestIDs.y;     bestWeights.z = bestWeights.y;
        bestIDs.y = matID;         bestWeights.y = weight;
    } else if (weight > bestWeights.z) {
        bestIDs.z = matID;         bestWeights.z = weight;
    }
}

void GetCandidateMaterials(
    vec3 fragPos,
    float steepness,
    float roadW,
    float grassW,
    out float matA,    out float matB,    out float matC,
    out float weightA, out float weightB, out float weightC
) {
    float altitude = fragPos.y;
    vec3 candidateIDs = vec3(0.0);
    vec3 candidateWeights = vec3(-1.0);

    float slopeFactor = 1.0 - smoothstep(0.3, 1.0, steepness);
    float clampedRoad = clamp(roadW, 0.0, 1.0);
    float terrainMask = 1.0 - clampedRoad;

    float wRoad = clampedRoad * 6.0;
    ConsiderMaterial(roadID, wRoad, candidateIDs, candidateWeights);

    float wRock = (slopeFactor) * 10.0 * terrainMask;
    ConsiderMaterial(rockID, wRock, candidateIDs, candidateWeights);

    float wGrass = grassW * 4.0 * terrainMask;
    ConsiderMaterial(grassID, wGrass, candidateIDs, candidateWeights);
    
    float beachFactor = 1.0 - smoothstep(3.0, 5.0, altitude);
    float wBeach = beachFactor * 10.0 * terrainMask;
    ConsiderMaterial(beachID, wBeach, candidateIDs, candidateWeights);

    float transitionFactor = smoothstep(3.0, 5.0, altitude) * (1.0 - smoothstep(5.0, 7.0, altitude));
    float wTransition = transitionFactor * 10.0 * terrainMask;
    ConsiderMaterial(transitionID, wTransition, candidateIDs, candidateWeights);

    float groundFactor = smoothstep(5.0, 7.0, altitude);
    float wGround = groundFactor * 1.0 * terrainMask;
    ConsiderMaterial(groundID, wGround, candidateIDs, candidateWeights);

    // Normalize weights so they blend smoothly without scaling artifacts
    float totalWeight = candidateWeights.x + max(0.0, candidateWeights.y) + max(0.0, candidateWeights.z);
    if (totalWeight > 0.0) {
        candidateWeights.x = max(0.0, candidateWeights.x) / totalWeight;
        candidateWeights.y = max(0.0, candidateWeights.y) / totalWeight;
        candidateWeights.z = max(0.0, candidateWeights.z) / totalWeight;
    }

    matA = candidateIDs.x; weightA = max(0.0, candidateWeights.x);
    matB = candidateIDs.y; weightB = max(0.0, candidateWeights.y);
    matC = candidateIDs.z; weightC = max(0.0, candidateWeights.z);
}

// Evaluate a continuous, blended height at the current ray step to prevent spikes
float EvaluateBlendedHeightAtStep(vec2 currentUV, vec3 baseWorldPos, float steepness) {
    vec2 currentWorldXZ = currentUV * textureTileSize;
    vec2 stepLocalXZ = clamp((currentWorldXZ - fragInstanceChunkPos) / fragInstanceChunkSizeXZ, 0.0, 1.0);
    vec2 sampleCoord = stepLocalXZ * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0);

    vec4 macroData1 = texture(normalHeightTextureArray, vec3(sampleCoord, fragInstanceHeightMapID));
    vec4 macroData2 = texture(grassTextureArray, vec3(sampleCoord, fragInstanceHeightMapID));

    float roadW = macroData1.z;
    float grassW = macroData2.r;
    
    vec3 stepWorldPos = vec3(currentWorldXZ.x, baseWorldPos.y, currentWorldXZ.y);

    float matA, matB, matC, wA, wB, wC;
    GetCandidateMaterials(stepWorldPos, steepness, roadW, grassW, matA, matB, matC, wA, wB, wC);

    float hA = lookupMaterial(currentUV, matA).a;
    float hB = lookupMaterial(currentUV, matB).a;
    float hC = lookupMaterial(currentUV, matC).a;

    // Continuous blended height across materials
    float blendedHeight = (hA * wA) + (hB * wB) + (hC * wC);
    return 1.0 - blendedHeight;
}

// Evaluate final crisp winning material at the resolved surface coordinate
float EvaluateFinalWinner(vec2 finalUV, vec3 baseWorldPos, float steepness) {
    vec2 finalWorldXZ = finalUV * textureTileSize;
    vec2 stepLocalXZ = clamp((finalWorldXZ - fragInstanceChunkPos) / fragInstanceChunkSizeXZ, 0.0, 1.0);
    vec2 sampleCoord = stepLocalXZ * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0);

    vec4 macroData1 = texture(normalHeightTextureArray, vec3(sampleCoord, fragInstanceHeightMapID));
    vec4 macroData2 = texture(grassTextureArray, vec3(sampleCoord, fragInstanceHeightMapID));

    float roadW = macroData1.z;
    float grassW = macroData2.r;
    
    vec3 stepWorldPos = vec3(finalWorldXZ.x, baseWorldPos.y, finalWorldXZ.y);

    float matA, matB, matC, wA, wB, wC;
    GetCandidateMaterials(stepWorldPos, steepness, roadW, grassW, matA, matB, matC, wA, wB, wC);

    float hA = lookupMaterial(finalUV, matA).a;
    float hB = lookupMaterial(finalUV, matB).a;
    float hC = lookupMaterial(finalUV, matC).a;

    float scoreA = hA * wA;
    float scoreB = hB * wB;
    float scoreC = hC * wC;

    float winningMat = matA;
    float maxScore = scoreA;

    if (scoreB > maxScore) {
        winningMat = matB;
        maxScore = scoreB;
    }
    if (scoreC > maxScore) {
        winningMat = matC;
    }

    return winningMat;
}

vec2 StepByStepParallaxMapping(vec2 baseUV, vec3 viewDir, vec3 baseWorldPos, float steepness, float depthScale, float layers) {
    float layerDepth = 1.0 / layers;

    vec2 P = (viewDir.xy / max(viewDir.z, 0.05)) * (depthScale / 5.0);
    vec2 deltaUV = P / layers;

    vec2 currentUV = baseUV;
    float currentMapHeight = EvaluateBlendedHeightAtStep(currentUV, baseWorldPos, steepness);

    // Safely bound jitter so it never exceeds the surface height (fixes peak distortion)
    float noise = hash21(gl_FragCoord.xy);
    float currentLayerDepth = noise * min(layerDepth, currentMapHeight);
    currentUV -= deltaUV * (currentLayerDepth / layerDepth);

    int steps = 0;
    while (currentLayerDepth < currentMapHeight && steps < int(layers)) {
        currentUV -= deltaUV;
        currentMapHeight = EvaluateBlendedHeightAtStep(currentUV, baseWorldPos, steepness);
        currentLayerDepth += layerDepth;
        steps++;
    }

    // Refinement step for smoothness
    vec2 prevUV = currentUV + deltaUV;
    float nextDepth = currentMapHeight - currentLayerDepth;
    float prevDepth = EvaluateBlendedHeightAtStep(prevUV, baseWorldPos, steepness) - currentLayerDepth + layerDepth;

    float weight = nextDepth / (nextDepth - prevDepth);
    return mix(currentUV, prevUV, clamp(weight, 0.0, 1.0));
}
void main() {
    vec3 N = normalize(fragWorldNormal);
    vec3 T = normalize(fragWorldTangent);
    T = normalize(T - dot(T, N) * N);
    vec3 B = normalize(cross(T, N));
    mat3 TBN = mat3(T, B, N);
    mat3 worldToTangent = transpose(TBN);

    vec3 viewDirWorld = normalize(viewPos - fragWorldPos);
    vec3 viewDir = worldToTangent * viewDirWorld;

    float steepness = dot(vec3(0.0, 1.0, 0.0), N);

    // 1. Ray march using smooth continuous height blending (no spikes)
    vec2 winningUV = fragUV;
    if (parallaxDepth > 0.001) {
        winningUV = StepByStepParallaxMapping(fragUV, viewDir, fragWorldPos, steepness, parallaxDepth, parallaxLayers);
    }

    // 2. Determine the crisp winning material at the final resolved surface coordinate
    float winningMat = EvaluateFinalWinner(winningUV, fragWorldPos, steepness);

    // 3. Fetch properties for the winning material
    MaterialProps material = LookupAllMaterialProps(winningUV, winningMat);

    gAlbedo.rgb = material.albedo;
    if (DEBUG_VIEW) {
        gAlbedo.rgb = vec3(hash13(gl_PrimitiveID)) * COLOR_TEST;
    }
    if (material.alphaBit == 0) discard;

    gAlbedo.a = material.subSurface;

    vec3 normalTangentSpace = material.normal;
    gNormal.a = material.ambient;
    if (!gl_FrontFacing) normalTangentSpace.z *= -1.0;
    normalTangentSpace.xyz = normalize(normalTangentSpace.xyz);

    mat3 pixelNormalTBN = mat3(transpose(invViewMatrix)) * TBN;
    vec3 normal = normalize(pixelNormalTBN * normalTangentSpace.xyz);
    gNormal.xyz = compressNormal(normal);

    gMaterials.r = material.roughness;
    gMaterials.g = material.emission;
    gMaterials.b = material.metalic;
    gMaterials.a = material.height;
}