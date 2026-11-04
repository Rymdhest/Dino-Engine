#ifndef DINO_WATER_COMMON_GLSL
#define DINO_WATER_COMMON_GLSL

// Shading shared by every water surface (Water_Still.frag, Water_Ocean.frag). Those include Globals.glsl,
// gBufferUtil.glsl, Lighting/lightingCalc.glsl and Wind.glsl before this file.
//
// Water is drawn after the deferred lighting, over a copy of the lit scene (WaterRenderer):
// - what lies under the water is the scene seen through the surface, shifted by the surface slope as refraction
//   would, and dimmed and tinted by absorption along the light's path down to it and back up (Beer-Lambert),
// - light scattered back out of the water itself (single scattering, integrated analytically along the view path)
//   gives deep water its colour,
// - the surface reflects the sky (the same gradient sky.frag draws) and, optionally, what is on screen,
// - the sun and the other lights leave GGX glints; ripples too small to see on screen widen them (LEAN mapping),
// - waves focus sunlight into caustics on the bottom.
// Everything is in metres and the engine's light units, so water sits with the deferred lighting.

const float WATER_PI = 3.14159265359;
const float WATER_IOR = 1.333;
const float WATER_F0 = 0.0204;               // ((n - 1) / (n + 1))^2 for water
const float WATER_BASE_ROUGHNESS2 = 0.0004;  // GGX alpha^2 of the capillary waves finer than the ripple texture (alpha 0.02)
const float WATER_SKY_AMBIENT = 1.1;         // Sky light relative to skyColour, as LightingSettings.SkyFill has it for surfaces
const vec3 WATER_FOAM_ALBEDO = vec3(0.82, 0.86, 0.88);
const float WATER_PATTERN_RESOLUTION = 512.0;  // WaterTextureBaker.PatternResolution
const float WATER_CAUSTIC_ENCODE = 3.0;        // WaterTextureBaker.CausticEncodeScale
const mat2 CAUSTIC_ROTATION = mat2(0.6967067, 0.7173561, -0.7173561, 0.6967067);  // WaterTextureBaker.CausticRotation (0.8 rad)
const float CAUSTIC_SCALE = 1.37;                                                 // WaterTextureBaker.CausticScale

// Array sizes must match WaterRenderer.
const int WATER_MAX_DIRECTIONAL_LIGHTS = 4;
const int WATER_MAX_CASCADE_SLOTS = 5;
const int WATER_MAX_POINT_LIGHTS = 8;
const int WATER_MAX_SPOT_LIGHTS = 8;

uniform sampler2D sceneColour;
uniform sampler2D sceneDepth;
uniform sampler2D windMap;
uniform sampler3D rippleTexture;
uniform sampler2D patternTexture;
uniform sampler2D rippleSimulation;

uniform vec4 sceneRect;
uniform float skyStrength;

// Wind ripples: two layers of the ripple texture. xy pattern offset (tiles), z animation phase (0..1), w 1 / tile size (1/m).
uniform vec4 rippleLayerA;
uniform vec4 rippleLayerB;
uniform float rippleScale;
// Caustics: xy offset of layer A, zw of layer B (tiles); x 1 / tile size (1/m), y strength; average of the crossed layers.
uniform vec4 causticLayers;
uniform vec2 causticParameters;
uniform float causticMean;
uniform float refractionStrength;
uniform int reflectionSteps;
// Ripples from things moving through the water: xy world corner, zw size of the simulated area (z = 0: none).
uniform vec4 rippleSimRegion;
uniform float rippleSimStrength;
// Foam lace: xy pattern offset (tiles), z 1 / tile size (1/m).
uniform vec3 foamPattern;
// The terrain mesh seen from above (WaterShoreField's capture): xy world corner, zw size (z = 0: none); x world height
// at depth 0, y metres the depth covers.
uniform sampler2D terrainHeights;
uniform vec4 terrainRegion;
uniform vec2 terrainHeightRange;

// The surface being drawn.
uniform vec3 absorption;
uniform vec3 scattering;
uniform float windExposure;
uniform float edgeFade;
uniform float bedDarkening;

// Lights, as VolumetricFog_March.frag takes them: directions point towards the light, colours are radiance.
uniform int directionalLightCount;
uniform vec3 directionalColours[4];
uniform vec3 directionalDirections[4];
uniform int directionalCascadeStart[4];
uniform int directionalCascadeCount[4];
uniform mat4 cascadeMatrices[5];
uniform sampler2DShadow cascadeShadowMaps[5];

uniform int pointLightCount;
uniform vec4 pointPositionRadius[8];
uniform vec4 pointColour[8];

uniform int spotLightCount;
uniform vec4 spotPositionRadius[8];
uniform vec4 spotColour[8];
uniform vec4 spotDirectionCutoff[8];
uniform float spotSoftness[8];

// ---------------------------------------------------------------- lights

// Sampler arrays are only indexed by loop counters checked against uniforms, so the index is dynamically uniform.
float waterDirectionalShadow(int light, vec3 position)
{
    int first = directionalCascadeStart[light];
    int end = first + directionalCascadeCount[light];
    for (int c = 0; c < WATER_MAX_CASCADE_SLOTS; c++)
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

// Same cone falloff as Spot_Light.frag.
float waterSpotCone(vec3 toLight, int light)
{
    float cutoff = spotDirectionCutoff[light].w;
    float theta = dot(toLight, -spotDirectionCutoff[light].xyz);
    if (theta < cutoff) return 0.0;
    float epsilon = max(spotSoftness[light] * (1.0 - cutoff), 1e-6);
    return smoothstep(0.0, 1.0, clamp((theta - cutoff) / epsilon, 0.0, 1.0));
}

float waterFresnel(float cosine)
{
    return WATER_F0 + (1.0 - WATER_F0) * pow(1.0 - clamp(cosine, 0.0, 1.0), 5.0);
}

// GGX reflection of one light, with the engine's distribution and geometry terms (lightingCalc.glsl).
vec3 waterSpecular(vec3 N, vec3 V, vec3 L, float roughness, vec3 radiance)
{
    float NdotL = dot(N, L);
    if (NdotL <= 0.0) return vec3(0.0);
    vec3 H = normalize(V + L);
    float distribution = DistributionGGX(N, H, roughness);
    float geometry = GeometrySmith(N, V, L, roughness);
    float fresnel = waterFresnel(dot(H, V));
    // D G F / (4 NdotV NdotL), times NdotL.
    return radiance * (distribution * geometry * fresnel / (4.0 * max(dot(N, V), 1e-4)));
}

// The engine's GGX takes a roughness it squares into alpha.
float waterRoughness(float alphaSquared)
{
    return sqrt(sqrt(clamp(alphaSquared, WATER_BASE_ROUGHNESS2, 1.0)));
}

// The glints of the directional lights on a surface (wet sand uses it without the rest of the water).
vec3 waterDirectionalSpecular(vec3 position, vec3 N, vec3 V, float roughness)
{
    vec3 specular = vec3(0.0);
    for (int l = 0; l < directionalLightCount; l++)
    {
        float visibility = waterDirectionalShadow(l, position);
        if (visibility <= 0.0) continue;
        specular += waterSpecular(N, V, directionalDirections[l], roughness, directionalColours[l] * visibility);
    }
    return specular;
}

// The sky as sky.frag draws it, seen in direction `direction`.
vec3 waterSkyRadiance(vec3 direction)
{
    return skyColour * skyStrength * (1.0 - pow(max(direction.y, 0.0), 0.7) * 0.9);
}

// ---------------------------------------------------------------- surface detail

float waterPatternLod(vec2 xzDx, vec2 xzDy, float inverseTileSize)
{
    float footprint = max(length(xzDx), length(xzDy)) * inverseTileSize * WATER_PATTERN_RESOLUTION;
    return max(log2(max(footprint, 1e-6)), 0.0);
}

// One ripple layer: xy slope resolved at this pixel, z mean square slope averaged away below it (the mip's variance).
vec3 waterRippleLayer(vec4 layer, vec2 xz, vec2 xzDx, vec2 xzDy)
{
    vec3 uvw = vec3(xz * layer.w + layer.xy, layer.z);
    vec4 s = textureGrad(rippleTexture, uvw, vec3(xzDx * layer.w, 0.0), vec3(xzDy * layer.w, 0.0));
    return vec3(s.xy, max(s.z + s.w - dot(s.xy, s.xy), 0.0));
}

// Wind ripples at xz: xy slope (dh/dx, dh/dz), z mean square slope too fine to resolve (goes into the roughness).
// Their total mean square slope follows the local wind (Cox and Munk: 0.00512 per m/s; WaterWaves.RippleMeanSquareSlope),
// so gust patches roughen the water as they pass (cat's paws) and still air leaves it glassy.
vec3 waterRipples(vec2 xz, vec2 xzDx, vec2 xzDy, float windSpeed, out float meanSquareSlope)
{
    float onset = smoothstep(0.3, 1.5, windSpeed);
    meanSquareSlope = 0.00512 * rippleScale * windExposure * max(windSpeed, 0.0) * onset;
    if (meanSquareSlope <= 1e-7) return vec3(0.0);
    vec3 a = waterRippleLayer(rippleLayerA, xz, xzDx, xzDy);
    vec3 b = waterRippleLayer(rippleLayerB, xz, xzDx, xzDy);
    // 60% of the slope energy in the larger layer.
    vec2 slope = a.xy * sqrt(0.6 * meanSquareSlope) + b.xy * sqrt(0.4 * meanSquareSlope);
    return vec3(slope, (a.z * 0.6 + b.z * 0.4) * meanSquareSlope);
}

// Slope of the ripples things moving through the water have left (WaterRenderer's ripple simulation).
vec2 waterInteractionSlope(vec2 xz)
{
    if (rippleSimRegion.z <= 0.0) return vec2(0.0);
    vec2 uv = (xz - rippleSimRegion.xy) / rippleSimRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return vec2(0.0);
    return textureLod(rippleSimulation, uv, 0.0).zw * rippleSimStrength;
}

// Foam lace coverage 0..1 for an amount of foam: little foam shows only the strongest strands of the lace, a lot covers it.
float waterFoamCoverage(float amount, vec2 xz, float lod)
{
    if (amount <= 1e-3) return 0.0;
    float pattern = textureLod(patternTexture, xz * foamPattern.z + foamPattern.xy, lod).r;
    float threshold = 1.0 - clamp(amount, 0.0, 1.0);
    return smoothstep(threshold, threshold + 0.25, pattern) * clamp(amount * 3.0, 0.0, 1.0);
}

// Caustic intensity on the bottom (mean 1): two drifting layers crossed with min(), the way light focused by one set
// of waves is broken up by another.
float waterCaustics(vec2 xz, float lod)
{
    vec2 p = xz * causticParameters.x;
    float a = textureLod(patternTexture, p + causticLayers.xy, lod).g;
    float b = textureLod(patternTexture, CAUSTIC_ROTATION * p * CAUSTIC_SCALE + causticLayers.zw, lod).g;
    return min(a, b) * WATER_CAUSTIC_ENCODE / causticMean;
}

// ---------------------------------------------------------------- what lies beneath

// Height of the terrain's mesh at xz, false where none was captured. Water decides where it stands from this, not from
// the depth buffer: parallax mapping draws the ground up to half a metre below its mesh, and water filling those dips
// would spread far beyond the shoreline (and over the whole of a shallow puddle's outline).
bool waterTerrainHeight(vec2 xz, out float height)
{
    height = -1e4;
    if (terrainRegion.z <= 0.0) return false;
    vec2 uv = (xz - terrainRegion.xy) / terrainRegion.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return false;
    float depth = textureLod(terrainHeights, uv, 0.0).r;
    if (depth >= 1.0) return false;
    height = terrainHeightRange.x - depth * terrainHeightRange.y;
    return true;
}

struct WaterBed
{
    vec3 colour;       // the lit scene behind the surface
    vec3 position;     // world position of what is seen there
    float depth;       // metres of water above it
    float pathLength;  // metres the view travels through water to reach it
    bool open;         // nothing is behind the water: the view runs on into deep water
};

WaterBed waterSceneBehind(vec2 uv, vec3 surface)
{
    WaterBed bed;
    ivec2 pixel = clamp(ivec2(uv * resolution), ivec2(0), ivec2(resolution) - 1);
    float depth = texelFetch(sceneDepth, pixel, 0).r;
    bed.open = depth >= 1.0;
    bed.colour = textureLod(sceneColour, uv, 0.0).rgb;
    bed.position = bed.open ? surface : ReconstructWorldSpacePosition(vec2(pixel) + 0.5, depth, invProjectionMatrix, invViewMatrix, resolution);
    bed.depth = bed.open ? 1e4 : max(surface.y - bed.position.y, 0.0);
    bed.pathLength = bed.open ? 1e4 : length(bed.position - surface);
    return bed;
}

// The scene seen through the surface: the straight view shifted by the surface slope, by about as much as a ray
// bending at the surface (Snell) shifts at this depth. Where the shifted view lands on something standing out of
// the water (the shore, a post), the straight view is kept.
WaterBed waterRefractedBed(vec3 surface, vec3 normal, WaterBed straight)
{
    float shiftDepth = min(straight.depth, 2.0);
    vec3 shift = -vec3(normal.x, 0.0, normal.z) * ((1.0 - 1.0 / WATER_IOR) * shiftDepth * refractionStrength);
    vec3 viewShift = mat3(viewMatrix) * shift;
    float viewDistance = max(-(viewMatrix * vec4(surface, 1.0)).z, 0.1);
    vec2 uvShift = vec2(projectionMatrix[0][0] * viewShift.x, projectionMatrix[1][1] * viewShift.y) * (0.5 / viewDistance);
    vec2 uv = clamp(gl_FragCoord.xy / resolution + uvShift, sceneRect.xy, sceneRect.zw);
    WaterBed bed = waterSceneBehind(uv, surface);
    if (!bed.open && bed.position.y > surface.y) return straight;
    return bed;
}

// Caustic brightening (or darkening) of the bottom, as a factor - 1. Waves need some depth to focus light; deep water
// blurs and absorbs the pattern; glassy water has none.
float waterCausticModulation(WaterBed bed, float meanSquareSlope, float lod)
{
    if (bed.open || causticParameters.y <= 0.0) return 0.0;
    float focus = smoothstep(0.02, 0.35, bed.depth) * exp(-bed.depth * 0.35);
    float waviness = clamp(sqrt(meanSquareSlope / 0.015), 0.0, 1.5);
    float weight = causticParameters.y * focus * waviness;
    if (weight <= 1e-3) return 0.0;
    return weight * (waterCaustics(bed.position.xz, lod) - 1.0);
}

// ---------------------------------------------------------------- reflections

// What a reflected ray hits on screen: rgb colour, a how much to trust it (0 = use the sky). Only the copied part of
// the scene (sceneRect) can be reflected; rays leaving it fall back to the sky.
vec4 waterScreenReflection(vec3 origin, vec3 direction)
{
    vec3 previous = (viewMatrix * vec4(origin, 1.0)).xyz;
    vec3 stepDirection = mat3(viewMatrix) * direction;
    float stepLength = 0.35 + 0.02 * -previous.z;
    for (int i = 0; i < reflectionSteps; i++)
    {
        vec3 current = previous + stepDirection * stepLength;
        vec4 clip = projectionMatrix * vec4(current, 1.0);
        if (clip.w <= 0.0) break;
        vec2 uv = clip.xy / clip.w * 0.5 + 0.5;
        if (any(lessThan(uv, sceneRect.xy)) || any(greaterThan(uv, sceneRect.zw))) break;

        float depth = texelFetch(sceneDepth, ivec2(uv * resolution), 0).r;
        if (depth < 1.0)
        {
            float behind = ReconstructViewSpacePosition(uv * resolution, depth, invProjectionMatrix, resolution).z - current.z;
            if (behind > 0.0 && behind < stepLength * 2.0 + 0.3)
            {
                // Bisect between the last point in front of the scene and this one behind it.
                vec3 front = previous;
                vec3 back = current;
                for (int r = 0; r < 4; r++)
                {
                    vec3 middle = (front + back) * 0.5;
                    vec4 middleClip = projectionMatrix * vec4(middle, 1.0);
                    vec2 middleUV = middleClip.xy / middleClip.w * 0.5 + 0.5;
                    float middleDepth = texelFetch(sceneDepth, clamp(ivec2(middleUV * resolution), ivec2(0), ivec2(resolution) - 1), 0).r;
                    float middleScene = ReconstructViewSpacePosition(middleUV * resolution, middleDepth, invProjectionMatrix, resolution).z;
                    if (middleDepth < 1.0 && middleScene > middle.z) back = middle;
                    else front = middle;
                }
                vec4 hitClip = projectionMatrix * vec4(back, 1.0);
                vec2 hitUV = clamp(hitClip.xy / hitClip.w * 0.5 + 0.5, sceneRect.xy, sceneRect.zw);
                vec2 edge = smoothstep(0.0, 0.08, hitUV - sceneRect.xy) * smoothstep(0.0, 0.08, sceneRect.zw - hitUV);
                return vec4(textureLod(sceneColour, hitUV, 0.0).rgb, edge.x * edge.y);
            }
        }
        previous = current;
        stepLength *= 1.4;
    }
    return vec4(0.0);
}

// ---------------------------------------------------------------- shading

struct WaterShading
{
    vec3 water;  // the water surface: what is beneath, the water's own colour, reflections and glints
    vec3 foam;   // foam on this surface, lit
};

// Adds one light: its glint, the light it sends into the water to scatter back out, and its light on foam.
void waterAddLight(vec3 N, vec3 V, vec3 L, vec3 radiance, float roughness, float viewCosine, vec3 extinction, float path,
                   inout vec3 specular, inout vec3 inScattered, inout vec3 foamLight)
{
    specular += waterSpecular(N, V, L, roughness, radiance);
    float NdotL = max(dot(N, L), 0.0);
    foamLight += radiance * (NdotL / WATER_PI);
    if (NdotL <= 0.0) return;
    // Light entering the water reaches depth z after z / lightCosine metres; seen along the view ray at distance s it
    // has come s * (1 + viewCosine / lightCosine) metres in all. Integrated over the view path in closed form.
    float lightCosine = sqrt(1.0 - (1.0 - NdotL * NdotL) / (WATER_IOR * WATER_IOR));
    float k = 1.0 + viewCosine / max(lightCosine, 0.05);
    inScattered += radiance * (NdotL * (1.0 - waterFresnel(NdotL))) * (1.0 - exp(-extinction * (path * k))) / k;
}

// The water surface at `position` with normal N, seen from direction V (towards the camera), over `bed`.
// alphaSquared is the GGX roughness, causticModulation the factor - 1 the bottom is brightened by in direct light,
// extraScattering particles stirred up in the water on top of its own scattering (per metre).
WaterShading waterShade(vec3 position, vec3 N, float alphaSquared, vec3 V, WaterBed bed, float causticModulation, vec3 extraScattering)
{
    float NdotV = max(dot(N, V), 1e-4);
    float fresnel = waterFresnel(NdotV);
    float roughness = waterRoughness(alphaSquared);
    vec3 totalScattering = scattering + extraScattering;
    vec3 extinction = max(absorption + totalScattering, vec3(1e-5));
    vec3 albedo = totalScattering / extinction;
    float viewCosine = sqrt(1.0 - (1.0 - NdotV * NdotV) / (WATER_IOR * WATER_IOR));
    float path = bed.pathLength;

    vec3 specular = vec3(0.0);
    vec3 inScattered = vec3(0.0);
    vec3 foamLight = skyColour * WATER_SKY_AMBIENT;
    float sunVisibility = 0.0;

    for (int l = 0; l < directionalLightCount; l++)
    {
        float visibility = waterDirectionalShadow(l, position);
        if (l == 0) sunVisibility = visibility;
        if (visibility <= 0.0) continue;
        waterAddLight(N, V, directionalDirections[l], directionalColours[l] * visibility, roughness, viewCosine, extinction, path,
                      specular, inScattered, foamLight);
    }
    for (int l = 0; l < pointLightCount; l++)
    {
        vec3 toLight = pointPositionRadius[l].xyz - position;
        float distanceSquared = dot(toLight, toLight);
        if (distanceSquared >= pointPositionRadius[l].w * pointPositionRadius[l].w) continue;
        float attenuation = lightFalloff(sqrt(distanceSquared), pointPositionRadius[l].w);
        if (attenuation <= 0.0) continue;
        waterAddLight(N, V, toLight * inversesqrt(max(distanceSquared, 1e-8)), pointColour[l].rgb * attenuation, roughness, viewCosine,
                      extinction, path, specular, inScattered, foamLight);
    }
    for (int l = 0; l < spotLightCount; l++)
    {
        vec3 toLight = spotPositionRadius[l].xyz - position;
        float distanceSquared = dot(toLight, toLight);
        if (distanceSquared >= spotPositionRadius[l].w * spotPositionRadius[l].w) continue;
        vec3 L = toLight * inversesqrt(max(distanceSquared, 1e-8));
        float cone = waterSpotCone(L, l);
        if (cone <= 0.0) continue;
        float attenuation = lightFalloff(sqrt(distanceSquared), spotPositionRadius[l].w);
        if (attenuation <= 0.0) continue;
        waterAddLight(N, V, L, spotColour[l].rgb * (cone * attenuation), roughness, viewCosine, extinction, path,
                      specular, inScattered, foamLight);
    }

    // Sky light enters from the whole sky; 0.8 is the average vertical cosine of its refracted rays.
    float kSky = 1.0 + viewCosine / 0.8;
    inScattered += skyColour * (WATER_SKY_AMBIENT * WATER_PI) * (1.0 - exp(-extinction * (path * kSky))) / kSky;
    // Isotropic phase function, and the share of the light the water scatters rather than absorbs.
    inScattered *= albedo / (4.0 * WATER_PI);

    // The bottom: wet (darker), focused light where the sun reaches it, then absorbed on the way down and back up.
    vec3 bedColour = bed.colour * (1.0 - bedDarkening) * max(1.0 + causticModulation * sunVisibility, 0.0);
    vec3 transmitted = bedColour * exp(-extinction * (path + bed.depth)) + inScattered;

    vec3 R = reflect(-V, N);
    R.y = abs(R.y);
    vec3 reflection = waterSkyRadiance(R);
    if (reflectionSteps > 0 && alphaSquared < 0.05)
    {
        vec4 screen = waterScreenReflection(position, R);
        reflection = mix(reflection, screen.rgb, screen.a);
    }

    WaterShading shading;
    shading.water = (1.0 - fresnel) * transmitted + fresnel * reflection + specular;
    shading.foam = WATER_FOAM_ALBEDO * foamLight;
    return shading;
}

#endif
