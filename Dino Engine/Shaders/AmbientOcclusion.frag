#version 420

#include gBufferUtil.glsl
#include Globals.glsl

out float FragColor;
in vec2 textureCoords;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D texNoise;

uniform vec3 samples[16];
const int kernelSize = 16;

uniform float radius;
uniform float strength;
uniform float bias;


uniform vec2 resolutionSSAO;

const float noiseSize = 4.0;
uniform vec2 noiseScale;


void main()
{
    // Get input for SSAO algorithm
    vec3 fragPos = ReconstructViewSpacePosition(gl_FragCoord.xy, texture(gDepth, textureCoords).r, invProjectionMatrix, resolutionSSAO);
    vec3 normal = unCompressNormal(texture(gNormal, textureCoords).rgb);
	//normal = normalize(normal);
   vec3 randomVec = texture(texNoise, textureCoords*noiseScale).xyz;
   //vec3 randomVec = vec3(1.0, 1.0, 0.0);
    //randomVec = vec3(1, 1, 0);

    // Create TBN change-of-basis matrix: from tangent-space to view-space
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN =  mat3(tangent, bitangent, normal);
    // Iterate over the sample kernel and calculate occlusion factor
    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {   
        // get sample position
        vec3 samplePos = TBN * samples[i]; // From tangent to view-space
        samplePos = fragPos + samplePos * radius; 
        
        // project sample position (to sample texture) (to get position on screen/texture): a perspective projection only scales x and y, and divides by -z
        vec2 offset = vec2(projectionMatrix[0][0], projectionMatrix[1][1]) * samplePos.xy / -samplePos.z * 0.5 + 0.5;

        // get sample depth
        float sampleDepth = ReconstructViewSpaceZ(texture(gDepth, offset).r, invProjectionMatrix);
        
        // range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth ));
        
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0)*rangeCheck;
    }
    occlusion = 1.0 - (occlusion / kernelSize);

    FragColor = pow (occlusion, strength);

}
