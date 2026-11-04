#version 420
#include globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 lightPositionViewSpace;
uniform vec3 lightColor;
uniform float lightRadius;
uniform float lightFill;        // Share of the light that reaches a surface however it faces, for what bounces off the things around it.

// World space metrics & dynamic clip planes
// NOTE: invViewMatrix is already defined in globals.glsl, do not re-declare it here
uniform vec3 lightPositionWorld;
uniform float shadowNearPlane;
uniform float shadowFarPlane;

uniform float shadowSoftness;   // Width of the filter per metre from the light.
uniform float shadowStrength;   // 1 fully shadowed, down to 0 as the light gives up its shadow map.

uniform bool isShadow;
uniform samplerCubeShadow shadowMap;

// Define a small array of 3D offsets to simulate PCF sampling in all directions
const vec3 gridOffsets[20] = vec3[](
   vec3( 1,  1,  1), vec3( 1, -1,  1), vec3(-1, -1,  1), vec3(-1,  1,  1), 
   vec3( 1,  1, -1), vec3( 1, -1, -1), vec3(-1, -1, -1), vec3(-1,  1, -1),
   vec3( 1,  1,  0), vec3( 1, -1,  0), vec3(-1, -1,  0), vec3(-1,  1,  0),
   vec3( 1,  0,  1), vec3( 1,  0, -1), vec3(-1,  0, -1), vec3(-1,  0,  1),
   vec3( 0,  1,  1), vec3( 0, -1,  1), vec3( 0, -1, -1), vec3( 0,  1, -1)
);

// How much of the light reaches this point, 0 to 1.
float calcShadow(vec3 fragPosViewSpace, vec3 normalViewSpace) {
    // Lifted off the surface a little, further the further from the light, so a surface doesn't shadow itself.
    vec3 worldPos = (invViewMatrix * vec4(fragPosViewSpace, 1.0)).xyz;
    vec3 fragToLight = worldPos - lightPositionWorld;
    float distanceToLight = length(fragToLight);
    fragToLight += mat3(invViewMatrix) * normalViewSpace * (0.02 + distanceToLight * 0.006);

    // The depth a cube face stores: its projection of the distance along the major axis.
    vec3 absVec = abs(fragToLight);
    float localZcomp = max(absVec.x, max(absVec.y, absVec.z));
    float normZ = (shadowFarPlane + shadowNearPlane) / (shadowFarPlane - shadowNearPlane) - (2.0 * shadowFarPlane * shadowNearPlane) / (localZcomp * (shadowFarPlane - shadowNearPlane));
    float depth = (normZ + 1.0) * 0.5;

    // On edge pixels, shaded once per sample, each sample takes a share of the filter.
#ifdef PER_SAMPLE
    int samples = 8;
    int first = msaaShadingSample() * 3;
#else
    int samples = 20;
    int first = 0;
#endif
    float filterWidth = distanceToLight * shadowSoftness;
    float lit = 0.0;
    for (int i = 0; i < samples; ++i) {
        vec3 samplingDir = fragToLight + gridOffsets[(first + i) % 20] * filterWidth;
        lit += texture(shadowMap, vec4(samplingDir, depth));
    }
    return lit / float(samples);
}

void main(void) {
    GBufferSample g = readGBufferSample();
    vec3 toLight = lightPositionViewSpace - g.position;
    float distanceToLight = length(toLight);
    float falloff = lightFalloff(distanceToLight, lightRadius);
    if (falloff <= 0.0) discard;

    Surface s = surfaceOf(g);
    vec3 radiance = lightColor * falloff;
    float visibility = isShadow ? mix(1.0, calcShadow(g.position, s.normal), shadowStrength) : 1.0;

    vec3 colour = lightSurface(s, toLight / max(distanceToLight, 1e-4), radiance * visibility);
    colour += s.albedo * radiance * (lightFill * g.ambient / LIGHT_PI);
    out_Colour = vec4(colour, 0.0);
}
