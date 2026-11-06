#version 420

#include Globals.glsl

in vec3 fragColor;
in vec2 fragUV;
in vec3 viewSpacePos;
in mat3 normalTBN;
in float textureIndex;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;
uniform vec4 hitFlash;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

#include textureUtil.glsl
#include procedural/fastHash.glsl
#include gBufferUtil.glsl
#include AntiAliasing/Msaa.glsl
#include FocusCutout.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Wetness.glsl

const float HIT_SHINE = 7.0;

void main() {

	vec3 worldPosition = (invViewMatrix * vec4(viewSpacePos, 1.0)).xyz;
	// Characters' own shader keeps them whole; for everything else this opens the view to the player.
#ifndef CHARACTER
	applyFocusCutout(worldPosition);
#endif

	vec2 uvDX = dFdx(fragUV);
	vec2 uvDY = dFdy(fragUV);

	vec2 parallaxedCoords = fragUV;
	if (parallaxDepth > 0.001) {
		// Tangent-space view direction, built per pixel from the interpolated frame. Interpolating tangent-space
		// positions from the vertices instead mixes vectors from different frames and warps on curved meshes.
		vec3 V = normalize(-viewSpacePos);
		vec3 viewDir = vec3(dot(V, normalize(normalTBN[0])), dot(V, normalize(normalTBN[1])), dot(V, normalize(normalTBN[2])));
		parallaxedCoords = ParallaxMapping(fragUV, viewDir, textureIndex, parallaxDepth, parallaxLayers, uvDX, uvDY);
	}
	//if(parallaxedCoords.x > 1.0 || parallaxedCoords.y > 1.0 || parallaxedCoords.x < 0.0 || parallaxedCoords.y < 0.0) discard;
	MaterialProps material = LookupAllMaterialPropsGrad(parallaxedCoords, textureIndex, uvDX, uvDY);
	// An emissive colour can be brighter than the albedo's 8 bits hold. Its hue stays there and the rest goes to the emission.
	float overbright = material.emission > 0.0 ? max(max(fragColor.r, max(fragColor.g, fragColor.b)), 1.0) : 1.0;
	gAlbedo.rgb = material.albedo * fragColor / overbright;

    // Cutouts (leaves) end between the MSAA samples where the alpha crosses the cutoff.
    int coverage = alphaTestCoverage(material.alpha, 0.5);
    if (coverage == 0) discard;
    gl_SampleMask[0] = coverage;

    gAlbedo.a = material.subSurface;

    //gAlbedo.rgb = vec3(hash13(gl_PrimitiveID));
    
    //gAlbedo.rgb = vec3(fract(fragUV), 0f);
	vec3 normalTangentSpace = material.normal;
    gNormal.a = material.ambient;
    if (!gl_FrontFacing) normalTangentSpace.z *= -1.0;
    normalTangentSpace.xyz = normalize(normalTangentSpace.xyz);
    vec3 normal = normalize(normalTBN*normalTangentSpace.xyz);
	float roughness = material.roughness;

	// Whatever glows goes on glowing in the rain, at the same strength.
	if (material.emission <= 0.0)
	{
		mat3 viewToWorld = mat3(invViewMatrix);
		vec3 geometryNormal = normalize(viewToWorld * normalTBN[2]) * (gl_FrontFacing ? 1.0 : -1.0);
		vec3 worldNormal = viewToWorld * normal;
		vec3 albedo = gAlbedo.rgb;
		weatherWetten(worldPosition, geometryNormal, weatherPorosity(textureIndex, numberOfMaterials), material.metalic, 0.0, material.height, albedo, roughness, worldNormal);
		gAlbedo.rgb = albedo;
		normal = mat3(viewMatrix) * worldNormal;
	}
	gNormal.xyz = compressNormal(normal);

	gMaterials.r = weatherFilterRoughness(roughness, normal);
	float shine = material.emission * EMISSION_GAIN * overbright;

	// A blow goes through a body as light, from the inside, whatever it's wearing and whatever the sun is doing.
#ifdef CHARACTER
	gAlbedo.rgb = mix(gAlbedo.rgb, hitFlash.rgb, hitFlash.a);
	shine += hitFlash.a * HIT_SHINE;
#endif
	gMaterials.g = encodeEmission(shine);
	gMaterials.b = material.metalic;
    gMaterials.a = material.height;

}