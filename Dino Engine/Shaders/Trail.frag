#version 420

// What's left in the air by a blow. It's over the scene with the particles, and it's only there for a moment, so all it has to say is where the blade went.

layout (location = 0) out vec4 out_Colour;

uniform vec3 colour;
uniform float strength;

in vec2 along;

void main(void)
{
	float age = clamp(along.x, 0.0, 1.0);

	// Bright at the tip, where a blade is going fastest, and gone by the hilt. It fades as it gets older, and sooner on the inside.
	float tip = pow(clamp(along.y, 0.0, 1.0), 1.6);
	float fade = pow(1.0 - age, 1.6);
	float alpha = tip * fade * strength;

	vec3 hot = mix(colour, vec3(1.0), pow(tip, 3.0) * (1.0 - age) * 0.7);
	out_Colour = vec4(hot * alpha * 2.2, alpha * 0.7);
}
