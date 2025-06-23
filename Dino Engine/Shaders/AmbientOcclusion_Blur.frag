#version 330
in vec2 textureCoords;

layout (location = 1) out vec4 gNormal;

uniform sampler2D ssaoInput;

void main() 
{
    gNormal.a = texture(ssaoInput, textureCoords).r;
}
