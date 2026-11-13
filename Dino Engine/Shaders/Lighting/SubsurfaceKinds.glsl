#ifndef DINO_SUBSURFACE_KINDS_GLSL
#define DINO_SUBSURFACE_KINDS_GLSL

// Subsurface scattering as the G-buffer keeps it, in gAlbedo.a (SUBSURFACE.md): which kind of scattering a surface does in the top two bits, and how much in the
// six below, 0 for none. The kind is what light does once it's in, which no amount can say: a sheet lets it through to its other side, a canopy of leaves or
// blades turned every way lets it through every way, and flesh and frost carry it on through themselves and let it out where they're thin, red through blood and
// blue through ice. A kind can't be averaged and an amount can, so the resolve keeps a pixel's samples of two kinds apart (Msaa_Resolve_GBuffer.frag). Included by
// gBufferUtil.glsl, for what writes the G-buffer, and by Lighting/lightingCalc.glsl, for what lights it; the numbers are Textures/SubsurfaceKind.cs's.
const int SSS_SHEET = 0;    // Cloth, paper, petals: anything thin with a face of its own. And everything that scatters nothing, at an amount of 0.
const int SSS_CANOPY = 1;   // Leaves, blades, moss and fur: masses of thin things whose normals are the mass's and not their own.
const int SSS_FLESH = 2;    // Skin and hide: thick, and red with blood where light gets through.
const int SSS_FROST = 3;    // Snow and ice: thick, and blue where light has gone a long way through.

float encodeSubsurface(int kind, float amount)
{
    return (float(kind * 64) + round(clamp(amount, 0.0, 1.0) * 63.0)) / 255.0;
}

int subsurfaceKind(float stored)
{
    return int(round(stored * 255.0)) >> 6;
}

float subsurfaceAmount(float stored)
{
    return float(int(round(stored * 255.0)) & 63) / 63.0;
}

// How far light goes in settled snow, as frost: the snow material's own (Textures/MaterialRecipes.Ground.cs).
const float SNOW_SCATTER = 0.45;

// A surface under settled snow (Weather/Wetness.glsl's weatherWetten says how much, 0 to 1): under a little, less light gets in to what's beneath; under a lot it's snow, and
// scatters as frost. Both go to nothing where one gives way to the other, so the change of kind leaves no line.
float encodeSubsurfaceUnderSnow(int kind, float amount, float snow)
{
    if (snow <= 0.0) return encodeSubsurface(kind, amount);
    if (kind == SSS_FROST) return encodeSubsurface(SSS_FROST, mix(amount, SNOW_SCATTER, snow));
    return snow < 0.5 ? encodeSubsurface(kind, amount * (1.0 - 2.0 * snow)) : encodeSubsurface(SSS_FROST, SNOW_SCATTER * (2.0 * snow - 1.0));
}

#endif
