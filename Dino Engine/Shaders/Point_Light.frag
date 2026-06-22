#version 420
#include globals.glsl
#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl

layout (location = 0) out vec4 out_Colour;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D gAlbedo;
uniform sampler2D gMaterials;

uniform vec3 lightPositionViewSpace;
uniform vec3 lightColor;
uniform vec3 attenuation;
uniform float lightAmbient;

// World space metrics & dynamic clip planes
// NOTE: invViewMatrix is already defined in globals.glsl, do not re-declare it here
uniform vec3 lightPositionWorld;
uniform float shadowNearPlane;
uniform float shadowFarPlane;

uniform float shadowSmoothRadius;

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

float calcShadow(vec3 fragPosViewSpace) {
    // 1. Reconstruct actual fragment World Space position
    vec3 worldPos = (invViewMatrix * vec4(fragPosViewSpace, 1.0)).xyz;
    
    // 2. Calculate look direction vector in pure World Space
    vec3 fragToLight = worldPos - lightPositionWorld;
    
    // 3. Find major axis linear distance
    vec3 absVec = abs(fragToLight);
    float localZcomp = max(absVec.x, max(absVec.y, absVec.z));
    
    // 4. Map distance to exact non-linear range matching your projection matrix
    float normZ = (shadowFarPlane + shadowNearPlane) / (shadowFarPlane - shadowNearPlane) - (2.0 * shadowFarPlane * shadowNearPlane) / (localZcomp * (shadowFarPlane - shadowNearPlane));
    float depth = (normZ + 1.0) * 0.5;
    
    // 5. PCF Sampling
    float shadow = 0.0;
    float diskRadius = shadowSmoothRadius; // Softness radius
    int samples = 20;

    for (int i = 0; i < samples; ++i) {
        // Offset the 3D sampling vector
        vec3 samplingDir = fragToLight + gridOffsets[i] * diskRadius;
        
        // Query hardware comparison sampler using the same depth
        shadow += texture(shadowMap, vec4(samplingDir, depth));
    }
    
    // Average the samples and invert (1.0 = shadow, 0.0 = lit)
    return 1.0 - (shadow / float(samples));
}

void main(void) {
    vec2 textureCoords = gl_FragCoord.xy / resolution;
    vec3 position = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolution);
    vec4 normalBuffer = texture(gNormal, textureCoords).xyzw;
    vec4 albedo = texture(gAlbedo, textureCoords).rgba;
    vec3 materialBuffer = texture(gMaterials, textureCoords).rgb;
    
    vec3 normal = unCompressNormal(normalBuffer.xyz);
    float ambient = normalBuffer.w;
    float roughness = materialBuffer.r;
    float metallic = materialBuffer.b;
    float materialTransparancy = albedo.a;
    
    vec3 viewDir = normalize(-position);
    vec3 lightDir = normalize(lightPositionViewSpace - position);  
    float attenuationFactor = calcAttunuation(lightPositionViewSpace, position, attenuation);

    float lightFactor = 1.0;
    if (isShadow) {
        // Correctly apply shadow factor: 1.0 (fully lit) - shadow_amount (0.0 to 1.0)
        lightFactor = 1.0 - calcShadow(position);
    }

    vec3 color = getLightPBR(albedo.rgb, normal, roughness, metallic, lightColor, attenuationFactor, lightAmbient * ambient, viewDir, lightDir, lightFactor, materialTransparancy, 0.0);

    out_Colour = vec4(color, 0.0);
}