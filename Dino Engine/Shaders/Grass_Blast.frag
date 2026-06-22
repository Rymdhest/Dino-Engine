#version 330
layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;
uniform float delta;
uniform float test;
uniform vec2 blastCenterWorld;
uniform float radius;
uniform vec2 simulationWorldSize;
uniform vec2 simulationWorldPosition;

uniform float power;
uniform float exponent;

uniform sampler2D lastTexture;

void main(void){
	vec2 worldPosition = simulationWorldPosition+textureCoords*simulationWorldSize;
	float dist = distance(blastCenterWorld, worldPosition);
	float bend = pow(clamp((radius-dist)/radius, 0, 1), exponent);
	vec2 direction =normalize(worldPosition-blastCenterWorld);
	vec2 newBend =  direction*bend*power;
	vec2 currentBendVec = texture(lastTexture, textureCoords).xy;

	if (length(newBend) > length(currentBendVec)){
		out_Colour.xy = newBend;
	} else {
		discard;
		//out_Colour.xy = currentBendVec;
	}
}
