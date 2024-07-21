#version 330
in vec2 textureCoords;
layout (location = 0) out vec4 properties;

float hash21(vec2 p) {
    p = fract(p * vec2(0.1031, 0.1030));
    p += dot(p, p + vec2(19.19, 19.19));
    p = fract(vec2(p.x * p.y, p.y * p.x) * 19.19);
    return fract(p.x + p.y);
}
void main(void)
{
    float height = hash21(textureCoords*1000f);
    float roughness = .6f;
    float metalic = 0.0f;
    float glow = 0.0f;
    properties = vec4(roughness, glow, metalic, height);

}