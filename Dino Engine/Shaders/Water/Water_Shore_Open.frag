#version 420

// How open the water is round each point (WaterShoreField): one way of a separable Gaussian over the coarse grid, of the share of known sea (x) and of
// known ground (y), which their ratio is taken from where it's read. A fixed reach in the world, so ground streaming in far off changes nothing here: the
// mips this replaced reached as far as their coarsest texel, which took in whatever had just loaded at the edge of the square.
// out_Open: x known sea, y known ground, both blurred.

layout (location = 0) out vec4 out_Open;

uniform sampler2D source;
uniform int level;          // The level of source that is the coarse grid: 3 of the seeds' openness, 0 of the first pass's result.
uniform vec2 direction;     // (1, 0) or (0, 1).
uniform float sigma;        // In coarse texels.
uniform int taps;           // Each side.

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 size = textureSize(source, level);
    ivec2 step = ivec2(direction);
    float inverse = 1.0 / (2.0 * sigma * sigma);
    vec2 sum = vec2(0.0);
    float weights = 0.0;
    for (int i = -24; i <= 24; i++)
    {
        if (i < -taps || i > taps) continue;
        float weight = exp(-float(i * i) * inverse);
        weights += weight;
        ivec2 tap = texel + step * i;
        // Past the edge of the square nothing is known.
        if (any(lessThan(tap, ivec2(0))) || any(greaterThanEqual(tap, size))) continue;
        sum += texelFetch(source, tap, level).rg * weight;
    }
    out_Open = vec4(sum / max(weights, 1e-6), 0.0, 0.0);
}
