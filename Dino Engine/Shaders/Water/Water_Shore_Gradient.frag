#version 420

// Last pass of the shore field: adds the gradient of the smoothed distance, the direction waves travel against.
// out_Field: x signed distance (m), y depth below sea level (m), zw d distance / d (x, z)

layout (location = 0) out vec4 out_Field;

uniform sampler2D source;
uniform float texelMetres;

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 maxTexel = textureSize(source, 0) - 1;
    vec3 blurred = texelFetch(source, texel, 0).rgb;
    // Normalise the depth by how much known terrain went into it; with none at all, assume open sea.
    vec2 here = vec2(blurred.x, blurred.z > 1e-3 ? blurred.y / blurred.z : 50.0);
    float left = texelFetch(source, clamp(texel - ivec2(1, 0), ivec2(0), maxTexel), 0).r;
    float right = texelFetch(source, clamp(texel + ivec2(1, 0), ivec2(0), maxTexel), 0).r;
    float down = texelFetch(source, clamp(texel - ivec2(0, 1), ivec2(0), maxTexel), 0).r;
    float up = texelFetch(source, clamp(texel + ivec2(0, 1), ivec2(0), maxTexel), 0).r;
    out_Field = vec4(here, vec2(right - left, up - down) / (2.0 * texelMetres));
}
