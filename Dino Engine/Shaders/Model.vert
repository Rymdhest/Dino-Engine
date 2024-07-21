#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;

out vec3 fragColor;
out vec3 positionViewSpace_pass;
out vec2 fragUV;
out mat4 TBM;
out vec3 TANGENT_DEBUG_PASS;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

void main() {
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	positionViewSpace_pass =  (vec4(position, 1.0)*modelViewMatrix).xyz;
	fragUV = uv;

	vec3 bitangent = normalize(cross(normal, tangent));
	TBM = mat4(
		tangent.x, bitangent.x, normal.x, 0f,
		tangent.y, bitangent.y, normal.y, 0f,
		tangent.z, bitangent.z, normal.z, 0f,
		0f,		0f,		0f,		1f
	);
	TBM = TBM*normalModelViewMatrix;

	fragColor = color;
}