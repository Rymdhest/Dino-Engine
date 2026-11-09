#ifndef DINO_GI_SKY_GLSL
#define DINO_GI_SKY_GLSL

// The sky as GI sees it: the sky map, with its clouds and its night and any lightning, as everything else reads it (Weather/Sky.glsl),
// scaled as the ambient pass scales the sky, with the sun the air scatters into the shade on top. Below the horizon, past the end of
// the voxels, there's only far-off land. Needs GI/GI.glsl, Weather/Weather.glsl and Weather/Sky.glsl.

vec3 giSkyRadiance(vec3 dir, float roughness)
{
    return skyEnvironment(dir, roughness, giGroundRadiance.rgb) * giSkyScale.w + giSkyScale.rgb;
}

#endif
