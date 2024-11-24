#version 330

#include procedural/interpolate.glsl
#include procedural/hash.glsl
#include procedural/multiHash.glsl
#include procedural/noise.glsl

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

float perlinNoise(vec2 pos, vec2 scale, mat2 transform, float seed)
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

    // transform gradients
    vec4 mt = vec4(transform);
    vec4 rg = vec4(gradientX.x, gradientY.x, gradientX.y, gradientY.y);
    rg = rg.xxzz * mt.xyxy + rg.yyww * mt.zwzw;
    gradientX.xy = rg.xz;
    gradientY.xy = rg.yw;

    rg = vec4(gradientX.z, gradientY.z, gradientX.w, gradientY.w);
    rg = rg.xxzz * mt.xyxy + rg.yyww * mt.zwzw;
    gradientX.zw = rg.xz;
    gradientY.zw = rg.yw;

    // perlin surflet
    vec4 gradients = inversesqrt(gradientX * gradientX + gradientY * gradientY) * (gradientX * f.xzxz + gradientY * f.yyww);
    // normalize: 1.0 / 0.75^3
    gradients *= 2.3703703703703703703703703703704;
    f = f * f;
    f = f.xzxz + f.yyww;
    vec4 xSq = 1.0 - min(vec4(1.0), f); 
    return dot(xSq * xSq * xSq, gradients);
}

// 2D Perlin noise with gradients rotation.
// @param scale Number of tiles, must be  integer for tileable results, range: [2, inf]
// @param rotation Rotation for the noise gradients, useful to animate flow, range: [0, PI]
// @param seed Seed to randomize result, range: [0, inf], default: 0.0
// @return Value of the noise, range: [-1, 1]
float perlinNoise(vec2 pos, vec2 scale, float rotation, float seed) 
{
    vec2 sinCos = vec2(sin(rotation), cos(rotation));
    return perlinNoise(pos, scale, mat2(sinCos.y, sinCos.x, sinCos.x, sinCos.y), seed);
}

float fbmPerlin(vec2 pos, vec2 scale, int octaves, float shift, float axialShift, float gain, float lacunarity, uint mode, float factor, float offset, float octaveFactor, float seed) 
{
    float amplitude = gain;
    vec2 frequency = floor(scale);
    float angle = axialShift;
    float n = 1.0;
    vec2 p = fract(pos) * frequency;

    float value = 0.0;
    for (int i = 0; i < octaves; i++) 
    {
        float pn = perlinNoise(p / frequency, frequency, angle, seed) + offset;
        if (mode == 0u)
        {
            n *= abs(pn);
        }
        else if (mode == 1u)
        {
            n = abs(pn);
        }
        else if (mode == 2u)
        {
            n = pn;
        }
        else if (mode == 3u)
        {
            n *= pn;
        }
        else if (mode == 4u)
        {
            n = pn * 0.5 + 0.5;
        }
        else
        {
            n *= pn * 0.5 + 0.5;
        }
        
        n = pow(n < 0.0 ? 0.0 : n, factor);
        value += amplitude * n;
        
        p = p * lacunarity + shift;
        frequency *= lacunarity;
        amplitude = pow(amplitude * gain, octaveFactor);
        angle += axialShift;
    }
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