#version 330

in vec2 textureCoords;

uniform sampler2D shadedColourTexture;
uniform sampler2D gPosition;

uniform float fogDensity;
uniform float heightFallOff;
uniform vec3 fogColor;

uniform mat4 inverseViewMatrix;

uniform vec3 cameraPosWorldSpace;


layout (location = 0) out vec4 out_Color;


vec3 applyVolumetricFog( in vec3  col,  // color of pixel
               in float t,    // distnace to point
               in vec3  ro,   // camera position
               in vec3  rd )  // camera to point vector
{
	float a = heightFallOff;
	float b = fogDensity;
    float fogAmount = (a/b) * exp(-ro.y*b) * (1.0-exp(-t*rd.y*b))/rd.y;
    return mix( col, fogColor, clamp(fogAmount, 0f, 1f) );
}

vec3 applySimpleFog( in vec3  col, float depth)
{
    float fogAmount = 1f -exp(-fogDensity *fogDensity*depth*depth);
    return mix(col, fogColor, fogAmount);
}



void main() {
	float depth = texture(gPosition, textureCoords).z;
	vec3 viewPosition = texture(gPosition, textureCoords).xyz;
	vec3 baseColor = texture(shadedColourTexture, textureCoords).rgb;


    vec3 worldPosition = (vec4(viewPosition, 1.0)*inverseViewMatrix).xyz;

	out_Color.rgb = applyVolumetricFog(baseColor, length(viewPosition), cameraPosWorldSpace,normalize(worldPosition-cameraPosWorldSpace));

}