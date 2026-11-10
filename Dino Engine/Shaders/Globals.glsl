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
    vec3 skyColour;
    float EMPTY;
    vec4 focusPoint;
    vec4 sunDirectionWorld;     // xyz towards the sun (or the moon, from the same place), unit, in the world: what a relief's own shadows fall from.
    // The halls near the camera, the buildings that can be walked into (Halls.glsl, HALLS.md), as HallShaderData lays them out.
    vec4 hallBox[8];            // xy its middle on the ground, zw how far it reaches to the outside of its walls, along its own x and z.
    vec4 hallFrame[8];          // xy the cosine and sine of its yaw, z the height of its floor, w how far above its floor its walls end.
    vec4 hallCut[8];            // x how thick its walls are, y how far above its floor a wall facing the camera comes down to, z how far its roof reaches past its walls, w how far in front of a wall facing the camera comes down with it.
    vec4 hallCutaway;           // x the hall the camera looks into, or -1, y how far it's opened, 0 to 1, z how many halls there are.
};