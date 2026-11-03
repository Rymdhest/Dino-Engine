#version 420

// Applies the half-resolution fog to the full-resolution image. The four nearest fog texels are
// weighted by how close their surface distance is to this pixel's (joint bilateral upsample), so
// fog doesn't bleed across depth edges such as tree canopies above fogged ground.
// Distance haze over the whole view is added analytically on top.

#include gBufferUtil.glsl
#include Globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2D shadedColourTexture;
uniform sampler2D gDepth;
uniform sampler2D fogTexture;
uniform sampler2D fogDistance;

uniform float hazeDensity;

void main(void)
{
    vec3 colour = texture(shadedColourTexture, textureCoords).rgb;
    float depth = texture(gDepth, textureCoords).r;
    vec3 surfaceView = ReconstructViewSpacePosition(gl_FragCoord.xy, depth, invProjectionMatrix, resolution);
    float surfaceDistance = length(surfaceView);

    ivec2 fogSize = textureSize(fogTexture, 0);
    vec2 fogPosition = textureCoords * vec2(fogSize) - 0.5;
    ivec2 fogBase = ivec2(floor(fogPosition));
    vec2 bilinear = fogPosition - vec2(fogBase);
    float tolerance = surfaceDistance * 0.02 + 0.05;

    vec4 fog = vec4(0.0);
    float totalWeight = 0.0;
    vec4 closestFog = vec4(0.0, 0.0, 0.0, 1.0);
    float closestDifference = 1e20;
    for (int i = 0; i < 4; i++)
    {
        ivec2 offset = ivec2(i & 1, i >> 1);
        ivec2 texel = clamp(fogBase + offset, ivec2(0), fogSize - 1);
        vec4 sampleFog = texelFetch(fogTexture, texel, 0);
        float difference = abs(texelFetch(fogDistance, texel, 0).r - surfaceDistance);

        vec2 axisWeights = mix(1.0 - bilinear, bilinear, vec2(offset));
        float weight = axisWeights.x * axisWeights.y * exp(-difference / tolerance);
        fog += sampleFog * weight;
        totalWeight += weight;

        if (difference < closestDifference)
        {
            closestDifference = difference;
            closestFog = sampleFog;
        }
    }
    fog = totalWeight > 1e-4 ? fog / totalWeight : closestFog;

    colour = colour * fog.a + fog.rgb;

    if (depth < 1.0)
    {
        float hazeTransmittance = exp(-hazeDensity * surfaceDistance);
        colour = mix(skyColour, colour, hazeTransmittance);
    }

    out_Colour = vec4(colour, 1.0);
}
