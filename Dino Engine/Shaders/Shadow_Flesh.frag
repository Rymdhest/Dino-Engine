#version 420

// Flesh alone, from the sun (FleshDepth, SUBSURFACE.md): where light goes into skin and hide, for how far it's gone through them where it comes out. Everything else on a body
// is left out, as armour, a harness or a strap stops light rather than carrying it, and it's the sun's own shadow map's to say what's in the way of the flesh.

in vec2 fragUV;
in float textureIndex;

uniform int numberOfMaterials;

#include Surface/MaterialTable.glsl
#include Lighting/SubsurfaceKinds.glsl

void main(void)
{
    if (materialSubsurfaceKind(textureIndex, numberOfMaterials) != SSS_FLESH) discard;
}
