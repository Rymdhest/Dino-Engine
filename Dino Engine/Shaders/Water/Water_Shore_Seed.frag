#version 420

// First pass of the shore field (WaterShoreField): reads the top-down capture of the terrain heights and marks each
// texel as sea, land, or unknown (no terrain loaded there).
// out_Seeds:    xy this texel if it is land, zw this texel if it is sea, -1 otherwise (jump flood seeds)
// out_Info:     x depth below sea level (m), y 1 if sea or unknown, z 1 if known, w unused
// out_Openness: x 1 if known sea, y 1 if known (mipmapped: their ratio is how much of the surroundings is sea)

layout (location = 0) out vec4 out_Seeds;
layout (location = 1) out vec4 out_Info;
layout (location = 2) out vec4 out_Openness;

uniform sampler2D terrainDepth;
uniform float captureTop;
uniform float captureRange;
uniform float seaLevel;
uniform float surfaceOffset;

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    float depth = texelFetch(terrainDepth, texel, 0).r;
    bool known = depth < 1.0;
    // The capture draws the bare height map; the visible ground sits surfaceOffset above it (TerrainRenderer.SurfaceOffset).
    float height = captureTop - depth * captureRange + surfaceOffset;
    bool sea = known && height < seaLevel;
    bool land = known && !sea;

    vec2 self = vec2(texel);
    out_Seeds = vec4(land ? self : vec2(-1.0), sea ? self : vec2(-1.0));
    out_Info = vec4(known ? seaLevel - height : 0.0, (sea || !known) ? 1.0 : 0.0, known ? 1.0 : 0.0, 0.0);
    out_Openness = vec4(sea ? 1.0 : 0.0, known ? 1.0 : 0.0, 0.0, 0.0);
}
