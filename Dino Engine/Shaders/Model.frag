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
#ifdef WILDLIFE
flat in float fragFade;
#endif
#if !defined(CHARACTER) && !defined(WILDLIFE)
flat in float fragFocusOccluder;
#endif

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

#include textureUtil.glsl
#include Rock.glsl
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
	// Only what's big enough to hide the player opens for them (FocusOccluder.glsl). Characters keep whole, and so does
	// wildlife, as a bird flying over the hero is never in the way.
#if !defined(CHARACTER) && !defined(WILDLIFE)
	if (fragFocusOccluder > 0.5) applyFocusCutout(worldPosition);
#endif
#ifdef WILDLIFE
	// What comes and goes does it by thinning out, as nothing that's only for looking at should ever be seen to pop.
	if (fragFade < 1.0 && fragFade <= focusDither(gl_FragCoord.xy)) discard;
#endif

	vec2 uvDX = dFdx(fragUV);
	vec2 uvDY = dFdy(fragUV);
	vec3 positionDX = dFdx(worldPosition);
	vec3 positionDY = dFdy(worldPosition);

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
#ifndef CHARACTER
	// A stone is the rock of the mountains: the same layers at the same heights, whatever its own shape.
	bool stone = isRock(textureIndex);
	RockLook rock;
	if (stone) {
		rock = rockLook(worldPosition, normalize(mat3(invViewMatrix) * normalTBN[2]) * (gl_FrontFacing ? 1.0 : -1.0), positionDX, positionDY);
		material.albedo = rock.albedo;
		material.roughness = rock.roughness;
		material.ambient = rock.ambient;
		material.height = rock.height;
	}
#endif
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
    // A card's normals aren't its face's: they're the soft shape of the mass it's part of, which is the same from either side. Turning them over would shade half a crown inside out.
    bool card = int(textureIndex + 0.5) >= numberOfMaterials;
    if (!gl_FrontFacing && !card) normalTangentSpace.z *= -1.0;
    normalTangentSpace.xyz = normalize(normalTangentSpace.xyz);
    vec3 normal = normalize(normalTBN*normalTangentSpace.xyz);
#ifndef CHARACTER
	if (stone) normal = normalize(mat3(viewMatrix) * rock.normal);
#endif
	float roughness = material.roughness;

	// Whatever glows goes on glowing in the rain, at the same strength.
	if (material.emission <= 0.0)
	{
		mat3 viewToWorld = mat3(invViewMatrix);
		vec3 geometryNormal = normalize(viewToWorld * normalTBN[2]) * (gl_FrontFacing || card ? 1.0 : -1.0);
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