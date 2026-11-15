#ifndef DINO_WATER_TERRAIN_HEIGHT_GLSL
#define DINO_WATER_TERRAIN_HEIGHT_GLSL

// The terrain mesh seen from above, which the water captures to know where it stands (WaterShoreField), for whatever needs to know how deep the water is: the water
// itself (WaterCommon.glsl) and what grows in it, which the waves sway (Wind_Vegetation.frag). WaterRenderer.BindTerrainCapture binds it with where it is.
//
// terrainRegion:       xy world corner, zw size (z = 0: none).
// terrainHeightRange:  x world height at depth 0, y metres the depth covers.

uniform sampler2D terrainHeights;
uniform vec4 terrainRegion;
uniform vec2 terrainHeightRange;

// Height of the terrain's mesh at xz, false where none was captured. Water decides where it stands from this, not from the depth buffer: parallax mapping draws the
// ground up to half a metre below its mesh, and water filling those dips would spread far beyond the shoreline (and over the whole of a shallow puddle's outline).
bool waterTerrainHeight(vec2 xz, out float height)
{
    height = -1e4;
    if (terrainRegion.z <= 0.0) return false;
    vec2 uv = (xz - terrainRegion.xy) / terrainRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return false;
    float depth = textureLod(terrainHeights, uv, 0.0).r;
    if (depth >= 1.0) return false;
    height = terrainHeightRange.x - depth * terrainHeightRange.y;
    return true;
}

#endif
