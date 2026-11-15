#version 430
#include globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Halls.glsl
#include Lighting/PointLightTiles.glsl

// Every point light at once, over the whole screen (PointLightRenderer): each pixel reads the G-buffer once and adds up the lights its tile has
// (Lighting/PointLight_Cull.comp), each lit exactly as a light's own volume once lit it. Spotlights with no shadow come too, with their cone as Spot_Light.frag has it.

layout (location = 0) out vec4 out_Colour;

uniform int tilesAcross;

uniform float shadowNearPlane;
uniform float shadowFarPlane;
uniform float shadowSoftness;   // Width of the filter per metre from the light.
uniform samplerCubeArrayShadow shadowMaps;

// A small array of 3D offsets to sample the map round the point in all directions: the corners of a cube first, then the middles of its edges.
const vec3 gridOffsets[20] = vec3[](
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1),
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1), vec3(-1,  0,  1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);

// The same offsets with every other corner of the cube first, (1, 1, 1), (-1, -1, 1), (1, -1, -1) and (-1, 1, -1), for a pixel's filter, which stops after them
// where they agree.
const vec3 pixelOffsets[20] = vec3[](
   vec3( 1,  1,  1), vec3(-1, -1,  1), vec3( 1, -1, -1), vec3(-1,  1, -1),
   vec3( 1, -1,  1), vec3(-1,  1,  1), vec3( 1,  1, -1), vec3(-1, -1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1), vec3(-1,  0,  1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);

// How much of a light reaches a point, 0 to 1.
float calcShadow(vec3 worldPos, vec3 normalWorld, vec3 lightWorld, float layer)
{
    // Lifted off the surface a little, further the further from the light, so a surface doesn't shadow itself.
    vec3 fragToLight = worldPos - lightWorld;
    float distanceToLight = length(fragToLight);
    fragToLight += normalWorld * (0.02 + distanceToLight * 0.006);

    // The depth a cube face stores: its projection of the distance along the major axis.
    vec3 absVec = abs(fragToLight);
    float localZcomp = max(absVec.x, max(absVec.y, absVec.z));
    float normZ = (shadowFarPlane + shadowNearPlane) / (shadowFarPlane - shadowNearPlane) - (2.0 * shadowFarPlane * shadowNearPlane) / (localZcomp * (shadowFarPlane - shadowNearPlane));
    float depth = (normZ + 1.0) * 0.5;
    float filterWidth = distanceToLight * shadowSoftness;

#ifdef PER_SAMPLE
    // On edge pixels, shaded once per sample, each sample takes a share of the filter, and stops at half of it where that half agrees.
    int first = msaaShadingSample() * 3;
    float lit = 0.0;
    for (int i = 0; i < 4; ++i)
    {
        lit += texture(shadowMaps, vec4(fragToLight + gridOffsets[(first + i) % 20] * filterWidth, layer), depth);
    }
    if (lit <= 0.0 || lit >= 4.0) return lit / 4.0;
    for (int i = 4; i < 8; ++i)
    {
        lit += texture(shadowMaps, vec4(fragToLight + gridOffsets[(first + i) % 20] * filterWidth, layer), depth);
    }
    return lit / 8.0;
#else
    // Four corners of the cube, every other one, span the whole filter, and seen from the light they fall where the other four do, as those differ from them only
    // along the way the face looks: where all four agree the point is wholly in or out of the shadow, and the rest would only say so again.
    float lit = 0.0;
    for (int i = 0; i < 4; ++i)
    {
        lit += texture(shadowMaps, vec4(fragToLight + pixelOffsets[i] * filterWidth, layer), depth);
    }
    if (lit <= 0.0 || lit >= 4.0) return lit / 4.0;
    for (int i = 4; i < 20; ++i)
    {
        lit += texture(shadowMaps, vec4(fragToLight + pixelOffsets[i] * filterWidth, layer), depth);
    }
    return lit / 20.0;
#endif
}

void main(void)
{
    GBufferSample g = locateGBufferSample();
    if (g.depth >= 1.0) discard;

    ivec2 tile = ivec2(gl_FragCoord.xy) / POINT_TILE;
    int base = pointTileBits(tile.y * tilesAcross + tile.x);
    vec3 worldPos = (invViewMatrix * vec4(g.position, 1.0)).xyz;
    uint rooms = hallRoomsOf(worldPos);

    bool finished = false;
    Surface s;
    vec3 normalWorld = vec3(0.0);
    vec3 colour = vec3(0.0);
    for (int w = 0; w < POINT_WORDS; w++)
    {
        uint bits = pointTiles[base + w];
        while (bits != 0u)
        {
            int i = w * 32 + findLSB(bits);
            bits &= bits - 1u;

            // Only where it reaches is the rest of a light read.
            vec4 positionRadius = pointLights[i].positionRadius;
            vec3 toLight = positionRadius.xyz - g.position;
            float distanceToLight = length(toLight);
            float falloff = lightFalloff(distanceToLight, positionRadius.w);
            if (falloff <= 0.0) continue;
            vec4 cone = pointLights[i].cone;
            vec4 shadowHalls = pointLights[i].shadowHalls;
            if (cone.w > -1.5)
            {
                // Spot_Light.frag's calcSoftEdge.
                float theta = dot(toLight / max(distanceToLight, 1e-4), cone.xyz);
                if (theta < cone.w) continue;
                float epsilon = max(shadowHalls.z * (1.0 - cone.w), 1e-6);
                falloff *= smoothstep(0.0, 1.0, clamp((theta - cone.w) / epsilon, 0.0, 1.0));
                if (falloff <= 0.0) continue;
            }
            // A hall keeps its own light in, and everyone else's out (Halls.glsl).
            if (uint(shadowHalls.y + 0.5) != rooms) continue;
            vec4 colourFill = pointLights[i].colourFill;

            if (!finished)
            {
                finishGBufferSample(g);
                s = surfaceOf(g);
                normalWorld = mat3(invViewMatrix) * s.normal;
                finished = true;
            }

            vec3 L = toLight / max(distanceToLight, 1e-4);
            vec3 radiance = colourFill.rgb * falloff;
            colour += s.albedo * radiance * (colourFill.a * g.ambient / LIGHT_PI);

            // A surface facing away from the light takes none of it unless light gets in under it, so neither the light nor its shadow can matter.
            if (dot(s.normal, L) <= 0.0 && !(s.scatter > 0.0 && sssSwitches.x > 0.0)) continue;
            float visibility = 1.0;
            if (shadowHalls.x > 0.0)
            {
                vec4 worldShadow = pointLights[i].worldShadow;
                if (worldShadow.w >= 0.0) visibility = mix(1.0, calcShadow(worldPos, normalWorld, worldShadow.xyz, worldShadow.w), shadowHalls.x);
            }
            colour += lightSurface(s, L, radiance * visibility);
        }
    }
    if (!finished) discard;
    out_Colour = vec4(colour, 0.0);
}
