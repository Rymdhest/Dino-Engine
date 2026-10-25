#version 330

layout(location = 0) in vec3 position;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

uniform mat4 modelViewProjectionMatrix;
uniform mat4 u_Bones[14];

out float textureIndex;
out vec2 fragUV;

void main(void) {
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

	gl_Position = modelViewProjectionMatrix * skinnedPosition;
	fragUV = uv;
	textureIndex = materialIndex;
}