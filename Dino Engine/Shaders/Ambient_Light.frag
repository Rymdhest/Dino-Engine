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

layout (location = 0) out vec4 out_Colour;

uniform vec3 skyLight;
uniform vec3 groundLight;
uniform float emissionScale;
uniform vec3 sunDirectionViewSpace;
uniform vec3 sunColour;

void main(void)
{
    GBufferSample g = readGBufferSample();
    if (g.depth >= 1.0) discard;

    Surface s = surfaceOf(g);
    float NdotV = max(dot(s.normal, s.view), 0.0);
    mat3 viewToWorld = mat3(invViewMatrix);
    vec3 worldNormal = viewToWorld * s.normal;

    vec3 surrounding = mix(groundLight, skyLight, worldNormal.y * 0.5 + 0.5);

    vec3 F0 = surfaceF0(s.albedo, s.metallic, s.roughness);
    vec3 specular = environmentBRDF(F0, max(s.roughness, MIN_ROUGHNESS), NdotV);
    vec3 diffuse = s.albedo * (1.0 - s.metallic) * (1.0 - specular);

    vec3 ambient = diffuse * surrounding * g.ambient;
    if (!reflectionWanted(s.roughness, s.translucency))
    {
        float roughness = max(s.roughness, MIN_ROUGHNESS);
        vec3 worldReflection = viewToWorld * reflect(-s.view, s.normal);
        ambient += specular * skyEnvironment(worldReflection, roughness, groundLight) * specularOcclusion(NdotV, g.ambient, roughness);
    }
    vec3 colour = ambient + decodeEmission(s.albedo, g.materials.g) * emissionScale;

    if (sunColour != vec3(0.0))
    {
        vec3 radiance = sunColour * clamp(calcShadow(g.position), 0.0, 1.0) * weatherSunVisibility((invViewMatrix * vec4(g.position, 1.0)).xz);
        colour += lightSurface(s, sunDirectionViewSpace, radiance);
    }
    out_Colour = vec4(colour, 1.0);
}
