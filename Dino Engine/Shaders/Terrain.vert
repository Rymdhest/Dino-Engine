#version 330 core

layout(location=0) in vec3 position;
layout(location=3) in vec3 instanceChunkPos;
layout(location=4) in vec3 instanceChunkSize;
layout(location=5) in float instanceHeightMapID;

out vec2 fragUV;
out vec2 fragInstanceChunkPos;
out vec2 fragInstanceChunkSizeXZ;
out float fragInstanceHeightMapID;
out vec3 fragWorldPos;
out vec3 fragWorldNormal;
out vec3 COLOR_TEST;

uniform float textureMapOffset;
uniform float textureTileSize;
uniform sampler2DArray normalHeightTextureArray;
uniform mat4 projectionViewMatrix;

#include procedural/fastHash.glsl

void main() {
    vec3 localPos = position;
    vec3 worldPos = localPos * instanceChunkSize + instanceChunkPos;

    vec4 textureData = texture(normalHeightTextureArray, vec3(position.xz*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), instanceHeightMapID));
    float height = textureData.a;
    
    // Center displacement around 0.5: grooves sink below base, peaks rise above base.
    // This allows trees/grass placed at the base position to sit naturally embedded.
    worldPos.y += (height + 0.15) * instanceChunkSize.y;

    fragWorldPos = worldPos;
    fragWorldNormal = normalize(vec3(textureData.x, sqrt(max(0.0, 1.0 - (textureData.x * textureData.x + textureData.y * textureData.y))), textureData.y));

    fragUV = worldPos.xz / textureTileSize;
    fragInstanceChunkPos = instanceChunkPos.xz;
    fragInstanceChunkSizeXZ = instanceChunkSize.xz;
    fragInstanceHeightMapID = instanceHeightMapID;

    gl_Position = projectionViewMatrix * vec4(worldPos, 1.0);
    COLOR_TEST = vec3(hash13(gl_InstanceID));
}