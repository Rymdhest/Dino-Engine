#version 330
in vec2 textureCoords;
layout (location = 0) out vec4 albedo_out;
layout (location = 1) out vec4 materials_out;


uniform vec4 albedo;
uniform vec3 materials;

uniform vec2 startFrequenzy;
uniform int octaves;
uniform bool rigged;
uniform bool invert;
uniform float seed;
uniform float exponent;
uniform float amplitudePerOctave;
uniform float frequenzyPerOctave;
uniform float heightFactor;

uniform bool scaleOutputToHeight;
uniform bool writeToHeight;
uniform bool depthCheck;
uniform int blendMode;

uniform sampler2D previousMaterialTexture;
uniform sampler2D previousAlbedoTexture;
// Permutation table

vec2 noiseInterpolate(const in vec2 x) 
{ 
    vec2 x2 = x * x;
    return x2 * x * (x * (x * 6.0 - 15.0) + 10.0); 
}

uvec4 ihash1D(uvec4 q)
{
    // hash by Hugo Elias, Integer Hash - I, 2017
    q = q * 747796405u + 2891336453u;
    q = (q << 13u) ^ q;
    return q * (q * q * 15731u + 789221u) + 1376312589u;
}

void betterHash2D(vec4 cell, out vec4 hashX, out vec4 hashY)
{
    uvec4 i = uvec4(cell);
    uvec4 hash0 = ihash1D(ihash1D(i.xzxz) + i.yyww);
    uvec4 hash1 = ihash1D(hash0 ^ 1933247u);
    hashX = vec4(hash0) * (1.0 / float(0xffffffffu));
    hashY = vec4(hash1) * (1.0 / float(0xffffffffu));
}

void smultiHash2D(vec4 cell, out vec4 hashX, out vec4 hashY)
{
    betterHash2D(cell, hashX, hashY);
    hashX = hashX * 2.0 - 1.0; 
    hashY = hashY * 2.0 - 1.0;
}

float gradientNoise(vec2 pos, vec2 scale, float seed) 
{
    pos *= scale;
    vec4 i = floor(pos).xyxy + vec2(0.0, 1.0).xxyy;
    vec4 f = (pos.xyxy - i.xyxy) - vec2(0.0, 1.0).xxyy;
    i = mod(i, scale.xyxy) + seed;

    vec4 hashX, hashY;
    smultiHash2D(i, hashX, hashY);

    vec4 gradients = hashX * f.xzxz + hashY * f.yyww;
    vec2 u = noiseInterpolate(f.xy);
    vec2 g = mix(gradients.xz, gradients.yw, u.x);
    return 1.4142135623730950 * mix(g.x, g.y, u.y);
}

///////////////////

void multiHash2D(vec4 cell, out vec4 hashX, out vec4 hashY)
{
    uvec4 i = uvec4(cell);
    uvec4 hash0 = ihash1D(ihash1D(i.xzxz) + i.yyww);
    uvec4 hash1 = ihash1D(hash0 ^ 1933247u);
    hashX = vec4(hash0) * (1.0 / float(0xffffffffu));
    hashY = vec4(hash1) * (1.0 / float(0xffffffffu));
}

float perlinNoise(vec2 pos, vec2 scale, float seed)
{
    // based on Modifications to Classic Perlin Noise by Brian Sharpe: https://archive.is/cJtlS
    pos *= scale;
    vec4 i = floor(pos).xyxy + vec2(0.0, 1.0).xxyy;
    vec4 f = (pos.xyxy - i.xyxy) - vec2(0.0, 1.0).xxyy;
    i = mod(i, scale.xyxy) + seed;

    // grid gradients
    vec4 gradientX, gradientY;
    multiHash2D(i, gradientX, gradientY);
    gradientX -= 0.49999;
    gradientY -= 0.49999;

    // perlin surflet
    vec4 gradients = inversesqrt(gradientX * gradientX + gradientY * gradientY) * (gradientX * f.xzxz + gradientY * f.yyww);
    // normalize: 1.0 / 0.75^3
    gradients *= 2.3703703703703703703703703703704;
    vec4 lengthSq = f * f;
    lengthSq = lengthSq.xzxz + lengthSq.yyww;
    vec4 xSq = 1.0 - min(vec4(1.0), lengthSq); 
    xSq = xSq * xSq * xSq;
    return dot(xSq, gradients);
}
vec4 multiHash2D(vec4 cell)    
{
    uvec4 i = uvec4(cell);
    uvec4 hash = ihash1D(ihash1D(i.xzxz) + i.yyww);
    return vec4(hash) * (1.0 / float(0xffffffffu));
}
float noise(vec2 pos, vec2 scale, float phase, float seed) 
{
    const float kPI2 = 6.2831853071;
    pos *= scale;
    vec4 i = floor(pos).xyxy + vec2(0.0, 1.0).xxyy;
    vec2 f = pos - i.xy;
    i = mod(i, scale.xyxy) + seed;

    vec4 hash = multiHash2D(i);
    hash = 0.5 * sin(phase + kPI2 * hash) + 0.5;
    float a = hash.x;
    float b = hash.y;
    float c = hash.z;
    float d = hash.w;

    vec2 u = noiseInterpolate(f);
    float value = mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
    return value * 2.0 - 1.0;
}

float perlinNoise(vec2 pos, vec2 scale, mat2 transform, float seed)
{
	float value = 0;
	float totalAmplitude = 0;
	float amplitude = 1;
	vec2 frequenzy = startFrequenzy;
	for (int i = 0 ; i < octaves ; i++) {

		float perlin = 0;
		if (rigged) {
			perlin =abs( amplitude*simplex3D(vec3(frequenzy*p, seed)));
		}
		else {
			perlin = amplitude*(simplex3D(vec3(frequenzy*p, seed))*0.5f+0.5f);
		}

float fbmPerlin(vec2 pos, vec2 scale, int octaves, float shift, float axialShift, float gain, float lacunarity, uint mode, float factor, float offset, float octaveFactor, float seed) 
{
    float amplitude = gain;
    vec2 frequency = floor(scale);
    float angle = axialShift;
    float n = 1.0;
    vec2 p = fract(pos) * frequency;

		amplitude *= amplitudePerOctave;
		frequenzy *= frequenzyPerOctave;
	}
	value /= totalAmplitude;

	if (invert) value = 1-value;

	return value;
}

void main(void)
{   
    
    float shift = 1.5;
    float axialShift = 3.1415/2.0;
    float gain = 0.5;
    float lacunarity = 2.0;
    uint mode = 4u;
    if (rigged) mode = 1u;
    float factor = exponent;
    float octaveFactor = 1.0;
    float offset = 0.0;
   
    float height = heightFactor*fbmPerlin(textureCoords, startFrequenzy, octaves, shift,axialShift, gain, lacunarity, mode, factor, offset, octaveFactor, seed);
    if (invert) height = 1-height;

    float roughness = materials.r;
    float glow = materials.g;
    float metalic =materials.b;

	vec4 newMaterial = vec4(roughness, glow, metalic, height);
	vec4 newAlbedo = albedo;
	if (scaleOutputToHeight) {
		newAlbedo *= height;
		newMaterial.rgb *= height;
	}

	vec4 previousAlbedo = texture(previousAlbedoTexture, textureCoords);
	vec4 previousMaterial = texture(previousMaterialTexture, textureCoords);

	if (depthCheck) {
		if (previousMaterial.a > newMaterial.a) {
			albedo_out = previousAlbedo;
			materials_out = previousMaterial;
			return;
		}
	}

 // additive
	if (blendMode == 0) { 
		albedo_out = newAlbedo + previousAlbedo;
		materials_out = newMaterial + previousMaterial;

// multiplicative
	} else if (blendMode == 1) { 
		albedo_out = newAlbedo * previousAlbedo;
		materials_out = newMaterial * previousMaterial;

// override
	} else if (blendMode ==2 ){ 
		albedo_out = newAlbedo;
		materials_out = newMaterial;
	}

	if (!writeToHeight) {
		materials_out.a = previousMaterial.a;
	}
}