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


void main() {
	mat4 modelView = viewMatrix*modelMatrix;
	gl_Position =  projectionMatrix*modelView*vec4(position, 1.0);
	mat4 normalModelViewMatrix = transpose(inverse(modelView));
	fragUV = uv;
	textureIndex = materialIndex;
	worldNormal = normal;
	
	vec3 T = tangent;
	vec3 N = normal;
    vec3 B = normalize( cross(T, N));
    mat3 TBN =  mat3(T, B, N);


    TangentFragPos = position*TBN;
    TangentViewPos = (inverse(modelMatrix)*vec4(viewPosWorld, 1.0)).xyz*TBN;
	normalTBN = mat3(normalModelViewMatrix)*TBN;

	fragColor = color;
}


