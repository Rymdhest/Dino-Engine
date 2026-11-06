#ifndef DINO_WEATHER_SURFACE_MAP_GLSL
#define DINO_WEATHER_SURFACE_MAP_GLSL

// What the weather has done to the ground, over the whole world (WeatherField.Bake). Needs Weather/Weather.glsl.

uniform sampler2D weatherMapA;      // x ground wetness, y puddles, z settled snow, w how hard anything is falling

vec4 weatherSurfaceAt(vec2 xz)
{
    return textureLod(weatherMapA, weatherMapCoord(xz), 0.0);
}

#endif
