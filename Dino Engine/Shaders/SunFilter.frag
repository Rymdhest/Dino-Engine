#version 330

#define NUM_SAMPLES 25


in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform vec2 screenResolution;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform float exponent;

uniform vec3 sunDirection;
uniform vec3 sunColour;


void main(void)
{
    vec4 clipSpacePos =vec4(sunDirection, 0.0)* viewMatrix * projectionMatrix;
    
    // Normalize to NDC (Normalized Device Coordinates)
    vec3 ndcPos = clipSpacePos.xyz / clipSpacePos.w;
    
    // Convert NDC to screen space coordinates (0 to 1)
    vec2 screenPos = ndcPos.xy * 0.5 + 0.5;
    
    //adapt for aspect ratio
	screenPos = ((screenPos*2)-1);
	screenPos = (screenPos*screenResolution)/screenResolution.y;

	vec2 uv = ((textureCoords*2)-1);
	uv = (uv*screenResolution)/screenResolution.y;

	vec3 viewDir = normalize((projectionMatrix*viewMatrix*vec4(uv, -1, 1.0f)).xyz);
    float dist = distance(uv, screenPos);
    float sunAmount = 1/(dist*exponent);
    sunAmount = clamp(sunAmount, 0, 999);

    vec3 viewDirWorldSpace =normalize((viewMatrix*vec4(0, 0, -1, 1.0f)).xyz);
    
    if (dot(viewDirWorldSpace, sunDirection) < 0.0) {
        sunAmount = 0;
    }
    out_Colour = vec4(sunColour*sunAmount, 1);

}