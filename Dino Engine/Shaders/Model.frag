#version 420
#include globals.glsl

in vec3 fragColor;
in vec2 fragUV;
in float textureIndex;

in vec3 fragWorldPos;
in vec3 fragWorldNormal;
in vec3 fragWorldTangent;

uniform int numberOfMaterials;
uniform float parallaxDepth;
uniform float parallaxLayers;

uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;
uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;  

#include textureUtil.glsl
#include procedural/fastHash.glsl
#include gBufferUtil.glsl

void main() {
	// 1. Re-normalize stable world vectors
	vec3 N = normalize(fragWorldNormal);
	vec3 T = normalize(fragWorldTangent);
	
	// 2. Handle Inside Faces ONCE (Flipping N inherently flips Tangent Space Z!)
	if (!gl_FrontFacing) {
		N = -N;
	}
	
	// 3. Gram-Schmidt Orthogonalization
	T = normalize(T - dot(T, N) * N);
	
	vec3 B = cross(N, T);
	if (dot(B, B) < 0.0001) {
		B = abs(N.y) < 0.99 ? cross(N, vec3(0.0, 1.0, 0.0)) : cross(N, vec3(1.0, 0.0, 0.0));
	}
	B = normalize(B);
	
	// 4. Enforce strict right-handed TBN frame (fixes Z-axis inversion on specific box faces)
	mat3 rawTBN = mat3(T, B, N);
	if (determinant(rawTBN) < 0.0) {
		B = -B;
	}

	// 5. Create World-to-Tangent Matrix
	mat3 worldToTangent = transpose(mat3(T, B, N));
	
	// 6. Accurate View Direction completely free of interpolation warping
	vec3 viewDirWorld = normalize(viewPosWorld - fragWorldPos);
	vec3 viewDir = worldToTangent * viewDirWorld;

	// 7. Parallax Mapping
	vec2 parallaxedCoords = fragUV;
	if (parallaxDepth > 0.001) {
		parallaxedCoords = ParallaxMapping(fragUV, viewDir, textureIndex, parallaxDepth, parallaxLayers);
	}

	MaterialProps material = LookupAllMaterialProps(parallaxedCoords, textureIndex);
	gAlbedo.rgb = material.albedo * fragColor;

	if (material.alphaBit == 0) discard;

	gAlbedo.a = material.subSurface;

	// 8. Normal Mapping (Converted per-pixel to View Space for the G-Buffer)
	vec3 normalTangentSpace = material.normal;
	gNormal.a = material.ambient;
	
	if (!gl_FrontFacing) {
		normalTangentSpace.z *= -1.0; 
	}
	normalTangentSpace.xyz = normalize(normalTangentSpace.xyz);
    
	mat3 worldToView = mat3(viewMatrix);
	vec3 T_view = normalize(worldToView * T);
	vec3 B_view = normalize(worldToView * B);
	vec3 N_view = normalize(worldToView * N);
	mat3 viewTBN = mat3(T_view, B_view, N_view);
	
	vec3 finalNormal = normalize(viewTBN * normalTangentSpace.xyz);
	gNormal.xyz = compressNormal(finalNormal);

	gMaterials.r = material.roughness;
	gMaterials.g = material.emission;
	gMaterials.b = material.metalic;
	gMaterials.a = material.height;
}