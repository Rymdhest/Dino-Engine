#version 420

// The light that comes from everywhere, and the light surfaces give off themselves. Ambient light is a hemisphere: the
// sky above and what the ground throws back below. Surfaces too rough to show what's around them reflect the sky's average
// here, for the cost of one lookup; anything smoother is the reflection pass's, which knows what it can really see
// (Reflections/Reflection_Composite.frag). Nothing here depends on a light entity, so with the sun and sky turned off only
// emissive surfaces remain. Drawn per pixel and per sample (MsaaRenderer.DrawSplit).

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl
#include Reflections/Reflection.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 skyLight;
uniform vec3 groundLight;
uniform float emissionScale;

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
    out_Colour = vec4(ambient + decodeEmission(s.albedo, g.materials.g) * emissionScale, 0.0);
}
