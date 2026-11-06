#ifndef DINO_WEATHER_RAIN_SHADOW_GLSL
#define DINO_WEATHER_RAIN_SHADOW_GLSL

// The world seen from the way the rain falls, as depth (WeatherRenderer): what's under something stays dry, what falls stops on what's on top, and a reflection knows what's over its head even
// when that's off the screen. Needs Weather/Weather.glsl.

uniform sampler2D rainShadowMap;

const float SHELTER_BIAS = 0.35;    // Metres a point must lie below what's above it to count as under it, or everything hides from the rain behind itself.

vec3 rainShadowCoord(vec3 worldPosition)
{
    return (rainShadowMatrix * vec4(worldPosition, 1.0)).xyz * 0.5 + 0.5;
}

bool rainShadowOutside(vec2 coord)
{
    return any(lessThan(coord, vec2(0.002))) || any(greaterThan(coord, vec2(0.998)));
}

// Whether something stands over this point.
bool rainShadowCovers(vec3 worldPosition)
{
    vec3 coord = rainShadowCoord(worldPosition);
    if (rainShadowOutside(coord.xy)) return false;
    return textureLod(rainShadowMap, coord.xy, 0.0).r < coord.z - SHELTER_BIAS / rainShadowInfo.y;
}

// How much of a place in the map is under something, weighing the four texels around it as a bilinear filter would, as a depth test can't be filtered any other way.
float rainShadowCoverAt(vec2 coord, float limit)
{
    vec2 size = vec2(textureSize(rainShadowMap, 0));
    vec2 place = coord * size - 0.5;
    vec2 fraction = fract(place);
    vec4 depths = textureGather(rainShadowMap, (floor(place) + 1.0) / size, 0);
    vec4 covered = step(depths, vec4(limit));
    return mix(mix(covered.w, covered.z, fraction.x), mix(covered.x, covered.y, fraction.x), fraction.y);
}

// The same, from 0 to 1, for what shows its edges.
float rainShadowCover(vec3 worldPosition)
{
    vec3 coord = rainShadowCoord(worldPosition);
    if (rainShadowOutside(coord.xy)) return 0.0;
    return rainShadowCoverAt(coord.xy, coord.z - SHELTER_BIAS / rainShadowInfo.y);
}

// How much of the sky's rain reaches this point, 0..1. Softened over about a metre, as water blows in under an edge and drips off it.
float rainReach(vec3 worldPosition)
{
    if (rainShadowInfo.x < 0.5) return 1.0;
    vec3 coord = rainShadowCoord(worldPosition);
    if (rainShadowOutside(coord.xy)) return 1.0;

    float limit = coord.z - SHELTER_BIAS / rainShadowInfo.y;
    float texel = 3.5 / float(textureSize(rainShadowMap, 0).x);
    float covered = rainShadowCoverAt(coord.xy, limit);
    covered += rainShadowCoverAt(coord.xy + vec2(texel, 0.0), limit);
    covered += rainShadowCoverAt(coord.xy - vec2(texel, 0.0), limit);
    covered += rainShadowCoverAt(coord.xy + vec2(0.0, texel), limit);
    covered += rainShadowCoverAt(coord.xy - vec2(0.0, texel), limit);
    return 1.0 - covered * 0.2;
}

#endif
