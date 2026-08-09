#version 330
in vec3 fragColor;
in vec2 fragUV;
in mat3 normalTBN;
in float textureIndex;
in vec3 positionViewSpace_pass;

uniform int numberOfMaterials;
uniform float maxDepth;

uniform int isOverdrawPass; 
uniform sampler2D overdrawTexture;
uniform vec2 resolution;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;
uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

#include textureUtil.glsl

void main() {
    gAlbedo = lookupAlbedo(fragUV, textureIndex);
    
    if (gAlbedo.a < 0.5) discard; 

    // ==========================================
    // PASS 0: OVERDRAW / THICKNESS COUNT
    // ==========================================
    if (isOverdrawPass == 1) {
        gAlbedo = vec4(4.0 / 255.0, 0.0, 0.0, 1.0);
        gNormal = vec4(0.0);
        gMaterials = vec4(0.0);
        return;
    }

    // ==========================================
    // PASS 1: MODEL BAKE TO G-BUFFER
    // ==========================================
    // 1. Framebuffer UV across the baked imposter image [0.0 to 1.0]
    vec2 screenUV = gl_FragCoord.xy / resolution;

    // 2. Clean Albedo
    gAlbedo.rgb *= fragColor;

    // 3. Read Pass 0 Overdraw Layer Count
    float layerValue = texture(overdrawTexture, screenUV).r * 255.0;
    int layerCount = int(round(layerValue / 4.0));

    // 4. Read Raw Local Leaf Normal
    NormalLookupResult normalLookup = lookupNorma(fragUV, textureIndex);
    vec3 rawNormal = normalize(normalTBN * normalLookup.normal);
    if (!gl_FrontFacing) rawNormal *= -1.0;

    vec3 finalNormal = rawNormal;

    // Store in [0, 1] range for G-buffer
    gNormal.xyz = (finalNormal * 0.5) + 0.5;

    // 6. BALANCED SUBSURFACE SCATTERING (Fixes Dark Backlighting):
    // Smooth pow(0.65) decay with a 0.20 floor so intermediate layers retain
    // translucent transmission while the dense core blocks full light bleed.
    float sssDecay = max(pow(0.5, float(max(0, layerCount - 1))), 0.00);
    gNormal.z = normalLookup.SSS * sssDecay;

    // 7. VOLUME AO DECAY:
    // Outer leaves keep ~1.0 AO. Inner branch gaps decay to 0.20 floor to keep cavities dark.
    float aoDecay = max(pow(0.55, float(max(0, layerCount - 1))), 1.00);
    gNormal.a = normalLookup.ambient * aoDecay;

    float depth = -positionViewSpace_pass.z / maxDepth;
    gMaterials = lookupMaterial(fragUV, textureIndex).rgba;
    gMaterials.a += 1.0 - depth;
    gMaterials.a *= 0.5;
}