#version 420
#include gBufferUtil.glsl
#include Globals.glsl

out float FragColor;
in vec2 textureCoords;

uniform sampler2D originalTexture;
uniform sampler2D gDepth;


uniform float depthStrength;
uniform int blurRange;
uniform vec2 resolutionInput; 

void main() 
{
    vec2 texelSize = 1.0 / resolutionInput;
    float result = 0.0;
    
    // 1. Get the depth of the center pixel we are trying to blur
    float centerDepth = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolutionInput).z;
    
    float totalWeight = 0.0;
    
    // 2. Loop through a 4x4 grid (adjust range for your needs)
    for (int x = -blurRange; x <= blurRange; ++x) 
    {
        for (int y = -blurRange; y <= blurRange; ++y) 
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleCoords = textureCoords + offset;
            
            // Get neighbor's SSAO value and Depth
            float sampleSSAO = texture(originalTexture, sampleCoords).r;
            float sampleDepth = ReconstructViewSpacePosition(gl_FragCoord.xy + vec2(x, y), texture(gDepth, sampleCoords).r, invProjectionMatrix, resolutionInput).z;
            
            // 3. Calculate Spatial Weight (Standard Gaussian approximation)
            // Pixels further away in the grid get less weight
            float spatialWeight = 1.0 / (1.0 + float(x*x + y*y)); 
            
            // 4. Calculate Depth Weight (The Bilateral Magic)
            // If the difference in depth is large, this weight drops to 0 instantly.
            // The multiplier (e.g., 200.0) controls how strict the edge detection is.
            float depthDiff = abs(centerDepth - sampleDepth);
            float depthWeight = exp(-depthDiff * depthStrength); 
            
            // Combine weights
            float finalWeight = spatialWeight * depthWeight;
            
            result += sampleSSAO * finalWeight;
            totalWeight += finalWeight;
        }
    }
    
    // Normalize the result so it doesn't get artificially bright or dark
    FragColor = result / totalWeight;
}