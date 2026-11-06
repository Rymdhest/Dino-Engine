#version 430

// A piece of a bolt, turned to face the eye. Each instance is one straight run of the channel, from a to b.

#include Globals.glsl

layout(location = 0) in vec4 start;     // xyz one end, w metres wide
layout(location = 1) in vec4 end;       // xyz the other, w how bright it is, 0..1

out float fragAcross;
flat out float fragStrength;

const float MIN_PIXELS = 3.0;
const float MAX_PIXELS = 15.0;  // A bolt passing right by the eye is no thicker for it: what gets wider is the glow, and bloom does that.

void main()
{
    int corners = gl_VertexID & 3;
    float across = (corners & 1) == 0 ? -1.0 : 1.0;
    bool far = (corners >> 1) != 0;

    vec4 clipStart = projectionMatrix * viewMatrix * vec4(start.xyz, 1.0);
    vec4 clipEnd = projectionMatrix * viewMatrix * vec4(end.xyz, 1.0);
    // Nothing behind the eye, and a bolt can well run over its head.
    if (clipStart.w < 0.5 && clipEnd.w < 0.5)
    {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        fragAcross = 0.0;
        fragStrength = 0.0;
        return;
    }
    if (clipStart.w < 0.5) clipStart = mix(clipEnd, clipStart, (clipEnd.w - 0.5) / max(clipEnd.w - clipStart.w, 1e-4));
    if (clipEnd.w < 0.5) clipEnd = mix(clipStart, clipEnd, (clipStart.w - 0.5) / max(clipStart.w - clipEnd.w, 1e-4));

    vec2 screenStart = clipStart.xy / clipStart.w * resolution * 0.5;
    vec2 screenEnd = clipEnd.xy / clipEnd.w * resolution * 0.5;
    vec2 line = screenEnd - screenStart;
    float lineLength = max(length(line), 1e-3);
    vec2 side = vec2(-line.y, line.x) / lineLength;

    vec4 clip = far ? clipEnd : clipStart;
    float pixelsPerMetre = 0.5 * resolution.y * projectionMatrix[1][1] / clip.w;
    float pixels = clamp(start.w * pixelsPerMetre, MIN_PIXELS, MAX_PIXELS);
    clip.xy += side * (across * pixels) / (resolution * 0.5) * clip.w;

    gl_Position = clip;
    fragAcross = across;
    fragStrength = end.w;
}
