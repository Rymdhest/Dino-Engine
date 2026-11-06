#ifndef DINO_WEATHER_SKY_GLSL
#define DINO_WEATHER_SKY_GLSL

// The sky as everything but the sky itself reads it: a paraboloid map of the whole upper hemisphere (Sky_Map.frag), the way Ghost of Tsushima holds its clouds, with mips for
// whatever reflects it less than sharply. The sun is not in it: that's the light passes' business. Needs Weather/Weather.glsl.

uniform sampler2D skyMap;

const float SKY_MAP_MIPS = 8.0;

// Paraboloid: the zenith in the middle and the horizon on the circle that touches the map's edges.
vec2 skyMapCoord(vec3 direction)
{
    return direction.xz / (1.0 + max(direction.y, 0.0)) * 0.5 + 0.5;
}

// What's seen looking along `direction`, from a surface of this roughness. Below the horizon there's only the land.
vec3 skyEnvironment(vec3 direction, float roughness, vec3 groundColour)
{
    float lod = roughness * SKY_MAP_MIPS;
    vec3 up = direction;
    up.y = max(up.y, 0.0);
    up = normalize(up + vec3(0.0, 1e-3, 0.0));
    vec3 sky = textureLod(skyMap, skyMapCoord(up), lod).rgb + weatherFlash.rgb * (0.35 + 0.65 * max(up.y, 0.0));
    float below = smoothstep(0.02, -0.2 - roughness * 0.3, direction.y);
    return mix(sky, groundColour, below);
}

#endif
