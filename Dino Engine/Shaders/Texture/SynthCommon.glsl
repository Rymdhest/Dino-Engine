#ifndef DINO_SYNTH_COMMON_GLSL
#define DINO_SYNTH_COMMON_GLSL

// What every pass of texture synthesis shares (Textures/Synthesis, TEXTURES.md). A canvas is the unit torus: every image tiles,
// and anything that reads past an edge reads the other side, so nothing made here can have a seam.

uniform vec2 targetSize;    // Texels on a side of what's being written.

ivec2 texelAt()
{
    return ivec2(gl_FragCoord.xy);
}

// The middle of this texel, in the tile: (i + 0.5) / size.
vec2 uvAt()
{
    return gl_FragCoord.xy / targetSize;
}

// One input of a pass: a channel of an image (0 to 3), all four of its channels (4), or a constant (anything below 0).
vec4 readInput(sampler2D image, int channel, vec4 constant, ivec2 at)
{
    if (channel < 0) return constant;
    vec4 v = texelFetch(image, at, 0);
    return channel >= 4 ? v : vec4(v[channel]);
}

// The same, filtered and wrapping, anywhere in the tile. Images are the canvas's size, so at a texel's middle this is that texel.
vec4 sampleInput(sampler2D image, int channel, vec4 constant, vec2 uv)
{
    if (channel < 0) return constant;
    vec4 v = texture(image, uv);
    return channel >= 4 ? v : vec4(v[channel]);
}

// Whole numbers wrapped into [0, period). GLSL leaves % undefined for anything negative, so this goes by floor, of a quotient half a
// step away from any whole number: a GPU's division is only nearly exact, and 7.0 / 7.0 can come out 0.99999994, which floored left the
// cell just past the tile's edge unwrapped, so the far edge had other stones than the near one. Only a period that's a power of two
// divided exactly, which is why some tiled and some didn't.
ivec2 wrapInt(ivec2 value, ivec2 period)
{
    return value - period * ivec2(floor((vec2(value) + 0.5) / vec2(period)));
}

// A texel anywhere, wrapping round the tile.
vec4 fetchWrapped(sampler2D image, ivec2 at)
{
    return texelFetch(image, wrapInt(at, textureSize(image, 0)), 0);
}

#endif
