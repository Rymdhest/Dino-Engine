#version 330

in vec2 position;
uniform mat4 uProjection;
uniform mat4 modelMatrix;

void main(void){

    vec4 pos = vec4(position.x, position.y, 0.0, 1.0);
    gl_Position = pos*modelMatrix*uProjection;
}