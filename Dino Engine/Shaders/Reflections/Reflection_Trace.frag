#version 420

// One reflected ray for each pixel of a lower-resolution grid. Its direction is drawn from the surface's own GGX lobe, from the normals that lobe can see (Heitz 2018), so that a rough surface
// spreads its rays exactly as it spreads light and a smooth one keeps them all together, and the walk through the depth pyramid is FidelityFX SSSR's: as long as the ray stays in front of a tile
// it moves up a level and skips it, and it comes down a level whenever it would pass behind one. It leaves what it found for Reflection_Resolve.frag, as where it landed and how far off that
// was, and how likely this ray was among all those the surface might have sent, which is what lets a neighbour use it too.

#include Globals.glsl
#include gBufferUtil.glsl
#include Reflections/Reflection.glsl

layout (location = 0) out vec4 out_Ray;         // xy where it landed (0..1), z metres it travelled, w probability density of its direction
layout (location = 1) out float out_Confidence; // How much to believe it, 0..1: 0 is a ray that found nothing.

in vec2 textureCoords;

uniform sampler2D depthPyramid;
uniform sampler2D gNormal;
uniform sampler2D gMaterials;
uniform sampler2D gAlbedo;

uniform int frameIndex;
uniform int maxSteps;
uniform int topLevel;
uniform float thicknessBase;
uniform float thicknessPerMetre;

const float BIG = 3.0e38;
// Trimming the tail of the lobe costs a little of its widest spread, and takes out most of the noise (Stachowiak's bias).
const float LOBE_TRIM = 0.82;

vec3 sampleLobe(vec3 view, float alpha, vec2 u)
{
    vec3 stretched = normalize(vec3(alpha * view.xy, view.z));
    float lengthSquared = dot(stretched.xy, stretched.xy);
    vec3 t1 = lengthSquared > 1e-7 ? vec3(-stretched.y, stretched.x, 0.0) * inversesqrt(lengthSquared) : vec3(1.0, 0.0, 0.0);
    vec3 t2 = cross(stretched, t1);

    float r = sqrt(u.x);
    float phi = 2.0 * REFLECT_PI * u.y;
    float a = r * cos(phi);
    float b = r * sin(phi);
    float s = 0.5 * (1.0 + stretched.z);
    b = (1.0 - s) * sqrt(max(1.0 - a * a, 0.0)) + s * b;

    vec3 normal = a * t1 + b * t2 + sqrt(max(1.0 - a * a - b * b, 0.0)) * stretched;
    return normalize(vec3(alpha * normal.xy, max(normal.z, 1e-5)));
}

vec3 toScreen(vec3 view)
{
    vec4 clip = projectionMatrix * vec4(view, 1.0);
    return clip.xyz / clip.w * 0.5 + 0.5;
}

vec3 fromScreen(vec3 screen)
{
    vec4 view = invProjectionMatrix * vec4(screen * 2.0 - 1.0, 1.0);
    return view.xyz / view.w;
}

// Walks the pyramid from `origin` along `direction`, both in screen space (uv and depth). Returns where it stopped; `stopped` says whether it reached anything rather than running out of steps.
vec3 walk(vec3 origin, vec3 direction, out bool stopped)
{
    vec3 inverse = vec3(direction.x != 0.0 ? 1.0 / direction.x : BIG, direction.y != 0.0 ? 1.0 / direction.y : BIG, direction.z != 0.0 ? 1.0 / direction.z : BIG);

    int level = 0;
    vec2 size = vec2(textureSize(depthPyramid, 0));
    vec2 nudge = 0.005 / resolution * vec2(direction.x < 0.0 ? -1.0 : 1.0, direction.y < 0.0 ? -1.0 : 1.0);
    vec2 edge = vec2(direction.x < 0.0 ? 0.0 : 1.0, direction.y < 0.0 ? 0.0 : 1.0);

    // First out of the pixel it starts in, or it would find itself.
    vec2 plane = (floor(size * origin.xy) + edge) / size + nudge;
    vec2 first = plane * inverse.xy - origin.xy * inverse.xy;
    float t = min(first.x, first.y);
    vec3 position = origin + t * direction;

    int steps = 0;
    while (steps < maxSteps && level >= 0)
    {
        steps++;
        if (any(lessThan(position.xy, vec2(0.0))) || any(greaterThanEqual(position.xy, vec2(1.0))) || position.z <= 0.0 || position.z >= 1.0)
        {
            stopped = false;
            return position;
        }

        vec2 texel = size * position.xy;
        float surface = texelFetch(depthPyramid, ivec2(texel), level).r;

        vec2 cellEdge = (floor(texel) + edge) / size + nudge;
        vec3 crossing = vec3(cellEdge, surface) * inverse - origin * inverse;
        // The surface only stops a ray going away from the eye: going towards it, it can only leave.
        if (direction.z <= 0.0) crossing.z = BIG;
        float nearest = min(min(crossing.x, crossing.y), crossing.z);

        bool above = surface > position.z;
        bool skipped = above && nearest != crossing.z;
        if (above) t = nearest;
        position = origin + t * direction;

        level = skipped ? min(level + 1, topLevel) : level - 1;
        size = vec2(textureSize(depthPyramid, max(level, 0)));
    }
    stopped = level < 0;
    return position;
}

// How much to trust where a ray stopped. A ray that goes behind something stops there too, and only the difference in depth tells it from one that really struck it.
float judge(vec3 hit, vec3 hitView, vec2 startUV, vec3 rayView)
{
    if (any(lessThan(hit.xy, vec2(0.0))) || any(greaterThanEqual(hit.xy, vec2(1.0)))) return 0.0;
    if (all(lessThan(abs(hit.xy - startUV), 1.5 / resolution))) return 0.0;

    ivec2 pixel = ivec2(hit.xy * resolution);
    float surfaceDepth = texelFetch(depthPyramid, pixel, 0).r;
    if (surfaceDepth >= 1.0) return 0.0;

    // Struck from behind: the ray came through something it can't have.
    vec3 hitNormal = unCompressNormal(texelFetch(gNormal, pixel, 0).xyz);
    if (dot(hitNormal, rayView) > 0.25) return 0.0;

    vec3 surfaceView = fromScreen(vec3(hit.xy, surfaceDepth));
    float gap = length(surfaceView - hitView);
    float thickness = thicknessBase + thicknessPerMetre * -surfaceView.z;
    float close = 1.0 - smoothstep(0.0, thickness, gap);

    vec2 fringe = vec2(0.06 * resolution.y / resolution.x, 0.06);
    vec2 border = smoothstep(vec2(0.0), fringe, hit.xy) * (1.0 - smoothstep(1.0 - fringe, vec2(1.0), hit.xy));
    return close * close * border.x * border.y;
}

void main(void)
{
    out_Ray = vec4(0.0);
    out_Confidence = 0.0;

    // Each of the pixels this one stands for gets a turn, so the whole picture is covered in a few frames.
    int turn = frameIndex & 3;
    ivec2 cell = ivec2(gl_FragCoord.xy);
    ivec2 pixel = min(cell * 2 + ivec2(turn & 1, (turn >> 1) ^ (turn & 1)), ivec2(resolution) - 1);

    float depth = texelFetch(depthPyramid, pixel, 0).r;
    if (depth >= 1.0) return;

    vec4 materials = texelFetch(gMaterials, pixel, 0);
    if (!reflectionWanted(materials.r, texelFetch(gAlbedo, pixel, 0).a)) return;
    float roughness = max(materials.r, 0.03);

    vec2 uv = (vec2(pixel) + 0.5) / resolution;
    vec3 position = fromScreen(vec3(uv, depth));
    vec3 N = normalize(unCompressNormal(texelFetch(gNormal, pixel, 0).xyz));
    vec3 V = normalize(-position);
    float NdotV = max(dot(N, V), 1e-3);

    // A frame around the normal, for the lobe.
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);

    float alpha = roughness * roughness;
    float alpha2 = alpha * alpha;
    vec3 H = N;
    float pdf = 1e4;
    if (roughness > REFLECT_MIRROR_ROUGHNESS)
    {
        vec2 u = reflectionNoise(cell, frameIndex);
        u.x *= LOBE_TRIM;
        vec3 localView = vec3(dot(V, tangent), dot(V, bitangent), dot(V, N));
        vec3 localH = sampleLobe(localView, alpha, u);
        H = normalize(tangent * localH.x + bitangent * localH.y + N * localH.z);
        pdf = reflectionD(max(dot(N, H), 0.0), alpha2) * reflectionG1(NdotV, alpha2) / (4.0 * NdotV);
    }

    vec3 R = reflect(-V, H);
    if (dot(R, N) <= 1e-3) return;

    // A little off the surface: depth here is what parallax and rounding left it at.
    vec3 start = position + N * (0.02 + 0.0015 * -position.z);
    vec3 end = start + R;
    if (end.z > -0.1) end = start + R * ((-0.1 - start.z) / max(R.z, 1e-4));
    vec3 screenStart = toScreen(start);
    vec3 screenDirection = toScreen(end) - screenStart;

    bool stopped;
    vec3 hit = walk(screenStart, screenDirection, stopped);
    vec3 hitView = fromScreen(hit);
    float confidence = stopped ? judge(hit, hitView, uv, R) : 0.0;

    out_Ray = vec4(hit.xy, length(hitView - position), max(pdf, 1e-4));
    out_Confidence = confidence;
}
