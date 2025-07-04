#version 330

layout(location=0) in vec2 position;

out vec2 fragUV;
out mat3 normalTBN;
out vec3 worldNormal;

out vec3 TangentViewPos;
out vec3 TangentFragPos;

uniform vec3 viewPos;
uniform mat4 modelMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;

uniform vec2 size;

uniform sampler2D heightMap;

void main() {
	float height = texture2D(heightMap, position/size);:
	gl_Position =  vec4(position.x, height, position.y,  1.0)*modelViewProjectionMatrix;
	fragUV = uv;
	worldNormal = normal;
	
	vec3 N = normal;
	vec3 T = vec3(1.0, 0.0, 0.0);
    vec3 B = normalize( cross(T, N));


    mat3 TBN =  mat3(T, B, N);

    TangentFragPos = position * (TBN);
    TangentViewPos = (vec4(viewPos, 1.0)*inverse(modelMatrix)).xyz*(TBN);

	normalTBN = transpose(mat3(T, normalize( cross(N, T)), N))*(mat3(normalModelViewMatrix));

}