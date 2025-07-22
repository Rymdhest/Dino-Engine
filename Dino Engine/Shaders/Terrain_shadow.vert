#version 330

layout(location=0) in vec3 position;
layout(location = 3) in vec3 instanceChunkPos;
layout(location = 4) in vec3 instanceChunkSize;
layout(location = 5) in float instanceHeightMapID;

uniform float textureMapOffset;
uniform mat4 projectionViewMatrix;
uniform sampler2DArray normalHeightTextureArray;

void main() {
	float height = texture(normalHeightTextureArray, vec3(position.xz*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID)).w;
	vec3 localPos = vec3(position.x, height, position.z);
    vec3 worldPos = localPos*instanceChunkSize+instanceChunkPos;
	gl_Position =  projectionViewMatrix*vec4(worldPos,  1.0);
}

