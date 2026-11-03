#version 420

// Accumulates the jittered half-resolution march over frames. History is reprojected through the
// visible surface, which is accurate here because the fog hugs that surface. It is then clipped to
// the spread of the current neighbourhood (variance clipping) so moving fog and disocclusions don't ghost.

#include Globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Fog;

uniform sampler2D currentFog;
uniform sampler2D currentDistance;
uniform sampler2D historyFog;

uniform mat4 previousViewProjection;
uniform float historyWeight;

vec4 clipToBox(vec4 boxCenter, vec4 boxExtents, vec4 value)
{
    vec4 offset = value - boxCenter;
    vec4 units = abs(offset / max(boxExtents, vec4(1e-4)));
    float maxUnit = max(max(units.x, units.y), max(units.z, units.w));
    return maxUnit > 1.0 ? boxCenter + offset / maxUnit : value;
}

void main(void)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 maxPixel = textureSize(currentFog, 0) - 1;
    vec4 current = texelFetch(currentFog, pixel, 0);

    vec4 mean = vec4(0.0);
    vec4 meanSquared = vec4(0.0);
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        vec4 neighbour = texelFetch(currentFog, clamp(pixel + ivec2(x, y), ivec2(0), maxPixel), 0);
        mean += neighbour;
        meanSquared += neighbour * neighbour;
    }
    mean /= 9.0;
    vec4 deviation = sqrt(max(meanSquared / 9.0 - mean * mean, vec4(0.0)));

    // Reproject the surface point this pixel's fog lies in front of.
    vec2 ndc = textureCoords * 2.0 - 1.0;
    vec4 viewRay = invProjectionMatrix * vec4(ndc, 1.0, 1.0);
    vec3 rayDirection = normalize((invViewMatrix * vec4(viewRay.xyz / viewRay.w, 0.0)).xyz);
    vec3 surface = viewPosWorld + rayDirection * texelFetch(currentDistance, pixel, 0).r;
    vec4 previousClip = previousViewProjection * vec4(surface, 1.0);
    vec2 previousUV = previousClip.xy / previousClip.w * 0.5 + 0.5;

    float weight = historyWeight;
    if (previousClip.w <= 0.0 || any(lessThan(previousUV, vec2(0.0))) || any(greaterThan(previousUV, vec2(1.0))))
    {
        weight = 0.0;
    }

    // Branch rather than mix with weight 0: a never-written history may hold NaNs.
    if (weight <= 0.0)
    {
        out_Fog = current;
        return;
    }

    vec4 history = textureLod(historyFog, previousUV, 0.0);
    history = clipToBox(mean, deviation * 1.25 + vec4(0.002), history);

    out_Fog = mix(current, history, weight);
}
