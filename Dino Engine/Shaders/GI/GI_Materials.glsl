#ifndef DINO_GI_MATERIALS_GLSL
#define DINO_GI_MATERIALS_GLSL

// What a compute shader colouring the ground needs of the materials: their albedo, of which only the mean is read, and the world's
// geology for its rock. textureUtil.glsl can't be used here, as it takes derivatives no compute shader has.

uniform sampler2DArray albedoMapTextureArray;

#include Geology.glsl

#endif
