#version 330

layout (location = 0) out vec4 out_Colour;

uniform vec3 color;
uniform float time;
uniform float seed;

in vec2 along;

void main(void){
	float across = abs(along.x);
	float height = along.y;

	// A hot thread in the middle of a soft shaft: most of what the eye takes for a beam is the falloff around it.
	float core = exp(-across * across * 90.0);
	float shaft = pow(max(1.0 - across, 0.0), 3.0);

	// Light climbing it, in slow bands that don't line up from one side to the other.
	float climb = 0.5 + 0.5 * sin(height * 19.0 - time * 3.4 + seed + along.x * 5.0);
	float rising = 0.5 + 0.5 * sin(height * 7.0 - time * 1.7 + seed * 2.3);
	float bands = 0.62 + 0.38 * climb * rising;

	// It leaves the ground all at once and thins away to nothing at the top, so there's no end to see.
	float foot = smoothstep(0.0, 0.02, height);
	float fade = pow(1.0 - height, 1.6);

	vec3 hue = color.rgb;
	float hottest = max(hue.r, max(hue.g, hue.b));
	vec3 glow = hue * (shaft * bands * 0.8) + mix(hue, vec3(hottest), 0.3) * (core * 1.3);

	// Light only added goes to white over a sunlit field, and a beam has to be its colour there above all. So it also stands in front of what's behind it, a little.
	float cover = min(shaft * 0.85 + core * 0.5, 0.95);
	out_Colour = vec4(glow * fade * foot, cover * fade * foot);
}
