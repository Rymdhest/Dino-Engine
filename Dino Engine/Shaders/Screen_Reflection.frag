#version 330
#include gBufferUtil.glsl
#include procedural/hash.glsl

layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D gMaterials;
uniform sampler2D shadedColor;

uniform vec3 skyColor;
uniform mat4 projectionMatrix;

uniform mat4 invProjection;
uniform vec2 resolution;
uniform mat4 invView;

uniform float rayStep;
uniform int iterationCount;
uniform int binaryIterationCount;
uniform float distanceBias;
uniform bool isBinarySearchEnabled;
uniform bool debugDraw;
uniform float stepExponent;


vec2 generateProjectedPosition(vec3 pos){
	vec4 samplePosition =  vec4(pos, 1.f) * projectionMatrix;
	samplePosition.xy = (samplePosition.xy / samplePosition.w) * 0.5 + 0.5;
	return samplePosition.xy;
}

vec3 SSR(vec3 position, vec3 reflection) {
	vec3 step = rayStep * reflection;
	vec3 marchingPosition = position + step;
	float delta;
	float depthFromScreen;
	vec2 screenPosition;
	int i = 0;

	for (; i < iterationCount; i++) {
		screenPosition = generateProjectedPosition(marchingPosition);
		depthFromScreen = -ReconstructViewSpacePosition(screenPosition*resolution, texture(gDepth, screenPosition).r, invProjection, resolution).z;
		delta = abs(marchingPosition.z) - depthFromScreen;
		if (abs(delta) < distanceBias) {
			vec3 color = vec3(1);
			if(debugDraw)
				color = vec3( 0.5+ sign(delta)/2,0.3,0.5- sign(delta)/2);
			return texture(shadedColor, screenPosition).xyz * color;
		}
		if (delta > 0) {
			break;
		}
		marchingPosition += step;
		step *= stepExponent;
    }
	if(isBinarySearchEnabled){
		for(; i < binaryIterationCount; i++){
			
			step *= 0.5;
			marchingPosition = marchingPosition - step * sign(delta);
			
			screenPosition = generateProjectedPosition(marchingPosition);
			depthFromScreen = -ReconstructViewSpacePosition(screenPosition*resolution, texture(gDepth, screenPosition).r, invProjection, resolution).z;
			delta = -(marchingPosition.z) - depthFromScreen;
			
			if (abs(delta) < distanceBias) {
                vec3 color = vec3(1);
                if(debugDraw) color = vec3( 0.5+ sign(delta)/2,0.3,0.5- sign(delta)/2);
				return texture(shadedColor, screenPosition).xyz * color;
			}
		}
	}
	
    return vec3(0.0);
}

void main(){
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjection, resolution);
	vec3 normal =  texture(gNormal, textureCoords).xyz;
	float rougness = texture(gMaterials, textureCoords).r*0.5f;
	float metallic = texture(gMaterials, textureCoords).b;
	vec3 modelColor = texture(shadedColor, textureCoords).rgb;
	vec3 positionWorld = ReconstructWorldSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjection, invView, resolution);
	vec3 hash = (vec3(((positionWorld))));
	hash = (hash*2.0)-1.0;
	//normal += hash*rougness;
	if (metallic < 0.0001) {
		discard;
		out_Colour = texture(shadedColor, textureCoords);
	} else {
		vec3 reflectionDirection = normalize(reflect(position, normalize(normal)));
		out_Colour = vec4(SSR(position, normalize(reflectionDirection)), 1.f);
		if (out_Colour.xyz == vec3(0.f)){
			float up = max(dot(vec3(0.0f, 1.0f, 0.0f), normalize(normal)), 0.0f);
			out_Colour.rgb = mix(modelColor, skyColor, up);
			out_Colour.rgb = skyColor;
		}
	}
}