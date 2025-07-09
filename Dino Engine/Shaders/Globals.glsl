layout(std140, binding = 0) uniform GlobalData{
    mat4 projectionMatrix;
    mat4 viewMatrix;
    mat4 invProjectionMatrix;
    mat4 invViewMatrix;
    vec3 viewPosWorld;
    float time;
    vec2 resolution;
    float delta;
    int worldSeed;
};