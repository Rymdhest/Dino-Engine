#version 420

// Writes the stencil bit of the edge pixels the G-buffer resolve flagged (MsaaRenderer sets the stencil state); the
// other pixels are discarded and keep a clear bit. Colour writes are off.

uniform sampler2D edges;

void main(void)
{
    if (texelFetch(edges, ivec2(gl_FragCoord.xy), 0).r < 0.5) discard;
}
