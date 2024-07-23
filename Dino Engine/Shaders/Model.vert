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
out mat4 TBN;
out mat3 TBN2;

out vec3 TangentViewPos;
out vec3 TangentFragPos;
out vec3 worldPos;

out float textureIndex;

uniform vec3 viewPos;
uniform mat4 modelMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

void main() {
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	positionViewSpace_pass =  (vec4(position, 1.0)*modelViewMatrix).xyz;
	fragUV = uv;
	textureIndex = materialIndex;
	vec3 bitangent = normalize(cross(normal, tangent));

	TBN = mat4(
		tangent.x, bitangent.x, normal.x, 0f,
		tangent.y, bitangent.y, normal.y, 0f,
		tangent.z, bitangent.z, normal.z, 0f,
		0f,		0f,		0f,		1f
	);
	
    vec3 N   = normalize((vec4(normal, 1.0f)*(modelMatrix)).xyz);
    vec3 T   = normalize((vec4(tangent, 1.0f)*(modelMatrix)).xyz);
	T = normalize(T - dot(T, N) * N);
    vec3 B   = cross(N, T);
    TBN2 = transpose(mat3(T, B, N));

	worldPos = (vec4(position, 1.0)*((modelMatrix))).xyz;
    TangentViewPos  = TBN2*vec3((viewPos)).xyz;
    TangentFragPos  = TBN2*vec3((position)).xyz;

	
	TBN = TBN*normalModelViewMatrix;

	fragColor = color;
}