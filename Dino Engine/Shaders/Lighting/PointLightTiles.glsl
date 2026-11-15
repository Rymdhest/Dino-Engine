#ifndef DINO_POINT_LIGHT_TILES_GLSL
#define DINO_POINT_LIGHT_TILES_GLSL

// The point lights as one pass lights them (PointLightRenderer): this frame's lights, and which of them can reach anything in each tile of the screen, as a bit a light
// (Lighting/PointLight_Cull.comp). Every pixel then reads the G-buffer once and adds up only the lights its tile has, where drawing a volume for each light read it and
// blended into the scene once a light, which over a square full of lanterns was a dozen times a pixel. The constants match PointLightRenderer's.

#define POINT_TILE 16
#define POINT_BATCH 256
#define POINT_WORDS (POINT_BATCH / 32)

struct PointLight
{
    vec4 positionRadius;    // xyz in view space, w how far it reaches, where lightFalloff comes to nothing
    vec4 colourFill;        // rgb what it gives at a metre, a the share of it that reaches a surface however it faces
    vec4 worldShadow;       // xyz in the world, w its cube in the shadow maps' array, or -1 for none
    vec4 shadowHalls;       // x how much of its shadow it's given (it fades as a light gains or gives up a map), y which halls' rooms it's in, as bits held as a whole number (Halls.glsl),
                            // z a spotlight's softness
    vec4 cone;              // a spotlight's: xyz in view space the way from what it lights back towards it, w the cosine of its edge; w -2 for a point light
};

layout(std430, binding = 30) readonly buffer PointLights { PointLight pointLights[]; };
layout(std430, binding = 31) buffer PointTiles { uint pointTiles[]; };

// Where a tile's bits start.
int pointTileBits(int tile)
{
    return tile * POINT_WORDS;
}

// Which halls' rooms a point is in, a bit a hall: no light crosses a hall's walls, so a light reaches a point only if they're in the same rooms (hallsLetThrough).
uint hallRoomsOf(vec3 p)
{
    uint rooms = 0u;
    int count = int(hallCutaway.z + 0.5);
    for (int i = 0; i < count; i++)
    {
        if (insideHall(i, p)) rooms |= 1u << uint(i);
    }
    return rooms;
}

#endif
