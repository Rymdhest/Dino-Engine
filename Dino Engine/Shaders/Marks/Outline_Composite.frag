#version 420

// Colours whatever lies just outside a body in the mask, so that the body itself stays as it was. Two rings of taps find the nearest of them, and the edge of the line is softened by a pixel.

layout (location = 0) out vec4 out_Colour;

uniform sampler2D mask;
uniform float thickness;
uniform float brightness;

in vec2 textureCoords;

const int Taps = 12;
const float Tau = 6.2831853;

void main(void)
{
	if (texture(mask, textureCoords).a > 0.5) discard;

	vec2 texel = 1.0 / vec2(textureSize(mask, 0));
	vec3 found = vec3(0.0);
	float nearest = 1000.0;
	for (int ring = 1; ring <= 2; ring++)
	{
		float reach = thickness * float(ring) * 0.5;
		for (int i = 0; i < Taps; i++)
		{
			float angle = Tau * (float(i) + 0.5 * float(ring)) / float(Taps);
			vec4 there = texture(mask, textureCoords + vec2(cos(angle), sin(angle)) * reach * texel);
			if (there.a > 0.5 && reach < nearest)
			{
				nearest = reach;
				found = there.rgb;
			}
		}
	}
	if (nearest > 999.0) discard;

	float shown = clamp(thickness + 1.0 - nearest, 0.0, 1.0);
	out_Colour = vec4(found * brightness * shown, shown);
}
