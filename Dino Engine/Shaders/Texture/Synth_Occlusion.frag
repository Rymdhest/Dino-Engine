#version 430

// How much of the sky each point of a height field sees, cosine-weighted about its own normal (Field.Occlusion and the bake): the
// horizon-based integral of Jimenez et al., "Practical Real-Time Strategies for Accurate Indirect Occlusion" (2016), done once and
// properly instead of each frame. The height is made physical first: `slope` is the relief over the tile's width, so a crack's walls
// are as steep as they'll look. Directions are spread evenly over half a turn, each slice looking both ways; steps grow outwards,
// reading coarser mips as they go, so a wide reach costs little and fine crevices still get their own darkness.
//
// result: visibility, 1 for open sky, in all channels.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;      // The height, with mips.
uniform int channelA;
uniform vec4 constantA;
uniform float slope;           // Relief over tile width.
uniform float reach;           // How far to look for what's in the way, in the tile.
uniform int directions;
uniform int steps;

const float PI = 3.14159265;

float heightAt(vec2 uv, float lod)
{
    if (channelA < 0) return constantA.x;
    vec4 v = textureLod(inputA, uv, lod);
    return channelA >= 4 ? v.x : v[channelA];
}

void main(void)
{
    vec2 uv = uvAt();
    vec2 texel = 1.0 / targetSize;
    float here = heightAt(uv, 0.0);

    // This point's own normal, in tile units: the height is scaled by `slope` to be as tall as it is wide.
    float hx = (heightAt(uv + vec2(texel.x, 0.0), 0.0) - heightAt(uv - vec2(texel.x, 0.0), 0.0)) * 0.5 * targetSize.x * slope;
    float hy = (heightAt(uv + vec2(0.0, texel.y), 0.0) - heightAt(uv - vec2(0.0, texel.y), 0.0)) * 0.5 * targetSize.y * slope;
    vec3 n = normalize(vec3(-hx, -hy, 1.0));

    int sliceCount = max(directions, 1);
    int stepCount = max(steps, 1);
    float visibility = 0.0;
    for (int d = 0; d < sliceCount; d++)
    {
        float angle = (float(d) + 0.5) / float(sliceCount) * PI;
        vec2 direction = vec2(cos(angle), sin(angle));

        // The normal seen in this slice: its angle from straight up, positive towards +direction, and how much of it lies in the slice.
        vec2 projected = vec2(dot(n.xy, direction), n.z);
        float projectedLength = length(projected);
        float gamma = atan(projected.x, projected.y);

        // The highest the ground rises on each side, as the sine of its elevation from here.
        float highest[2] = float[2](-1.0, -1.0);
        for (int side = 0; side < 2; side++)
        {
            vec2 way = side == 0 ? direction : -direction;
            for (int i = 0; i < stepCount; i++)
            {
                float t = (float(i) + 1.0) / float(stepCount);
                float distance = reach * t * t;
                float lod = max(log2(distance * targetSize.x / 3.0), 0.0);
                float rise = (heightAt(uv + way * distance, lod) - here) * slope;
                float sine = rise * inversesqrt(rise * rise + distance * distance);
                highest[side] = max(highest[side], sine);
            }
        }

        // Horizon angles from straight up: h2 on the +direction side, h1 (negative) on the other, each kept within the normal's
        // own hemisphere.
        float h2 = PI * 0.5 - asin(clamp(highest[0], -1.0, 1.0));
        float h1 = -(PI * 0.5 - asin(clamp(highest[1], -1.0, 1.0)));
        h1 = gamma + max(h1 - gamma, -PI * 0.5);
        h2 = gamma + min(h2 - gamma, PI * 0.5);
        float sinGamma = sin(gamma);
        float cosGamma = cos(gamma);
        float slice = 0.25 * (-cos(2.0 * h1 - gamma) + cosGamma + 2.0 * h1 * sinGamma)
                    + 0.25 * (-cos(2.0 * h2 - gamma) + cosGamma + 2.0 * h2 * sinGamma);
        visibility += projectedLength * slice;
    }
    // Averaged over the slices themselves and not over the normal's share of each: a normal lying in open ground then sees half the
    // sky, (2 / pi) * (pi / 4), as it should, and a level one all of it.
    result = vec4(clamp(visibility / float(sliceCount), 0.0, 1.0));
}
