#version 420

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec2 uv;
layout(location = 5) in float materialIndex;
#ifdef SKINNED
layout(location = 6) in vec2 boneIndices;
layout(location = 7) in vec2 boneWeights;
#endif

out vec3 fragColor;
out vec2 fragUV;
out float textureIndex;
out mat3 normalTBN;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
#ifdef SKINNED
uniform mat4 u_Bones[32];
#endif

void main() {
	vec3 place = position;
	vec3 n = normal;
	vec3 t = tangent;
#ifdef SKINNED
	float total = boneWeights.x + boneWeights.y;
	int a = int(floor(boneIndices.x + 0.5));
	int b = int(floor(boneIndices.y + 0.5));
	mat4 skin = total < 0.001 ? u_Bones[a] : (boneWeights.x / total) * u_Bones[a] + (boneWeights.y / total) * u_Bones[b];
	place = (skin * vec4(position, 1.0)).xyz;
	n = mat3(skin) * normal;
	t = mat3(skin) * tangent;
#endif

	gl_Position = projectionMatrix * viewMatrix * vec4(place, 1.0);
	fragColor = color;
	fragUV = uv;
	textureIndex = materialIndex;

	vec3 N = normalize(mat3(viewMatrix) * n);
	vec3 T = normalize(mat3(viewMatrix) * t);
	T = normalize(T - dot(T, N) * N);
	normalTBN = mat3(T, cross(N, T), N);
}
