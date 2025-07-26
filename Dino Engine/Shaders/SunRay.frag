#version 420
#include globals.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform float Density;
uniform float Weight;
uniform float Exposure;
uniform float Decay;
uniform float illuminationDecay;
uniform int samples;
uniform vec3 sunDirection;
uniform vec2 celestialRayResolution;
uniform sampler2D sunTexture;


void main(void)
{
    float illuminationDecay2 = illuminationDecay;
    // Transform the sun direction into clip space
    vec4 clipSpacePos =projectionMatrix* viewMatrix *vec4(sunDirection, 0.0);
    
    // Normalize to NDC (Normalized Device Coordinates)
    vec3 ndcPos = clipSpacePos.xyz / clipSpacePos.w;
    
    // Convert NDC to screen space coordinates (0 to 1)
    vec2 screenPos = ndcPos.xy * 0.5 + 0.5;
    
	screenPos = ((screenPos*2)-1);
	screenPos = (screenPos*celestialRayResolution)/celestialRayResolution.xy;

	vec2 uv = ((textureCoords*2)-1);
	uv = (uv*celestialRayResolution)/celestialRayResolution.xy;

  // Calculate vector from pixel to light source in screen space.
  vec2 deltaTexCoord = (uv - screenPos.xy);

  // Divide by number of samples and scale by control factor.
  deltaTexCoord *= 1.0f / samples * Density;

  // Store initial sample.
  vec2 sameCoords = textureCoords;
  vec3 color = texture(sunTexture, sameCoords).rgb;

  // Evaluate summation from Equation 3 NUM_SAMPLES iterations.
  for (int i = 0; i < samples; i++) {
    // Step sample location along ray.
    sameCoords -= deltaTexCoord;

    // Retrieve sample at new location.
    vec3 sampleCol = texture(sunTexture, sameCoords).rgb;

    // Apply sample attenuation scale/decay factors.
    sampleCol *= illuminationDecay2 * Weight;

    // Accumulate combined color.
    color += sampleCol;

    // Update exponential decay factor.
    illuminationDecay2 *= Decay;
  }

  // Output final color with a further scale control factor.
     vec3 godray = color * Exposure;

    out_Colour = vec4(godray/samples, 0);

}