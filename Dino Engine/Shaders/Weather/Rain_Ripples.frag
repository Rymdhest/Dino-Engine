#version 420

// The rings rain makes on anything that holds water, as a tile of slopes, drawn fresh each frame and read by puddles, wet ground and every water surface alike. Each cell of a few
// grids holds one drop, which lands somewhere new each time round, sends a ring out and dies away (after the rings of Lagarde, "Water drop 2b"). The harder it rains the more of the
// cells are live. Everything wraps at the tile's edge, so it tiles.

in vec2 textureCoords;
layout (location = 0) out vec2 out_Slope;

uniform float rippleClock;
uniform float intensity;

uint rippleHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float rippleRandom(inout uint state)
{
    state = rippleHash(state);
    return float(state >> 8u) * (1.0 / 16777216.0);
}

vec2 grid(vec2 uv, int cells, float rate, uint salt)
{
    vec2 scaled = uv * float(cells);
    ivec2 base = ivec2(floor(scaled));
    vec2 slope = vec2(0.0);

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            ivec2 cell = base + ivec2(x, y);
            ivec2 wrapped = (cell + cells) % cells;
            uint state = rippleHash(uint(wrapped.x) * 73856093u ^ uint(wrapped.y) * 19349663u ^ salt);

            // Each drop keeps its own time, so they don't all land together.
            float offset = rippleRandom(state);
            float cycle = rippleClock * rate + offset;
            float generation = floor(cycle);
            float age = cycle - generation;

            uint drop = rippleHash(state ^ uint(generation) * 2654435761u);
            if (rippleRandom(drop) > intensity) continue;
            vec2 centre = vec2(cell) + vec2(rippleRandom(drop), rippleRandom(drop));
            float size = 0.6 + 0.6 * rippleRandom(drop);

            vec2 away = scaled - centre;
            float dist = length(away);
            float radius = age * 1.15 * size;
            float front = (dist - radius) / (0.09 + 0.15 * age);
            if (abs(front) > 2.5) continue;

            // A short train of rings, gone by the time it has spread.
            float rings = sin(front * 3.6) * exp(-front * front);
            float fade = (1.0 - age) * (1.0 - age) * smoothstep(0.0, 0.05, age);
            slope += away / max(dist, 1e-3) * (rings * fade);
        }
    }
    return slope;
}

void main(void)
{
    vec2 slope = grid(textureCoords, 9, 1.1, 0x1234u) + grid(fract(textureCoords + 0.37), 14, 1.45, 0x9876u) * 0.8;
    out_Slope = slope;
}
