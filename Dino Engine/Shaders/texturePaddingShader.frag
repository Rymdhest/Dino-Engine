#version 330

uniform sampler2D AlbedoIn;
uniform sampler2D NormalIn;
uniform sampler2D MaterialIn;

layout (location = 0) out vec4 AlbedoOut;
layout (location = 1) out vec4 NormalOut;
layout (location = 2) out vec4 MaterialsOut;

// Returns true only if the pixel is completely empty unrendered background
bool isEmpty(vec4 a, vec4 n, vec4 m) {
    return dot(a, a) == 0.0 && dot(n, n) == 0.0 && dot(m, m) == 0.0;
}

void main() {
    ivec2 centerCoord = ivec2(gl_FragCoord.xy);

    vec4 centerAlbedo = texelFetch(AlbedoIn, centerCoord, 0);
    vec4 centerNormal = texelFetch(NormalIn, centerCoord, 0);
    vec4 centerMat = texelFetch(MaterialIn, centerCoord, 0);

    // 1. If this pixel ALREADY has data (Original Mesh OR already Padded), 
    // pass it through EXACTLY as-is. This preserves your mesh's natural alpha perfectly.
    if (!isEmpty(centerAlbedo, centerNormal, centerMat)) {
        AlbedoOut = centerAlbedo;
        NormalOut = centerNormal;
        MaterialsOut = centerMat;
        return;
    }

    // 2. This is an empty background pixel. Look for a valid neighbor to copy.
    vec4 bestAlbedo = centerAlbedo;
    vec4 bestNormal = centerNormal;
    vec4 bestMat = centerMat;
    bool found = false;
    
    ivec2 texSize = textureSize(AlbedoIn, 0);

    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            if (x == 0 && y == 0) continue;

            ivec2 neighborCoord = centerCoord + ivec2(x, y);
            
            // Bounds check
            if (neighborCoord.x < 0 || neighborCoord.x >= texSize.x || 
                neighborCoord.y < 0 || neighborCoord.y >= texSize.y) continue;

            vec4 nAlbedo = texelFetch(AlbedoIn, neighborCoord, 0);
            vec4 nNormal = texelFetch(NormalIn, neighborCoord, 0);
            vec4 nMat = texelFetch(MaterialIn, neighborCoord, 0);

            // If the neighbor HAS data, copy it!
            if (!isEmpty(nAlbedo, nNormal, nMat)) {
                // Copy the neighbor's RGB, but force Alpha to 0.0 for the padded background
                bestAlbedo = vec4(nAlbedo.rgb, 0.0);
                bestNormal = nNormal;
                bestMat = nMat;
                found = true;
                break;
            }
        }
        if (found) break;
    }

    AlbedoOut = bestAlbedo;
    NormalOut = bestNormal;
    MaterialsOut = bestMat;
}