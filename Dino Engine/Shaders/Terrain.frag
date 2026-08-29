#version 330

in vec2 fragUV;
in vec3 TangentViewPos;
in vec3 TangentFragPos;
in vec3 fragWorldPos;
in mat3 normalTBN;
in vec3 worldNormal;
in vec3 COLOR_TEST;
in float roadWeight;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;
uniform float groundID;
uniform float rockID;
uniform float roadID;
uniform float beachID;

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

void GetCandidateMaterials(
    vec3 fragPos,
    float steepness,
    float roadW,
    out float matA,
    out float matB,
    out float weightA,
    out float weightB
) {
    float altitude = fragPos.y;
    float beachMaxAltitude = 5.0;
    // Priority 1: Road vs Terrain (Ground / Rock / Beach)
    if (roadW > 0.001) {
        matA = roadID;
        weightA = roadW * 6.0;

        if (steepness < 0.5) {
            matB = rockID;
            weightB = (1.0 - steepness) * 6.0;
        } else if (altitude < beachMaxAltitude) {
            matB = beachID;
            weightB = 1.0;
        } else {
            matB = groundID;
            weightB = steepness * 1.0;
        }
        return;
    }

    // Priority 2: Beach Zone at Low Altitudes
    // Transition range around beachMaxAltitude
    float beachBlend = clamp((beachMaxAltitude - altitude) * 0.5 + 0.5, 0.0, 1.0);

    if (beachBlend > 0.0) {
        if (steepness < 0.5) {
            // Coastal Cliff: Rock vs Beach
            matA = rockID;
            weightA = (1.0 - steepness) * 6.0;
            matB = beachID;
            weightB = beachBlend * 3.0;
        } else {
            // Flat Shoreline: Beach vs Grass (Ground)
            matA = beachID;
            weightA = beachBlend * 3.0;
            matB = groundID;
            weightB = (1.0 - beachBlend) * 1.5;
        }
        return;
    }

    // Priority 3: Inland Terrain (Ground vs Rock)
    matA = groundID;
    weightA = steepness * 1.0;

    matB = rockID;
    weightB = (1.0 - steepness) * 6.0;
}

void main() {
    vec3 viewDir = normalize(TangentViewPos - TangentFragPos);
    float steepness = dot(vec3(0.0, 1.0, 0.0), worldNormal);

    // 1. Determine top 2 competing candidates based on spatial/macro rules
    float candidateA, candidateB;
    float macroWeightA, macroWeightB;

    GetCandidateMaterials(
        fragWorldPos,
        steepness,
        roadWeight,
        candidateA,
        candidateB,
        macroWeightA,
        macroWeightB
    );

    // 2. Parallax Phase: Compute offsets ONLY for Candidate A and Candidate B
    vec2 uvA = fragUV;
    vec2 uvB = fragUV;

    if (parallaxDepth > 0.001) {
        uvA = ParallaxMapping(fragUV, viewDir, candidateA, parallaxDepth, parallaxLayers);
        uvB = ParallaxMapping(fragUV, viewDir, candidateB, parallaxDepth, parallaxLayers);
    }

    // 3. Height Fetch: Sample displacement height ONLY for Candidate A and Candidate B
    float heightA = lookupMaterial(uvA, candidateA).a;
    float heightB = lookupMaterial(uvB, candidateB).a;

    // 4. Hard Selection: Evaluate macro-weighted micro-height scores
    float scoreA = heightA * macroWeightA;
    float scoreB = heightB * macroWeightB;

    float winningMat = candidateA;
    vec2 winningUV = uvA;

    if (scoreB > scoreA) {
        winningMat = candidateB;
        winningUV = uvB;
    }

    // 5. Final Fetch: Retrieve properties ONLY for the single winning material
    MaterialProps material = LookupAllMaterialProps(winningUV, winningMat);

    // 6. Output to G-Buffer
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