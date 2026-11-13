#version 430
#define SHELL_INSTANCED

// The shells of what InstancedModelRenderer draws: stone, the rock's skin and trees (Shells/ShellVertex.glsl).

uniform int plant;
uniform vec3 modelSize;
uniform float focusOpens;   // 1 for what may thin away for the player at all (glModel.OpensForFocus): trees.

#include Plant.glsl
#include FocusOccluder.glsl

#include Shells/ShellVertex.glsl
