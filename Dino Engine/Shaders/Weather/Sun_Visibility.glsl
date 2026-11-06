#ifndef DINO_WEATHER_SUN_GLSL
#define DINO_WEATHER_SUN_GLSL

// How much of the sun gets through the clouds here, 1 under a clear sky (Cloud_Shadow.frag). Needs Weather/Weather.glsl.

uniform sampler2D cloudShadowMap;

float weatherSunVisibility(vec2 xz)
{
    if (weatherKind.z < 0.5) return 1.0;
    return textureLod(cloudShadowMap, (xz - cloudRegion.xy) / cloudRegion.zw, 0.0).r;
}

#endif
