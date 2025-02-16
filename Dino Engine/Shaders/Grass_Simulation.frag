#version 330
layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;
uniform sampler2D lastTexture;
uniform float delta;
uniform float regenTime;
uniform float time;
uniform vec2 grassFieldSize;

vec2 calcWind(vec2 uv) {
	float x = 0;

	vec2 worldPos = uv*grassFieldSize;

	x += 0.08f*(sin(worldPos.x*0.77f+time));
	x += 0.05317f*(sin(worldPos.x*0.124f+time*2.412));
	x += 0.0239973f*(sin(worldPos.x*0.02567f+time*7.328995));
	float y =0;
	return vec2(x, y)*delta;
}

void main(void){
	vec2 bend =  texture(lastTexture, textureCoords).xy;
	vec2 nextBend = bend*(1-min(delta/regenTime, 1));

	vec2 wind = calcWind(textureCoords);
	out_Colour.xy = nextBend+wind;
}
