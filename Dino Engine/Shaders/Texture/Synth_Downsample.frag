#version 430

// One step down a material's mip chain, in floats, before it's packed into eight bits (MaterialBaker): each texel the average of the
// four under it. Normals are averaged as vectors and not made unit again, as how much shorter they come out is how much they
// disagreed, which the pack turns into roughness (Synth_Pack.frag): a bumpy surface far off is a rough one. The images are bound with
// only the level below readable, so their level 0 here is that one.

layout (location = 0) out vec4 albedoOut;
layout (location = 1) out vec4 normalOut;
layout (location = 2) out vec4 propertiesOut;
layout (location = 3) out vec4 occlusionOut;

uniform sampler2D albedoIn;
uniform sampler2D normalIn;
uniform sampler2D propertiesIn;
uniform sampler2D occlusionIn;

vec4 average(sampler2D image, ivec2 at)
{
    return 0.25 * (texelFetch(image, at, 0) + texelFetch(image, at + ivec2(1, 0), 0)
                 + texelFetch(image, at + ivec2(0, 1), 0) + texelFetch(image, at + ivec2(1, 1), 0));
}

void main(void)
{
    ivec2 at = ivec2(gl_FragCoord.xy) * 2;
    albedoOut = average(albedoIn, at);
    normalOut = average(normalIn, at);
    propertiesOut = average(propertiesIn, at);
    occlusionOut = average(occlusionIn, at);
}
