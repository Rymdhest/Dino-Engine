#version 420

#include globals.glsl
#include FocusOccluder.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=11) in vec4 edges;      // Metres to each edge of the face (MeshBuilder), or far off: the relief comes up to nothing at a corner.

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;

out vec3 viewSpacePos;

out float textureIndex;
out vec4 fragEdges;
flat out float fragFocusOccluder;

uniform mat4 modelMatrix;
uniform mat4 normalModelViewMatrix;
uniform vec3 modelSize;

void main() {
	vec4 viewPos = viewMatrix*modelMatrix*vec4(position, 1.0);
	viewSpacePos = viewPos.xyz;
	gl_Position =  projectionMatrix*viewPos;

	fragUV = uv;
	fragEdges = edges;
	textureIndex = materialIndex;
	fragFocusOccluder = focusOccluder(modelSize, modelMatrix);
	worldNormal = normal;
	
	vec3 T = tangent;
	vec3 N = normal;
    vec3 B = normalize( cross(T, N));
    mat3 TBN =  mat3(T, B, N);

	normalTBN = mat3(normalModelViewMatrix)*TBN;

	fragColor = color;
}


