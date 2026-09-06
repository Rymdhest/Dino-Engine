#version 330 core

in vec3 fragWorldPos;
out vec4 fragColor;

uniform sampler2DArray normalHeightTextureArray;
uniform float chunkArrayID;
uniform vec2 chunkWorldPos;
uniform vec2 chunkSize;

void main() {
    // Map fragment world XZ to the chunk's UV space [0, 1]
    vec2 localUV = (fragWorldPos.xz - chunkWorldPos) / chunkSize;
    
    // Out of bounds check
    if (localUV.x < 0.0 || localUV.x > 1.0 || localUV.y < 0.0 || localUV.y > 1.0) {
        discard;
    }

    // Sample terrain height from the chunk's height slice (stored in .w / .a)
    vec4 terrainData = texture(normalHeightTextureArray, vec3(localUV, chunkArrayID));
    float terrainHeight = terrainData.a; 

    // Height clipping boundaries:
    float depthAllowance = 0.2f;   // Allows geometry slightly under the terrain to still carve
    float maxCarveHeight = 0.85f;    // Prevents floating parts (like tree canopies) from carving

    // Discard if it's too far below or too far above the ground
    if (fragWorldPos.y < terrainHeight - depthAllowance || fragWorldPos.y > terrainHeight + maxCarveHeight) {
        discard;
    }

    // Carve out grass by outputting 0.0
    fragColor = vec4(0.0, 0.0, 0.0, 0.0);
}