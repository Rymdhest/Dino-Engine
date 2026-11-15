#version 420

// What the shaders read of the shore field, moved a step towards the newest build each frame (WaterShoreField.Settle), so nothing about the coast ever
// jumps: the newest build where nothing was shown before, and otherwise what was shown, at the same place in the world, mixed with it.
// out_Field: as Water_Shore_Compose.frag's.

layout (location = 0) out vec4 out_Field;

uniform sampler2D built;        // The newest build, texel for texel.
uniform sampler2D previous;     // What was shown, over previousRegion.
uniform vec4 region;            // xy world corner, zw size of the newest build, which is what this pass writes.
uniform vec4 previousRegion;    // Of what was shown; z = 0 for nothing.
uniform float keep;             // How much of what was shown to keep.

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    vec4 now = texelFetch(built, texel, 0);
    if (previousRegion.z <= 0.0 || keep <= 0.0)
    {
        out_Field = now;
        return;
    }
    vec2 xz = region.xy + (vec2(texel) + 0.5) / vec2(textureSize(built, 0)) * region.zw;
    vec2 uv = (xz - previousRegion.xy) / previousRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0))))
    {
        out_Field = now;
        return;
    }
    out_Field = mix(now, textureLod(previous, uv, 0.0), keep);
}
