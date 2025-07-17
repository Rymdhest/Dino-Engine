#version 420
#include globals.glsl

layout(location=0) in vec3 positions;

uniform mat4 TransformationMatrix;

void main() {
	gl_Position =  projectionMatrix*viewMatrix*TransformationMatrix*vec4(positions, 1.0);
}