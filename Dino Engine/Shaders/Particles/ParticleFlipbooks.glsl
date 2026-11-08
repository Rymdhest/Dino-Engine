#ifndef DINO_PARTICLE_FLIPBOOKS_GLSL
#define DINO_PARTICLE_FLIPBOOKS_GLSL

// How the flipbooks ParticleBaker grows at load are laid out in their texture arrays, one frame a layer. These must match ParticleBaker's constants.
//
// Flames (FLAME_WIDTH x FLAME_HEIGHT, rgba16f): r how much of the pixel is burning, g that times its temperature (0 to about 1.3), so a mip still knows how hot what's in it is,
// ba how far the fire there moves by the next frame, in texture coordinates. The source of the fire is at the bottom. Hearth and Wick go round for good; Tongues play once.
//
// Puffs of smoke (PUFF_SIZE square, two rgba16f arrays): six-way lightmaps, after Unity's and EmberGen's, premultiplied by how dense the smoke is. A is the light reaching
// the eye from a light on the right, above and in front, and the smoke's alpha; B from the left, below and behind, and how deep in the puff its light comes from (the core of a
// fireball is hottest). A puff swells and thins over its frames.

const int FLAME_WIDTH = 96;
const int FLAME_HEIGHT = 144;
const int FLAME_LOOP_FRAMES = 48;
const int FLAME_TONGUE_FRAMES = 32;
const int FLAME_TONGUES = 3;
const int FLAME_HEARTH_FIRST = 0;
const int FLAME_WICK_FIRST = FLAME_LOOP_FRAMES;
const int FLAME_TONGUE_FIRST = FLAME_LOOP_FRAMES * 2;
const int FLAME_LAYERS = FLAME_TONGUE_FIRST + FLAME_TONGUE_FRAMES * FLAME_TONGUES;

const int PUFF_SIZE = 128;
const int PUFF_FRAMES = 32;
const int PUFF_VARIANTS = 4;
const int PUFF_LAYERS = PUFF_FRAMES * PUFF_VARIANTS;

// How big a puff is through its life, as a share of its frame: quick to swell at first, slowing as it spreads. The puff's flow between frames is this growth, so the draw
// warps by it to go smoothly from one frame to the next.
float puffRadius(float t)
{
    return mix(0.48, 0.95, 1.0 - pow(1.0 - clamp(t, 0.0, 1.0), 2.2));
}

#endif
