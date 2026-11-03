#version 420

// One jump flood pass (Rong and Tan 2006): every texel looks at the nearest land seed and the nearest sea seed found
// so far by itself and its eight neighbours stepSize texels away, and keeps the closest of each. Halving the step from
// half the field down to 1 propagates the nearest seeds everywhere in log2(size) passes.

layout (location = 0) out vec4 out_Seeds;

uniform sampler2D seeds;
uniform int stepSize;

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 size = textureSize(seeds, 0);
    vec2 self = vec2(texel);

    vec4 best = vec4(-1.0);
    float bestLand = 1e20;
    float bestSea = 1e20;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        ivec2 neighbour = texel + ivec2(x, y) * stepSize;
        if (any(lessThan(neighbour, ivec2(0))) || any(greaterThanEqual(neighbour, size))) continue;
        vec4 candidate = texelFetch(seeds, neighbour, 0);
        if (candidate.x >= 0.0)
        {
            vec2 offset = candidate.xy - self;
            float distanceSquared = dot(offset, offset);
            if (distanceSquared < bestLand)
            {
                bestLand = distanceSquared;
                best.xy = candidate.xy;
            }
        }
        if (candidate.z >= 0.0)
        {
            vec2 offset = candidate.zw - self;
            float distanceSquared = dot(offset, offset);
            if (distanceSquared < bestSea)
            {
                bestSea = distanceSquared;
                best.zw = candidate.zw;
            }
        }
    }
    out_Seeds = best;
}
