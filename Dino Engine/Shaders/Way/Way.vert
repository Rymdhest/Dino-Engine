#version 420

// An open way and its runes' light (WayRenderer, WAYGATES.md), in the gate's own frame: x across, y up, the way's face at z = 0, front towards +z.
// The geometry pass and the light pass both run this, and the second has to land on exactly the depth of the first: hence invariant.

#include Globals.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;       // x 0 for the way and 1 for a rune; y when a rune lights, 0 to 1; z which rune
layout(location=2) in vec3 normal;
layout(location=4) in vec2 uv;          // A rune's: across its stroke, -1 to 1

uniform mat4 modelMatrix;

out vec3 wayLocal;
out vec3 wayWorld;
out vec3 wayRuneData;
out vec2 wayRuneUV;
out vec3 wayWorldNormal;

invariant gl_Position;

void main()
{
	vec4 world = modelMatrix * vec4(position, 1.0);
	gl_Position = projectionMatrix * (viewMatrix * world);
	wayLocal = position;
	wayWorld = world.xyz;
	wayRuneData = color;
	wayRuneUV = uv;
	wayWorldNormal = mat3(modelMatrix) * normal;
}
