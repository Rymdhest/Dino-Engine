#version 330 core
layout(location = 0) in vec3 position;

uniform mat4 modelMatrix;
uniform mat4 orthoProjectionView;

out vec3 fragWorldPos;

void main() {
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    fragWorldPos = worldPos.xyz;
    
    // Top-down orthographic projection mapping (World X -> Screen X, World Z -> Screen Y)
    vec4 topDownPos = vec4(worldPos.x, worldPos.z, -worldPos.y, 1.0);
    gl_Position = orthoProjectionView * topDownPos;
}