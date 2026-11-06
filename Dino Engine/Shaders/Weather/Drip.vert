#version 430

// A drop coming off something, drawn in the air after the fog: it swells at the edge, lets go, falls faster and faster, and lands. Each instance is one place that drips (Weather/Drip.glsl).
// One draw for the drops and one for what they do when they land, as a ring can outlast the turn it landed in.

#include Globals.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/Drip.glsl
#include Weather/Sun_Visibility.glsl

out vec2 fragCoord;     // x across it, -1..1; y along it, 0..1, or -1..1 where it's round
flat out vec4 fragLight;
flat out int fragKind;  // 0 a falling streak, 1 a bead, 2 a ring
flat out float fragAge;

uniform int dripStage;  // 0 what's falling, 1 what has landed
uniform vec2 windVelocity;
uniform float opacity;

uniform sampler2D sceneBlur;

const float EXPOSURE = 0.04;    // Seconds of its fall the eye holds as a streak.
const float WIDTH = 0.02;       // Metres.
const float BEAD_SIZE = 0.03;   // Metres.
const float MIN_PIXELS = 1.25;

void hide()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    fragCoord = vec2(0.0);
    fragLight = vec4(0.0);
    fragKind = 0;
    fragAge = 1.0;
}

vec3 dropAt(in DripSource source, float seconds)
{
    vec2 carried = windVelocity * (DRIP_LEAN * seconds * seconds);
    return vec3(source.at.x + carried.x, source.top - 0.5 * DRIP_GRAVITY * seconds * seconds, source.at.y + carried.y);
}

// It picks up the light around it, as rain does (Precipitation.vert), and being a fat drop it catches more of it.
vec3 lightOn(vec3 position, vec2 screen)
{
    vec3 around = textureLod(sceneBlur, screen * 0.5 + 0.5, 3.0).rgb;
    return skyColour * 1.6 + weatherSunColour.rgb * (weatherSunVisibility(position.xz) * 0.12) + around * 0.6 + weatherFlash.rgb;
}

void flatRing(in DripSource source, float age, uint drop)
{
    float size = 0.3 + 0.2 * dripRandom(drop);
    float life = age / DRIP_RING_LIFE;
    vec3 position = dropAt(source, source.falling);
    position.y = source.landing + 0.05;

    int corners = gl_VertexID & 3;
    vec2 corner = vec2((corners & 1) == 0 ? -1.0 : 1.0, (corners >> 1) == 0 ? -1.0 : 1.0);
    vec4 clip = projectionMatrix * viewMatrix * vec4(position + vec3(corner.x, 0.0, corner.y) * (size * (0.25 + 0.75 * sqrt(life))), 1.0);
    gl_Position = clip;
    fragCoord = corner;
    fragKind = 2;
    fragAge = life;
    fragLight = vec4(lightOn(position, clip.xy / clip.w), opacity);
}

void main()
{
    DripSource source;
    if (!readDripSource(gl_InstanceID, source))
    {
        hide();
        return;
    }

    float age;
    uint drop;
    if (dripStage == 1)
    {
        if (!dripTurn(source, DRIP_HANG + source.falling, age, drop) || age >= DRIP_RING_LIFE) hide();
        else flatRing(source, age, drop);
        return;
    }

    if (!dripTurn(source, 0.0, age, drop) || age >= DRIP_HANG + source.falling)
    {
        hide();
        return;
    }

    float moving = age - DRIP_HANG;
    vec3 end = dropAt(source, max(moving, 0.0));
    vec3 start = dropAt(source, max(moving - EXPOSURE, 0.0));

    vec4 clipStart = projectionMatrix * viewMatrix * vec4(start, 1.0);
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
    vec2 ndcStart = clipStart.xy / clipStart.w;
    vec2 pixelLine = (clipEnd.xy / clipEnd.w - ndcStart) * resolution * 0.5;
    float lineLength = length(pixelLine);

    vec2 offset;
    float strength;
    float drawn = max(WIDTH * pixelsPerMetre, MIN_PIXELS);
    if (moving <= 0.0 || lineLength < drawn)
    {
        // Still on the edge, or seen end on: a bead, which grows until it's too heavy to hold.
        float swollen = moving <= 0.0 ? smoothstep(0.0, DRIP_HANG, age) : 1.0;
        float size = max(BEAD_SIZE * pixelsPerMetre * swollen, MIN_PIXELS);
        offset = vec2(across, along * 2.0 - 1.0) * size;
        fragCoord = vec2(across, along * 2.0 - 1.0);
        fragKind = 1;
        strength = swollen;
    }
    else
    {
        vec2 direction = pixelLine / lineLength;
        offset = vec2(-direction.y, direction.x) * (across * drawn * 0.5) + pixelLine * along;
        fragCoord = vec2(across, along);
        fragKind = 0;
        strength = min(WIDTH * pixelsPerMetre / MIN_PIXELS, 1.0);
    }

    vec4 clip = clipStart;
    clip.xy += offset / (resolution * 0.5) * clip.w;
    gl_Position = clip;
    fragAge = 0.0;
    fragLight = vec4(lightOn(end, ndcStart), opacity * strength);
}
