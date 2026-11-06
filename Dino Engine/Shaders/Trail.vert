#version 420

// A ribbon, from where a weapon's tip has been. Each point comes with how old it is and which edge of the ribbon it's on.

#include Globals.glsl

layout (location = 0) in vec3 position;
layout (location = 1) in vec2 ribbon;

out vec2 along;

void main(void)
{
	// ribbon: x how far along it, 0 the newest. y 0 at the hilt and 1 at the tip.
	along = ribbon;
	gl_Position = projectionMatrix * viewMatrix * vec4(position, 1.0);
}
