#version 330
uniform sampler2D srcTexture;
uniform float strength;
uniform vec2 srcResolution;
in vec2 textureCoords;
layout (location = 0)

out vec4 upsample;

void main()
{
    // One source texel out: a tent over the texels around this point, so the blur stays round as it grows.
    float x = 1.0 / srcResolution.x;
    float y = 1.0 / srcResolution.y;

    // Take 9 samples around current texel:
    // a - b - c
    // d - e - f
    // g - h - i
    // === ('e' is the current texel) ===
    vec3 a = texture(srcTexture, vec2(textureCoords.x - x, textureCoords.y + y)).rgb;
    vec3 b = texture(srcTexture, vec2(textureCoords.x,     textureCoords.y + y)).rgb;
    vec3 c = texture(srcTexture, vec2(textureCoords.x + x, textureCoords.y + y)).rgb;

    vec3 d = texture(srcTexture, vec2(textureCoords.x - x, textureCoords.y)).rgb;
    vec3 e = texture(srcTexture, vec2(textureCoords.x,     textureCoords.y)).rgb;
    vec3 f = texture(srcTexture, vec2(textureCoords.x + x, textureCoords.y)).rgb;

    vec3 g = texture(srcTexture, vec2(textureCoords.x - x, textureCoords.y - y)).rgb;
    vec3 h = texture(srcTexture, vec2(textureCoords.x,     textureCoords.y - y)).rgb;
    vec3 i = texture(srcTexture, vec2(textureCoords.x + x, textureCoords.y - y)).rgb;

    // Apply weighted distribution, by using a 3x3 tent filter:
    //  1   | 1 2 1 |
    // -- * | 2 4 2 |
    // 16   | 1 2 1 |
    upsample.rgb = e*4.0;
    upsample.rgb += (b+d+f+h)*2.0;
    upsample.rgb += (a+c+g+i);
    upsample.rgb *= strength / 16.0;

    upsample.a = 1.0;
}