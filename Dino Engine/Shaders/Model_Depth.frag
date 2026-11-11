#version 420

// The depth of what Model.frag draws, and nothing else, before it's drawn (InstancedModelRenderer): Model.frag then shades on an equal depth test with its
// depth tested first, so each sample is shaded once, by what's in front, and nothing hidden behind it is shaded at all. That's the same as grass does
// (Grass_Depth.frag). It has to cut away exactly the samples Model.frag would: what opens for the player, a hall the camera looks into, and a card's cutout,
// read from the same texel with the same derivatives. Materials are all opaque (nothing in MaterialRecipes has an alpha) and only cards cut out, and cards
// have no relief, so a card's alpha is read where the mesh puts it, as Model.frag reads it.

#include Globals.glsl

in vec2 fragUV;
in vec3 viewSpacePos;
in float textureIndex;
flat in float fragFocusOccluder;

uniform int numberOfMaterials;
uniform sampler2DArray albedoMapModelTextureArray;

#include AntiAliasing/Msaa.glsl
#include FocusCutout.glsl
#include Halls.glsl

void main()
{
	vec3 worldPosition = (invViewMatrix * vec4(viewSpacePos, 1.0)).xyz;
	bool card = int(textureIndex + 0.5) >= numberOfMaterials;
	vec2 uvDx = dFdx(fragUV);
	vec2 uvDy = dFdy(fragUV);

	if (fragFocusOccluder > 0.5) applyFocusCutout(worldPosition);
	if (cutByHall(worldPosition) * hallCutaway.y > focusDither(gl_FragCoord.xy)) discard;

	float alpha = card ? textureGrad(albedoMapModelTextureArray, vec3(fragUV, textureIndex - float(numberOfMaterials)), uvDx, uvDy).a : 1.0;
	int coverage = alphaTestCoverage(alpha, 0.5);
	if (coverage == 0) discard;
	gl_SampleMask[0] = coverage;
}
