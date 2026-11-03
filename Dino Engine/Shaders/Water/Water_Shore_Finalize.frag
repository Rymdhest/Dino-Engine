#version 420

// Turns the jump-flooded seeds into a signed distance to the shoreline in metres: positive at sea (distance to the
// nearest land), negative on land (distance to the nearest sea). Unknown texels count as sea, so nothing appears to
// lie beyond the edge of the loaded terrain.
// out_Field: x signed distance (m), y depth below sea level (m) times z, z 1 if known (the blur normalises by it), w unused

layout (location = 0) out vec4 out_Field;

uniform sampler2D seeds;
uniform sampler2D info;
uniform float texelMetres;
uniform float maxDistance;

void main(void)
{
    ivec2 texel = ivec2(gl_FragCoord.xy);
    vec4 nearest = texelFetch(seeds, texel, 0);
    vec4 here = texelFetch(info, texel, 0);
    vec2 self = vec2(texel);

    float distance;
    if (here.y > 0.5)
    {
        // Half a texel less: the shoreline runs between the last sea texel and the first land texel.
        distance = nearest.x >= 0.0 ? max(length(nearest.xy - self) - 0.5, 0.0) * texelMetres : maxDistance;
    }
    else
    {
        distance = nearest.z >= 0.0 ? -max(length(nearest.zw - self) - 0.5, 0.0) * texelMetres : -maxDistance;
    }
    out_Field = vec4(clamp(distance, -maxDistance, maxDistance), here.x * here.z, here.z, 0.0);
}
