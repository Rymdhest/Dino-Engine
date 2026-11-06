#version 420

// The same mesh and the same materials as the world, under lights of its own: a warm key from the upper left, a cool fill, a rim from behind, with the diffuse stepped a little so that a
// picture 50 pixels wide still reads as a drawn object. Metal looks at a painted sky rather than real surroundings, as it has none here. This is what makes an icon, and it's only here.

in vec3 fragColor;
in vec2 fragUV;
in float textureIndex;
in mat3 normalTBN;

uniform int numberOfMaterials;
uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;
uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 outColour;
layout (location = 1) out vec4 outNormal;

#include textureUtil.glsl

const vec3 KEY = normalize(vec3(-0.55, 0.7, 0.55));
const vec3 FILL = normalize(vec3(0.75, 0.05, 0.45));
const vec3 RIM = normalize(vec3(0.3, 0.45, -0.85));
const vec3 KEY_COLOUR = vec3(1.0, 0.94, 0.84);
const vec3 FILL_COLOUR = vec3(0.42, 0.5, 0.68);

// Soft steps, so that light falls off in bands without the hard edge of real cel shading.
float stepped(float light) {
	return 0.34 * smoothstep(-0.12, 0.06, light) + 0.36 * smoothstep(0.2, 0.34, light) + 0.3 * smoothstep(0.62, 0.76, light);
}

vec3 paintedSky(vec3 direction) {
	float up = direction.y;
	vec3 ground = vec3(0.1, 0.075, 0.06);
	vec3 horizon = vec3(0.78, 0.74, 0.68);
	vec3 sky = vec3(0.34, 0.42, 0.56);
	vec3 colour = mix(ground, horizon, smoothstep(-0.32, -0.02, up));
	return mix(colour, sky, smoothstep(0.02, 0.5, up));
}

void main() {
	vec2 dx = dFdx(fragUV);
	vec2 dy = dFdy(fragUV);
	MaterialProps material = LookupAllMaterialPropsGrad(fragUV, textureIndex, dx, dy);
	if (material.alpha < 0.5) discard;

	vec3 tangentNormal = normalize(material.normal);
	if (!gl_FrontFacing) tangentNormal.z *= -1.0;
	vec3 N = normalize(normalTBN * tangentNormal);
	if (!gl_FrontFacing) N = -N;

	float overbright = max(max(fragColor.r, max(fragColor.g, fragColor.b)), 1.0);
	vec3 albedo = material.albedo * fragColor / overbright;

	vec3 lit;
	if (material.emission > 0.0) {
		lit = fragColor;
	} else {
		float key = stepped(dot(N, KEY));
		float fill = 0.5 + 0.5 * dot(N, FILL);
		float rim = pow(1.0 - clamp(N.z, 0.0, 1.0), 2.6) * smoothstep(-0.2, 0.5, dot(N, RIM));

		vec3 diffuse = albedo * (KEY_COLOUR * (0.22 + key * 1.08) + FILL_COLOUR * fill * 0.3);

		vec3 V = vec3(0.0, 0.0, 1.0);
		vec3 R = reflect(-V, N);
		vec3 sheen = paintedSky(R);
		float gloss = 1.0 - material.roughness;

		// Fresnel, and a highlight with an edge to it.
		float facing = 1.0 - clamp(dot(N, V), 0.0, 1.0);
		float fresnel = 0.04 + 0.96 * pow(facing, 5.0);
		float highlight = smoothstep(0.86 - gloss * 0.08, 0.92, dot(normalize(KEY + V), N)) * (0.3 + gloss);

		vec3 metal = albedo * (sheen * 1.5 + 0.18) + albedo * highlight * 1.4;
		vec3 plain = diffuse + sheen * fresnel * gloss * 0.6 + vec3(highlight * gloss * 0.3);
		lit = mix(plain, metal, material.metalic) + vec3(0.85, 0.9, 1.0) * rim * (0.16 + 0.4 * material.metalic);
	}

	outColour = vec4(lit, 1.0);
	outNormal = vec4(N * 0.5 + 0.5, 1.0);
}
