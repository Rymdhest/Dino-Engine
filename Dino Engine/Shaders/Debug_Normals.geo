#version 330 core

layout(points) in;
layout(line_strip, max_vertices = 6) out;

in vec3 vPosition[];
in vec3 vNormal[];
in vec3 vTangent[];
in vec3 vBitangent[];

out vec3 color;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform float lineLength;

void main()
{
    // Line for the normal
    color = vec3(0.0, 0.0, 1.0); 
    gl_Position = vec4(vPosition[0], 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    gl_Position = vec4(vPosition[0] + vNormal[0] * lineLength, 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    EndPrimitive();

    // Line for the tangent
    color = vec3(1.0, 0.0, 0.0); 
    gl_Position = vec4(vPosition[0], 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    gl_Position = vec4(vPosition[0] + vTangent[0] * lineLength, 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    EndPrimitive();

    // Line for the biTangent
    color = vec3(0.0, 1.0, 0.0); 
    gl_Position = vec4(vPosition[0], 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    gl_Position = vec4(vPosition[0] + vBitangent[0] * lineLength, 1.0) * viewMatrix * projectionMatrix;
    EmitVertex();
    EndPrimitive();
}