#version 330
#include gBufferUtil.glsl

in vec2 textureCoords;

layout (location = 1) out vec4 gNormal;

uniform sampler2D ssaoInput;

void main() 
{
    gNormal.a = texture(ssaoInput, textureCoords).r;
}
