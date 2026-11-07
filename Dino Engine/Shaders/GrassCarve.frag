#version 330 core

in vec3 fragWorldPos;
flat in float fragCard;
out vec4 fragColor;

uniform sampler2DArray normalHeightTextureArray;
uniform float chunkArrayID;
uniform vec2 chunkWorldPos;
uniform vec2 chunkSize;

void main() {
    // Only what's solid clears the grass under it. A card is mostly air, and carving its whole quad stamps a rectangle in the ground.
    if (fragCard > 0.5) discard;

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