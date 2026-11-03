#version 420

// Debug view: tints the edge pixels, which the lights and composites shade once per sample, over the final image.

uniform sampler2D edges;

layout (location = 0) out vec4 out_Colour;

void main(void)
{
    if (texelFetch(edges, ivec2(gl_FragCoord.xy), 0).r < 0.5) discard;
    out_Colour = vec4(1.0, 0.0, 1.0, 0.45);
}
