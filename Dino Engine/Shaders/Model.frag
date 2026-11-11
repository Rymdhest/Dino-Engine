#version 420

// DEPTH_SETTLED: Model_Depth.frag has already laid down the depth, with every cut this shader would make, so depth is tested first and on equal
// (InstancedModelRenderer), and only the samples that are seen are shaded, once each.
#ifdef DEPTH_SETTLED
layout(early_fragment_tests) in;
#endif

#include Globals.glsl

in vec3 fragColor;
in vec2 fragUV;
in vec3 viewSpacePos;
in mat3 normalTBN;
in float textureIndex;

uniform int numberOfMaterials;
uniform float reliefScale;      // 1 for each material's own relief, 0 for none at all (Graphics quality).

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;

uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;
uniform vec4 hitFlash;
uniform vec4 characterSight;
#ifdef WILDLIFE
flat in float fragFade;
#endif
#if !defined(CHARACTER) && !defined(WILDLIFE)
flat in float fragFocusOccluder;
in vec4 fragEdges;      // Metres to each edge of the face (MeshBuilder), or far off.
#else
const vec4 fragEdges = vec4(1e6);
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
#include Halls.glsl
#include Surface/MaterialTable.glsl
#include Surface/SurfaceFrame.glsl
#include Surface/Relief.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Wetness.glsl

const float HIT_SHINE = 7.0;
const float MATERIAL_SIZE = 1024.0;     // Texels across a material as the arrays keep it (TextureGenerator.MaterialLevelsDropped), for the mip a relief reads.

void main() {

	vec3 worldPosition = (invViewMatrix * vec4(viewSpacePos, 1.0)).xyz;

	// A card's normals aren't its face's: they're the soft shape of the mass it's part of, which is the same from either side. Turning them over would shade half a crown inside out.
	bool card = int(textureIndex + 0.5) >= numberOfMaterials;
	vec3 smoothNormal = normalize(normalTBN[2]);
	vec3 facing = gl_FrontFacing ? smoothNormal : -smoothNormal;
	// Everything that needs derivatives, before anything can be discarded: after that they mean nothing.
	SurfaceFrame frame = surfaceFrame(viewSpacePos, facing, fragUV);
	vec3 positionDX = mat3(invViewMatrix) * frame.positionDx;
	vec3 positionDY = mat3(invViewMatrix) * frame.positionDy;

	// Only what's big enough to hide the player opens for them (FocusOccluder.glsl). Characters keep whole, and so does
	// wildlife, as a bird flying over the hero is never in the way.
#if !defined(CHARACTER) && !defined(WILDLIFE)
#ifndef DEPTH_SETTLED
	if (fragFocusOccluder > 0.5) applyFocusCutout(worldPosition);
	// The hall the camera looks into opens while someone stands in it: its roof and the walls facing the camera go, thinning out as it opens, for this pass
	// alone (Halls.glsl). Where that opens something up, its inside is drawn as the face of the cut.
	if (cutByHall(worldPosition) * hallCutaway.y > focusDither(gl_FragCoord.xy)) discard;
#endif
	bool cap = !gl_FrontFacing && !card && hallCapped(worldPosition);
#else
	const bool cap = false;
#endif
#ifdef WILDLIFE
	// What comes and goes does it by thinning out, as nothing that's only for looking at should ever be seen to pop. And over a hall that's open, which the camera sees
	// into as if it had no roof, what flies goes with the roof, as a chimney's smoke does (Halls.glsl): a bird crossing over the room would read as one inside it.
	float shown = fragFade * (1.0 - cutByHallSoft(worldPosition, 2.0));
	if (shown < 1.0 && shown <= focusDither(gl_FragCoord.xy)) discard;
#endif
	// Only a character is told these: one big enough to hide the player opens for them as a tree does, and one coming or going thins in or out. Everything else is told nothing, which is 0.
	if (characterSight.x > 0.5) applyFocusCutout(worldPosition);
	if (characterSight.y > 0.0 && 1.0 - characterSight.y <= focusDither(gl_FragCoord.xy)) discard;

	// A stone is the rock of the mountains: the same layers at the same heights, whatever its own shape. It's read from where it is and not from its texture
	// coordinates (Rock.glsl), so there's no relief of the material's to follow, and a march through one would shade it with shadows of a surface not drawn.
#ifndef CHARACTER
	bool stone = isRock(textureIndex);
#else
	const bool stone = false;
#endif

	// The relief, where the material has one: the texture is read where the view ray meets it, and the sun is asked whether the relief around hides it there.
	Relief relief;
	relief.uv = fragUV;
	relief.depth = 0.0;
	relief.reliefMetres = 0.0;
	float sunThrough = 1.0;
	if (!card && !stone && !cap && reliefScale > 0.0)
	{
		float ratio = materialReliefRatio(textureIndex, numberOfMaterials);
		if (ratio > 0.0)
		{
			surfaceShape(frame);
			float lod = reliefLod(frame, MATERIAL_SIZE);
			float edge = min(min(fragEdges.x, fragEdges.y), min(fragEdges.z, fragEdges.w));
			relief = reliefMarch(frame, fragUV, normalize(-viewSpacePos), textureIndex, ratio * frame.tileMetres * reliefScale, edge, lod);
			sunThrough = reliefShadow(frame, relief, normalize(mat3(viewMatrix) * sunDirectionWorld.xyz), textureIndex, lod);
		}
	}

	// Filtered by the footprint of the surface itself: the relief's coordinates jump where one thing hides another, and their own derivatives would pick the wrong mip.
	MaterialProps material = LookupAllMaterialPropsGrad(relief.uv, textureIndex, frame.uvDx, frame.uvDy);
#ifndef CHARACTER
	RockLook rock;
	if (stone) {
		rock = rockLook(worldPosition, normalize(mat3(invViewMatrix) * facing), positionDX, positionDY);
		material.albedo = rock.albedo;
		material.roughness = rock.roughness;
		material.ambient = rock.ambient;
		material.height = rock.height;
	}
#endif
	// An emissive colour can be brighter than the albedo's 8 bits hold. Its hue stays there and the rest goes to the emission.
	float overbright = material.emission > 0.0 ? max(max(fragColor.r, max(fragColor.g, fragColor.b)), 1.0) : 1.0;
	gAlbedo.rgb = material.albedo * fragColor / overbright;

    // Cutouts (leaves) end between the MSAA samples where the alpha crosses the cutoff. With the depth settled, the equal test has already kept only those.
#ifndef DEPTH_SETTLED
    int coverage = alphaTestCoverage(material.alpha, 0.5);
    if (coverage == 0) discard;
    gl_SampleMask[0] = coverage;
#endif

    gAlbedo.a = transmittance(material.subSurface, material.height);
    gNormal.a = material.ambient;

    vec3 normal = surfaceNormal(frame, normalize(material.normal), card ? smoothNormal : facing);
#ifndef CHARACTER
	if (stone) normal = normalize(mat3(viewMatrix) * rock.normal);
#endif
	float roughness = material.roughness;

	// Whatever glows goes on glowing in the rain, at the same strength.
	if (material.emission <= 0.0)
	{
		mat3 viewToWorld = mat3(invViewMatrix);
		vec3 geometryNormal = normalize(viewToWorld * (card ? smoothNormal : facing));
		vec3 worldNormal = viewToWorld * normal;
		vec3 albedo = gAlbedo.rgb;
		weatherWetten(worldPosition, geometryNormal, weatherPorosity(textureIndex, numberOfMaterials), material.metalic, 0.0, material.height, albedo, roughness, worldNormal);
		gAlbedo.rgb = albedo;
		normal = mat3(viewMatrix) * worldNormal;
	}
	// The face of a cut: flat, facing up, a little darker than what it's cut through, as a section is.
	if (cap)
	{
		normal = normalize(mat3(viewMatrix) * vec3(0.0, 1.0, 0.0));
		gAlbedo.rgb *= 0.62;
		roughness = 0.92;
		material.emission = 0.0;
		sunThrough = 1.0;
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
    gMaterials.a = sunThrough;

}
