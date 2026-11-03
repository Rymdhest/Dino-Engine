#version 420

// One step of the ripples things leave in the water (WaterRippleSimulation): the damped 2D wave equation, leapfrog
// in time, on a grid that follows the camera in whole texels (state carried over by texelShift, as Wind_Vegetation.frag
// does). Contacts push the surface down; the edge of the grid absorbs waves instead of reflecting them.
// out_State: x height now (m), y height one step ago, zw slope of the height (one step behind, which never shows)

layout (location = 0) out vec4 out_State;

uniform sampler2D previousState;
uniform vec2 texelShift;
uniform int resetState;
uniform vec4 region;
uniform float courantSquared;
uniform float damping;

uniform int contactCount;
uniform vec4 contacts[32];

vec2 stateAt(ivec2 texel, ivec2 size)
{
    if (resetState == 1 || any(lessThan(texel, ivec2(0))) || any(greaterThanEqual(texel, size))) return vec2(0.0);
    return texelFetch(previousState, texel, 0).xy;
}

void main(void)
{
    ivec2 size = textureSize(previousState, 0);
    ivec2 texel = ivec2(gl_FragCoord.xy);
    ivec2 source = texel + ivec2(round(texelShift));
    float texelMetres = region.z / float(size.x);

    vec2 centre = stateAt(source, size);
    float left = stateAt(source - ivec2(1, 0), size).x;
    float right = stateAt(source + ivec2(1, 0), size).x;
    float down = stateAt(source - ivec2(0, 1), size).x;
    float up = stateAt(source + ivec2(0, 1), size).x;

    float height = centre.x;
    float laplacian = left + right + down + up - 4.0 * height;
    float next = height + (height - centre.y) * damping + courantSquared * laplacian;

    vec2 xz = region.xy + (vec2(texel) + 0.5) * texelMetres;
    for (int i = 0; i < contactCount; i++)
    {
        vec4 contact = contacts[i];
        float r = length(xz - contact.xy) / max(contact.z, 1e-4);
        if (r >= 1.0) continue;
        float falloff = 1.0 - r * r;
        next -= contact.w * falloff * falloff;
    }

    // Absorb at the edge so nothing bounces back off the end of the grid.
    vec2 uv = (vec2(texel) + 0.5) / vec2(size);
    float border = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    float fade = smoothstep(0.0, 0.06, border);
    next *= fade;

    out_State = vec4(next, height * fade, vec2(right - left, up - down) / (2.0 * texelMetres));
}
