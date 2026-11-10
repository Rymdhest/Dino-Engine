#version 430

// A field run through a curve (Field.Equalize and Field.Curve): what the field holds over `range` is read as a place along a row of
// numbers, linearly between them. Equalize's curve is the field's own cumulative histogram, which spreads it evenly over 0 to 1.

#include Texture/SynthCommon.glsl

layout (location = 0) out vec4 result;

uniform sampler2D inputA;
uniform int channelA;
uniform vec4 constantA;
uniform sampler2D curve;       // One row of values.
uniform vec2 range;

void main(void)
{
    float value = readInput(inputA, channelA, constantA, texelAt()).x;
    float width = float(textureSize(curve, 0).x);
    float place = clamp((value - range.x) / max(range.y - range.x, 1e-20), 0.0, 1.0);
    result = vec4(texture(curve, vec2((place * (width - 1.0) + 0.5) / width, 0.5)).r);
}
