#version 430

// A drop's ring opening out and gone.

in vec2 fragCoord;
flat in vec4 fragLight;
flat in float fragAge;

layout (location = 0) out vec4 out_Colour;

uniform float opacity;

void main(void)
{
    float r = length(fragCoord);
    float ring = exp(-pow((r - 0.7) * 5.0, 2.0));
    // Right at the start it's a burst, and then only the ring.
    float burst = exp(-r * r * 6.0) * (1.0 - smoothstep(0.0, 0.25, fragAge));
    float alpha = (ring * (1.0 - fragAge) * (1.0 - fragAge) + burst) * opacity;
    if (alpha <= 0.002) discard;
    out_Colour = vec4(fragLight.rgb * alpha, alpha * 0.2);
}
