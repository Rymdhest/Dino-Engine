#version 330
in vec2 textureCoords;

layout (location = 0) out vec4 albedoOut;
layout (location = 1) out vec4 materialsOut;

uniform sampler2D inputTexture1;
uniform sampler2D inputTexture2;
uniform float borderBlendRadius;
uniform vec2 texSize;

float smoothBlend(float t) {
    return t * t * (3.0 - 2.0 * t);
}
vec2 getWrappedCoords(vec2 uv, vec2 size) {
    return mod(uv, size) / size;
}
vec4 makeSeamsless(sampler2D originalTexture, vec2 TexCoords) {
    
    return texture(originalTexture, TexCoords+vec2(0.5f));
}

void main()
{
    albedoOut = makeSeamsless(inputTexture1, textureCoords);
    materialsOut = makeSeamsless(inputTexture2, textureCoords);
}