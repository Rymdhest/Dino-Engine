#version 420

#include globals.glsl
#include Wind.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;
out vec3 viewSpacePos;
out float textureIndex;

uniform sampler2D bendMap;
uniform sampler2D windMap;

uniform float swayAmount;

mat4 rotXMatrix(float a) {
	return mat4(
	1, 0, 0, 0,
	0, cos(a), -sin(a), 0,
	0,sin(a),cos(a), 0,
	0, 0, 0, 1);
}
mat4 rotYMatrix(float a) {
	return mat4(
	cos(a), 0, sin(a), 0,
	0, 1, 0, 0,
	-sin(a),0,cos(a), 0,
	0, 0, 0, 1);
}
mat4 rotZMatrix(float a) {
	return mat4(
	cos(a), -sin(a), 0, 0,
	sin(a), cos(a), 0, 0,
	0,0,1, 0,
	0,0,0, 1); 
}
void main() {
	vec3 modelPosWorldSpace = (modelMatrix*vec4(position, 1.0)).xyz;
	vec2 bendMapValue = vegetationBend(bendMap, windMap, modelPosWorldSpace.xz).yx;
	bendMapValue.x *= -1.0;
	bendMapValue *= (position.y*0.001+length(position.xz)*0.01)*swayAmount;
	float rotX = bendMapValue.x;
	float rotZ = bendMapValue.y;
	
	mat4 localRotMatrix = rotZMatrix(0.0)*rotXMatrix(0.0)*rotYMatrix(0.0);
	localRotMatrix = rotXMatrix(rotX)*rotZMatrix(rotZ)*localRotMatrix;

	mat4 modelView = viewMatrix*modelMatrix*localRotMatrix;
	vec4 viewPos = modelView*vec4(position, 1.0);
	viewSpacePos = viewPos.xyz;
	gl_Position =  projectionMatrix*viewPos;
	mat4 normalModelViewMatrix = transpose(inverse(modelView));
	fragUV = uv;
	textureIndex = materialIndex;
	worldNormal = normal;
	
	vec3 T = tangent;
	vec3 N = normal;
    vec3 B = normalize( cross(T, N));
    mat3 TBN =  mat3(T, B, N);

	normalTBN = mat3(normalModelViewMatrix)*TBN;

	fragColor = color;
}