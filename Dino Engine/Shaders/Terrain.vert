#version 330

layout(location=0) in vec3 position;
layout(location = 3) in vec3 instanceChunkPos;
layout(location = 4) in vec3 instanceChunkSize;
layout(location = 5) in float instanceHeightMapID;

out vec2 fragUV;
out mat3 normalTBN;
out mat3 TBN;
out vec3 worldNormal;
out vec3 TangentViewPos;
out vec3 TangentFragPos;
out vec3 fragWorldPos;
out vec3 COLOR_TEST;
out float roadWeight;
out float grassWeight;

uniform vec3 viewPos;
uniform mat4 invViewMatrix;
uniform mat4 projectionViewMatrix;
uniform float textureMapOffset;
uniform float textureTileSize;
uniform sampler2DArray normalHeightTextureArray;
uniform sampler2DArray grassTextureArray;

#include procedural/fastHash.glsl

vec3 reconstructTangent(vec2 uv)
{
	float hL = texture(normalHeightTextureArray, vec3((uv - vec2(textureMapOffset, 0.0))*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID)).w;
	float hR = texture(normalHeightTextureArray, vec3((uv + vec2(textureMapOffset, 0.0))*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID)).w;
    float dHeight_dx = (hR - hL) / (2.0 * textureMapOffset); // Assuming world scale == UV scale
    return normalize(vec3(1.0, dHeight_dx, 0.0));
}

void main() {
	vec4 textureData =  texture(normalHeightTextureArray, vec3(position.xz*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID)).xyzw;
	vec4 textureData2 =  texture(grassTextureArray, vec3(position.xz*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID)).xyzw;
	float height = textureData.a;
	roadWeight = textureData.z;
	grassWeight = textureData2.r;
	float nx = textureData.x;
	float nz = textureData.y;
	float ny = sqrt(max(0.0, 1.0 - (nx * nx + nz * nz)));
	worldNormal = normalize(vec3(nx, ny, nz));
	vec3 localPos = vec3(position.x, height, position.z);
    vec3 worldPos = localPos*instanceChunkSize+instanceChunkPos;

	vec3 N = worldNormal;
	vec3 T = reconstructTangent(position.xz);
	T = normalize(T - dot(T, N) * N); // Gram-Schmidt
	vec3 B = normalize(cross(T, N));
	mat3 TBN = mat3(T, B, N);
	fragWorldPos = worldPos;
	TangentFragPos = (worldPos)*TBN;
	TangentViewPos = (viewPos)*TBN;
	normalTBN = mat3(transpose(invViewMatrix))*TBN;

	gl_Position =  projectionViewMatrix*vec4(worldPos,  1.0);

	COLOR_TEST = vec3(hash13(gl_InstanceID));


	fragUV = (worldPos.xz)/textureTileSize;
}

