#ifndef DINO_GI_HEIGHT_GLSL
#define DINO_GI_HEIGHT_GLSL

// The world's height map, as the world was generated (WorldData.Height), for where the ground is. Needs GI/GI.glsl.

uniform sampler2D giHeightMap;

float giGroundHeight(vec2 xz)
{
    return textureLod(giHeightMap, xz * giHeightMapping.xy + giHeightMapping.zw, 0.0).r;
}

#endif
