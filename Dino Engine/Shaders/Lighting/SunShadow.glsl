#ifndef DINO_LIGHTING_SUN_SHADOW_GLSL
#define DINO_LIGHTING_SUN_SHADOW_GLSL

// How much of a directional light reaches a point, through its cascades of shadow maps. DirectionalLightRenderer binds them.
// Needs AntiAliasing/Msaa.glsl included first.

uniform int numberOfCascades;


uniform mat4 sunSpaceMatrices[5];
uniform sampler2DShadow shadowMaps[5];


uniform int pcfRadius;

const vec2 poissonDisk[16] = vec2[](
    vec2( -0.94201624, -0.39906216 ),
    vec2( 0.94558609, -0.76890725 ),
    vec2( -0.094184101, -0.92938870 ),
    vec2( 0.34495938, 0.29387760 ),
    vec2( -0.91588581, 0.45771432 ),
    vec2( -0.81544232, -0.87912464 ),
    vec2( -0.38277543, 0.27676845 ),
    vec2( 0.97484398, 0.75648379 ),
    vec2( 0.44323325, -0.97511554 ),
    vec2( 0.53742981, -0.47373420 ),
    vec2( -0.26496911, -0.41893023 ),
    vec2( 0.79197514, 0.19090188 ),
    vec2( -0.24188840, 0.99706507 ),
    vec2( -0.81409955, 0.91437590 ),
    vec2( 0.19984126, 0.78641367 ),
    vec2( 0.14383161, -0.14100790 )
);

// Air fills a long shadow with light, and it matters here: a mountain can lay its shadow over everything for hundreds of metres in a low sun, and nobody can fight what they can't see.
// So a shadow gets lighter the further off whatever casts it. Close up it's as dark as ever, which is what gives a rock its shape, and what's standing on the ground keeps its own.
const float SHADOW_FILL_NEAR = 24.0;    // Metres from a point to what's shading it, where the shadow starts to lift,
const float SHADOW_FILL_FAR = 90.0;     // and where it's lifted as far as it goes.
const float SHADOW_FILL_MOST = 0.5;
const int SHADOW_FILL_STEPS = 4;        // Each is a line inside the shadow, so there are several, each too faint to see, and each read over a wide patch.
const float SHADOW_FILL_SPREAD = 9.0;   // Times the width of the shadow's own filter.

float sunShadowRandom(vec3 seed) {
    return fract(sin(dot(seed, vec3(12.9898, 78.233, 45.164))) * 43758.5453123);
}

float calcShadow(vec3 positionViewSpace) {
    float finalSunFactor = 1.0;
    float bias = 0.0001;

    // Per pixel, all 16 taps of the disk. On edge pixels, shaded once per sample, the samples share them out (every
    // sampleCount-th tap each, at least 4) with rotations of their own, so an edge pixel costs about as much as any
    // other and its samples of one surface average to the full filter.
    int sampleIndex = msaaShadingSample();
#ifdef PER_SAMPLE
    int numSamples = max(16 / msaaSampleCount(), 4);
#else
    int numSamples = 16;
#endif
    int tapStride = 16 / numSamples;

    for (int i = 0; i < numberOfCascades; i++) {
        vec4 positionSunSpace = sunSpaceMatrices[i] * vec4(positionViewSpace, 1.0);
        positionSunSpace = positionSunSpace * 0.5 + 0.5;

        // Outside this cascade's box.
        if (any(lessThan(positionSunSpace.xyz, vec3(0.0))) ||
            any(greaterThan(positionSunSpace.xyz, vec3(1.0)))) {
            continue;
        }

        float sunFactorSum = 0.0;

        // Generate a random rotation angle per pixel (and sample) to prevent fixed grid patterns
        float randomAngle = sunShadowRandom(vec3(floor(gl_FragCoord.xy) + 0.5, float(i) + 7.0 * float(sampleIndex))) * 6.2831853;
        mat2 rotation = mat2(
            cos(randomAngle), -sin(randomAngle),
            sin(randomAngle),  cos(randomAngle)
        );

        vec2 texelSize = 1.0 / vec2(textureSize(shadowMaps[i], 0));
        float filterRadius = float(pcfRadius); // Scale spread using your existing pcfRadius uniform

        for (int j = 0; j < numSamples; j++) {
            vec2 rotatedOffset = rotation * poissonDisk[(sampleIndex + j * tapStride) & 15];
            vec2 offsetCoord = rotatedOffset * texelSize * filterRadius;

            vec3 sampleCoord = vec3(
                positionSunSpace.xy + offsetCoord,
                positionSunSpace.z - bias
            );

            sunFactorSum += texture(shadowMaps[i], sampleCoord);
        }

        float sunFactor = sunFactorSum / float(numSamples);
        if (sunFactor < 0.999) {
            // Asked again from a point further up the ray, a shadow that's gone means what cast it was nearer than that.
            mat4 sunSpace = sunSpaceMatrices[i];
            float perMetre = 0.5 * length(vec3(sunSpace[0][2], sunSpace[1][2], sunSpace[2][2]));
            float lifted = 0.0;
            for (int step = 0; step < SHADOW_FILL_STEPS; step++) {
                float reach = mix(SHADOW_FILL_NEAR, SHADOW_FILL_FAR, float(step) / float(SHADOW_FILL_STEPS - 1));
                vec2 aside = rotation * poissonDisk[(step * 5 + sampleIndex) & 15] * texelSize * filterRadius * SHADOW_FILL_SPREAD;
                lifted += 1.0 - texture(shadowMaps[i], vec3(positionSunSpace.xy + aside, positionSunSpace.z - reach * perMetre));
            }
            sunFactor = max(sunFactor, lifted * (SHADOW_FILL_MOST / float(SHADOW_FILL_STEPS)));
        }
        finalSunFactor = min(finalSunFactor, sunFactor);
    }
    return finalSunFactor;
}

#endif
