vec3 ReconstructViewSpacePosition(vec2 fragCoord, float depth, mat4 invProjection, vec2 resolution)
{
    // Convert fragment coordinate to normalized device coordinates (NDC)
    vec2 ndc;
    ndc.x = (fragCoord.x / resolution.x) * 2.0 - 1.0;
    ndc.y = (fragCoord.y / resolution.y) * 2.0 - 1.0;

    // Depth is in [0, 1], convert to NDC depth in [-1, 1]
    float ndcZ = depth * 2.0 - 1.0;

    // Reconstruct clip-space position
    vec4 clipPos = vec4(ndc.x, ndc.y, ndcZ, 1.0);

    // Transform to view space using the inverse projection matrix
    vec4 viewPos = clipPos*invProjection;

    // Perspective divide
    return viewPos.xyz / viewPos.w;
}

vec3 ReconstructWorldSpacePosition(vec2 fragCoord, float depth, mat4 invProjection, mat4 invView, vec2 resolution)
{
    // Convert fragment coordinate to normalized device coordinates (NDC)
    vec2 ndc;
    ndc.x = (fragCoord.x / resolution.x) * 2.0 - 1.0;
    ndc.y = (fragCoord.y / resolution.y) * 2.0 - 1.0;

    // Convert depth from [0, 1] to NDC [-1, 1]
    float ndcZ = depth * 2.0 - 1.0;

    // Clip-space position
    vec4 clipPos = vec4(ndc, ndcZ, 1.0);

    // View-space position
    vec4 viewPos = invProjection * clipPos;
    viewPos /= viewPos.w;

    // World-space position
    vec4 worldPos = invView * viewPos;

    return worldPos.xyz;
}