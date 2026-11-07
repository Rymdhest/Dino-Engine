#version 430

layout(location = 0) in vec3 position;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;

layout(std430, binding = 3) readonly buffer WildlifeBones {
	mat4 wildlifeBones[];
};

uniform mat4 viewProjection;
uniform int firstBone;
uniform int bonesEach;

out float textureIndex;
out vec2 fragUV;

void main(void) {
	int first = firstBone + gl_InstanceID * bonesEach;
	int a = first + int(boneIndices.x + 0.5);
	int b = first + int(boneIndices.y + 0.5);
	float total = boneWeights.x + boneWeights.y;
	vec2 weights = total > 0.001 ? boneWeights / total : vec2(1.0, 0.0);
	mat4 skin = weights.y > 0.0 ? wildlifeBones[a] * weights.x + wildlifeBones[b] * weights.y : wildlifeBones[a];

	gl_Position = viewProjection * skin * vec4(position, 1.0);
	fragUV = uv;
	textureIndex = materialIndex;
}
