#version 420
#include Plant.glsl


layout(location=0) in vec3 position;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;
layout(location=10) in float joint;

out float textureIndex;
out vec2 fragUV;
uniform mat4 viewpPojectionMatrix;

uniform int plant;

void main(void){
	vec3 VertexPositionLocal = position;
	vec3 unusedNormal = vec3(0.0);
	vec3 unusedTangent = vec3(0.0);
	if (plant == 1 && joint > -0.5) plantPose(int(joint + 0.5), modelMatrix, VertexPositionLocal, unusedNormal, unusedTangent);

	gl_Position = viewpPojectionMatrix * (modelMatrix*vec4(VertexPositionLocal, 1.0));
	fragUV = uv;
	textureIndex = materialIndex;
}