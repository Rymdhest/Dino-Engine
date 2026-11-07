#version 420

// Half-resolution ray-march through the ground fog layer.
// The camera looks down on a thin layer, so instead of a froxel volume each ray is clipped to the
// slab the fog can exist in and marched only there: a few metres of path, so even ~32 steps give
// sub-30cm sampling. Steps are jittered per pixel and frame and resolved by the temporal pass.
//
// Lighting: sky ambient, directional lights (cascaded shadows + self-shadowing through the billows),
// point lights (cube shadows) and spotlights (cone + shadow map). Point and spot attenuation, cones and
// shadow lookups match the deferred light shaders so fog and surfaces are lit by the same light.
//
// Fog is slow and is averaged over many frames, so at a high frame rate each frame need only march half of it, in tiles
// that take turns: it then gets as many rays a second as it would at a lower rate, and the others keep what they have.
//
// out_Fog:      rgb = in-scattered light, a = transmittance along the ray
// out_Distance: camera distance of the full-resolution depth sample this pixel represents

#include gBufferUtil.glsl
#include Globals.glsl
#include Lighting/lightingCalc.glsl
#include Wind.glsl
#include VolumetricFog_Tiles.glsl

layout (location = 0) out vec4 out_Fog;
layout (location = 1) out float out_Distance;

// Array sizes must match VolumetricFogRenderer.
const int MAX_DIRECTIONAL_LIGHTS = 4;
const int MAX_CASCADE_SLOTS = 5;
const int MAX_POINT_LIGHTS = 16;
const int MAX_SPOT_LIGHTS = 16;

uniform sampler2D gDepth;
uniform int restingTiles;       // 0 or 1, which of the tiles have this frame off, and -1 for none. Must match VolumetricFog_Temporal.frag.
uniform sampler3D noiseTexture;
uniform sampler2D terrainHeightMap;
uniform sampler2D windMap;
uniform float gustResponse;

// Directional lights: colour is pre-multiplied by the fog's scattering setting, direction points towards the light.
// Each light owns directionalCascadeCount[i] consecutive cascade slots from directionalCascadeStart[i], finest first.
uniform int directionalLightCount;
uniform vec3 directionalColours[4];
uniform vec3 directionalDirections[4];
uniform int directionalCascadeStart[4];
uniform int directionalCascadeCount[4];
uniform mat4 cascadeMatrices[5];
uniform sampler2DShadow cascadeShadowMaps[5];

// Point lights: xyz position + w radius, rgb colour + w shadow map index (-1 = none).
uniform int pointLightCount;
uniform vec4 pointPositionRadius[16];
uniform vec4 pointColourShadow[16];
uniform samplerCubeShadow pointShadowMaps[3];
uniform float pointShadowNear;
uniform float pointShadowFar;

// Spotlights: as point lights, plus xyz direction the light shines in + w cone cutoff cosine, and edge softness.
uniform int spotLightCount;
uniform vec4 spotPositionRadius[16];
uniform vec4 spotColourShadow[16];
uniform vec4 spotDirectionCutoff[16];
uniform float spotSoftness[16];
uniform mat4 spotShadowMatrices[4];
uniform sampler2DShadow spotShadowMaps[4];

uniform vec3 scatteringAlbedo;
uniform float ambientScattering;
uniform float anisotropy;

uniform float density;
uniform float layerHeight;
uniform float heightFalloff;
uniform float terrainFollow;
uniform float baseHeight;
uniform float coverage;
uniform float edgeSoftness;
uniform float clearingRadius;
uniform float clearingDepth;
uniform float pooling;
uniform float banks;

uniform float invNoiseScale;
uniform float noiseTexelSize;
uniform float turbulence;
uniform float detail;
uniform float topVariation;
uniform vec3 baseOffset;
uniform vec3 warpOffset;
uniform vec3 detailOffset;

uniform int maxSteps;
uniform float maxDistance;
uniform int frameIndex;

uniform int terrainHeightValid;
uniform mat4 terrainHeightMatrix;
uniform float terrainEyeY;
uniform float terrainDepthRange;

const float PI = 3.14159265359;
// Billows are flatter than they are wide.
const vec3 NOISE_STRETCH = vec3(1.0, 2.0, 1.0);
const float WARP_FREQUENCY = 0.5;
const float DETAIL_FREQUENCY = 3.0;
const float MIN_STEP_LENGTH = 0.15;
const int LIGHT_SAMPLES = 2;
const float HOLLOW_REACH = 14.0;    // Metres to the ground a place is compared with to call it a hollow.
const float HOLLOW_DEPTH = 2.5;     // Metres below that which count as all the hollow there is.
const float POOL_COVERAGE = 0.3;    // How much a hollow's fog closes up, and a rise's opens out.
const float POOL_HEIGHT = 0.6;      // And how much higher it stands in one, as a share of the layer.
const vec2 BANK_EDGES = vec2(0.4, 0.58);    // Where in the noise's broad swells the ground between banks ends and a bank begins.

// Set once per ray, as what the land does to the fog changes far more slowly than a ray crosses it, and so do gusts.
float rayCoverage;
float rayHeight;
vec3 rayPush;       // how far gusts have pushed the fog here (world metres)

float interleavedGradientNoise(vec2 pixel)
{
    pixel += 5.588238 * float(frameIndex % 64);
    return fract(52.9829189 * fract(dot(pixel, vec2(0.06711056, 0.00583715))));
}

float henyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * PI * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5));
}

// cosTheta is between the view ray and the direction towards the light.
// Phases of the multiple scattering octaves, each with half the eccentricity of the previous.
vec3 octavePhases(float cosTheta)
{
    return vec3(henyeyGreenstein(cosTheta, anisotropy),
                henyeyGreenstein(cosTheta, anisotropy * 0.5),
                henyeyGreenstein(cosTheta, anisotropy * 0.25));
}

// Multiple scattering approximation (Wrenninge): octaves with half the energy and half the extinction each,
// so light still reaches deep into dense fog instead of it turning grey.
float multipleScattering(vec3 phases, float opticalDepth)
{
    return phases.x * exp(-opticalDepth)
         + phases.y * 0.5 * exp(-opticalDepth * 0.5)
         + phases.z * 0.25 * exp(-opticalDepth * 0.25);
}

// Height of the fog layer's floor below this point, or `otherwise` past what was captured.
float groundHeightOr(vec2 xz, float otherwise)
{
    if (terrainHeightValid == 0 || terrainFollow <= 0.0) return otherwise;

    vec2 uv = (terrainHeightMatrix * vec4(xz.x, 0.0, xz.y, 1.0)).xy * 0.5 + 0.5;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return otherwise;

    float depth = textureLod(terrainHeightMap, uv, 0.0).r;
    if (depth >= 1.0) return otherwise; // nothing captured here
    return mix(baseHeight, terrainEyeY - depth * terrainDepthRange, terrainFollow);
}

float groundHeight(vec2 xz)
{
    return groundHeightOr(xz, baseHeight);
}

// How far the ground here lies below the ground around it, from -1 up on a rise to 1 down in a hollow.
float hollowAt(vec2 xz)
{
    float here = groundHeight(xz);
    float around = groundHeightOr(xz + vec2(HOLLOW_REACH, 0.0), here) + groundHeightOr(xz - vec2(HOLLOW_REACH, 0.0), here)
                 + groundHeightOr(xz + vec2(0.0, HOLLOW_REACH), here) + groundHeightOr(xz - vec2(0.0, HOLLOW_REACH), here);
    return clamp((around * 0.25 - here) / HOLLOW_DEPTH, -1.0, 1.0);
}

// How much of the layer's height is left here: the weather's fog sinks to the ankles around what's being looked at, so a fight is never lost in it.
float clearedHeight(vec2 xz)
{
    if (clearingDepth <= 0.0 || focusPoint.w <= 0.0) return 1.0;
    return 1.0 - clearingDepth * (1.0 - smoothstep(clearingRadius * 0.35, clearingRadius, distance(xz, focusPoint.xz)));
}

// ---------------------------------------------------------------- shadows
// Sampler arrays are only indexed by loop counters or by values read from uniforms with them, so the
// index is always dynamically uniform as GLSL requires.

float directionalShadow(int light, vec3 position)
{
    int first = directionalCascadeStart[light];
    int end = first + directionalCascadeCount[light];
    for (int c = 0; c < MAX_CASCADE_SLOTS; c++)
    {
        if (c < first || c >= end) continue;
        vec3 shadowCoord = (cascadeMatrices[c] * vec4(position, 1.0)).xyz * 0.5 + 0.5;
        if (all(greaterThan(shadowCoord, vec3(0.0))) && all(lessThan(shadowCoord, vec3(1.0))))
        {
            return textureLod(cascadeShadowMaps[c], vec3(shadowCoord.xy, shadowCoord.z - 0.0002), 0.0);
        }
    }
    return 1.0;
}

// Same depth reconstruction as Point_Light.frag. Cube shadow maps have no mipmaps, so the implicit
// LOD of texture() can't change the result (there is no textureLod for samplerCubeShadow in GLSL 4.20).
float pointShadow(int shadowIndex, vec3 lightToPosition)
{
    vec3 absolute = abs(lightToPosition);
    float majorAxis = max(absolute.x, max(absolute.y, absolute.z));
    float normalizedZ = (pointShadowFar + pointShadowNear) / (pointShadowFar - pointShadowNear)
                      - (2.0 * pointShadowFar * pointShadowNear) / (majorAxis * (pointShadowFar - pointShadowNear));
    return texture(pointShadowMaps[shadowIndex], vec4(lightToPosition, normalizedZ * 0.5 + 0.5));
}

float spotShadow(int shadowIndex, vec3 position)
{
    vec4 lightClip = spotShadowMatrices[shadowIndex] * vec4(position, 1.0);
    if (lightClip.w <= 0.0) return 1.0;
    vec3 shadowCoord = lightClip.xyz / lightClip.w * 0.5 + 0.5;
    return textureLod(spotShadowMaps[shadowIndex], shadowCoord, 0.0);
}

// Same cone falloff as Spot_Light.frag.
float spotCone(vec3 toLight, int light)
{
    float cutoff = spotDirectionCutoff[light].w;
    float theta = dot(toLight, -spotDirectionCutoff[light].xyz);
    if (theta < cutoff) return 0.0;
    float epsilon = max(spotSoftness[light] * (1.0 - cutoff), 1e-6);
    return smoothstep(0.0, 1.0, clamp((theta - cutoff) / epsilon, 0.0, 1.0));
}

// Bit i is set when the ray segment passes through light i's sphere of influence.
uint lightsTouchingSegment(vec3 rayOrigin, vec3 rayDirection, float tStart, float tEnd, bool spot)
{
    uint mask = 0u;
    int count = spot ? spotLightCount : pointLightCount;
    for (int i = 0; i < count; i++)
    {
        vec4 sphere = spot ? spotPositionRadius[i] : pointPositionRadius[i];
        vec3 toCenter = rayOrigin - sphere.xyz;
        float b = dot(toCenter, rayDirection);
        float c = dot(toCenter, toCenter) - sphere.w * sphere.w;
        float discriminant = b * b - c;
        if (discriminant <= 0.0) continue;
        float root = sqrt(discriminant);
        if (-b + root > tStart && -b - root < tEnd) mask |= 1u << uint(i);
    }
    return mask;
}

// Optical depth between a point in the fog and a local light, estimated from the local extinction. The fog
// thins out upwards, so only the part of the path below the layer's top counts, at half the local density.
float localLightOpticalDepth(float extinction, vec3 position, vec3 lightPosition, float distanceToLight, float layerTop)
{
    float insideFraction = 1.0;
    if (lightPosition.y > layerTop)
    {
        insideFraction = clamp((layerTop - position.y) / max(lightPosition.y - position.y, 1e-3), 0.0, 1.0);
    }
    return extinction * 0.5 * distanceToLight * insideFraction;
}

// ---------------------------------------------------------------- density

struct FogSample
{
    vec3 noisePosition;
    vec3 warp;
    float top;      // local height of the layer's top above its floor
    float ground;
    float cover;    // local coverage: broad swells of the noise part the fog into banks
};

FogSample prepareSample(vec3 position)
{
    FogSample s;
    s.noisePosition = (position - rayPush) * invNoiseScale * NOISE_STRETCH;
    vec4 low = textureLod(noiseTexture, s.noisePosition * WARP_FREQUENCY + warpOffset, 0.0);
    s.warp = (low.agb - 0.5) * turbulence;
    s.top = layerHeight * rayHeight * clearedHeight(position.xz) * max(1.0 + (low.a - 0.5) * 2.0 * topVariation, 0.05);
    s.ground = groundHeight(position.xz);
    s.cover = rayCoverage * mix(1.0, smoothstep(BANK_EDGES.x, BANK_EDGES.y, low.a), banks);
    return s;
}

// The fog drifts with the mean wind (baseOffset); where the wind locally runs ahead of or against the mean (gusts, blasts)
// it is pushed further. Capped at a quarter noise tile so blasts shove it rather than tear it.
vec3 windPush(vec2 xz)
{
    vec3 push = (sampleWindMap(windMap, xz).xyz - vec3(windMean.x, 0.0, windMean.y)) * gustResponse;
    float pushLength = length(push);
    float maxPush = 0.25 / invNoiseScale;
    return pushLength > maxPush ? push * (maxPush / pushLength) : push;
}

// Coverage-shaped billows; the layer thins out towards its top so only the tallest billows reach it.
float shapeDensity(vec3 noisePosition, vec3 warp, float heightAboveGround, float top, float cover)
{
    float profile = pow(clamp(1.0 - heightAboveGround / top, 0.0, 1.0), heightFalloff);
    if (profile <= 0.0) return 0.0;

    float billows = textureLod(noiseTexture, noisePosition + warp + baseOffset, 0.0).r;
    return clamp((billows - (1.0 - cover * profile)) / edgeSoftness, 0.0, 1.0);
}

float erode(float shape, vec3 noisePosition, float detailLod)
{
    if (detail <= 0.0) return shape;
    float wisps = textureLod(noiseTexture, noisePosition * DETAIL_FREQUENCY + detailOffset, detailLod).g;
    float erosion = wisps * detail;
    return clamp((shape - erosion) / max(1.0 - erosion, 1e-3), 0.0, 1.0);
}

// Ray parameters [tStart, tEnd] of the part of the ray that can contain fog.
bool clipToFogSlab(vec3 rayOrigin, vec3 rayDirection, float surfaceDistance, out float tStart, out float tEnd)
{
    float tFar = min(surfaceDistance, maxDistance);
    float maxTop = layerHeight * rayHeight * (1.0 + topVariation);

    // The floor is sampled where the ray ends and where it would enter the layer above that,
    // which brackets the terrain under the (mostly downward) ray.
    vec3 farPoint = rayOrigin + rayDirection * tFar;
    float floorFar = groundHeight(farPoint.xz);
    float slabTop = floorFar + maxTop;
    if (rayDirection.y < -1e-4)
    {
        float tEntry = clamp((slabTop - rayOrigin.y) / rayDirection.y, 0.0, tFar);
        float floorEntry = groundHeight((rayOrigin + rayDirection * tEntry).xz);
        float floorMid = groundHeight((rayOrigin + rayDirection * (tEntry + tFar) * 0.5).xz);
        slabTop = max(slabTop, max(floorEntry, floorMid) + maxTop);
    }
    slabTop += 0.25;

    if (rayOrigin.y <= slabTop)
    {
        tStart = 0.0;
        tEnd = rayDirection.y > 1e-4 ? min(tFar, (slabTop - rayOrigin.y) / rayDirection.y) : tFar;
    }
    else
    {
        if (rayDirection.y >= -1e-4) return false;
        tStart = (slabTop - rayOrigin.y) / rayDirection.y;
        tEnd = tFar;
    }
    return tEnd > tStart;
}

void main(void)
{
    // Pick one of the four full-resolution depths under this half-resolution pixel. Alternating the
    // nearest and the farthest in a checkerboard keeps both sides of depth edges represented, which
    // the bilateral upsample needs.
    ivec2 fullPixel = ivec2(gl_FragCoord.xy) * 2;
    ivec2 maxPixel = ivec2(resolution) - 1;
    bool pickFar = ((int(gl_FragCoord.x) + int(gl_FragCoord.y)) & 1) == 0;
    ivec2 chosenPixel = min(fullPixel, maxPixel);
    float chosenDepth = texelFetch(gDepth, chosenPixel, 0).r;
    for (int i = 1; i < 4; i++)
    {
        ivec2 pixel = min(fullPixel + ivec2(i & 1, i >> 1), maxPixel);
        float depth = texelFetch(gDepth, pixel, 0).r;
        if (pickFar ? depth > chosenDepth : depth < chosenDepth)
        {
            chosenDepth = depth;
            chosenPixel = pixel;
        }
    }

    vec3 surfaceView = ReconstructViewSpacePosition(vec2(chosenPixel) + 0.5, chosenDepth, invProjectionMatrix, resolution);
    vec3 surfaceWorld = (invViewMatrix * vec4(surfaceView, 1.0)).xyz;
    vec3 rayOrigin = viewPosWorld;
    float surfaceDistance = length(surfaceWorld - rayOrigin);
    vec3 rayDirection = (surfaceWorld - rayOrigin) / max(surfaceDistance, 1e-5);
    out_Distance = surfaceDistance;

    // VolumetricFog_Temporal.frag knows which tiles these are, and reads nothing from them.
    if (fogTileRests(ivec2(gl_FragCoord.xy), restingTiles))
    {
        out_Fog = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    rayCoverage = coverage;
    rayHeight = 1.0;
    if (pooling > 0.0 && density > 0.0)
    {
        float hollow = hollowAt((rayOrigin + rayDirection * min(surfaceDistance, maxDistance)).xz) * pooling;
        rayCoverage = clamp(coverage + hollow * POOL_COVERAGE, 0.0, 1.0);
        rayHeight = 1.0 + max(hollow, -0.5) * POOL_HEIGHT;
    }

    float tStart, tEnd;
    if (density <= 0.0 || !clipToFogSlab(rayOrigin, rayDirection, surfaceDistance, tStart, tEnd))
    {
        out_Fog = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    float segment = tEnd - tStart;
    int steps = clamp(int(ceil(segment / MIN_STEP_LENGTH)), 4, maxSteps);
    float stepLength = segment / float(steps);
    float jitter = interleavedGradientNoise(gl_FragCoord.xy);
    rayPush = windPush((rayOrigin + rayDirection * ((tStart + tEnd) * 0.5)).xz);

    // Mip for the detail noise so the high-frequency wisps don't alias at a distance.
    float pixelFootprint = (tStart + tEnd) * 0.5 * 2.0 / (resolution.y * projectionMatrix[1][1]);
    float detailTexel = noiseTexelSize / (invNoiseScale * DETAIL_FREQUENCY);
    float detailLod = max(log2(pixelFootprint / detailTexel), 0.0);

    vec3 directionalPhases[MAX_DIRECTIONAL_LIGHTS];
    for (int l = 0; l < directionalLightCount; l++)
    {
        directionalPhases[l] = octavePhases(dot(rayDirection, directionalDirections[l]));
    }
    uint pointMask = lightsTouchingSegment(rayOrigin, rayDirection, tStart, tEnd, false);
    uint spotMask = lightsTouchingSegment(rayOrigin, rayDirection, tStart, tEnd, true);

    vec3 ambient = skyColour * ambientScattering * 0.25;
    float lightStep = layerHeight * 0.25;

    vec3 scattered = vec3(0.0);
    float transmittance = 1.0;

    for (int i = 0; i < steps; i++)
    {
        vec3 position = rayOrigin + rayDirection * (tStart + (float(i) + jitter) * stepLength);
        FogSample s = prepareSample(position);
        float heightAboveGround = position.y - s.ground;

        float shape = shapeDensity(s.noisePosition, s.warp, heightAboveGround, s.top, s.cover);
        if (shape <= 0.0) continue;
        shape = erode(shape, s.noisePosition, detailLod);
        float extinction = shape * density;
        if (extinction <= 1e-5) continue;

        // Sky light, dimmed by the fog that lies above this point.
        float fogAbove = max(s.top - max(heightAboveGround, 0.0), 0.0);
        vec3 inScattered = ambient * exp(-density * coverage * fogAbove * 0.5);

        for (int l = 0; l < directionalLightCount; l++)
        {
            float visibility = directionalShadow(l, position);
            if (visibility <= 0.0) continue;

            // Self-shadowing: optical depth towards the light through the nearby billows.
            float opticalDepth = 0.0;
            for (int k = 0; k < LIGHT_SAMPLES; k++)
            {
                vec3 lightPosition = position + directionalDirections[l] * (lightStep * (float(k) + 0.5));
                vec3 lightNoisePosition = (lightPosition - rayPush) * invNoiseScale * NOISE_STRETCH;
                opticalDepth += shapeDensity(lightNoisePosition, s.warp, lightPosition.y - s.ground, s.top, s.cover) * density * lightStep;
            }
            inScattered += directionalColours[l] * (visibility * multipleScattering(directionalPhases[l], opticalDepth));
        }

        float layerTop = s.ground + s.top;

        for (int l = 0; l < pointLightCount; l++)
        {
            if ((pointMask & (1u << uint(l))) == 0u) continue;
            vec3 lightPosition = pointPositionRadius[l].xyz;
            vec3 toLightVector = lightPosition - position;
            float distanceSquared = dot(toLightVector, toLightVector);
            if (distanceSquared >= pointPositionRadius[l].w * pointPositionRadius[l].w) continue;

            float distanceToLight = sqrt(distanceSquared);
            float attenuation = lightFalloff(distanceToLight, pointPositionRadius[l].w);
            if (attenuation <= 0.0) continue;
            int shadowIndex = int(floor(pointColourShadow[l].w + 0.5));
            float visibility = shadowIndex >= 0 ? pointShadow(shadowIndex, -toLightVector) : 1.0;
            if (visibility <= 0.0) continue;

            vec3 toLight = toLightVector / max(distanceToLight, 1e-5);
            float opticalDepth = localLightOpticalDepth(extinction, position, lightPosition, distanceToLight, layerTop);
            inScattered += pointColourShadow[l].rgb * (attenuation * visibility * multipleScattering(octavePhases(dot(rayDirection, toLight)), opticalDepth));
        }

        for (int l = 0; l < spotLightCount; l++)
        {
            if ((spotMask & (1u << uint(l))) == 0u) continue;
            vec3 lightPosition = spotPositionRadius[l].xyz;
            vec3 toLightVector = lightPosition - position;
            float distanceSquared = dot(toLightVector, toLightVector);
            if (distanceSquared >= spotPositionRadius[l].w * spotPositionRadius[l].w) continue;

            float distanceToLight = sqrt(distanceSquared);
            vec3 toLight = toLightVector / max(distanceToLight, 1e-5);
            float cone = spotCone(toLight, l);
            if (cone <= 0.0) continue;
            float attenuation = lightFalloff(distanceToLight, spotPositionRadius[l].w);
            if (attenuation <= 0.0) continue;
            int shadowIndex = int(floor(spotColourShadow[l].w + 0.5));
            float visibility = shadowIndex >= 0 ? spotShadow(shadowIndex, position) : 1.0;
            if (visibility <= 0.0) continue;

            float opticalDepth = localLightOpticalDepth(extinction, position, lightPosition, distanceToLight, layerTop);
            inScattered += spotColourShadow[l].rgb * (cone * attenuation * visibility * multipleScattering(octavePhases(dot(rayDirection, toLight)), opticalDepth));
        }

        // Energy-conserving integration of the in-scattering over the step (Hillaire 2015).
        float stepTransmittance = exp(-extinction * stepLength);
        vec3 source = inScattered * scatteringAlbedo * extinction;
        scattered += transmittance * (source - source * stepTransmittance) / extinction;
        transmittance *= stepTransmittance;

        if (transmittance < 0.003)
        {
            transmittance = 0.0;
            break;
        }
    }

    out_Fog = vec4(scattered, transmittance);
}
