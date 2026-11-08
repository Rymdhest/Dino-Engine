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

// How a surface's own depth in the map changes with a step across it, per unit of the map's coordinates. The rain comes in at a slant whenever there's wind, and a slope it falls across is
// deeper in the map on one side than the other: comparing every texel near a point with the point's own depth then has the ground hiding from the rain behind itself, in stripes. So each
// texel is weighed against the surface's plane where that texel is (a receiver plane depth bias). The map is orthographic, so the rows of its matrix are its axes in the world.
vec2 rainShadowSlope(vec3 normal)
{
    vec3 across = vec3(rainShadowMatrix[0].x, rainShadowMatrix[1].x, rainShadowMatrix[2].x);
    vec3 down = vec3(rainShadowMatrix[0].y, rainShadowMatrix[1].y, rainShadowMatrix[2].y);
    vec3 along = vec3(rainShadowMatrix[0].z, rainShadowMatrix[1].z, rainShadowMatrix[2].z);
    // What the rain only grazes would take its plane from metres off: past that it's left to the bias.
    float facing = min(dot(normal, normalize(along)), -0.15);
    vec2 sideways = vec2(dot(normal, across) / dot(across, across), dot(normal, down) / dot(down, down));
    return -sideways * (length(along) / facing);
}

// How much of a place in the map is under something, weighing the four texels around it as a bilinear filter would, as a depth test can't be filtered any other way. `slope` is the
// surface's from rainShadowSlope, or nothing for a point in the air.
float rainShadowCoverAt(vec2 coord, float limit, vec2 slope)
{
    vec2 size = vec2(textureSize(rainShadowMap, 0));
    vec2 place = coord * size - 0.5;
    vec2 fraction = fract(place);
    vec4 depths = textureGather(rainShadowMap, (floor(place) + 1.0) / size, 0);
    // Gather's order: (0,1), (1,1), (1,0), (0,0) from the texel below and left.
    vec2 low = -fraction / size;
    vec2 high = (1.0 - fraction) / size;
    vec4 limits = limit + slope.x * vec4(low.x, high.x, high.x, low.x) + slope.y * vec4(high.y, high.y, low.y, low.y);
    vec4 covered = step(depths, limits);
    return mix(mix(covered.w, covered.z, fraction.x), mix(covered.x, covered.y, fraction.x), fraction.y);
}

// The same, from 0 to 1, for what shows its edges. A point in the air has no plane of its own, so it's given the one of the ground it's looked at from, which is what lies under it in the map.
float rainShadowCover(vec3 worldPosition, vec2 slope)
{
    vec3 coord = rainShadowCoord(worldPosition);
    if (rainShadowOutside(coord.xy)) return 0.0;
    return rainShadowCoverAt(coord.xy, coord.z - SHELTER_BIAS / rainShadowInfo.y, slope);
}

// How much of the sky's rain reaches this point, 0..1, on a surface facing `normal`. Softened over about a metre, as water blows in under an edge and drips off it.
float rainReach(vec3 worldPosition, vec3 normal)
{
    if (rainShadowInfo.x < 0.5) return 1.0;
    vec3 coord = rainShadowCoord(worldPosition);
    if (rainShadowOutside(coord.xy)) return 1.0;

    vec2 slope = rainShadowSlope(normal);
    float limit = coord.z - SHELTER_BIAS / rainShadowInfo.y;
    float texel = 3.5 / float(textureSize(rainShadowMap, 0).x);
    float covered = rainShadowCoverAt(coord.xy, limit, slope);
    covered += rainShadowCoverAt(coord.xy + vec2(texel, 0.0), limit + slope.x * texel, slope);
    covered += rainShadowCoverAt(coord.xy - vec2(texel, 0.0), limit - slope.x * texel, slope);
    covered += rainShadowCoverAt(coord.xy + vec2(0.0, texel), limit + slope.y * texel, slope);
    covered += rainShadowCoverAt(coord.xy - vec2(0.0, texel), limit - slope.y * texel, slope);
    return 1.0 - covered * 0.2;
}

#endif
