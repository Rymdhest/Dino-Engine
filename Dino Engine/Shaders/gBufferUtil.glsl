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

vec3 compressNormal(vec3 normal) {
    return normal*0.5f+0.5f;
}

vec3 unCompressNormal(vec3 normal) {
    return normal * 2.0f - 1.0f;
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
