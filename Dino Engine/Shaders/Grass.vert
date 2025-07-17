#version 420

#include procedural/fasthash.glsl
#include globals.glsl

layout(location=0) in vec3 position;
layout(location=2) in vec3 normal;


layout(std140, binding = 5) uniform ChunkDataBuffer {
    vec4 chunkData[1024]; 
};

out vec3 fragColor;
out vec3 fragNormal;
out float valid;
out float tipFactor;



uniform float swayAmount;
uniform float bladeHeight;
uniform float bendyness;
uniform float heightError;
uniform float radiusError;
uniform float cutOffThreshold;
uniform float colourError;
uniform float groundNormalStrength;
uniform vec3 baseColor;

uniform int bladesPerChunk;
uniform int bladesPerAxis;

uniform float textureMapOffset;


uniform sampler2DArray heightmaps;
uniform sampler2D grassNoise;

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

vec3 calcLocalCellPosition(vec2 gridPosition, float spacing) {
	vec2 offset = hash22(gridPosition)*spacing;
	return vec3(offset.x, 0, offset.y);	
}

vec4 readHeightmap(vec2 uv, int chunkID) {
	return texture(heightmaps, vec3(uv.xy*(1.0-textureMapOffset)+vec2(textureMapOffset/2.0), chunkID)).xyzw;
}

void main() {

	valid = 1.0f;
	tipFactor = position.y/bladeHeight;
	
    int chunkIndex = gl_InstanceID / bladesPerChunk;
    int bladeIndex = gl_InstanceID % bladesPerChunk;

    vec4 chunkDataVector = chunkData[chunkIndex];
    vec2 chunkOrigin = chunkDataVector.xy;
    float chunkSize = chunkDataVector.z;
    float heightMapIndex = chunkDataVector.w;
	float spacing = chunkSize/bladesPerAxis;
	vec2 gridPosition = vec2((floor(bladeIndex/bladesPerAxis)), mod(float(bladeIndex),bladesPerAxis))*spacing;
	vec2 bladePositionChunkSpace = gridPosition+vec2(hash23(gridPosition))*spacing;
	vec4 heightMapData;
	if (tipFactor < 0.001f) {
		heightMapData = readHeightmap((position.xz+bladePositionChunkSpace)/chunkSize ,int(heightMapIndex));
	} else {
		heightMapData = readHeightmap(bladePositionChunkSpace/chunkSize ,int(heightMapIndex));
	}
	vec3 bladePositionWorld = vec3(chunkOrigin.x, 0, chunkOrigin.y)+vec3(bladePositionChunkSpace.x, 0, bladePositionChunkSpace.y)+vec3(0, heightMapData.w, 0);
	vec3 terrainNormal = heightMapData.xyz;
	float steepness = 1.0-dot(vec3(0.0, 1.0, 0.0), terrainNormal);
	if (steepness > 0.4) valid = 0.0;
	
	vec3 VertexPositionLocal = position;
	vec2 bladeWorldSeed = chunkOrigin+gridPosition;

	float voronoiNoiseFactor = texture(grassNoise, bladePositionWorld.xz*0.01).w;
	float heightErrorFactor = 1.0+hash11(bladeIndex)*2.0*heightError-heightError;
	float heightFactor = voronoiNoiseFactor*heightErrorFactor*(1.0-steepness);
	if (heightFactor < cutOffThreshold) valid = 0.0;

	VertexPositionLocal.y *= heightFactor;
	VertexPositionLocal.xz *= 1.0+hash21(bladeWorldSeed)*2.0*radiusError-radiusError;

	float rotX = (hash21(bladeWorldSeed))*PI*tipFactor*bendyness;
	float rotZ = (hash21(bladeWorldSeed))*PI*tipFactor*bendyness;
	float rotY = hash21(bladeWorldSeed)*PI*2;
	mat3 localRotMatrix = rotYMatrix(rotY)*rotXMatrix(rotX)*rotZMatrix(rotZ);
	
	VertexPositionLocal = localRotMatrix*VertexPositionLocal;
	float windX = tipFactor*sin(time+bladePositionWorld.z*0.2f)*0.45*cos(time*2.371+bladePositionWorld.z*0.2f)*swayAmount*heightFactor;
	float windZ = tipFactor*sin(time*3.0+bladePositionWorld.x)*swayAmount*0.3f*heightFactor;
	VertexPositionLocal = VertexPositionLocal*rotXMatrix(windX)*rotZMatrix(windZ);

	
	vec3 vertexPositionWorld = VertexPositionLocal+bladePositionWorld;

	fragColor = baseColor+baseColor*vec3(hash23(bladeWorldSeed))*colourError*2-colourError*baseColor;
	//vec3 rotatedNormal = normal.xyz * inverse(transpose(rotationMatrix));
	vec3 rotatedNormal = transpose(inverse(localRotMatrix))*normal.xyz;
	//vec3 rotatedNormal = localRotMatrix*normal.xyz;
	vec3 adjustedNormal = normalize(rotatedNormal+terrainNormal*groundNormalStrength);
	fragNormal = (transpose(invViewMatrix)*vec4(adjustedNormal, 1.0f)).xyz;
	
	gl_Position =  projectionMatrix*viewMatrix*vec4(vertexPositionWorld, 1.0);
}