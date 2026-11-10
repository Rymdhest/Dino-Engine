#version 420

// The light that comes from everywhere, the light surfaces give off themselves, and the sun. Ambient light is a hemisphere:
// the sky above and what the ground throws back below. Surfaces too rough to show what's around them reflect the sky's
// average here, for the cost of one lookup; anything smoother is the reflection pass's, which knows what it can really see
// (Reflections/Reflection_Composite.frag). The sun is here so that the scene is written once rather than twice, as nothing
// costs more than going over all of it, and a sunColour of 0 leaves it out. Nothing else here depends on a light entity, so
// with the sun and sky turned off only emissive surfaces remain. Drawn per pixel and per sample (MsaaRenderer.DrawSplit).

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl
#include Reflections/Reflection.glsl
#include Weather/Sun_Visibility.glsl
#include Lighting/SunShadow.glsl
#include Halls.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 skyLight;
uniform vec3 groundLight;
uniform float emissionScale;
uniform vec3 sunDirectionViewSpace;
uniform vec3 sunColour;

// Global illumination (GI.md): what GlobalIlluminationRenderer gathered, at half the resolution each way, a layer for each colour's
// irradiance A + B.n and a fourth with each texel's depth and how sure it is. 0 for giMix leaves the flat sky and ground.
uniform sampler2DArray giGathered;
uniform float giMix;
uniform float giStrength;
uniform int giDebug;

// The light gathered around this pixel, from the four texels about it, each by how near it is and how close its depth is to this one's,
// so that light never runs across the edge of what's in front. Returns the irradiance for a world-space normal, and in w how much of
// it GI has an answer for; `reflected` is the light coming along a direction, as blurred as the roughest surfaces see it.
vec4 giIrradiance(vec3 normal, float viewZ, vec3 direction, out vec3 reflected)
{
    vec2 at = gl_FragCoord.xy * 0.5 - 0.25;
    ivec2 low = ivec2(floor(at));
    vec2 f = at - vec2(low);
    ivec2 last = textureSize(giGathered, 0).xy - 1;
    vec4 red = vec4(0.0), green = vec4(0.0), blue = vec4(0.0);
    float known = 0.0, total = 0.0;
    for (int i = 0; i < 4; i++)
    {
        ivec2 corner = ivec2(i & 1, i >> 1);
        ivec2 q = clamp(low + corner, ivec2(0), last);
        vec4 guide = texelFetch(giGathered, ivec3(q, 3), 0);
        vec2 w2 = mix(1.0 - f, f, vec2(corner));
        float apart = abs(guide.x - viewZ) / (abs(viewZ) * 0.025 + 0.04);
        float w = w2.x * w2.y / (1.0 + apart * apart) + 1e-5;
        float sure = w * guide.y;
        red += texelFetch(giGathered, ivec3(q, 0), 0) * sure;
        green += texelFetch(giGathered, ivec3(q, 1), 0) * sure;
        blue += texelFetch(giGathered, ivec3(q, 2), 0) * sure;
        known += sure;
        total += w;
    }
    if (known <= 1e-6)
    {
        reflected = vec3(0.0);
        return vec4(0.0);
    }
    red /= known;
    green /= known;
    blue /= known;
    vec3 a = vec3(red.x, green.x, blue.x);
    vec3 irradiance = max(a + vec3(dot(red.yzw, normal), dot(green.yzw, normal), dot(blue.yzw, normal)), vec3(0.0));
    reflected = max(a / LIGHT_PI + (1.5 / LIGHT_PI) * vec3(dot(red.yzw, direction), dot(green.yzw, direction), dot(blue.yzw, direction)), vec3(0.0));
    return vec4(irradiance, clamp(known / total, 0.0, 1.0));
}

void main(void)
{
    GBufferSample g = readGBufferSample();
    if (g.depth >= 1.0) discard;

    Surface s = surfaceOf(g);
    float NdotV = max(dot(s.normal, s.view), 0.0);
    mat3 viewToWorld = mat3(invViewMatrix);
    vec3 worldNormal = viewToWorld * s.normal;
    float roughness = max(s.roughness, MIN_ROUGHNESS);
    vec3 worldReflection = viewToWorld * reflect(-s.view, s.normal);

    vec3 surrounding = mix(groundLight, skyLight, worldNormal.y * 0.5 + 0.5);
    vec3 environment = skyEnvironment(worldReflection, roughness, groundLight);
    // The flat sky and ground only stand in for what global illumination doesn't answer, and in a hall's room little of either comes in.
    float shelter = hallShelter((invViewMatrix * vec4(g.position, 1.0)).xyz);
    surrounding *= shelter;
    environment *= shelter;
    float gathered = 0.0;
    vec3 giLight = vec3(0.0);
    if (giMix > 0.0)
    {
        vec3 reflected;
        vec4 gi = giIrradiance(worldNormal, g.position.z, worldReflection, reflected);
        gathered = giMix * gi.w;
        giLight = gi.rgb / LIGHT_PI * giStrength;
        surrounding = mix(surrounding, giLight, gathered);
        environment = mix(environment, reflected * giStrength, gathered);
    }

    // A hall's room glows a little with the daylight let in, whatever GI made of it.
    surrounding += hallRoomLight((invViewMatrix * vec4(g.position, 1.0)).xyz, skyLight, sunColour);

    vec3 F0 = surfaceF0(s.albedo, s.metallic, s.roughness);
    vec3 specular = environmentBRDF(F0, roughness, NdotV);
    vec3 diffuse = s.albedo * (1.0 - s.metallic) * (1.0 - specular);

    vec3 ambient = diffuse * surrounding * g.ambient;
    if (!reflectionWanted(s.roughness, s.translucency))
    {
        ambient += specular * environment * specularOcclusion(NdotV, g.ambient, roughness);
    }
    vec3 colour = ambient + decodeEmission(s.albedo, g.materials.g) * emissionScale;

    // --render gidebug=3: the light GI gives each surface, on white and with nothing else; 4: where it has an answer (green) and not (red).
    if (giDebug == 3 || giDebug == 5)
    {
        out_Colour = vec4(giLight * 0.6 * g.ambient, 1.0);
        return;
    }
    if (giDebug == 4)
    {
        out_Colour = vec4(mix(vec3(1.2, 0.05, 0.02), vec3(0.05, 0.9, 0.1), gathered / max(giMix, 1e-4)), 1.0);
        return;
    }

    if (sunColour != vec3(0.0))
    {
        // The relief's own shadow is the sun's alone: it was cast along the sun's way, and no other light's.
        vec3 radiance = sunColour * clamp(calcShadow(g.position), 0.0, 1.0) * g.materials.a * weatherSunVisibility((invViewMatrix * vec4(g.position, 1.0)).xz);
        colour += lightSurface(s, sunDirectionViewSpace, radiance);
    }
    out_Colour = vec4(colour, 1.0);
}
