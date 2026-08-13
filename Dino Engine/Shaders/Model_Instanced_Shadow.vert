#version 420


layout(location=0) in vec3 position;
layout(location=4) in vec2 uv;
layout(location=5) in float materialIndex;
layout(location=6) in mat4 modelMatrix;

out float textureIndex;
out vec2 fragUV;
uniform mat4 viewpPojectionMatrix;

uniform sampler2D bendMap;
uniform vec2 simulationWorldSize;
uniform vec2 simulationWorldPosition;

mat3 rotXMatrix(float a) {
	return mat3(
	1, 0, 0,
	0, cos(a), -sin(a),
	0,sin(a),cos(a));
}
mat3 rotYMatrix(float a) {
	return mat3(
	cos(a), 0, sin(a),
	0, 1, 0,
	-sin(a),0,cos(a));
}
mat3 rotZMatrix(float a) {
	return mat3(
	cos(a), -sin(a), 0,
	sin(a), cos(a), 0,
	0,0,1); 
}

void main(void){
	vec3 modelPosWorldSpace = (modelMatrix*vec4(position, 1.0)).xyz;
	vec2 bendMapUVPosition = (modelPosWorldSpace.xz-simulationWorldPosition)/simulationWorldSize;
	vec2 bendMapValue = texture(bendMap, bendMapUVPosition).yx;
	bendMapValue.x *= -1.0;
	bendMapValue *= (position.y*0.001+length(position.xz)*0.01);
	float rotX = bendMapValue.x;
	float rotZ = bendMapValue.y;
	
	mat3 localRotMatrix = rotZMatrix(0.0)*rotXMatrix(0.0)*rotYMatrix(0.0);
	localRotMatrix = rotXMatrix(rotX)*rotZMatrix(rotZ)*localRotMatrix;

	vec3 VertexPositionLocal = localRotMatrix*position;


	gl_Position = viewpPojectionMatrix * modelMatrix*vec4(VertexPositionLocal, 1.0);
	fragUV = uv;
	textureIndex = materialIndex;
}