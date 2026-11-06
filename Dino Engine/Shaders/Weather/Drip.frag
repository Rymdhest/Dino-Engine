#version 430

// A drop, as a streak falling or a bead hanging, or the ring it leaves. Blended premultiplied, as what falls with it is (Precipitation.frag).

in vec2 fragCoord;
flat in vec4 fragLight;
flat in int fragKind;
flat in float fragAge;

layout (location = 0) out vec4 out_Colour;

void main(void)
{
    float strength;
    if (fragKind == 0)
    {
        float taper = sin(clamp(fragCoord.y, 0.0, 1.0) * 3.14159265);
        strength = exp(-fragCoord.x * fragCoord.x * 2.5) * sqrt(max(taper, 0.0));
    }
    else if (fragKind == 1)
    {
        strength = exp(-dot(fragCoord, fragCoord) * 3.2) - 0.04;
    }
    else
    {
        float r = length(fragCoord);
        float ring = exp(-pow((r - 0.72) * 6.0, 2.0)) * (1.0 - fragAge) * (1.0 - fragAge);
        float burst = exp(-r * r * 9.0) * (1.0 - smoothstep(0.0, 0.2, fragAge));
        strength = ring * 0.6 + burst;
    }
    float alpha = max(strength, 0.0) * fragLight.a;
    if (alpha <= 0.002) discard;
    out_Colour = vec4(fragLight.rgb * alpha, alpha * 0.25);
}
