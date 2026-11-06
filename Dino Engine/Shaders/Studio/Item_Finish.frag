#version 420

// Brings a picture down from the size it was drawn at to the size it's kept at, and draws its outline on the way: dark where the object stops, and fainter where one part of it stands in
// front of another or a crease turns sharply. An inverted hull can't do this for gear, as it opens up along hard edges and a blade is nothing but hard edges. The result goes to the
// interface, which takes premultiplied alpha and doesn't tone map, so this is where a picture becomes what's shown.

in vec2 textureCoords;

uniform sampler2D colourMap;
uniform sampler2D normalMap;
uniform sampler2D depthMap;
uniform vec2 texel;             // One pixel of what's being read.
uniform int shrink;             // Pixels read across each pixel written.
uniform float outlineWidth;     // In what's being read.
uniform vec4 halo;              // What's behind it, and how strongly.

out vec4 outColour;

float covered(vec2 uv) {
	return texture(colourMap, uv).a;
}

// How much of a disc around a point is the object.
float reach(vec2 uv, float radius) {
	float most = 0.0;
	for (int i = 0; i < 12; i++) {
		float angle = float(i) * 0.5235988;
		most = max(most, covered(uv + vec2(cos(angle), sin(angle)) * radius * texel));
	}
	return most;
}

float crease(vec2 uv) {
	vec3 n = texture(normalMap, uv).xyz * 2.0 - 1.0;
	float d = texture(depthMap, uv).r;
	float edge = 0.0;
	for (int i = 0; i < 4; i++) {
		vec2 step = vec2(i < 2 ? (i == 0 ? 1.0 : -1.0) : 0.0, i < 2 ? 0.0 : (i == 2 ? 1.0 : -1.0)) * texel * outlineWidth * 0.5;
		vec3 other = texture(normalMap, uv + step).xyz * 2.0 - 1.0;
		float otherDepth = texture(depthMap, uv + step).r;
		edge = max(edge, smoothstep(0.012, 0.03, abs(otherDepth - d)));
		edge = max(edge, 0.55 * smoothstep(0.55, 0.9, 1.0 - dot(n, other)));
	}
	return edge;
}

void main() {
	vec3 colour = vec3(0.0);
	float alpha = 0.0;
	float line = 0.0;
	float outline = 0.0;

	for (int y = 0; y < shrink; y++) {
		for (int x = 0; x < shrink; x++) {
			vec2 uv = textureCoords + (vec2(x, y) - 0.5 * float(shrink - 1)) * texel;
			vec4 read = texture(colourMap, uv);
			colour += read.rgb * read.a;
			alpha += read.a;
			if (read.a > 0.5) line += crease(uv);
			else outline += reach(uv, outlineWidth);
		}
	}

	float samples = float(shrink * shrink);
	colour = alpha > 0.0 ? colour / alpha : vec3(0.0);
	alpha /= samples;
	line = alpha > 0.0 ? line / (alpha * samples) : 0.0;
	outline /= samples;

	// Into what a screen shows, with a shoulder so that a bright thing keeps its colour.
	colour = colour / (1.0 + 0.32 * colour);
	colour = pow(max(colour, vec3(0.0)), vec3(1.0 / 2.2));

	const vec3 INK = vec3(0.045, 0.035, 0.03);
	colour = mix(colour, INK, line * 0.72);

	// Premultiplied, from the back: the halo, then the outline, then the thing itself.
	vec4 result = vec4(halo.rgb * halo.a, halo.a) * smoothstep(0.0, 0.9, reach(textureCoords, outlineWidth * 5.0)) * 0.5;
	result = vec4(INK, 1.0) * outline + result * (1.0 - outline);
	result = vec4(colour * alpha, alpha) + result * (1.0 - alpha);
	outColour = result;
}
