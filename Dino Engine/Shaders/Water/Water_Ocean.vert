#version 420

// Sea surface quads, one per terrain chunk that can hold sea (instanced). They sit at the top of the slab the waves
// move within; Water_Ocean.frag finds the actual surface below them along each view ray.

layout (location = 0) in vec2 corner;
layout (location = 1) in vec4 instanceRect;   // xy world xz corner, zw size

out vec3 fragWorldPosition;

uniform mat4 viewProjection;
uniform float surfaceHeight;

void main(void)
{
    vec2 xz = instanceRect.xy + corner * instanceRect.zw;
    fragWorldPosition = vec3(xz.x, surfaceHeight, xz.y);
    gl_Position = viewProjection * vec4(fragWorldPosition, 1.0);
}
