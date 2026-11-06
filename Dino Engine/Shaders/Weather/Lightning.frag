#version 430

// A bolt's channel: a white-hot core and a blue glow around it. Bloom spreads what's left.

in float fragAcross;
flat in float fragStrength;

layout (location = 0) out vec4 out_Colour;

uniform float brightness;

void main(void)
{
    float x = abs(fragAcross);
    float core = exp(-x * x * 26.0);
    float glow = exp(-x * x * 3.4) * 0.16;
    vec3 colour = vec3(0.92, 0.95, 1.0) * core + vec3(0.46, 0.6, 1.0) * glow;
    out_Colour = vec4(colour * (brightness * fragStrength), 0.0);
}
