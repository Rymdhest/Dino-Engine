#version 420

// Holds a reflection steady from one frame to the next. Where a surface's own point was a frame ago is easy, but what's seen in a mirror moves as if it stood behind the mirror, so the sharper
// the surface, the more the history is looked up from there (Stachowiak's hit-based reprojection). Whatever the history holds is then kept within what's around this pixel now, so nothing can
// leave a ghost, and a place that has only just come into view keeps nothing at all. Mirrors have no noise to average away, and take little from the past.

#include Globals.glsl
#include gBufferUtil.glsl
#include Reflections/Reflection.glsl

layout (location = 0) out vec4 out_History;
layout (location = 1) out float out_Distance;   // Metres from the eye of the surface this pixel shows, for the next frame's check and the composite's upsample.

uniform sampler2D current;
uniform sampler2D previous;
uniform sampler2D previousDistance;
uniform sampler2D rays;
uniform sampler2D depthPyramid;
uniform sampler2D gMaterials;

uniform mat4 previousViewProjection;
uniform float historyWeight;    // 0 when there's no history to use.

vec3 fromScreen(vec3 screen)
{
    vec4 view = invProjectionMatrix * vec4(screen * 2.0 - 1.0, 1.0);
    return view.xyz / view.w;
}

vec2 toPrevious(vec3 world)
{
    vec4 clip = previousViewProjection * vec4(world, 1.0);
    return clip.xy / clip.w * 0.5 + 0.5;
}

void main(void)
{
    ivec2 cell = ivec2(gl_FragCoord.xy);
    ivec2 size = textureSize(current, 0);
    ivec2 pixel = min(cell * 2, ivec2(resolution) - 1);

    float depth = texelFetch(depthPyramid, pixel, 0).r;
    vec2 uv = (vec2(pixel) + 0.5) / resolution;
    vec3 viewPosition = fromScreen(vec3(uv, depth));
    float distanceNow = length(viewPosition);
    out_Distance = distanceNow;

    vec4 now = texelFetch(current, cell, 0);
    float roughness = texelFetch(gMaterials, pixel, 0).r;
    // Nothing shot rays here, and its neighbours aren't worth looking at for what it doesn't show.
    if (depth >= 1.0 || roughness >= REFLECT_MAX_ROUGHNESS)
    {
        out_History = vec4(0.0);
        return;
    }

    // What's around this pixel now, as a mean and a spread: history outside of it is stale.
    vec4 mean = vec4(0.0);
    vec4 meanSquare = vec4(0.0);
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec4 tap = texelFetch(current, clamp(cell + ivec2(x, y), ivec2(0), size - 1), 0);
            mean += tap;
            meanSquare += tap * tap;
        }
    }
    mean *= 1.0 / 9.0;
    vec4 spread = sqrt(max(meanSquare * (1.0 / 9.0) - mean * mean, vec4(0.0)));

    vec3 world = (invViewMatrix * vec4(viewPosition, 1.0)).xyz;
    float travelled = texelFetch(rays, cell, 0).z;
    vec3 eyeRay = normalize(world - viewPosWorld);
    vec3 mirrored = world + eyeRay * travelled;
    float surfaceShare = smoothstep(REFLECT_MIRROR_ROUGHNESS, 0.45, roughness);
    vec2 historyUV = mix(toPrevious(mirrored), toPrevious(world), surfaceShare);

    float keep = historyWeight;
    if (any(lessThan(historyUV, vec2(0.0))) || any(greaterThan(historyUV, vec2(1.0)))) keep = 0.0;

    // Something else stands here now.
    vec2 surfaceUV = toPrevious(world);
    float then = textureLod(previousDistance, surfaceUV, 0.0).r;
    float expected = length(world - viewPosWorld);
    if (abs(then - expected) > 0.04 * expected + 0.2) keep = 0.0;

    vec4 history = textureLod(previous, historyUV, 0.0);
    float box = mix(1.0, 2.5, surfaceShare);
    history = clamp(history, mean - spread * box, mean + spread * box);

    keep *= mix(0.62, 1.0, surfaceShare);
    out_History = mix(now, history, keep);
}
