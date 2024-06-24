#version 330

out vec4 out_colour;

in vec2 blurTextureCoords[21];

uniform float weights[21];
uniform sampler2D originalTexture;
uniform int blurRadius;

void main(void){
	out_colour = vec4(0.0);
	int total = blurRadius*2+1;
    for (int i=0 ; i<total ; i++) {
	    out_colour += texture(originalTexture, blurTextureCoords[i]) * weights[i];
	}
}