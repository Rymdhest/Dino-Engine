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

// The light flesh is measured over: texels from the point, past which they count for nothing. It takes in the four by four nearest, and no further, as how thick something is
// changes much faster across it than whether it's lit.
const float THICKNESS_FILTER_RADIUS = 2.25;
// Metres of flesh in front of the point at which a texel counts in full: less, and it's the point's own surface or next to it, which the light reaches directly.
const float THICKNESS_FULL = 0.01;
// Metres of the sun's own shadow map's offset, and of the two maps' different texels, that whatever's in front of the flesh is allowed before it counts as in the way.
const float THICKNESS_ENTRY_SLACK = 0.12;

// How much of the sun gets through flesh to a point, per colour, for every unit of it falling square on the flesh it went in by (Lighting/lightingCalc.glsl, SUBSURFACE.md).
// The flesh map holds the depth where the sun goes into skin and hide, and the point is where light comes out, so the depth between them is how far it went through flesh.
// Each texel counts by how squarely the sun met the flesh there, as light that meets a surface edge on hardly gets in, which is read from how fast the map's depth changes
// across it: that's also what makes it safe, as the only texels that would read thin where nothing is are those where the map grazes a body's outline from the sun or steps
// from one part to another, and there the depth runs away. Only flesh in front counts, so a point the sun lights directly gets nothing from it, and a texel's thickness goes
// through what it takes of each colour before they're averaged, so a thin edge isn't lost in a thick average. Then the sun's own shadow map says whether anything else,
// armour, a tree, a wall, stood between the sun and where it went in. fleshDepthMap is flesh alone from the sun, drawn with no offset (FleshDepth), and fleshSpace takes view
// space to it as sunSpaceMatrices do to the cascades; towardsSun is in view space; extinction is per metre, per colour (sssExtinction).
vec3 sunFleshTransmittance(sampler2D fleshDepthMap, mat4 fleshSpace, vec3 positionViewSpace, vec3 towardsSun, vec3 extinction)
{
    vec3 point = (fleshSpace * vec4(positionViewSpace, 1.0)).xyz * 0.5 + 0.5;
    if (any(lessThan(point, vec3(0.0))) || any(greaterThan(point, vec3(1.0)))) return vec3(0.0);

    float perMetre = 0.5 * length(vec3(fleshSpace[0][2], fleshSpace[1][2], fleshSpace[2][2]));
    vec2 size = vec2(textureSize(fleshDepthMap, 0));
    float texelMetres = 2.0 / (length(vec3(fleshSpace[0][0], fleshSpace[1][0], fleshSpace[2][0])) * size.x);
    // What a step in depth from one texel to the next comes to as a slope across the sun.
    float slopePerStep = 1.0 / (perMetre * texelMetres);
    vec2 at = point.xy * size - 0.5;
    vec2 base = floor(at);
    vec2 f = at - base;
    vec3 decay = extinction * 1.44269504;    // For exp2.
    const float inverseRadiusSquared = 1.0 / (THICKNESS_FILTER_RADIUS * THICKNESS_FILTER_RADIUS);

    vec3 through = vec3(0.0);
    float total = 0.0;
    float thickness = 0.0;
    float counted = 0.0;
    for (int y = -1; y <= 1; y += 2)
    {
        for (int x = -1; x <= 1; x += 2)
        {
            // Texels (0, 1), (1, 1), (1, 0) and (0, 0) of a square of four.
            vec4 depth = textureGather(fleshDepthMap, (base + vec2(x, y) + 1.0) / size);
            vec4 dx = vec4(x, x + 1, x + 1, x) - f.x;
            vec4 dy = vec4(y + 1, y + 1, y, y) - f.y;
            vec4 weight = max(1.0 - (dx * dx + dy * dy) * inverseRadiusSquared, 0.0);
            weight *= weight;
            total += weight.x + weight.y + weight.z + weight.w;

            // How squarely the sun met the flesh across these four: the cosine of a surface whose depth climbs by this much a texel either way. Where they step off a body
            // or from one part to another, the depth runs away and it's next to nothing.
            float slopeAcross = (depth.y - depth.x + depth.z - depth.w) * 0.5 * slopePerStep;
            float slopeAlong = (depth.x - depth.w + depth.y - depth.z) * 0.5 * slopePerStep;
            float square = inversesqrt(1.0 + slopeAcross * slopeAcross + slopeAlong * slopeAlong);

            // Metres of flesh from where the light went in to here.
            vec4 metres = (point.z - depth) / perMetre;
            vec4 flesh = weight * smoothstep(0.0, THICKNESS_FULL, metres) * square;
            metres = max(metres, 0.0);
            through += flesh.x * exp2(-decay * metres.x);
            through += flesh.y * exp2(-decay * metres.y);
            through += flesh.z * exp2(-decay * metres.z);
            through += flesh.w * exp2(-decay * metres.w);
            thickness += dot(flesh, metres);
            counted += flesh.x + flesh.y + flesh.z + flesh.w;
        }
    }
    if (counted <= 1e-4) return vec3(0.0);

    // Whether the sun reached where it went in: the cascade's first surface along the same ray mustn't be nearer the sun than the flesh's, past its own offset.
    vec3 entry = positionViewSpace + towardsSun * (thickness / counted);
    for (int i = 0; i < numberOfCascades; i++)
    {
        vec3 cascade = (sunSpaceMatrices[i] * vec4(entry, 1.0)).xyz * 0.5 + 0.5;
        if (any(lessThan(cascade, vec3(0.0))) || any(greaterThan(cascade, vec3(1.0)))) continue;
        mat4 sunSpace = sunSpaceMatrices[i];
        float cascadePerMetre = 0.5 * length(vec3(sunSpace[0][2], sunSpace[1][2], sunSpace[2][2]));
        vec4 first = textureGather(shadowMaps[i], cascade.xy);
        vec4 open = step(cascade.z - THICKNESS_ENTRY_SLACK * cascadePerMetre, first);
        return through / max(total, 1e-5) * (0.25 * (open.x + open.y + open.z + open.w));
    }
    return vec3(0.0);
}

#endif
