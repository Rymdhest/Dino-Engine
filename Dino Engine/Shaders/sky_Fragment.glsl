#version 330

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform vec3 skyColor;
uniform vec3 horizonColor;
uniform vec3 viewPositionWorld;
uniform vec2 screenResolution;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
void main(void){

	vec2 uv = ((textureCoords*2f)-1f);
	uv = (uv*screenResolution)/screenResolution.y;

	float horizonSize = 0.9f;

	vec3 viewDir = normalize((viewMatrix*vec4(uv, -1f, 1.0f)).xyz);
	vec3 upNormalViewSpace = vec3(0f, 1f, 0.0f);
	float horizonFactor = clamp( horizonSize+dot( viewDir.y, upNormalViewSpace.y ), 0.0 , 1.0f);

	out_Colour.rgb = skyColor;
	out_Colour.rgb = mix( horizonColor, skyColor, horizonFactor);

	out_Colour.rgb *= 1f-pow(max( dot( viewDir, upNormalViewSpace), 0.0 ), 1)*0.9f;

	
	out_Colour.a = 1.0f;
}