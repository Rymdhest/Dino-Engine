#version 330 core

layout(location=0) in vec3 position;
layout(location=2) in vec3 normal;
layout(location=3) in vec3 tangent;

out vec3 vPosition;
out vec3 vNormal;
out vec3 vTangent;
out vec3 vBitangent;

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

void main()
{
    gl_Position = vec4(position, 1.0)*modelMatrix * viewMatrix * projectionMatrix;
    vPosition = vec3(vec4(position, 1.0)*modelMatrix);
    vNormal = normal*mat3(transpose(inverse(modelMatrix)));
    vTangent = tangent* mat3(transpose(inverse(modelMatrix)));
    vec3 bitanget = cross(tangent, normal);
    vBitangent = bitanget* mat3(transpose(inverse(modelMatrix)));
}