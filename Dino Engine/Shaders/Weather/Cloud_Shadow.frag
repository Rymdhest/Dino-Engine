#version 420

// How much of the sun reaches each point of the land around the camera, 0..1: the clouds in Clouds.glsl, seen along the sun's own rays. From up here that's the first thing that tells
// a player the weather is changing, shadows sliding over the land long before any rain arrives. The light passes multiply the sun by this (Sun_Visibility.glsl).

#include Weather/Weather.glsl
#include Weather/Clouds.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

const int STEPS = 5;
const float EXTINCTION = 0.013;

void main(void)
{
    vec2 ground = cloudRegion.xy + textureCoords * cloudRegion.zw;
    vec3 towardsSun = weatherSun.xyz;
    float rise = max(towardsSun.y, 0.08);

    float depth = 0.0;
    float stepLength = cloudShape.y / float(STEPS) / rise;
    for (int i = 0; i < STEPS; i++)
    {
        float altitude = cloudShape.x + cloudShape.y * ((float(i) + 0.5) / float(STEPS));
        vec2 under = ground + towardsSun.xz * (altitude / rise);
        depth += cloudDensity(vec3(under.x, altitude, under.y), cloudCoverAt(under), 1.0) * stepLength;
    }

    out_Colour = vec4(mix(weatherSun.w, 1.0, exp(-depth * EXTINCTION)));
}
