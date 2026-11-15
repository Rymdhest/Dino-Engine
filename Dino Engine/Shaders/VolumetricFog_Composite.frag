#version 420

// Applies the half-resolution fog to the full-resolution multisampled scene. The four nearest fog texels are
// weighted by how close their surface distance is to this sample's (joint bilateral upsample), so
// fog doesn't bleed across depth edges such as tree canopies above fogged ground.
// Distance haze over the whole view is added analytically on top.
// Blended with (one, source alpha): the output is the light the fog and haze add (rgb) and the share of the scene that
// still shows through them (a). Drawn per pixel and per sample (MsaaRenderer.DrawSplit), so every sample of an edge
// pixel gets the fog of its own distance.
//
// The haze is the same everywhere and the sky's one colour for the game's camera, which looks down on a few dozen metres. For a view that looks out across a land (the picture
// behind the main menu) it's what says how far off anything is, so there it lies low, thinning with height, and takes the colour of the sky along the horizon behind whatever it's in
// front of: a ridge is paler than the one before it, a mountain goes into the sky it stands against, and all of it brightens towards a low sun, as the sky does.

#include gBufferUtil.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform sampler2DMS gDepth;
uniform sampler2D fogTexture;
uniform sampler2D fogDistance;

uniform float hazeDensity;
uniform vec4 hazeShape;     // x 1 / the height the haze thins to 1/e over (0: the same at any height), y the height it's as thick as hazeDensity at, z how far it takes the horizon's colour, w the most it takes away

const float HAZE_SKY_LOD = 3.0;     // Of the sky map: the light of all the air out that way, not of one cloud.

// How much haze lies between the eye and a surface that far along a ray: its density summed along the way, which thins as the ray climbs.
float hazeDepth(vec3 direction, float surfaceDistance)
{
    if (hazeShape.x <= 0.0) return hazeDensity * surfaceDistance;
    float atEye = hazeDensity * exp(-clamp((viewPosWorld.y - hazeShape.y) * hazeShape.x, -4.0, 40.0));
    float climb = clamp(direction.y * hazeShape.x * surfaceDistance, -20.0, 40.0);
    // (1 - e^-c) / c, which is 1 along a level ray.
    float along = abs(climb) > 1e-3 ? (1.0 - exp(-climb)) / climb : 1.0 - climb * 0.5;
    return atEye * surfaceDistance * along;
}

vec3 hazeColour(vec3 direction)
{
    if (hazeShape.z <= 0.0) return skyColour;
    vec3 level = normalize(vec3(direction.x, max(direction.y, 0.0) * 0.5 + 0.04, direction.z));
    return mix(skyColour, textureLod(skyMap, skyMapCoord(level), HAZE_SKY_LOD).rgb, hazeShape.z);
}

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

    // colour * fog.a + fog.rgb, then mixed towards the haze's colour by the haze in front of the surface. The sky has none: it's what the haze is going towards.
    float hazeTransmittance = 1.0;
    vec3 haze = skyColour;
    if (depth < 1.0)
    {
        vec3 direction = mat3(invViewMatrix) * (surfaceView / max(surfaceDistance, 1e-5));
        hazeTransmittance = max(exp(-hazeDepth(direction, surfaceDistance)), 1.0 - hazeShape.w);
        haze = hazeColour(direction);
    }
    out_Colour = vec4(fog.rgb * hazeTransmittance + haze * (1.0 - hazeTransmittance), fog.a * hazeTransmittance);
}
