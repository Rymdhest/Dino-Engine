#version 430

// Drops landing. Each instance comes up again and again at a new point of the rain shadow's own map, so it lands on whatever the rain really falls on, a roof or a tree or the road, and
// nothing has to know where those are. Each lives a moment, and where it isn't raining it doesn't come up at all.

#include Globals.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Sun_Visibility.glsl

out vec2 fragCoord;     // -1..1 across the ring
flat out vec4 fragLight;
flat out float fragAge;

uniform vec3 focus;
uniform float reach;        // Metres around the focus that splashes are seen.
uniform float splashClock;
uniform float waterLevel;
uniform mat4 rainShadowInverse;

uniform sampler2D sceneBlur;

const float LIFE = 0.32;    // Seconds. SplashPeriod in WeatherSystem is a whole number of these.

uint splashHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float splashRandom(inout uint state)
{
    state = splashHash(state);
    return float(state >> 8u) * (1.0 / 16777216.0);
}

void hide()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    fragCoord = vec2(0.0);
    fragLight = vec4(0.0);
    fragAge = 1.0;
}

void main()
{
    uint state = splashHash(uint(gl_InstanceID) * 2654435761u + 91u);
    float offset = splashRandom(state);
    float cycle = splashClock / LIFE + offset;
    float generation = floor(cycle);
    float age = cycle - generation;

    uint drop = splashHash(state ^ uint(generation) * 2246822519u);
    vec2 around = vec2(splashRandom(drop), splashRandom(drop)) * 2.0 - 1.0;
    float keep = splashRandom(drop);
    float size = 0.14 + 0.2 * splashRandom(drop);

    vec2 centre = (rainShadowMatrix * vec4(focus, 1.0)).xy * 0.5 + 0.5;
    vec2 scale = vec2(length(vec3(rainShadowMatrix[0].x, rainShadowMatrix[1].x, rainShadowMatrix[2].x)),
                      length(vec3(rainShadowMatrix[0].y, rainShadowMatrix[1].y, rainShadowMatrix[2].y)));
    vec2 coord = centre + around * reach * scale * 0.5;
    if (rainShadowInfo.x < 0.5 || rainShadowOutside(coord))
    {
        hide();
        return;
    }

    float depth = textureLod(rainShadowMap, coord, 0.0).r;
    if (depth >= 1.0)
    {
        hide();
        return;
    }
    vec4 landed = rainShadowInverse * vec4(vec3(coord, depth) * 2.0 - 1.0, 1.0);
    vec3 position = landed.xyz / landed.w;

    vec4 weather = weatherSurfaceAt(position.xz);
    float rain = weather.w * (1.0 - weatherKind.x) * (1.0 - weatherKind.y);
    if (keep > rain)
    {
        hide();
        return;
    }

    // Lying on what it hit, or on the sea if that's over it.
    position.y = max(position.y, waterLevel) + 0.06;

    int corners = gl_VertexID & 3;
    vec2 corner = vec2((corners & 1) == 0 ? -1.0 : 1.0, (corners >> 1) == 0 ? -1.0 : 1.0);
    float radius = size * (0.3 + 0.7 * sqrt(age));
    vec3 vertex = position + vec3(corner.x, 0.0, corner.y) * radius;

    vec4 clip = projectionMatrix * viewMatrix * vec4(vertex, 1.0);
    gl_Position = clip;
    fragCoord = corner;
    fragAge = age;

    vec2 screen = clip.xy / clip.w * 0.5 + 0.5;
    vec3 aroundLight = textureLod(sceneBlur, screen, 3.0).rgb;
    float sun = weatherSunVisibility(position.xz);
    fragLight = vec4(skyColour * 1.6 + weatherSunColour.rgb * (sun * 0.12) + aroundLight * 0.5 + weatherFlash.rgb, 1.0);
}
