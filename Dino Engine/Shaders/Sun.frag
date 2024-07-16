#version 330

#define NUM_SAMPLES 25


in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

uniform vec2 screenResolution;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

uniform vec3 sunDirectionViewSpace;
uniform vec3 sunColour;

uniform sampler2D shadedColourTexture;
uniform sampler2D gPosition;


void main(void)
{
    float Density = 0.2f;
    float Weight = 0.3f;
    float Exposure = 0.2f;
    float Decay = 1f;
  float illuminationDecay = 1.0f;
  vec2 texCoords = textureCoords;
  // Calculate vector from pixel to light source in screen space.
  vec2 ScreenLightPos = vec2(0.5f);
  vec2 deltaTexCoord = (texCoords - ScreenLightPos.xy);

  // Divide by number of samples and scale by control factor.
  deltaTexCoord *= 1.0f / NUM_SAMPLES * Density;

  // Store initial sample.
  vec3 color = texture(shadedColourTexture, texCoords).xyz;

  // Evaluate summation from Equation 3 NUM_SAMPLES iterations.
  for (int i = 0; i < NUM_SAMPLES; i++) {
    // Step sample location along ray.
    texCoords -= deltaTexCoord;

    // Retrieve sample at new location.
    vec3 sampleCol = texture(shadedColourTexture, texCoords).xyz;

    // Apply sample attenuation scale/decay factors.
    sampleCol *= illuminationDecay * Weight;

    // Accumulate combined color.
    color += sampleCol;

    // Update exponential decay factor.
    illuminationDecay *= Decay;
  }

  // Output final color with a further scale control factor.
  out_Colour = vec4(color * Exposure, 1f);



  
        // Transform the sun direction into clip space
    vec4 clipSpacePos =vec4(sunDirectionViewSpace, 0.0)* viewMatrix * projectionMatrix;
    
    // Normalize to NDC (Normalized Device Coordinates)
    vec3 ndcPos = clipSpacePos.xyz / clipSpacePos.w;
    
    // Convert NDC to screen space coordinates (0 to 1)
    vec2 screenPos = ndcPos.xy * 0.5 + 0.5;
    

	vec2 uv = ((textureCoords*2f)-1f);
	uv = (uv*screenResolution)/screenResolution.y;
	vec3 viewDir = normalize((viewMatrix*vec4(uv, -1f, 1.0f)).xyz);
	float sunAmount = pow(max( dot(viewDir, sunDirectionViewSpace ), 0.0 ), 256);
    vec3 shadedColor = texture(shadedColourTexture, textureCoords).xyz;
    out_Colour = vec4(shadedColor+sunColour*sunAmount, 1f);

}