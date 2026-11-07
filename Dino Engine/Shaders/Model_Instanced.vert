#version 420

#include globals.glsl
#include Plant.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;
layout(location=10) in float joint;

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;
out vec3 viewSpacePos;
out float textureIndex;

uniform int plant;

void main() {
	// Only what bends has any business reading what's pushing it: everything else stands as it was built.
	vec3 pose = position;
	vec3 N = normal;
	vec3 T = tangent;
	if (plant == 1 && joint > -0.5) plantPose(int(joint + 0.5), modelMatrix, pose, N, T);

	vec4 viewPos = viewMatrix*(modelMatrix*vec4(pose, 1.0));
	viewSpacePos = viewPos.xyz;
	gl_Position =  projectionMatrix*viewPos;

	// The cofactors are the inverse transpose times the determinant, and everything that reads the frame normalises it, so that
	// scale goes away. Only its sign matters, on what's mirrored. A rotation is its own inverse transpose.
	mat3 modelView = mat3(viewMatrix)*mat3(modelMatrix);
	mat3 normalMatrix = mat3(cross(modelView[1], modelView[2]), cross(modelView[2], modelView[0]), cross(modelView[0], modelView[1]));
	if (dot(modelView[0], normalMatrix[0]) < 0.0) normalMatrix = -normalMatrix;
	fragUV = uv;
	textureIndex = materialIndex;
	worldNormal = N;
	
    vec3 B = normalize( cross(T, N));
    mat3 TBN =  mat3(T, B, N);

	normalTBN = normalMatrix*TBN;

	fragColor = color;
}