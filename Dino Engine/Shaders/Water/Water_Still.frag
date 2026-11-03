#version 420

// Still water: puddles, ponds, lakes. A flat surface at the body's level; the depth test against the ground gives it
// its shoreline, so its depth is the quad's own (early depth rejection stays on). Wind ripples, ripples from things
// wading through it, refraction, absorption, scattering, reflections and caustics come from WaterCommon.glsl.

#include Globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Wind.glsl
#include Water/WaterShape.glsl
#include Water/WaterCommon.glsl

in vec3 fragWorldPosition;
in vec2 fragLocalPosition;

layout (location = 0) out vec4 out_Colour;

uniform int bodyShape;
uniform vec2 bodyHalfExtents;
uniform float bodyEdgeSoftness;
uniform float bodyEdgeNoise;
uniform float bodyInvNoiseScale;
uniform float bodyEdgeFoam;

void main(void)
{
    vec3 position = fragWorldPosition;
    // Derivatives up front, in uniform control flow; every lookup below takes explicit gradients or levels.
    vec2 xzDx = dFdx(position.xz);
    vec2 xzDy = dFdy(position.xz);

    float coverage = waterShapeCoverage(bodyShape, fragLocalPosition, bodyHalfExtents, bodyEdgeSoftness, bodyEdgeNoise, bodyInvNoiseScale, position.xz);
    if (coverage <= 0.0) discard;

    // No water where the ground's mesh stands above the surface, even if parallax relief dips below it.
    float groundHeight;
    float groundDepth = waterTerrainHeight(position.xz, groundHeight) ? position.y - groundHeight : 1e4;
    if (groundDepth <= 0.0) discard;

    WaterBed straight = waterSceneBehind(gl_FragCoord.xy / resolution, position);
    if (!straight.open && straight.position.y >= position.y) discard;
    // Depth for the shoreline fade: the mesh's gives a smooth outline, the scene's fades around things standing in the water.
    float edgeDepth = min(straight.depth, groundDepth);

    vec3 V = normalize(viewPosWorld - position);
    float windSpeed = length(sampleWindMap(windMap, position.xz).xz);
    float rippleSlope;
    vec3 ripples = waterRipples(position.xz, xzDx, xzDy, windSpeed, rippleSlope);
    vec2 slope = ripples.xy + waterInteractionSlope(position.xz);
    vec3 N = normalize(vec3(-slope.x, 1.0, -slope.y));
    float alphaSquared = WATER_BASE_ROUGHNESS2 + ripples.z;

    WaterBed bed = waterRefractedBed(position, N, straight);
    float causticLod = waterPatternLod(xzDx, xzDy, causticParameters.x);
    float caustics = waterCausticModulation(bed, rippleSlope + dot(slope, slope) * 0.5, causticLod);
    WaterShading shading = waterShade(position, N, alphaSquared, V, bed, caustics, vec3(0.0));

    // Fade in over the first few centimetres of depth, and over the outline's soft edge.
    float edge = smoothstep(0.0, max(edgeFade, 1e-3), edgeDepth) * coverage;
    vec3 colour = mix(straight.colour, shading.water, edge);

    if (bodyEdgeFoam > 0.0)
    {
        float waterline = (1.0 - smoothstep(0.0, 0.12, edgeDepth)) * bodyEdgeFoam * coverage;
        colour = mix(colour, shading.foam, waterFoamCoverage(waterline, position.xz, waterPatternLod(xzDx, xzDy, foamPattern.z)));
    }

    out_Colour = vec4(colour, 1.0);
}
