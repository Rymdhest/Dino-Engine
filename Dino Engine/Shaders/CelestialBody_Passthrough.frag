#version 330
#include gBufferUtil.glsl

in vec2 textureCoords;

layout (location = 0) out vec4 out_color;

uniform sampler2D inputTexture;

void main() 
{
    out_color.rgba = vec4(texture(inputTexture, textureCoords).rgb, 0.0);
}
