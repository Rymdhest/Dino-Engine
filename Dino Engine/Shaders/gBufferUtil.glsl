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