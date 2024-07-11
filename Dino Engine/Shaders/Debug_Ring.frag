#version 330

in vec2 uv;
layout (location = 0) out vec4 out_Colour;
uniform vec2 center;
uniform vec3 color;
uniform float radius;
uniform float width;

void main(void){
	float dist = distance(gl_FragCoord.xy, center);

	vec3 red = vec3(1, 0 ,0);
	vec3 blue = vec3(0, 0 ,1);

	if (dist >= radius-width/2.0 && dist <= radius) {
		out_Colour = vec4(color, smoothstep(radius-width/2.0f,radius, dist));
	} else if (dist >= radius && dist <= radius+width/2.0f){
		out_Colour = vec4(color, smoothstep(radius+width/2.0f,radius, dist));
	} else {
		discard;
		//out_Colour = vec4(0,0,1.0, 1);
	}
}