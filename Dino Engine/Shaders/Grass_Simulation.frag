#version 330
layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;
uniform sampler2D lastTexture;
uniform float delta;
uniform float regenTime;
uniform float time;

vec2 calcWind(vec2 uv) {
	float x = 0f;
	x += 0.05f*(sin(uv.x*71.77f+time));
	x += 0.02317f*(sin(uv.x*109.124f+time*2.412));
	x += 0.01239973f*(sin(uv.x*187.567f+time*7.328995));
	float y =0f;
	return vec2(x, y);
}

void main(void){
	vec2 bend =  texture(lastTexture, textureCoords).xy;
	vec2 nextBend = bend*(1f-min(delta/regenTime, 1f));

	vec2 wind = calcWind(textureCoords);
	out_Colour.xy = nextBend+wind;
}
