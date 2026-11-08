#version 420

// The mirror-like light of whatever's smooth enough to show its surroundings, added to the lit scene. Anything rougher has had what it reflects from the ambient pass, and leaves here at once, which
// is nearly all of a dry land. Each sample gets what its own material makes of it: how much a surface reflects depends on its colour, its roughness and
// the angle it's seen at, for wet stone as for bright iron. What's reflected comes from three places, in order. What the rays found on screen. For a ray that left the screen, the world seen from
// above: the rain shadow says what stands overhead, so that a puddle under a tree doesn't show open sky. And past that the sky itself, clouds and all. From this high up nearly every ray off the
// ground goes out into the sky, so that last is most of what's seen on wet land. Drawn per pixel and per sample (MsaaRenderer.DrawSplit).

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl
#include Weather/RainShadow.glsl
#include Reflections/Reflection.glsl

layout (location = 0) out vec4 out_Colour;

uniform sampler2D reflectionTexture;
uniform sampler2D reflectionDistance;

uniform int screenReflections;
uniform int debugView;
uniform vec3 groundLight;

const int OPENNESS_STEPS = 5;

// How much of the sky a ray leaving here can see, by walking it up through the rain shadow: anything below what's overhead there has run into it.
float skyOpenness(vec3 world, vec3 direction, vec3 normal, float roughness)
{
    if (rainShadowInfo.x < 0.5 || direction.y <= 0.02) return 1.0;

    vec2 slope = rainShadowSlope(normal);
    float open = 1.0;
    float along = 1.2;
    for (int i = 0; i < OPENNESS_STEPS; i++)
    {
        open *= 1.0 - rainShadowCover(world + direction * along, slope) / float(OPENNESS_STEPS - i);
        along *= 2.1;
        if (open <= 0.01) break;
    }
    return mix(open, 1.0, roughness * roughness);
}

// The half-resolution reflection at this sample, taking only from what's at this distance, so that it doesn't bleed across the edges of things.
vec4 screenReflection(float distanceHere)
{
    ivec2 size = textureSize(reflectionTexture, 0);
    vec2 place = gl_FragCoord.xy / resolution * vec2(size) - 0.5;
    ivec2 base = ivec2(floor(place));
    vec2 fraction = place - vec2(base);
    float tolerance = distanceHere * 0.015 + 0.04;

    vec4 sum = vec4(0.0);
    float weights = 0.0;
    vec4 closest = vec4(0.0);
    float closestDifference = 1e20;
    for (int i = 0; i < 4; i++)
    {
        ivec2 offset = ivec2(i & 1, i >> 1);
        ivec2 texel = clamp(base + offset, ivec2(0), size - 1);
        vec4 tap = texelFetch(reflectionTexture, texel, 0);
        float difference = abs(texelFetch(reflectionDistance, texel, 0).r - distanceHere);
        vec2 axes = mix(1.0 - fraction, fraction, vec2(offset));
        float weight = axes.x * axes.y * exp(-difference / tolerance);
        sum += tap * weight;
        weights += weight;
        if (difference < closestDifference)
        {
            closestDifference = difference;
            closest = tap;
        }
    }
    return weights > 1e-4 ? sum / weights : closest;
}

void main(void)
{
    // Only as much as it takes to know whether this is any of our business.
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    vec4 quickMaterials = texelFetch(gMaterials, pixel, msaaShadingSample());
    if (debugView == 0 && !reflectionWanted(quickMaterials.r, texelFetch(gAlbedo, pixel, msaaShadingSample()).a)) discard;

    GBufferSample g = readGBufferSample();
    if (g.depth >= 1.0) discard;

    Surface s = surfaceOf(g);
    float NdotV = max(dot(s.normal, s.view), 0.0);
    float roughness = max(s.roughness, MIN_ROUGHNESS);
    mat3 viewToWorld = mat3(invViewMatrix);
    vec3 worldReflection = viewToWorld * reflect(-s.view, s.normal);
    vec3 world = (invViewMatrix * vec4(g.position, 1.0)).xyz;

    vec3 F0 = surfaceF0(s.albedo, s.metallic, s.roughness);
    vec3 strength = environmentBRDF(F0, roughness, NdotV) * specularOcclusion(NdotV, g.ambient, roughness);

    vec3 ground = groundLight;
    vec3 sky = skyEnvironment(worldReflection, roughness, ground);
    float openness = skyOpenness(world, worldReflection, viewToWorld * s.normal, roughness);
    vec3 reflected = mix(ground * 0.6, sky, openness);

    float rayFound = 0.0;
    if (screenReflections != 0)
    {
        vec4 found = screenReflection(length(g.position));
        rayFound = clamp(found.a, 0.0, 1.0);
        reflected = found.rgb + reflected * (1.0 - rayFound);
    }

    out_Colour = vec4(strength * reflected, 0.0);
    if (debugView == 1) out_Colour = vec4(vec3(rayFound) * 4.0, 0.0);
    else if (debugView == 2) out_Colour = vec4(vec3(openness) * 4.0, 0.0);
    else if (debugView == 3) out_Colour = vec4(reflected, 0.0);
    else if (debugView == 4) out_Colour = vec4(textureLod(skyMap, gl_FragCoord.xy / resolution, 0.0).rgb, 0.0);
}
