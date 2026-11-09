#ifndef DINO_LIGHTING_SUN_SHADOW_GLSL
#define DINO_LIGHTING_SUN_SHADOW_GLSL

// How much of a directional light reaches a point, through its cascades of shadow maps. DirectionalLightRenderer binds them,
// as plain depth: each texel's own depth is needed, not only whether it's in the way.
//
// Nothing here is random. There's no temporal accumulation to average noise away, so a filter that turned its taps by a random
// angle each pixel left that noise in the picture for good, fixed to the screen and crawling over the ground as the camera moved,
// worst in a long shadow under leaves. Instead every texel near the point is read and weighted by a smooth kernel of how far it
// is from the point, so the shadow is a function of where the point is alone: the same on every pixel that looks at it, smooth
// as it moves, and as still in the world as the maps, which are snapped to their texels.

uniform int numberOfCascades;

uniform mat4 sunSpaceMatrices[5];
uniform sampler2D shadowMaps[5];

// The filter's radius, in texels. Its weights, (1 - (r/R)^2)^2, fall to nothing at the edge with no step in them or in their
// slope, so a penumbra has no ring, and every texel within the radius lies in the 8 x 8 read below.
const float SHADOW_FILTER_RADIUS = 4.0;

// In the shadow map's depth, which runs 0 to 1 over a few hundred metres.
const float SHADOW_BIAS = 0.0001;

// Air fills a long shadow with light, and it matters here: a mountain can lay its shadow over everything for hundreds of metres in a low sun, and nobody can fight what they can't see.
// So a shadow gets lighter the further off whatever casts it. Close up it's as dark as ever, which is what gives a rock its shape, and what's standing on the ground keeps its own.
// Every texel says how far its own caster is, so this is part of the same filter as the shadow, and follows it exactly.
const float SHADOW_FILL_NEAR = 16.0;    // Metres from a point to what's shading it, where the shadow starts to lift,
const float SHADOW_FILL_FAR = 96.0;     // and where it's lifted as far as it goes.
const float SHADOW_FILL_MOST = 0.5;

// How much of the light gets through one cascade, at a point in its space (0 to 1 on each axis). perMetre is how far its depth
// moves for a metre along the light.
float sunShadowThrough(sampler2D map, vec3 point, float perMetre)
{
    vec2 size = vec2(textureSize(map, 0));
    vec2 at = point.xy * size - 0.5;        // In texels, with texel i's centre at i.
    vec2 base = floor(at);
    vec2 f = at - base;
    float reference = point.z - SHADOW_BIAS;
    const float inverseRadiusSquared = 1.0 / (SHADOW_FILTER_RADIUS * SHADOW_FILTER_RADIUS);

    float through = 0.0;
    float total = 0.0;
    for (int y = -3; y <= 3; y += 2)
    {
        for (int x = -3; x <= 3; x += 2)
        {
            // The four texels from base + (x, y) to base + (x + 1, y + 1), in the order a gather gives them.
            vec4 depth = textureGather(map, (base + vec2(x, y) + 1.0) / size);
            vec4 dx = vec4(x, x + 1, x + 1, x) - f.x;
            vec4 dy = vec4(y + 1, y + 1, y, y) - f.y;
            vec4 weight = max(1.0 - (dx * dx + dy * dy) * inverseRadiusSquared, 0.0);
            weight *= weight;

            // Metres from here up to what's in the way, at or below 0 where nothing is.
            vec4 caster = (reference - depth) / perMetre;
            vec4 lifted = SHADOW_FILL_MOST * clamp((caster - SHADOW_FILL_NEAR) / (SHADOW_FILL_FAR - SHADOW_FILL_NEAR), 0.0, 1.0);
            vec4 open = step(caster, vec4(0.0));
            through += dot(weight, mix(lifted, vec4(1.0), open));
            total += weight.x + weight.y + weight.z + weight.w;
        }
    }
    return through / total;
}

float calcShadow(vec3 positionViewSpace)
{
    float finalSunFactor = 1.0;
    for (int i = 0; i < numberOfCascades; i++)
    {
        vec4 positionSunSpace = sunSpaceMatrices[i] * vec4(positionViewSpace, 1.0);
        positionSunSpace = positionSunSpace * 0.5 + 0.5;

        // Outside this cascade's box.
        if (any(lessThan(positionSunSpace.xyz, vec3(0.0))) ||
            any(greaterThan(positionSunSpace.xyz, vec3(1.0)))) {
            continue;
        }

        mat4 sunSpace = sunSpaceMatrices[i];
        float perMetre = 0.5 * length(vec3(sunSpace[0][2], sunSpace[1][2], sunSpace[2][2]));
        finalSunFactor = min(finalSunFactor, sunShadowThrough(shadowMaps[i], positionSunSpace.xyz, perMetre));
    }
    return finalSunFactor;
}

#endif
