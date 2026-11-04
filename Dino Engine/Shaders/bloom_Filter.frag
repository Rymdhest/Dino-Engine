#version 330

// What bloom spreads: only light brighter than the threshold, with a soft knee so nothing switches on at an edge. The
// rest of the scene is left out entirely, or the blur would lay a veil over the whole picture.

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D shadedInput;
uniform float threshold;
uniform float knee;

void main(void){

	vec3 colour = texture(shadedInput, textureCoords).rgb;
	float brightness = max(colour.r, max(colour.g, colour.b));

	float soft = clamp(brightness - threshold + knee, 0.0, 2.0 * knee);
	soft = soft * soft / (4.0 * knee + 1e-4);
	float kept = max(soft, brightness - threshold) / max(brightness, 1e-4);

	out_Colour = vec4(colour * kept, 1.0);
}
