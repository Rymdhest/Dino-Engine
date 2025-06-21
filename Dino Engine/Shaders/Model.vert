#version 330

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;

out vec3 fragColor;
out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;

out vec3 TangentViewPos;
out vec3 TangentFragPos;

out float textureIndex;

uniform vec3 viewPos;
uniform mat4 modelMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

void main() {
	gl_Position =  vec4(position, 1.0)*modelViewProjectionMatrix;
	fragUV = uv;
	textureIndex = materialIndex;
	worldNormal = normal;
	
	vec3 N = normal;
	vec3 T = tangent;
    vec3 B = normalize( cross(T, N));


    mat3 TBN =  mat3(T, B, N);

    TangentFragPos = position * (TBN);
    TangentViewPos = (vec4(viewPos, 1.0)*inverse(modelMatrix)).xyz*(TBN);

	normalTBN = transpose(mat3(T, normalize( cross(N, T)), N))*(mat3(normalModelViewMatrix));

	fragColor = color;
}