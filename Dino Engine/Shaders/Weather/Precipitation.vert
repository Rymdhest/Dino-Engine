#version 430

// What falls through the air, with no vertex data and nothing simulated. Each instance is a point of a box of air around the view, drawn from its own number, moved by the running totals
// WeatherSystem keeps, and wrapped at the box's edges, so what falls stays where it is in the world while the box moves through it, as rain does past a walking man. Where it's raining
// here decides which of them are there at all, so a front has an edge, and the rain shadow puts out whatever would be falling under a roof or a tree.

#include Globals.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Sun_Visibility.glsl

out vec2 fragCoord;     // x across it, -1..1; y along it, 0..1
flat out vec4 fragLight;
flat out int fragRound;

uniform int fallKind;           // 0 streaks of rain, 1 flakes
uniform vec3 volumeCentre;
uniform float volumeSize;
uniform float fallen;           // Metres fallen, for the slowest speed of eight: see speedShare.
uniform vec2 drifted;           // Metres the air has carried it.
uniform vec2 windVelocity;
uniform float fallSpeed;        // Metres per second.
uniform float exposure;         // Seconds of its fall the eye holds as a streak.
uniform float width;            // Metres.
uniform float opacity;
uniform vec3 tint;
uniform float share;            // How much of what's falling is this.

uniform sampler2D sceneBlur;

const float MIN_PIXELS = 1.25;
const float EDGE_FADE = 5.0;    // Metres in from the box's edges that things fade in, so none is seen arriving.
const vec2 NEAR_FADE = vec2(1.5, 5.0);          // Metres from the eye that a streak fades out, as there it would be a line across the screen.
const vec2 NEAR_FADE_FLAKE = vec2(8.0, 18.0);   // And a flake, which would be a blob over whatever's being looked at.

uint fallHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float fallRandom(inout uint state)
{
    state = fallHash(state);
    return float(state >> 8u) * (1.0 / 16777216.0);
}

void hide()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    fragCoord = vec2(0.0);
    fragLight = vec4(0.0);
    fragRound = 0;
}

void main()
{
    uint state = fallHash(uint(gl_InstanceID) * 2654435761u + uint(fallKind) * 40503u + 17u);
    vec3 base = vec3(fallRandom(state), fallRandom(state), fallRandom(state)) * volumeSize;
    // Eighths, so that a total wrapped at eight volumes moves nothing (WeatherSystem).
    float speedShare = float(6u + (state & 3u)) * 0.125;
    float keep = fallRandom(state);
    float spin = fallRandom(state) * 6.2831853;

    vec3 corner = volumeCentre - vec3(volumeSize * 0.5);
    vec3 position = base + vec3(drifted.x, -fallen * speedShare, drifted.y);
    if (fallKind == 1)
    {
        // Flakes wander as they fall.
        float wander = fallen * 0.35 * speedShare + spin;
        position.xz += vec2(sin(wander), cos(wander * 0.8)) * 0.6;
    }
    position = corner + mod(position - corner, vec3(volumeSize));

    if (keep > weatherSurfaceAt(position.xz).w * share)
    {
        hide();
        return;
    }

    vec3 velocity = vec3(windVelocity.x, -fallSpeed * speedShare, windVelocity.y);
    vec3 end = position + velocity * exposure;
    if (rainShadowInfo.x > 0.5 && rainShadowCovers(end))
    {
        hide();
        return;
    }

    vec4 clipStart = projectionMatrix * viewMatrix * vec4(position, 1.0);
    vec4 clipEnd = projectionMatrix * viewMatrix * vec4(end, 1.0);
    if (clipStart.w < 0.4 || clipEnd.w < 0.4)
    {
        hide();
        return;
    }

    int corners = gl_VertexID & 3;
    float across = (corners & 1) == 0 ? -1.0 : 1.0;
    float along = (corners >> 1) == 0 ? 0.0 : 1.0;

    float pixelsPerMetre = 0.5 * resolution.y * projectionMatrix[1][1] / clipStart.w;
    float pixels = width * pixelsPerMetre;
    float drawn = max(pixels, MIN_PIXELS);
    float thinned = min(pixels / MIN_PIXELS, 1.0);

    vec2 ndcStart = clipStart.xy / clipStart.w;
    vec2 ndcEnd = clipEnd.xy / clipEnd.w;
    vec2 pixelLine = (ndcEnd - ndcStart) * resolution * 0.5;
    float lineLength = length(pixelLine);

    vec2 offset;
    bool round = fallKind == 1 || lineLength < drawn;
    if (round)
    {
        // Too short to be a streak: a soft dot, as a drop falling straight away from the eye is.
        offset = vec2(across, along * 2.0 - 1.0) * drawn;
        fragCoord = vec2(across, along * 2.0 - 1.0);
        fragRound = 1;
    }
    else
    {
        vec2 direction = pixelLine / lineLength;
        offset = vec2(-direction.y, direction.x) * (across * drawn * 0.5) + pixelLine * along;
        fragCoord = vec2(across, along);
        fragRound = 0;
    }

    vec4 clip = clipStart;
    clip.xy += offset / (resolution * 0.5) * clip.w;
    gl_Position = clip;

    // It picks up the light of whatever's around it, as a drop does: the sky, the sun through the clouds, and any fire it's falling past.
    vec2 screen = ndcStart * 0.5 + 0.5;
    vec3 around = textureLod(sceneBlur, screen, 4.0).rgb;
    float sun = weatherSunVisibility(position.xz);
    vec3 light = skyColour * 1.3 + weatherSunColour.rgb * (sun * 0.1) + around * 0.55 + weatherFlash.rgb;

    vec3 fromEdge = min(position - corner, corner + vec3(volumeSize) - position);
    float fade = smoothstep(0.0, EDGE_FADE, min(fromEdge.x, min(fromEdge.y, fromEdge.z)));
    vec2 nearFade = fallKind == 1 ? NEAR_FADE_FLAKE : NEAR_FADE;
    float near = smoothstep(nearFade.x, nearFade.y, clipStart.w);
    fragLight = vec4(light * tint, opacity * thinned * fade * near);
}
