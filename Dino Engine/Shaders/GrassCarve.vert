#version 330 core
layout(location = 0) in vec3 position;
layout(location = 5) in float materialIndex;

uniform mat4 modelMatrix;
uniform mat4 orthoProjectionView;
uniform float firstCard;

out vec3 fragWorldPos;
flat out float fragCard;

void main() {
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    fragWorldPos = worldPos.xyz;
    fragCard = materialIndex >= firstCard - 0.5 ? 1.0 : 0.0;
    
    // Top-down orthographic projection mapping (World X -> Screen X, World Z -> Screen Y)
    vec4 topDownPos = vec4(worldPos.x, worldPos.z, -worldPos.y, 1.0);
    gl_Position = orthoProjectionView * topDownPos;
}