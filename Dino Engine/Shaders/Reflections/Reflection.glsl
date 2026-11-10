#ifndef DINO_REFLECTION_GLSL
#define DINO_REFLECTION_GLSL

// What the reflection passes share (ScreenSpaceReflectionRenderer). After Stachowiak's stochastic screen-space reflections (SIGGRAPH 2015) and AMD's FidelityFX SSSR.

const float REFLECT_PI = 3.14159265359;
// Past this a surface's reflection is only the sky's average, and costs a ray for nothing it can show.
const float REFLECT_MAX_ROUGHNESS = 0.72;
// Below this a surface is a mirror: one ray straight down the reflection, with no noise to filter.
const float REFLECT_MIRROR_ROUGHNESS = 0.09;

// Whether a surface gets rays of its own, and the reflection pass's attention. Thin, translucent things (blades, leaves) are too fine to reflect anything but the sky, and rays off them would only
// sparkle. Whatever this turns down reflects the sky in the ambient pass, so every pass that asks must ask of the same stored values. Translucency is stored as what gets through, less what the
// thickness takes (gBufferUtil.glsl's transmittance), which is a third or so of what a leaf or a blade says of itself: anything that lets a tenth through is thin.
bool reflectionWanted(float roughness, float translucency)
{
    return roughness < REFLECT_MAX_ROUGHNESS && translucency < 0.1;
}

float reflectionD(float NdotH, float alpha2)
{
    float denominator = NdotH * NdotH * (alpha2 - 1.0) + 1.0;
    return alpha2 / max(REFLECT_PI * denominator * denominator, 1e-7);
}

float reflectionG1(float NdotX, float alpha2)
{
    return 2.0 * NdotX / max(NdotX + sqrt(alpha2 + (1.0 - alpha2) * NdotX * NdotX), 1e-5);
}

// Half a ball's worth of the lobe's spread, as the tangent of the cone that holds most of it.
float reflectionConeTangent(float roughness)
{
    float alpha = roughness * roughness;
    return min(alpha * 1.1, 1.0);
}

// Noise that averages out fast over a few frames: interleaved gradient noise, walked by the frame.
vec2 reflectionNoise(ivec2 pixel, int frame)
{
    float a = fract(52.9829189 * fract(dot(vec2(pixel) + 5.588238 * float(frame & 63), vec2(0.06711056, 0.00583715))));
    float b = fract(52.9829189 * fract(dot(vec2(pixel.yx) + vec2(47.0, 17.0) + 5.588238 * float((frame + 31) & 63), vec2(0.06711056, 0.00583715))));
    return vec2(a, b);
}

// A way to view what's bright without letting one bright sample rule its neighbours (Karis): filtered in this space, a lone firefly stays lone.
float reflectionTone(vec3 colour)
{
    return 1.0 / (1.0 + max(colour.r, max(colour.g, colour.b)));
}

#endif
