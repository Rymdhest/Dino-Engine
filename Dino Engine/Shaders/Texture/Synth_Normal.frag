#version 430

// The normal of a height field, exactly as deep as its parallax (MaterialSpec.ReliefRatio): `slope` is the relief over the tile's
// width, both in metres, so a height of 0 to 1 over a tile of 0 to 1 becomes a surface whose normal is (-s dh/du, -s dh/dv, 1). The
// renderer marches the same height to the same depth (Shaders/Surface/Relief.glsl), so what the light says is where the parallax
// puts it, and nothing between them is a matter of taste. A Sobel filter, normalised to a true derivative: a plain difference over
// two texels picks up the texel grid where a slope is shallow.
//
// result: the unit normal in xyz, x along +u and y along +v; the height in w.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;      // The height.
uniform int channelA;
uniform vec4 constantA;
uniform float slope;           // Relief over tile width.

float heightAt(ivec2 at)
{
    if (channelA < 0) return constantA.x;
    vec4 v = fetchWrapped(inputA, at);
    return channelA >= 4 ? v.x : v[channelA];
}

void main(void)
{
    ivec2 at = texelAt();
    float h00 = heightAt(at + ivec2(-1, -1));
    float h10 = heightAt(at + ivec2(0, -1));
    float h20 = heightAt(at + ivec2(1, -1));
    float h01 = heightAt(at + ivec2(-1, 0));
    float h21 = heightAt(at + ivec2(1, 0));
    float h02 = heightAt(at + ivec2(-1, 1));
    float h12 = heightAt(at + ivec2(0, 1));
    float h22 = heightAt(at + ivec2(1, 1));

    // Each side of the kernel weighs 4 and the sides are two texels apart, so the sum over 8 texels is the derivative per texel.
    float du = ((h20 + 2.0 * h21 + h22) - (h00 + 2.0 * h01 + h02)) / 8.0 * targetSize.x;
    float dv = ((h02 + 2.0 * h12 + h22) - (h00 + 2.0 * h10 + h20)) / 8.0 * targetSize.y;
    vec3 n = normalize(vec3(-slope * du, -slope * dv, 1.0));
    result = vec4(n, heightAt(at));
}
