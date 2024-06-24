#version 330

in vec2 position;

out vec2 blurTextureCoords[21];

uniform float pixelSize;
uniform int blurRadius;
uniform vec2 blurAxis;

void main(void){

	gl_Position = vec4(position, 0.0, 1.0);
	vec2 centerTexCoords = position * 0.5 + 0.5;
	for (int i=-blurRadius ; i<=blurRadius ; i++) {
		blurTextureCoords[i+blurRadius] = centerTexCoords + vec2(pixelSize*i)*blurAxis;
	}
}