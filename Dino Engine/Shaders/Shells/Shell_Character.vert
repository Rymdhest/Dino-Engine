#version 430
#define SHELL_CHARACTER

// The shells of a body CharacterRenderer draws: an animal's coat (Shells/ShellVertex.glsl).

uniform mat4 modelMatrix;
uniform mat4 u_Bones[48];
uniform sampler2D bendMap;
uniform sampler2D windMap;

#include Wind.glsl
#include Shells/ShellVertex.glsl
