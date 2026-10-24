#version 330

layout(location = 0) in vec3 position;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in ivec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

uniform mat4 modelViewProjectionMatrix;
uniform mat4 u_Bones[14];

out float textureIndex;
out vec2 fragUV;

void main(void) {
	mat4 skinMatrix = boneWeights.x * u_Bones[boneIndices.x] +
	                  boneWeights.y * u_Bones[boneIndices.y];

	vec4 skinnedPosition = skinMatrix * vec4(position, 1.0);

	gl_Position = modelViewProjectionMatrix * skinnedPosition;
	fragUV = uv;
	textureIndex = materialIndex;
}