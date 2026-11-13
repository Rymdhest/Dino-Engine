#version 430
#define SHELL_TERRAIN
#define SHELL_PLANAR

// The ground's moss and the moss on its rock (Shells/ShellFragment.glsl), its clumps laid out on the ground's own plane (Shells/Shell.glsl).

uniform float groundMossMaterial;   // The ground's moss, whose colour the moss standing on it takes, read as Terrain.frag reads it.

#include Shells/ShellFragment.glsl
