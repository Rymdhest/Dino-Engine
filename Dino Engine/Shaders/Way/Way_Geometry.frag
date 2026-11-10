#version 420

// An open way in the geometry pass (WayRenderer): an opaque surface that takes no light at all. Black, so the sun, the sky and every lamp add nothing to it; wholly occluded, so the
// ambient pass leaves it alone; and never in the sun. What it gives off comes in Way.frag, after the lights, in floats. It writes depth like anything solid, from either side, so it
// hides what's behind it and everything after it knows it's there.

#include Globals.glsl
#include gBufferUtil.glsl
#include Way/WayShape.glsl

in vec3 wayLocal;
in vec3 wayWorld;
in vec3 wayRuneData;
in vec2 wayRuneUV;
in vec3 wayWorldNormal;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

void main()
{
    if (!wayDrawn(wayLocal, wayWorld, wayRuneData)) discard;

    vec3 normal = normalize(mat3(viewMatrix) * wayWorldNormal);
    if (!gl_FrontFacing) normal = -normal;

    gAlbedo = vec4(0.0);
    gNormal = vec4(compressNormal(normal), 0.0);
    gMaterials = vec4(1.0, encodeEmission(0.0), 0.0, 0.0);
}
