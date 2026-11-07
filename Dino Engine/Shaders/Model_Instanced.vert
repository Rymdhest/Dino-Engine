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

mat3 rotXMatrix(float a) {
	return mat3(
	1, 0, 0,
	0, cos(a), -sin(a),
	0,sin(a),cos(a));
}
mat3 rotZMatrix(float a) {
	return mat3(
	cos(a), -sin(a), 0,
	sin(a), cos(a), 0,
	0,0,1);
}
void main() {
	// Only what the wind moves has any business reading it: everything else stands as it was built.
	mat3 bendRotation = mat3(1.0);
	if (swayAmount > 0.0) {
		vec3 modelPosWorldSpace = (modelMatrix*vec4(position, 1.0)).xyz;
		vec2 bendMapValue = vegetationBend(bendMap, windMap, modelPosWorldSpace.xz).yx;
		bendMapValue.x *= -1.0;
		bendMapValue *= (position.y*0.001+length(position.xz)*0.01)*swayAmount;
		bendRotation = rotXMatrix(bendMapValue.x)*rotZMatrix(bendMapValue.y);
	}

	vec4 viewPos = viewMatrix*(modelMatrix*vec4(bendRotation*position, 1.0));
	viewSpacePos = viewPos.xyz;
	gl_Position =  projectionMatrix*viewPos;

	// The cofactors are the inverse transpose times the determinant, and everything that reads the frame normalises it, so that
	// scale goes away. Only its sign matters, on what's mirrored. A rotation is its own inverse transpose.
	mat3 modelView = mat3(viewMatrix)*mat3(modelMatrix);
	mat3 normalMatrix = mat3(cross(modelView[1], modelView[2]), cross(modelView[2], modelView[0]), cross(modelView[0], modelView[1]));
	if (dot(modelView[0], normalMatrix[0]) < 0.0) normalMatrix = -normalMatrix;
	fragUV = uv;
	textureIndex = materialIndex;
	worldNormal = normal;
	
	vec3 T = tangent;
	vec3 N = normal;
    vec3 B = normalize( cross(T, N));
    mat3 TBN =  mat3(T, B, N);

	normalTBN = normalMatrix*bendRotation*TBN;

	fragColor = color;
}