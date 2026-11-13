#version 430
#define SHELL_WILDLIFE

// The shells of what's alive, WildlifeRenderer's: a rabbit's coat (Shells/ShellVertex.glsl).

uniform int firstBone;
uniform int bonesEach;
uniform int firstTint;
uniform sampler2D bendMap;
uniform sampler2D windMap;

#include Wind.glsl
#include Shells/ShellVertex.glsl
