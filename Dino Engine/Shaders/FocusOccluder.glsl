#ifndef DINO_FOCUS_OCCLUDER_GLSL
#define DINO_FOCUS_OCCLUDER_GLSL

// Whether a model is big enough to open for the focus (FocusCutout.glsl). Only what can really hide someone does: a tree, a house, a crag.
// A fence, a gate, a hat or a post is in front of the hero all the time and hides little, and thinning it out only reads as a fault.
// size is the model's box in its own units; the instance's scale is read off its matrix.

const float FOCUS_OCCLUDER_HEIGHT = 3.0;  // metres tall at least
const float FOCUS_OCCLUDER_WIDTH = 2.5;   // metres across at its narrowest, so that nothing thin, however tall or long, counts

float focusOccluder(vec3 size, mat4 modelMatrix)
{
    vec3 scale = vec3(length(modelMatrix[0].xyz), length(modelMatrix[1].xyz), length(modelMatrix[2].xyz));
    vec3 world = size * scale;
    return world.y >= FOCUS_OCCLUDER_HEIGHT && min(world.x, world.z) >= FOCUS_OCCLUDER_WIDTH ? 1.0 : 0.0;
}

#endif
