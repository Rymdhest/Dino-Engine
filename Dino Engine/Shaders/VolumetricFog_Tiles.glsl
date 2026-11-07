#ifndef DINO_VOLUMETRIC_FOG_TILES_GLSL
#define DINO_VOLUMETRIC_FOG_TILES_GLSL

// The tiles the fog's half-resolution march takes turns in. Big enough that a whole group of pixels the GPU shades together
// has the frame off, as otherwise it would wait for the ones that don't.
const int FOG_TILE_SHIFT = 4;

bool fogTileRests(ivec2 pixel, int restingTiles)
{
    return restingTiles >= 0 && (((pixel.x >> FOG_TILE_SHIFT) + (pixel.y >> FOG_TILE_SHIFT)) & 1) == restingTiles;
}

#endif
