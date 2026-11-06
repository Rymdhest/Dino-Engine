#version 430

// A streak or a dot. Rain is mostly light passing through water, so it adds to what's behind it and hides little of it; flakes hide more. Blended premultiplied, with alpha as how much of
// what's behind stays hidden.

in vec2 fragCoord;
flat in vec4 fragLight;
flat in int fragRound;

layout (location = 0) out vec4 out_Colour;

uniform float hides;    // Share of its strength that blocks what's behind it.

void main(void)
{
    float strength;
    if (fragRound != 0)
    {
        float r = dot(fragCoord, fragCoord);
        strength = exp(-r * 3.2) - 0.04;
    }
    else
    {
        float taper = sin(clamp(fragCoord.y, 0.0, 1.0) * 3.14159265);
        strength = exp(-fragCoord.x * fragCoord.x * 2.5) * sqrt(max(taper, 0.0));
    }
    float alpha = max(strength, 0.0) * fragLight.a;
    if (alpha <= 0.0005) discard;
    out_Colour = vec4(fragLight.rgb * alpha, alpha * hides);
}
