#version 420 core

#include Globals.glsl

in vec2 fragUV;
in vec2 fragInstanceChunkPos;
in vec2 fragInstanceChunkSizeXZ;
in float fragInstanceHeightMapID;
in vec3 fragWorldPos;
in vec3 fragWorldNormal;
in vec3 COLOR_TEST;

uniform vec3 viewPos;
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

// The parallax hit always lies further along the view ray than the rasterized surface, so depth only grows.
// Declaring that keeps early depth rejection working even though gl_FragDepth is written.
layout (depth_greater) out float gl_FragDepth;

#include textureUtil.glsl
#include gBufferUtil.glsl
#include procedural/fastHash.glsl
#include Grass/GrassField.glsl

// parallaxDepth * PARALLAX_DEPTH_SCALE is the height field depth in texture tiles.
const float PARALLAX_DEPTH_SCALE = 0.04;
// Max sideways travel of the parallax ray, as a multiple of the height field depth (tan of ~83 degrees).
const float PARALLAX_MAX_TAN = 8.0;

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

    float wGrass = grassW * 6.0 * terrainMask;
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

struct TerrainMaterialSample {
    vec3 mats;
    vec3 weights;
    vec3 heights;
};

// Picks the candidate materials at a tile UV and samples their heights.
// Derivatives are passed in because this runs inside the parallax loop, where implicit derivatives are undefined.
// uvDX/uvDY are in tile UV space, macroDX/macroDY in the chunk's heightmap UV space.
TerrainMaterialSample SampleTerrainMaterials(vec2 uv, float altitude, float steepness, vec2 uvDX, vec2 uvDY, vec2 macroDX, vec2 macroDY) {
    vec2 worldXZ = uv * textureTileSize;
    vec2 localXZ = clamp((worldXZ - fragInstanceChunkPos) / fragInstanceChunkSizeXZ, 0.0, 1.0);
    vec2 macroCoord = localXZ * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0);

    float roadW = textureGrad(normalHeightTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).z;
    float grassW = textureGrad(grassTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).r;

    TerrainMaterialSample s;
    GetCandidateMaterials(vec3(worldXZ.x, altitude, worldXZ.y), steepness, roadW, grassW,
        s.mats.x, s.mats.y, s.mats.z, s.weights.x, s.weights.y, s.weights.z);

    s.heights.x = textureGrad(materialMapTextureArray, vec3(uv, s.mats.x), uvDX, uvDY).a;
    s.heights.y = textureGrad(materialMapTextureArray, vec3(uv, s.mats.y), uvDX, uvDY).a;
    s.heights.z = textureGrad(materialMapTextureArray, vec3(uv, s.mats.z), uvDX, uvDY).a;
    return s;
}

// Smoothly blended depth below the top of the height field (0 = top, 1 = bottom), so the march sees no false cliffs
float BlendedDepth(TerrainMaterialSample s) {
    return 1.0 - dot(s.heights, s.weights);
}

// The single sharp winning material at the resolved surface coordinate
float WinningMaterial(TerrainMaterialSample s) {
    vec3 scores = s.heights * s.weights;

    float winningMat = s.mats.x;
    float maxScore = scores.x;

    if (scores.y > maxScore) {
        winningMat = s.mats.y;
        maxScore = scores.y;
    }
    if (scores.z > maxScore) {
        winningMat = s.mats.z;
    }

    return winningMat;
}

// Parallax occlusion mapping, done in world space.
// The surface near the fragment is treated as its tangent plane (normal N), with the height field hanging up to
// depthWorld below it along -N. The view ray R(t) = fragWorldPos - V*t is t*dot(N, V) below that plane, and
// projecting it back onto the plane along N gives the UV it lies under. Terrain UVs are a planar XZ projection
// (uv = worldXZ / textureTileSize), so that UV is exact on any slope, with no tangent frame involved.
// Returns the hit UV. rayT gets the distance along -V from fragWorldPos to the hit, for the depth write.
vec2 ParallaxOcclusionMapping(vec2 uv, vec3 V, vec3 N, float altitude, float steepness, vec2 uvDX, vec2 uvDY, vec2 macroDX, vec2 macroDY, out float rayT) {
    rayT = 0.0;

    float NdotV = dot(N, V);
    if (NdotV <= 1e-4) return uv;

    // At grazing angles the ray travels depth * tan(theta) sideways. Rather than bending the ray (so the hit would
    // leave this pixel's view ray), make the height field shallower so the sideways travel stays capped.
    float depthWorld = parallaxDepth * PARALLAX_DEPTH_SCALE * textureTileSize;
    float tanTheta = sqrt(max(1.0 - NdotV * NdotV, 0.0)) / NdotV;
    depthWorld *= min(1.0, PARALLAX_MAX_TAN / max(tanTheta, 1e-4));

    float numLayers = mix(parallaxLayers * 2.0, parallaxLayers, NdotV);
    float layerStep = 1.0 / numLayers;
    // Enough steps that the last layer reaches the bottom of the height field, so the ray always hits
    int maxSteps = int(ceil(numLayers));

    // Ray distance to descend one layer, and how far the UV moves per layer
    float tPerLayer = depthWorld * layerStep / NdotV;
    vec2 uvPerLayer = -(V - N * NdotV).xz * (tPerLayer / textureTileSize);

    float prevSurfaceDepth = BlendedDepth(SampleTerrainMaterials(uv, altitude, steepness, uvDX, uvDY, macroDX, macroDY));
    if (prevSurfaceDepth <= 0.0) return uv;

    float prevLayerDepth = 0.0;
    float layerDepth = 0.0;
    float surfaceDepth = prevSurfaceDepth;
    int layer = 0;

    while (layer < maxSteps) {
        layer++;
        layerDepth = float(layer) * layerStep;
        surfaceDepth = BlendedDepth(SampleTerrainMaterials(uv + uvPerLayer * float(layer), altitude, steepness, uvDX, uvDY, macroDX, macroDY));
        if (layerDepth >= surfaceDepth) break;
        prevLayerDepth = layerDepth;
        prevSurfaceDepth = surfaceDepth;
    }

    // Intersect the ray with the height field linearly interpolated between the last two layers.
    // before > 0 (the ray was above the surface) and after <= 0 (it is at or below it). The guard only matters if
    // float rounding left the last layer a hair above the bottom; the hit then lands on that last layer.
    float before = prevSurfaceDepth - prevLayerDepth;
    float after = surfaceDepth - layerDepth;
    float weight = clamp(before / max(before - after, 1e-6), 0.0, 1.0);

    float hitLayer = mix(float(layer - 1), float(layer), weight);
    rayT = hitLayer * tPerLayer;
    return uv + uvPerLayer * hitLayer;
}

void main() {
    vec3 N = normalize(fragWorldNormal);
    // Tangent of the planar UV mapping: the in-plane direction of +u (world +X at constant Z).
    // Only degenerate on vertical faces, which a heightmap can't produce.
    vec3 T = vec3(N.y, -N.x, 0.0);
    float tangentLength = length(T);
    T = tangentLength > 1e-4 ? T / tangentLength : vec3(1.0, 0.0, 0.0);
    vec3 B = cross(T, N);
    mat3 TBN = mat3(T, B, N);

    vec3 V = normalize(viewPos - fragWorldPos);

    float steepness = dot(vec3(0.0, 1.0, 0.0), N);

    vec2 uvDX = dFdx(fragUV);
    vec2 uvDY = dFdy(fragUV);
    vec2 uvToMacro = textureTileSize / fragInstanceChunkSizeXZ * (1.0 - textureMapOffset);
    vec2 macroDX = uvDX * uvToMacro;
    vec2 macroDY = uvDY * uvToMacro;

    // 1. Ray march using smoothly blended heights to eliminate false cliffs and window artifacts
    vec2 winningUV = fragUV;
    float rayT = 0.0;
    if (parallaxDepth > 0.0001) {
        winningUV = ParallaxOcclusionMapping(fragUV, V, N, fragWorldPos.y, steepness, uvDX, uvDY, macroDX, macroDY, rayT);
    }

    // 2. Determine the single sharp winning material at the final resolved surface coordinate
    float winningMat = WinningMaterial(SampleTerrainMaterials(winningUV, fragWorldPos.y, steepness, uvDX, uvDY, macroDX, macroDY));

    // 3. Write the depth of the parallax hit so trees/grass don't float. The hit is on this pixel's view ray, rayT
    //    behind the rasterized surface. gl_FragDepth is written on every path; leaving it unwritten is undefined.
    vec4 clipPos = projectionMatrix * viewMatrix * vec4(fragWorldPos - V * rayT, 1.0);
    gl_FragDepth = max(gl_FragCoord.z, (clipPos.z / clipPos.w) * 0.5 + 0.5);

    // 4. Lookup final properties using the crisp winning material. Explicit gradients, because winningUV jumps at
    //    occlusion edges and its own derivatives would pick the wrong mip level there.
    MaterialProps material = LookupAllMaterialPropsGrad(winningUV, winningMat, uvDX, uvDY);

    gAlbedo.rgb = material.albedo;

    // Under the grass the ground takes on the colour of the field, so gaps between blades read as more grass.
    vec2 macroCoord = clamp((fragWorldPos.xz - fragInstanceChunkPos) / fragInstanceChunkSizeXZ, 0.0, 1.0) * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0);
    float tintRoad = textureGrad(normalHeightTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).z;
    float tintGrass = textureGrad(grassTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).r;
    float tintFertility = grassFertility(tintGrass, N.y, tintRoad);
    gAlbedo.rgb = grassGroundTint(gAlbedo.rgb, fragWorldPos.xz, tintFertility, length(viewPos - fragWorldPos));

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
