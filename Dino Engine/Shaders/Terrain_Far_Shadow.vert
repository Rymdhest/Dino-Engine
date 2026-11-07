#version 330

// The mountains that lie past the end of the streamed terrain, for the sun's shadow map alone. See TerrainRenderer.SetFarRock.

layout(location=0) in vec3 position;
layout(location = 3) in vec3 instanceChunkPos;
layout(location = 4) in vec3 instanceChunkSize;

uniform mat4 projectionViewMatrix;
uniform sampler2D farHeightMap;
uniform vec2 farHeightMapping;  // x texture units to a metre, y the offset of the first texel's centre

void main() {
	vec2 xz = position.xz * instanceChunkSize.xz + instanceChunkPos.xz;
	float height = texture(farHeightMap, xz * farHeightMapping.x + farHeightMapping.y).r;
	gl_Position = projectionViewMatrix * vec4(xz.x, height, xz.y, 1.0);
}
