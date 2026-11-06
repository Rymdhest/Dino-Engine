#version 430

// Where water comes off things, a texel for each tile of the depth map looking straight down on the world. Rain falling on a roof or a tree runs to the edge and leaves it, so the place to find is a
// step down: a surface with air beside it and something lower under that air. The drop falls down the low side of the step. Only drawn when the map is, as what stands in the world doesn't move.

in vec2 textureCoords;
layout (location = 0) out vec4 out_Source;  // Weather/Drip.glsl

uniform sampler2D dripDepth;
uniform mat4 dripInverse;   // From the map's clip space back to the world.
uniform float depthRange;   // Metres of depth it covers.

const int TILE = 8;             // Texels along a tile's side: must match WeatherRenderer.DripTile.
const float SMALLEST_STEP = 0.6;    // Metres. Steep ground and a pitched roof are steps too, of a few centimetres a texel.
const float TALLEST_STEP = 30.0;    // WeatherRenderer's period of a drop has to fit a fall from here.

float scatter(ivec2 texel)
{
    uint x = uint(texel.x) * 73856093u ^ uint(texel.y) * 19349663u;
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    return float(x >> 8u) * (1.0 / 16777216.0);
}

vec3 worldAt(ivec2 texel, float depth)
{
    vec2 uv = (vec2(texel) + 0.5) / vec2(textureSize(dripDepth, 0));
    vec4 world = dripInverse * vec4(vec3(uv, depth) * 2.0 - 1.0, 1.0);
    return world.xyz / world.w;
}

void main(void)
{
    ivec2 first = ivec2(gl_FragCoord.xy) * TILE;
    ivec2 last = textureSize(dripDepth, 0) - 1;

    ivec2 low = ivec2(0);
    float lowDepth = 1.0;
    float highDepth = 1.0;
    float best = 0.0;
    for (int y = 0; y < TILE; y++)
    {
        for (int x = 0; x < TILE; x++)
        {
            ivec2 texel = first + ivec2(x, y);
            float here = texelFetch(dripDepth, texel, 0).r;
            if (here >= 1.0) continue;
            float above = min(min(texelFetch(dripDepth, min(texel + ivec2(1, 0), last), 0).r, texelFetch(dripDepth, max(texel - ivec2(1, 0), ivec2(0)), 0).r),
                              min(texelFetch(dripDepth, min(texel + ivec2(0, 1), last), 0).r, texelFetch(dripDepth, max(texel - ivec2(0, 1), ivec2(0)), 0).r));

            float step = (here - above) * depthRange;
            if (step < SMALLEST_STEP || step > TALLEST_STEP) continue;

            // Along a straight edge every texel is as good as the next, and picking by height alone would space them as evenly as fence posts.
            float score = step * (0.5 + scatter(texel));
            if (score > best)
            {
                best = score;
                low = texel;
                lowDepth = here;
                highDepth = above;
            }
        }
    }

    if (best <= 0.0)
    {
        out_Source = vec4(0.0);
        return;
    }
    vec3 landing = worldAt(low, lowDepth);
    out_Source = vec4(landing.xz, worldAt(low, highDepth).y, landing.y);
}
