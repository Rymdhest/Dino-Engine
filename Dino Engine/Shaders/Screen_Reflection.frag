#version 420
#include globals.glsl
#include gBufferUtil.glsl
#include procedural/hash.glsl

layout (location = 0) out vec4 out_Colour;

in vec2 textureCoords;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D gMaterials;
uniform sampler2D shadedColor;

uniform vec2 resolutionSSR;
uniform float rayStep;
uniform int iterationCount;
uniform int binaryIterationCount;
uniform float distanceBias;
uniform bool isBinarySearchEnabled;
uniform bool debugDraw;
uniform float stepExponent;


vec2 generateProjectedPosition(vec3 pos){
	vec4 samplePosition =  projectionMatrix*vec4(pos, 1.f);
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
		depthFromScreen = -ReconstructViewSpacePosition(screenPosition*resolutionSSR, texture(gDepth, screenPosition).r, invProjectionMatrix, resolutionSSR).z;
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
			depthFromScreen = -ReconstructViewSpacePosition(screenPosition*resolutionSSR, texture(gDepth, screenPosition).r, invProjectionMatrix, resolutionSSR).z;
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
	float metallic = texture(gMaterials, textureCoords).b;
	if (metallic < 0.0001) discard;

	float depth = texture(gDepth, textureCoords).r;
	vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, depth, invProjectionMatrix, resolutionSSR);
	vec3 positionWorld = ReconstructWorldSpacePosition(gl_FragCoord.xy, depth, invProjectionMatrix, invViewMatrix, resolutionSSR);
	float roughness = texture(gMaterials, textureCoords).r * 0.5;

	// A rough surface scatters its reflection: the normal is shaken by a little noise, fixed in the world.
	vec3 normal = unCompressNormal(texture(gNormal, textureCoords).xyz);
	normal += (hash3D(floor(positionWorld * 64.0)) * 2.0 - 1.0) * roughness;

	vec3 reflectionDirection = normalize(reflect(position, normalize(normal)));
	vec3 reflected = SSR(position, reflectionDirection);

	// A ray that finds nothing leaves the surface as the light passes shaded it, ambient reflection and all.
	out_Colour = vec4(reflected == vec3(0.0) ? texture(shadedColor, textureCoords).rgb : reflected, 1.0);
}
