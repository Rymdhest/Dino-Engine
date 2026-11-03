#version 420

// Separable Gaussian blur of the shore field's distance and depth (one axis per pass). Smoothing the distance rounds
// the kinks where wave fronts from two shorelines meet, so crests curve instead of forming sharp chevrons. The depth is
// blurred as depth x known and known separately (normalised convolution), so unloaded terrain doesn't bleed into it.

layout (location = 0) out vec4 out_Field;

uniform sampler2D source;
uniform vec2 stepTexels;

const float WEIGHTS[5] = float[](0.2041637, 0.1801738, 0.1238315, 0.0662822, 0.0276306);

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 maxTexel = textureSize(source, 0) - 1;
    vec3 sum = texelFetch(source, texel, 0).rgb * WEIGHTS[0];
    for (int i = 1; i < 5; i++)
    {
        ivec2 offset = ivec2(round(stepTexels * float(i)));
        sum += texelFetch(source, clamp(texel + offset, ivec2(0), maxTexel), 0).rgb * WEIGHTS[i];
        sum += texelFetch(source, clamp(texel - offset, ivec2(0), maxTexel), 0).rgb * WEIGHTS[i];
    }
    out_Field = vec4(sum, 0.0);
}
