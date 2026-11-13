#version 430
#define SHELL_TERRAIN

// The shells of the ground: its moss, and the moss on its rock (Shells/ShellVertex.glsl).

uniform float textureMapOffset;
uniform sampler2DArray normalHeightTextureArray;
uniform sampler2DArray surfaceWeightsA;
uniform sampler2DArray surfaceWeightsB;
uniform sampler2DArray surfaceWeightsC;
uniform ivec2 groundSlots;      // The palette's slot of the ground's moss and of its rock, -1 for none.
uniform sampler2D bendMap;
uniform sampler2D windMap;
uniform sampler2D touchMap;

#include Wind.glsl
#include Shells/ShellVertex.glsl
