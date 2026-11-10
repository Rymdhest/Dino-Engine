#ifndef DINO_MATERIAL_TABLE_GLSL
#define DINO_MATERIAL_TABLE_GLSL

// What each tiling material is, beyond its textures (Textures/MaterialCatalog.cs, MaterialTableBuffer.cs): x its relief over its tile's width, the one number its
// normal map was made with and its parallax marches to (Surface/Relief.glsl); y the metres a tile spans at its natural size. Cards and anything past the
// materials have nothing here, and no relief.

layout(std140, binding = 12) uniform MaterialTable
{
    vec4 materialInfo[128];
};

// The relief ratio of a layer, or 0 for anything that isn't a tiling material.
float materialReliefRatio(float layer, int materialCount)
{
    int index = int(layer + 0.5);
    return index >= 0 && index < materialCount ? materialInfo[index].x : 0.0;
}

float materialTileMetres(float layer, int materialCount)
{
    int index = int(layer + 0.5);
    return index >= 0 && index < materialCount ? materialInfo[index].y : 1.0;
}

#endif
