#version 330

// Snow falling into what was pressed, and kicked-up clumps settling: taken off every texel of the trail by a subtracting blend (SnowTrailRenderer.TakeOffFill).

uniform vec4 fill;

in vec2 textureCoords;
out vec4 taken;

void main(void)
{
    taken = fill;
}
