#version 420
#include globals.glsl

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;
out vec3 TangentViewPos;
out vec3 TangentFragPos;
out float textureIndex;

uniform mat4 modelMatrix;
uniform mat4 normalModelViewMatrix;
uniform mat4 u_Bones[14];

void main() {
    // 1. Calculate combined weight and normalize
    float totalWeight = boneWeights.x + boneWeights.y;
    mat4 skinMatrix;

    int idxA = int(floor(boneIndices.x + 0.5));
    int idxB = int(floor(boneIndices.y + 0.5));

    if (totalWeight < 0.001) {
        skinMatrix = u_Bones[idxA];
    } else {
        vec2 normWeights = boneWeights / totalWeight;
        skinMatrix = normWeights.x * u_Bones[idxA] +
                     normWeights.y * u_Bones[idxB];
    }

    // 2. Transform vertex attributes into skinned space (Entity Model Space)
    vec4 skinnedPosition = skinMatrix * vec4(position, 1.0);
    
    mat3 skinMat3 = mat3(skinMatrix);
    mat3 skinNormalMat = transpose(inverse(skinMat3));
    
    vec3 skinnedNormal  = normalize(skinNormalMat * normal);
    vec3 skinnedTangent = normalize(skinMat3 * tangent);

    // 3. Transform from Entity Local Space -> World Space -> Clip Space
    vec4 worldPos = modelMatrix * skinnedPosition;
    gl_Position = projectionMatrix * viewMatrix * worldPos;

    fragUV = uv;
    textureIndex = materialIndex;
    worldNormal = normalize(mat3(modelMatrix) * skinnedNormal);

    // 4. Construct TBN Matrix with Gram-Schmidt re-orthogonalization
    vec3 N = normalize(skinnedNormal);
    vec3 T = normalize(skinnedTangent - dot(skinnedTangent, N) * N);
    vec3 B = cross(N, T);

    mat3 TBN = mat3(T, B, N);
    mat3 invTBN = transpose(TBN); // Model Space -> Tangent Space

    // 5. Transform positions to Tangent Space
    TangentFragPos = invTBN * skinnedPosition.xyz;
     
    // Model-space view position transformed into tangent space
    vec3 modelViewPos = (inverse(modelMatrix) * vec4(viewPosWorld, 1.0)).xyz;
    TangentViewPos = invTBN * modelViewPos;
    
    normalTBN = mat3(normalModelViewMatrix) * TBN;
    fragColor = color;

}