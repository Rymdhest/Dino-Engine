#version 430
#include globals.glsl

// Whatever's alive: one call for all of a shape, with every bone of every instance in one buffer, each already all the way into the world. That's why there's no model matrix here.

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

layout(std430, binding = 3) readonly buffer WildlifeBones {
	mat4 wildlifeBones[];
};

layout(std430, binding = 4) readonly buffer WildlifeTints {
	vec4 wildlifeTints[];
};

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 viewSpacePos;
out float textureIndex;
flat out float fragFade;

uniform int firstBone;
uniform int bonesEach;
uniform int firstTint;

void main() {
	int first = firstBone + gl_InstanceID * bonesEach;
	int a = first + int(boneIndices.x + 0.5);
	int b = first + int(boneIndices.y + 0.5);
	float total = boneWeights.x + boneWeights.y;
	vec2 weights = total > 0.001 ? boneWeights / total : vec2(1.0, 0.0);
	mat4 skin = weights.y > 0.0 ? wildlifeBones[a] * weights.x + wildlifeBones[b] * weights.y : wildlifeBones[a];

	vec4 worldPos = skin * vec4(position, 1.0);
	vec4 viewPos = viewMatrix * worldPos;
	viewSpacePos = viewPos.xyz;
	gl_Position = projectionMatrix * viewPos;

	// Bones turn and scale evenly, so a normal only has to be turned, and everything that reads it normalises it.
	mat3 turn = mat3(skin);
	vec3 N = normalize(turn * normal);
	vec3 T = turn * tangent;
	T = normalize(T - dot(T, N) * N);
	vec3 B = cross(N, T);
	normalTBN = mat3(viewMatrix) * mat3(T, B, N);

	vec4 tint = wildlifeTints[firstTint + gl_InstanceID];
	fragUV = uv;
	textureIndex = materialIndex;
	fragColor = color * tint.rgb;
	fragFade = tint.a;
}
