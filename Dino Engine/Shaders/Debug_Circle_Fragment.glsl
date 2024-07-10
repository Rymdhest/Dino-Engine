#version 330

layout (location = 0) out vec4 out_Colour;
uniform vec2 center;
uniform vec3 color;

uniform float radius;

void main(void){
	float dist = distance(gl_FragCoord.xy, center);
	float smoothRadius =radius*0.8f;
	if (dist >= smoothRadius) {
		float alpha = smoothstep(radius, smoothRadius, dist);
		out_Colour = vec4(color, alpha);
	}
	else if (dist <= radius) {
		out_Colour = vec4(color, 1.0f);
	}
	else {
		//out_Colour.rgb = vec3(0.0f, 1.0f, 0.0f);
		discard;
	}
}