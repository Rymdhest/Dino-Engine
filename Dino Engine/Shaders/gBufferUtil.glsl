vec3 ReconstructViewSpacePosition(vec2 fragCoord, float depth, mat4 invProjection, vec2 resolution)
{
    // Convert fragment coordinate to normalized device coordinates (NDC)
    vec2 ndc;
    ndc.x = (fragCoord.x / resolution.x) * 2.0 - 1.0;
    ndc.y = (fragCoord.y / resolution.y) * 2.0 - 1.0;

    // Depth is in [0, 1], convert to NDC depth in [-1, 1]
    float ndcZ = depth * 2.0 - 1.0;

    // Reconstruct clip-space position
    vec4 clipPos = vec4(ndc.x, ndc.y, ndcZ,1.0);

    // Transform to view space using the inverse projection matrix
    vec4 viewPos = invProjection* clipPos;

    // Perspective divide
    return viewPos.xyz / viewPos.w;
}

// The z of ReconstructViewSpacePosition, for the many taps of a pass that needs nothing else. A perspective projection's
// depth doesn't depend on x and y.
float ReconstructViewSpaceZ(float depth, mat4 invProjection)
{
    float ndcZ = depth * 2.0 - 1.0;
    return (invProjection[2][2] * ndcZ + invProjection[3][2]) / (invProjection[2][3] * ndcZ + invProjection[3][3]);
}

vec3 ReconstructWorldSpacePosition(vec2 fragCoord, float depth, mat4 invProjection, mat4 invView, vec2 resolution)
{
    vec3 viewPos = ReconstructViewSpacePosition(fragCoord, depth, invProjection, resolution);
    vec4 worldPos = invView * vec4(viewPos, 1.0);
    return worldPos.xyz;
}

// What a thin surface lets through to its far side: its translucency, less what's absorbed crossing its thickness, 0 to 1. Worked out where the surface is drawn,
// as the channel a thickness once had now holds what a relief lets of the sun (gMaterials.a, Surface/Relief.glsl). It's the amount a sheet or a canopy scatters
// (Lighting/SubsurfaceKinds.glsl, which gAlbedo.a is written with).
float transmittance(float translucency, float thickness) {
    return translucency / (pow(thickness * 2.0, 3.0) + 1.0);
}

#include Lighting/SubsurfaceKinds.glsl

// A unit normal as the G-buffer keeps it, in its three 8-bit channels: folded onto an octahedron (Cigolle et al., "A Survey of Efficient Representations for Independent
// Unit Vectors", 2014), its two coordinates 12 bits each, split 8, 4 and 4, 8 across the channels. Stored as x, y and z it was a step of half a degree, which a wet stone's or
// a blade's highlight a few degrees wide showed as contours and a reflection ray as steps; this is a twentieth of a degree for the same bytes. Nothing may filter or blend
// it, as a channel's bits only mean anything whole: read it with texelFetch, and the MSAA resolve averages what it decodes (Msaa_Resolve_GBuffer.frag).
const float NORMAL_STEPS = 4095.0;

vec3 compressNormal(vec3 normal) {
    vec3 n = normal / max(abs(normal.x) + abs(normal.y) + abs(normal.z), 1e-6);
    vec2 folded = n.z >= 0.0 ? n.xy : (1.0 - abs(n.yx)) * vec2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    uvec2 q = uvec2(round(clamp(folded * 0.5 + 0.5, 0.0, 1.0) * NORMAL_STEPS));
    return vec3(float(q.x >> 4u), float(((q.x & 15u) << 4u) | (q.y >> 8u)), float(q.y & 255u)) / 255.0;
}

vec3 unCompressNormal(vec3 stored) {
    uvec3 b = uvec3(round(clamp(stored, 0.0, 1.0) * 255.0));
    vec2 folded = vec2(uvec2((b.x << 4u) | (b.y >> 4u), ((b.y & 15u) << 8u) | b.z)) / NORMAL_STEPS * 2.0 - 1.0;
    vec3 n = vec3(folded, 1.0 - abs(folded.x) - abs(folded.y));
    float under = max(-n.z, 0.0);
    n.xy += vec2(n.x >= 0.0 ? -under : under, n.y >= 0.0 ? -under : under);
    return normalize(n);
}

// Emission as the G-buffer holds it. A surface shines at its albedo times this, up to MAX_EMISSION; the 8 bits of the
// channel are linear in it. EMISSION_GAIN is how bright a material of emission 1 and colour 1 is.
const float MAX_EMISSION = 32.0;
const float EMISSION_GAIN = 4.0;

float encodeEmission(float strength) {
    return strength / MAX_EMISSION;
}

vec3 decodeEmission(vec3 albedo, float stored) {
    return albedo * (stored * MAX_EMISSION);
}
