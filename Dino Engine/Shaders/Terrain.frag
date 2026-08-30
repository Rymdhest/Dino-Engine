#version 330 core

in vec2 fragUV;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in vec3 fragWorldPos;
in mat3 normalTBN;
in vec3 worldNormal;
in vec3 COLOR_TEST;
in float roadWeight;
in float grassWeight;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;

uniform float groundID;
uniform float rockID;
uniform float roadID;
uniform float grassID;
uniform float beachID;
uniform float transitionID;

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
    out float matA,    out float matB,    out float matC,
    out float weightA, out float weightB, out float weightC
) {
    float altitude = fragPos.y;

    // Accumulator registers for top 3
    vec3 candidateIDs = vec3(0.0);
    vec3 candidateWeights = vec3(-1.0);

    // Common Masks & Shared Factors
    float slopeFactor = 1.0-smoothstep(0.3, 1.0, steepness);
    float clampedRoad = clamp(roadW, 0.0, 1.0);
    float terrainMask = 1.0 - clampedRoad;

    // =========================================================================
    // 1. OVERRIDE / MASK MATERIALS (Roads, Structures)
    // =========================================================================
    float wRoad = clampedRoad * 6.0;
    ConsiderMaterial(roadID, wRoad, candidateIDs, candidateWeights);

    // =========================================================================
    // 2. SLOPE MATERIALS (Cliffs, Steep Rock)
    // =========================================================================
    float wRock = (slopeFactor) * 10.0 * terrainMask;
    ConsiderMaterial(rockID, wRock, candidateIDs, candidateWeights);

    // =========================================================================
    // 3. CONDITIONAL / TEXTURE-BASED MATERIALS (Grass, Vegetation)
    // =========================================================================
    // Lazy Evaluation: Only sample the grass map if terrain is flat enough and above water
    
    float wGrass =  grassWeight * 4.0 * terrainMask;
    ConsiderMaterial(grassID, wGrass, candidateIDs, candidateWeights);
    
    // =========================================================================
    // 4. ALTITUDE BANDS (Beach, Dirt Transition, High Ground, Snow)
    // =========================================================================
    float beachFactor = 1.0 - smoothstep(3.0, 5.0, altitude);
    float wBeach =  beachFactor * 10.0 * terrainMask;
    ConsiderMaterial(beachID, wBeach, candidateIDs, candidateWeights);

    float transitionFactor = smoothstep(3.0, 5.0, altitude) * (1.0 - smoothstep(5.0, 7.0, altitude));
    float wTransition = transitionFactor * 10.0 * terrainMask;
    ConsiderMaterial(transitionID, wTransition, candidateIDs, candidateWeights);

    float groundFactor = smoothstep(5.0, 7.0, altitude);
    // Scale ground weight down if grass is taking over
    float wGround =  groundFactor * 1.0 * terrainMask;
    ConsiderMaterial(groundID, wGround, candidateIDs, candidateWeights);

    // =========================================================================
    // 5. UNPACK OUTPUT
    // =========================================================================
    matA = candidateIDs.x; weightA = candidateWeights.x;
    matB = candidateIDs.y; weightB = candidateWeights.y;
    matC = candidateIDs.z; weightC = candidateWeights.z;
}

void main() {
    vec3 viewDir = normalize(TangentViewPos - TangentFragPos);
    float steepness = dot(vec3(0.0, 1.0, 0.0), worldNormal);

    // 1. Determine top 3 competing candidates based on spatial/macro rules
    float candidateA, candidateB, candidateC;
    float macroWeightA, macroWeightB, macroWeightC;

    GetCandidateMaterials(
        fragWorldPos,
        steepness,
        roadWeight,
        candidateA,
        candidateB,
        candidateC,
        macroWeightA,
        macroWeightB,
        macroWeightC
    );

    // 2. Parallax Phase: Compute UV offsets for Candidate A, B, and C
    vec2 uvA = fragUV;
    vec2 uvB = fragUV;
    vec2 uvC = fragUV;

    if (parallaxDepth > 0.001) {
        uvA = ParallaxMapping(fragUV, viewDir, candidateA, parallaxDepth, parallaxLayers);
        uvB = ParallaxMapping(fragUV, viewDir, candidateB, parallaxDepth, parallaxLayers);
        uvC = ParallaxMapping(fragUV, viewDir, candidateC, parallaxDepth, parallaxLayers);
    }

    // 3. Height Fetch: Sample displacement heights for top 3 candidates
    float heightA = lookupMaterial(uvA, candidateA).a;
    float heightB = lookupMaterial(uvB, candidateB).a;
    float heightC = lookupMaterial(uvC, candidateC).a;

    // 4. Evaluate macro-weighted micro-height scores
    float scoreA = heightA * macroWeightA;
    float scoreB = heightB * macroWeightB;
    float scoreC = heightC * macroWeightC;

    // 5. Winner selection across 3 candidates
    float winningMat = candidateA;
    vec2 winningUV = uvA;
    float maxScore = scoreA;

    if (scoreB > maxScore) {
        winningMat = candidateB;
        winningUV = uvB;
        maxScore = scoreB;
    }

    if (scoreC > maxScore) {
        winningMat = candidateC;
        winningUV = uvC;
    }

    // 6. Final Fetch: Retrieve properties for the winning material
    MaterialProps material = LookupAllMaterialProps(winningUV, winningMat);

    // 7. Output to G-Buffer
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

    vec3 normal = normalize(normalTBN * normalTangentSpace.xyz);
    gNormal.xyz = compressNormal(normal);

    gMaterials.r = material.roughness;
    gMaterials.g = material.emission;
    gMaterials.b = material.metalic;
    gMaterials.a = material.height;

}