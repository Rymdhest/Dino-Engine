#ifndef DINO_WEATHER_CLOUDS_GLSL
#define DINO_WEATHER_CLOUDS_GLSL

// Where the clouds are. The weather map says how much of the sky is covered over each part of the world, and tiling noise says what that cover looks like, drifting and changing
// by the running totals WeatherSystem keeps. Sky_Map.frag marches through this for the sky, Cloud_Shadow.frag for the shadows on the land, so what's seen overhead is what's
// shading the ground. Needs Weather/Weather.glsl.

uniform sampler3D cloudNoise;   // VolumetricFogRenderer's tiling noise: r billows, g detail, a broad swells
uniform sampler2D weatherMapB;  // x cloud, y storm, z mist, w dust

float cloudCoverAt(vec2 xz)
{
    return textureLod(weatherMapB, weatherMapCoord(xz), 0.0).x;
}

// Density at a point of the cloud layer, 0..1, for a cover of 0..1. Tops are round and billowing and bases nearly flat, and the more of the sky is covered the more of the noise passes,
// until it's a single deck with only its underside to tell it by.
float cloudDensity(vec3 position, float cover, float detailLod)
{
    float height = (position.y - cloudShape.x) / cloudShape.y;
    if (height <= 0.0 || height >= 1.0 || cover <= 0.01) return 0.0;

    vec2 tile = position.xz * cloudShape.z + cloudMotion.xy;
    vec3 q = vec3(tile.x, height * 0.22 + cloudShape.w, tile.y);
    vec4 broad = textureLod(cloudNoise, q * 0.5, detailLod);
    float billows = textureLod(cloudNoise, q * 1.7 + broad.a * 0.12, detailLod).r;

    float profile = smoothstep(0.0, 0.1, height) * smoothstep(1.0, 0.38 + 0.3 * broad.a, height);
    float shape = (billows * 0.62 + broad.r * 0.38) * mix(profile, 1.0, cover * cover * 0.8);
    // The noise's middle is around 0.55, so a cover of a half leaves about half the sky open.
    float threshold = mix(0.95, 0.2, cover);
    float density = clamp((shape - threshold) / 0.12, 0.0, 1.0);
    if (density <= 0.0) return 0.0;

    // The edges break up in wisps.
    float wisps = textureLod(cloudNoise, q * 7.0, detailLod).g;
    return clamp(density - (1.0 - density) * wisps * 0.7, 0.0, 1.0);
}

#endif
