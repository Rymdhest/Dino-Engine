#version 330

#include gBufferUtil.glsl

out vec3 FragColor;
in vec2 textureCoords;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D texNoise;

uniform vec3 samples[16];
int kernelSize = 16;

uniform float radius;
uniform float strength;
uniform float bias;


uniform mat4 invProjection;
uniform vec2 resolution;

const float noiseSize = 4.0;
uniform vec2 noiseScale;

uniform mat4 projectionMatrix;


void main()
{
    // Get input for SSAO algorithm
    vec3 fragPos = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjection, resolution);
    vec3 normal = texture(gNormal, textureCoords).rgb;
	//normal = normalize(normal);
   vec3 randomVec = texture(texNoise, textureCoords*noiseScale).xyz;
   //vec3 randomVec = vec3(1.0, 1.0, 0.0);
    //randomVec = vec3(1, 1, 0);
   float depthScaledRadius = radius*-fragPos.z;
   float depthScaledStrength = strength;
   float depthScaledBias = bias;
    // Create TBN change-of-basis matrix: from tangent-space to view-space
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN =  mat3(tangent, bitangent, normal);
    // Iterate over the sample kernel and calculate occlusion factor
    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {   
        // get sample position
        vec3 sample = TBN * samples[i]; // From tangent to view-space
        sample = fragPos + sample * depthScaledRadius; 
        
        // project sample position (to sample texture) (to get position on screen/texture)
        vec4 offset = vec4(sample, 1.0);
        offset = offset * projectionMatrix; // from view to clip-space
        offset.xyz /= offset.w; // perspective divide
        offset.xyz = offset.xyz * 0.5 + 0.5; // transform to range 0.0 - 1.0
        
        // get sample depth
        float sampleDepth =ReconstructViewSpacePosition(offset.xy * resolution, texture(gDepth, offset.xy).r, invProjection, resolution).z;
        
        // range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, depthScaledRadius / abs(fragPos.z - sampleDepth ));
        
        occlusion += (sampleDepth >= sample.z + depthScaledBias ? 1.0 : 0.0)*rangeCheck;
    }
    occlusion = 1.0 - (occlusion / kernelSize);

    FragColor = vec3(pow (occlusion, depthScaledStrength));

}
