#version 430

// Writes one mip level of a finished material into its layer of the three arrays the renderers read (textureUtil.glsl):
//   albedo   (sRGB):  colour, alpha. Stored as sRGB so that the dark end gets as many steps as the light; reads back linear.
//   normal   (RGBA8): normal x and y (z is rebuilt, as it's never negative), subsurface, ambient occlusion.
//   material (RGBA8): roughness, emission, metallic, height.
// A level's normals come in averaged and shorter than one, by as much as the bumps under them disagreed. That spread is folded into
// the roughness, by fitting a von Mises-Fisher lobe to it and widening GGX by as much (Neubelt and Pettineo, "Crafting a Next-Gen
// Material Pipeline for The Order: 1886", 2013): far off, a rough-bumped surface is a rough one, and doesn't sparkle.

layout (location = 0) out vec4 albedoOut;
layout (location = 1) out vec4 normalOut;
layout (location = 2) out vec4 materialOut;

uniform sampler2D albedoIn;
uniform sampler2D normalIn;
uniform sampler2D propertiesIn;   // roughness, emission, metallic, subsurface
uniform sampler2D occlusionIn;
uniform int level;

void main(void)
{
    ivec2 at = ivec2(gl_FragCoord.xy);
    vec4 albedo = texelFetch(albedoIn, at, level);
    vec4 normalHeight = texelFetch(normalIn, at, level);
    vec4 properties = texelFetch(propertiesIn, at, level);
    float occlusion = texelFetch(occlusionIn, at, level).r;

    vec3 averaged = normalHeight.xyz;
    float agreement = clamp(length(averaged), 1e-4, 1.0);
    vec3 n = averaged / agreement;

    float roughness = clamp(properties.r, 0.0, 1.0);
    float alpha = roughness * roughness;
    if (agreement < 0.9999)
    {
        float kappa = (3.0 * agreement - agreement * agreement * agreement) / (1.0 - agreement * agreement);
        alpha = sqrt(alpha * alpha + 2.0 / kappa);
    }
    roughness = clamp(sqrt(alpha), 0.0, 1.0);

    albedoOut = vec4(clamp(albedo.rgb, 0.0, 1.0), clamp(albedo.a, 0.0, 1.0));
    normalOut = vec4(n.xy * 0.5 + 0.5, clamp(properties.a, 0.0, 1.0), clamp(occlusion, 0.0, 1.0));
    materialOut = vec4(roughness, clamp(properties.g, 0.0, 1.0), clamp(properties.b, 0.0, 1.0), clamp(normalHeight.w, 0.0, 1.0));
}
