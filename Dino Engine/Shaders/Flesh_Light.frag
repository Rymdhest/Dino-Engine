#version 420

// The sun through flesh wherever it's thin enough, from its far side: a crest, an ear, a web between toes, the edge of a limb (SUBSURFACE.md). Its own shadow says nothing of
// that, as it's what's in the way, so what gets through is measured in a map of flesh alone from the sun, which knows where the light went in (FleshDepth,
// Lighting/SunShadow.glsl). Added over the ambient pass on characters alone, by their stencil bit, so that nothing else carries its depth reads and exponentials. Drawn per
// pixel and per sample (AmbientLightRenderer.DrawFlesh). With --render sssdebug=2 it draws what got through instead, white where all of it would.

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sun_Visibility.glsl
#include Lighting/SunShadow.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 sunDirectionViewSpace;
uniform vec3 sunColour;
uniform sampler2D fleshDepthMap;
uniform mat4 fleshSpaceMatrix;

// How far a surface has to face away from the sun, as a cosine, before what comes through flesh shows in full. Its normal is the shading one, bumps and all, and a hard cut
// would follow every bump of a back turned nearly edge on.
const float SSS_FACING_AWAY = 0.2;

void main(void)
{
    GBufferSample g = readGBufferSample();
    if (g.depth >= 1.0) discard;
    Surface s = surfaceOf(g);
    if (s.scatterKind != SSS_FLESH || s.scatter <= 0.0) discard;

    // Only what faces away from the sun lets any out: what faces it at all is lit by it, and the ambient pass has that, carried round the terminator as flesh carries it.
    bool picture = sssSwitches.z > 1.5;
    SssProfile flesh = sssProfileOf(SSS_FLESH);
    float away = -dot(s.normal, sunDirectionViewSpace);
    if (away <= 0.0 && !picture) discard;

    vec3 through = sunFleshTransmittance(fleshDepthMap, fleshSpaceMatrix, g.position, sunDirectionViewSpace, sssExtinction(s, flesh));
    if (picture)
    {
        out_Colour = vec4(through, 0.0);
        return;
    }
    through *= smoothstep(0.0, SSS_FACING_AWAY, away);
    float clouds = weatherSunVisibility((invViewMatrix * vec4(g.position, 1.0)).xz);
    out_Colour = vec4(sssThickLight(s, flesh, through) * sunColour * clouds, 0.0);
}
