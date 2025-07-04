#version 330

#include procedural/fasthash.glsl

layout(location=0) in vec3 position;
layout(location=1) in vec3 color;
layout(location=2) in vec3 normal;

out vec3 fragColor;
out vec3 fragMaterials;
out vec3 fragNormal;
out float valid;
out float tipFactor;
uniform mat4 modelMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 modelViewProjectionMatrix;
uniform mat4 normalModelViewMatrix;
uniform vec2 worldSize;

uniform float swayAmount;
uniform float time;
uniform float bladeHeight;
uniform float bendyness;
uniform float heightError;
uniform float cutOffThreshold;
uniform float spacing;
uniform float colourError;
uniform float groundNormalStrength;
uniform vec2 bladesPerAxis;
uniform vec2 grassFieldSizeWorld;

uniform vec2 offsetTest;
uniform vec2 offsetTest2;

uniform sampler2D terrainNormalMap;
uniform sampler2D heightMap;
uniform sampler2D grassMap;
uniform sampler2D bendMap;

#define PI 3.1415926538

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

mat3 createRotationMatrix(vec3 axis, float angle) {
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;
    float cosAngle = cos(angle);
    float sinAngle = sin(angle);
    float t = 1.0 - cosAngle;

    return mat3(
        t * x * x + cosAngle,      t * x * y - sinAngle * z, t * x * z + sinAngle * y,
        t * x * y + sinAngle * z,  t * y * y + cosAngle,     t * y * z - sinAngle * x,
        t * x * z - sinAngle * y,  t * y * z + sinAngle * x, t * z * z + cosAngle
    );
}

vec3 calcLocalCellPosition(vec2 gridPosition) {
	vec2 offset = hash22(gridPosition)*spacing;
	return vec3(offset.x, 0, offset.y);
}

float calcHeightFactor(vec2 textureCoords) {
	float heightFactor = texture(grassMap, textureCoords).r;
	//apply error
	heightFactor *= 1+ hash21(textureCoords)*heightError*2-heightError;
	if (heightFactor < cutOffThreshold) valid = 0.0f;
	return heightFactor;
}


void addBendFromMap() {

}

mat3 calcRotMatrixFromBendMap(vec2 textureCoords, float tipFactor) {
	vec2 bendMapValue = texture(bendMap, textureCoords).xy;
	vec3 initialDirection = vec3(0.0, 1.0, 0.0); // Initial direction (pointing up)
	vec3 targetDirection = normalize(vec3(bendMapValue.x*tipFactor, 1, bendMapValue.y*tipFactor)); // Your defined target vector
	vec3 rotationAxis = normalize(cross(initialDirection, targetDirection));
	float dotProduct = dot(initialDirection, targetDirection);
	float angle = acos(dotProduct);
	mat3 rotationMatrix;
    if (length(targetDirection - initialDirection) < 0.0001) {
        // If the initial direction and target direction are the same, no rotation is needed
        rotationMatrix = mat3(1.0); // Identity matrix
    } else {
        rotationAxis = normalize(rotationAxis);
        rotationMatrix = createRotationMatrix(rotationAxis, angle);
    }
	return rotationMatrix;
}

void main() {
	valid = 1.0f;
	tipFactor = position.y/bladeHeight;
	vec3 gridPosition = vec3((floor(gl_InstanceID/(bladesPerAxis.y))), 0, mod(float(gl_InstanceID),bladesPerAxis.y))*spacing;

	
	/*
	float rotY = hash21(gridPosition.xz+vec2(154f))*PI*2f;
	float rotX = (hash21(gridPosition.xz+vec2(1234f))-0.5f)*PI*tipFactor*bendyness;
	float rotZ = (hash21(gridPosition.xz-vec2(341f))-0.5f)*PI*tipFactor*bendyness;

	rotX += sin(time+sin(gridPosition.z*0.1777f))*swayAmount*tipFactor;
	rotX += 0.3f*sin(time*2.3+gridPosition.z*0.4f)*swayAmount*tipFactor;
	rotX += 0.15f*sin(time*7.13+gridPosition.z*0.9f)*swayAmount*tipFactor;
	rotZ += 0.15f*cos(time*1.2f+gridPosition.x*0.2f)*swayAmount*tipFactor;

	rotX += bendMapValue.y;
	rotZ += bendMapValue.x;
	mat3 rotationMatrix =rotZMatrix(rotZ)*rotXMatrix(rotX)*rotYMatrix(rotY);
	*/
	



	
	vec3 modelPositionCellSpace = calcLocalCellPosition(gridPosition.xz);
	vec3 modelPositionGridSpace = modelPositionCellSpace+gridPosition;

	
	vec2 textureCoordsModel = (modelPositionGridSpace.xz+offsetTest)/((grassFieldSizeWorld+offsetTest2));


	
	float rotX = (hash21(gridPosition.xz))*PI*tipFactor*bendyness;
	float rotZ = (hash21(gridPosition.xz))*PI*tipFactor*bendyness;
	float rotY = hash21(gridPosition.xz)*PI*2;
	mat3 localRotMatrix = rotZMatrix(rotZ)*rotXMatrix(rotX)*rotYMatrix(rotY);
	vec3 VertexPositionLocal = position;

	mat3 rotationMatrix = localRotMatrix*calcRotMatrixFromBendMap(textureCoordsModel, tipFactor);
	
	VertexPositionLocal.y *= calcHeightFactor(textureCoordsModel);
	VertexPositionLocal = VertexPositionLocal*rotationMatrix;


	vec3 VertexPositionGridSpace =gridPosition+modelPositionCellSpace+VertexPositionLocal;

	vec2 textureCoordsVertex = (VertexPositionGridSpace.xz+offsetTest)/((grassFieldSizeWorld+offsetTest2));

	if (tipFactor < 0.0001f) VertexPositionGridSpace.y += texture(heightMap, textureCoordsVertex).r;
	else VertexPositionGridSpace.y += texture(heightMap, textureCoordsModel).r;




	gl_Position =  vec4(VertexPositionGridSpace, 1.0)*modelViewProjectionMatrix;
	fragColor = color+color*vec3(hash23(gridPosition.xz))*colourError*2-colourError*color;
	fragMaterials = vec3(0.95f, 0.0f, 0.0);
	
	vec3 rotatedNormal = normal.xyz * inverse(transpose(rotationMatrix));
	vec3 terrainNormal = texture(terrainNormalMap, textureCoordsVertex).xyz;
	vec3 adjustedNormal = normalize(rotatedNormal+terrainNormal*groundNormalStrength);
	fragNormal = (vec4(adjustedNormal, 1.0f)*normalModelViewMatrix).xyz;
}