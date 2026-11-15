#version 420

// Last pass of a shore field build (WaterShoreField): the smoothed distance, its gradient (the way waves travel against) and how open the water is.
// out_Field: x signed distance (m), yz d distance / d (x, z), w how open the water is (0..1, 1 out in open sea or where nothing is known)

layout (location = 0) out vec4 out_Field;

uniform sampler2D source;     // x the smoothed distance
uniform sampler2D openness;   // Known sea and known ground round each point, on the coarse grid (Water_Shore_Open.frag)
uniform float texelMetres;

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 size = textureSize(source, 0);
    ivec2 maxTexel = size - 1;
    float distance = texelFetch(source, texel, 0).r;
    float left = texelFetch(source, clamp(texel - ivec2(1, 0), ivec2(0), maxTexel), 0).r;
    float right = texelFetch(source, clamp(texel + ivec2(1, 0), ivec2(0), maxTexel), 0).r;
    float down = texelFetch(source, clamp(texel - ivec2(0, 1), ivec2(0), maxTexel), 0).r;
    float up = texelFetch(source, clamp(texel + ivec2(0, 1), ivec2(0), maxTexel), 0).r;

    // The coarse grid is the field's own mip, so its texel centres fall where this texel's uv says.
    vec2 open = textureLod(openness, (vec2(texel) + 0.5) / vec2(size), 0.0).rg;
    float share = open.g > 1e-3 ? clamp(open.r / open.g, 0.0, 1.0) : 1.0;

    out_Field = vec4(distance, vec2(right - left, up - down) / (2.0 * texelMetres), share);
}
