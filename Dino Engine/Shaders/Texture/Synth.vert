#version 430

// One triangle over the whole of whatever's being written, made from nothing but its vertex numbers. Every synthesis pass works
// texel by texel from gl_FragCoord, so it needs nothing else.

void main(void)
{
    vec2 corner = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    gl_Position = vec4(corner * 2.0 - 1.0, 0.0, 1.0);
}
