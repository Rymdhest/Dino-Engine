#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 materials;
layout(location=3) in vec3 normal;

out vec3 fragColor;
out vec3 positionViewSpace_pass;
out vec3 fragMaterials;
out vec3 fragNormal;
out float valid;

uniform mat4 modelMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

uniform float swayAmount;
uniform float time;
uniform float bladeHeight;
uniform float bendyness;
uniform float heightError;
uniform float cutOffThreshold;
uniform float spacing;
uniform float colourError;
uniform float groundNormalStrength;
uniform vec2 bladesPerAxis;
uniform vec2 grassFieldSizeWorld;

uniform sampler2D terrainNormalMap;
uniform sampler2D heightMap;
uniform sampler2D grassMap;
uniform sampler2D windMap;

#define PI 3.1415926538
float hash11(float t) {
    float x = fract(sin(t*1.35)*144563.5);
    return x;
}
vec2 hash12(float t) {
    float x = fract(sin(t*14.65)*41.5);
    float y = fract(sin(t*55.1*x)*85.3);
    return vec2(x,y);
}
vec2 hash22(vec2 t) {
    float x = fract(sin(t.x*876.65*t.y)*234.5);
    float y = fract(sin(t.y*5435.1*x)*645.3);
    
    return vec2(x,y);
}
float hash21(vec2 t) {
    float x = fract(sin(t.y*396.1+t.x*6122)*46.0);
    
    return x;
}
vec3 hash13(float t) {
    float x = hash11(t);
    float y = fract(sin(113.1*x)*6652.0);
    float z = fract(sin(1755.1*y)*51336.0);
    
    return vec3(x, y, z);
}

vec3 hash23(vec2 t) {
    float y = fract(sin(113.1*t.x)*6552.0);
    float z = fract(sin(1755.1*t.y)*51336.0);
    float x = hash21(vec2(y, z));
    
    return vec3(x, y, z);
}

mat3 rotXMatrix(float a) {
	return mat3(
	1, 0f, 0,
	0f, cos(a), -sin(a),
	0f,sin(a),cos(a));
}
mat3 rotYMatrix(float a) {
	return mat3(
	cos(a), 0f, sin(a),
	0f, 1f, 0f,
	-sin(a),0f,cos(a));
}
mat3 rotZMatrix(float a) {
	return mat3(
	cos(a), -sin(a), 0,
	sin(a), cos(a), 0,
	0f,0,1f);
}
void main() {
	valid = 1.0f;

	vec3 gridPosition = vec3((floor(gl_InstanceID/(bladesPerAxis.x))), 0f, mod(float(gl_InstanceID),bladesPerAxis.y))*spacing;
	float tipFactor = position.y/bladeHeight;

	float rotY = hash21(gridPosition.xz+vec2(9554f))*PI*2f;
	float rotX = (hash21(gridPosition.xz+vec2(1234f))-0.5f)*PI*tipFactor*bendyness;
	float rotZ = (hash21(gridPosition.xz-vec2(341f))-0.5f)*PI*tipFactor*bendyness;

	rotX += sin(time)*swayAmount;

	mat3 rotationMatrix =rotZMatrix(rotZ)*rotXMatrix(rotX)*rotYMatrix(rotY);
	vec3 localPosition = position*rotationMatrix;
	vec3 rotatedNormal =normal* inverse(transpose(rotationMatrix));
	vec3 bladePosition =localPosition+gridPosition;

	bladePosition.xz += hash12(gl_InstanceID)*spacing;

	// height adjust on terrain
	vec2 textureCoords = (bladePosition.xz+vec2(0.5f))/(grassFieldSizeWorld);
	vec2 textureCoordsGrid = (gridPosition.xz+vec2(0.5f))/(grassFieldSizeWorld);
	float height = texture(heightMap, textureCoords).r;
	vec3 terrainNormal = texture(terrainNormalMap, textureCoordsGrid).xyz;
	float heightFactor = texture(grassMap, textureCoordsGrid).r;
	heightFactor *= 1f+ hash21(gridPosition.xz)*heightError*2f-heightError;

	if (heightFactor < cutOffThreshold) valid = 0.0f;
	bladePosition.y *= heightFactor;

	bladePosition.y += height;


	gl_Position =  vec4(bladePosition, 1.0)*modelViewProjectionMatrix;
	positionViewSpace_pass =  (vec4(bladePosition, 1.0)*modelViewMatrix).xyz;
	fragColor = color+vec3(hash23(gridPosition.xz))*colourError*2f-colourError;
	fragMaterials = materials;

	vec3 adjustedNormal = normalize(rotatedNormal+terrainNormal*groundNormalStrength);
	fragNormal = (vec4(adjustedNormal, 1.0f)*normalModelViewMatrix).xyz;
}