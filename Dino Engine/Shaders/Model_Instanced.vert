#version 420
#include globals.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;

out vec3 fragColor;
out vec2 fragUV;
out float textureIndex;

// Stable World Space Attributes
out vec3 fragWorldPos;
out vec3 fragWorldNormal;
out vec3 fragWorldTangent;

uniform sampler2D bendMap;
uniform vec2 simulationWorldSize;
uniform vec2 simulationWorldPosition;
uniform float swayAmount;

mat4 rotXMatrix(float a) { return mat4(1,0,0,0, 0,cos(a),-sin(a),0, 0,sin(a),cos(a),0, 0,0,0,1); }
mat4 rotZMatrix(float a) { return mat4(cos(a),-sin(a),0,0, sin(a),cos(a),0,0, 0,0,1,0, 0,0,0,1); }

void main() {
	vec3 modelPosWorldSpace = (modelMatrix * vec4(position, 1.0)).xyz;
	vec2 bendMapUVPosition = (modelPosWorldSpace.xz - simulationWorldPosition) / simulationWorldSize;
	vec2 bendMapValue = texture(bendMap, bendMapUVPosition).yx;
	bendMapValue.x *= -1.0;
	bendMapValue *= (position.y * 0.001 + length(position.xz) * 0.01) * swayAmount;
	
	mat4 localRotMatrix = rotXMatrix(bendMapValue.x) * rotZMatrix(bendMapValue.y);
	
	// Final absolute world matrix
	mat4 finalModelMatrix = modelMatrix * localRotMatrix;
	vec4 worldPos = finalModelMatrix * vec4(position, 1.0);
	
	fragWorldPos = worldPos.xyz;
	
	// Normal Matrix (Transpose of Inverse of Final Model Matrix) guarantees correct scale/rotation
	mat3 normalMatrix = transpose(inverse(mat3(finalModelMatrix)));
	fragWorldNormal = normalMatrix * normal;
	fragWorldTangent = normalMatrix * tangent;

	mat4 modelView = viewMatrix * finalModelMatrix;
	gl_Position = projectionMatrix * modelView * vec4(position, 1.0);
	
	fragUV = uv;
	textureIndex = materialIndex;
	fragColor = color;
}