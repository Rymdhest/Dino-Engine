#version 330

// Broken rock: every cell a flat face at a tilt of its own, with a crack where two meet. A cell of Texture_Voronoi is level and only shows at its edge, which reads as dried mud. It's the
// planes that catch the light differently from each other that look like stone. Tiles, for a whole number of cells.

in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 material_out;
layout (location = 2) out float height_out;

uniform vec4 albedo;
uniform vec4 material;

uniform vec2 scale;
uniform float seed;
uniform float tilt;     // How far a face leans, in the height map's units for a cell's width.
uniform float relief;   // How far faces stand from each other.
uniform float crack;    // How deep the cracks between them go.
uniform float width;    // How wide those cracks are, in cells.

uvec3 mixed(uvec3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v;
}

vec3 random3(vec2 cell)
{
    uvec2 wrapped = uvec2(mod(cell, scale));
    return vec3(mixed(uvec3(wrapped, uint(seed)))) * (1.0 / 4294967295.0);
}

void main(void)
{
    vec2 p = textureCoords * scale;
    vec2 base = floor(p);
    vec2 inside = p - base;

    vec2 toNearest = vec2(0.0);
    vec2 nearestCell = vec2(0.0);
    float nearest = 1e9;
    for (int j = -2; j <= 2; j++) {
        for (int i = -2; i <= 2; i++) {
            vec2 offset = vec2(i, j);
            vec2 to = offset + random3(base + offset).xy - inside;
            float distance = dot(to, to);
            if (distance < nearest) {
                nearest = distance;
                toNearest = to;
                nearestCell = base + offset;
            }
        }
    }

    // The true distance to the edge of the cell, so that a crack keeps its width round every corner (Quilez).
    float border = 1e9;
    for (int j = -2; j <= 2; j++) {
        for (int i = -2; i <= 2; i++) {
            vec2 offset = vec2(i, j);
            vec2 cell = base + offset;
            if (cell == nearestCell) continue;
            vec2 to = offset + random3(cell).xy - inside;
            vec2 between = to - toNearest;
            if (dot(between, between) < 1e-6) continue;
            border = min(border, dot(0.5 * (toNearest + to), normalize(between)));
        }
    }

    vec3 own = random3(nearestCell + 17.0);
    float turn = own.x * 6.2831853;
    vec2 lean = vec2(cos(turn), sin(turn)) * (0.35 + 0.65 * own.y);
    float height = 0.5 + (own.z - 0.5) * relief - dot(toNearest, lean) * tilt;
    height -= crack * (1.0 - smoothstep(0.0, width, border));

    albedo_out = albedo;
    material_out = material;
    height_out = clamp(height, 0.0, 1.0);
}
