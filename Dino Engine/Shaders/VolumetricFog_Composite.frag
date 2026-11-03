#version 420

// Applies the half-resolution fog to the full-resolution multisampled scene. The four nearest fog texels are
// weighted by how close their surface distance is to this sample's (joint bilateral upsample), so
// fog doesn't bleed across depth edges such as tree canopies above fogged ground.
// Distance haze over the whole view is added analytically on top.
// Blended with (one, source alpha): the output is the light the fog and haze add (rgb) and the share of the scene that
// still shows through them (a). Drawn per pixel and per sample (MsaaRenderer.DrawSplit), so every sample of an edge
// pixel gets the fog of its own distance.

#include gBufferUtil.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2DMS gDepth;
uniform sampler2D fogTexture;
uniform sampler2D fogDistance;

uniform float hazeDensity;

void main(void)
{
    float depth = texelFetch(gDepth, ivec2(gl_FragCoord.xy), msaaShadingSample()).r;
    vec3 surfaceView = ReconstructViewSpacePosition(msaaShadingCoord(), depth, invProjectionMatrix, resolution);
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

    // colour * fog.a + fog.rgb, then mixed towards the sky colour by the haze in front of the surface.
    float hazeTransmittance = depth < 1.0 ? exp(-hazeDensity * surfaceDistance) : 1.0;
    out_Colour = vec4(fog.rgb * hazeTransmittance + skyColour * (1.0 - hazeTransmittance), fog.a * hazeTransmittance);
}
