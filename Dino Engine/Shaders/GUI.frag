#version 330 core

in vec2 vUV;
in vec2 vLocal;
flat in vec4 vColor;
flat in vec4 vBorderColor;
flat in vec2 vHalfSize;
flat in vec4 vParams; // x: corner radius, y: border width, z: mode, w: edge softness

layout (location = 0) out vec4 out_Colour;

uniform sampler2D uTexture;

// Signed distance to a rounded box centred at the origin.
float roundedBoxDistance(vec2 p, vec2 halfSize, float radius){
	vec2 q = abs(p) - halfSize + vec2(radius);
	return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

void main(void){
	// Sample and take derivatives in uniform control flow.
	vec4 texel = texture(uTexture, vUV);
	float radius = min(vParams.x, min(vHalfSize.x, vHalfSize.y));
	float dist = roundedBoxDistance(vLocal, vHalfSize, radius);
	float pixel = max(fwidth(dist), 0.0001);

	float softness = vParams.w;
	float coverage;
	if (softness > pixel) {
		coverage = 1.0 - smoothstep(-softness * 0.5, softness * 0.5, dist);
	} else {
		coverage = clamp(0.5 - dist / pixel, 0.0, 1.0);
	}

	int mode = int(vParams.z + 0.5);
	vec4 colour;
	if (mode == 1) {
		// text: coverage stored in the red channel of the glyph atlas; the quad is the exact glyph box
		colour = vec4(vColor.rgb, vColor.a * texel.r);
		coverage = 1.0;
	} else if (mode == 2) {
		colour = vColor * texel;
	} else if (mode == 3) {
		colour = vec4(vColor.rgb * texel.rgb, vColor.a);
	} else if (mode == 4) {
		colour = vec4(vColor.rgb * texel.rrr, vColor.a);
	} else if (mode == 5) {
		// vertical gradient from colour (top) to border colour (bottom)
		colour = mix(vColor, vBorderColor, clamp(vLocal.y / max(vHalfSize.y, 0.0001) * 0.5 + 0.5, 0.0, 1.0));
	} else {
		float borderWidth = vParams.y;
		if (borderWidth > 0.0) {
			// Mix border and fill in premultiplied space so transparent fills do not darken the edge.
			float inner = clamp(0.5 - (dist + borderWidth) / pixel, 0.0, 1.0);
			vec4 fillPremultiplied = vec4(vColor.rgb * vColor.a, vColor.a);
			vec4 borderPremultiplied = vec4(vBorderColor.rgb * vBorderColor.a, vBorderColor.a);
			out_Colour = mix(borderPremultiplied, fillPremultiplied, inner) * coverage;
			return;
		}
		colour = vColor;
	}

	colour.a *= coverage;
	out_Colour = vec4(colour.rgb * colour.a, colour.a);
}
