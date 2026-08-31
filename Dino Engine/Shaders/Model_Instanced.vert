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
out mat3 normalTBN;
out vec3 worldNormal;
out vec3 TangentViewPos;
out vec3 TangentFragPos;
out float textureIndex;

uniform sampler2D bendMap;
uniform vec2 simulationWorldSize;
uniform vec2 simulationWorldPosition;
uniform float swayAmount;

mat4 rotXMatrix(float a) {
	return mat4(
	1, 0, 0, 0,
	0, cos(a), -sin(a), 0,
	0, sin(a), cos(a), 0,
	0, 0, 0, 1);
}
mat4 rotYMatrix(float a) {
	return mat4(
	cos(a), 0, sin(a), 0,
	0, 1, 0, 0,
	-sin(a), 0, cos(a), 0,
	0, 0, 0, 1);
}
mat4 rotZMatrix(float a) {
	return mat4(
	cos(a), -sin(a), 0, 0,
	sin(a), cos(a), 0, 0,
	0, 0, 1, 0,
	0, 0, 0, 1); 
}

void main() {
	vec4 localPos = vec4(position, 1.0);
	vec3 modelPosWorldSpace = (modelMatrix * localPos).xyz;
	vec2 bendMapUVPosition = (modelPosWorldSpace.xz - simulationWorldPosition) / simulationWorldSize;
	vec2 bendMapValue = texture(bendMap, bendMapUVPosition).yx;
	bendMapValue.x *= -1.0;
	bendMapValue *= (position.y * 0.001 + length(position.xz) * 0.01) * swayAmount;
	float rotX = bendMapValue.x;
	float rotZ = bendMapValue.y;
	
	mat4 localRotMatrix = rotZMatrix(0.0) * rotXMatrix(0.0) * rotYMatrix(0.0);
	localRotMatrix = rotXMatrix(rotX) * rotZMatrix(rotZ) * localRotMatrix;

	vec4 deformedLocalPos = localRotMatrix * localPos;
	vec4 worldPos = modelMatrix * deformedLocalPos;

	mat4 modelView = viewMatrix * modelMatrix * localRotMatrix;
	gl_Position =  projectionMatrix * modelView * localPos; // Note: using deformedLocalPos keeps the vertex animation correct
	
	fragUV = uv;
	textureIndex = materialIndex;
	
	// Combine entity rotation and vertex bending/sway rotations for the TBN frame
	mat3 fullModelMatrix3 = mat3(modelMatrix * localRotMatrix);
	vec3 T = normalize(fullModelMatrix3 * tangent);
	vec3 N = normalize(fullModelMatrix3 * normal);
	
	// Gram-Schmidt orthogonalization to prevent skewing and tearing during rotation
	T = normalize(T - dot(T, N) * N);
	vec3 B = cross(T, N);
    
	worldNormal = N;

	// Build World-to-Tangent space transformation matrix
	mat3 TBN = mat3(T, B, N);
	mat3 worldToTangent = transpose(TBN);

	// Transform positions into consistent Tangent Space
	TangentFragPos = worldToTangent * worldPos.xyz;
	TangentViewPos = worldToTangent * viewPosWorld;

	// Transform normals into View Space for your G-Buffer
	mat4 normalModelViewMatrix = transpose(inverse(modelView));
	mat3 viewTBN = mat3(
		normalize(mat3(modelView) * tangent),
		normalize(mat3(modelView) * B),
		normalize(mat3(modelView) * normal)
	);
	normalTBN = viewTBN;

	fragColor = color;
}