#version 330
out vec3 FragColor;
in vec2 textureCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D texNoise;

uniform vec3 samples[32];
int kernelSize = 32;

uniform float radius;
uniform float strength;
uniform float bias;

const float noiseSize = 4.0;
uniform vec2 noiseScale;

uniform mat4 projectionMatrix;


void main()
{
    // Get input for SSAO algorithm
    vec3 fragPos = texture(gPosition, textureCoords).xyz;
    vec3 normal = texture(gNormal, textureCoords).rgb;
	//normal = normalize(normal);
   vec3 randomVec = texture(texNoise, textureCoords*noiseScale).xyz;
   //vec3 randomVec = vec3(1.0, 1.0, 0.0);
    //randomVec = vec3(1, 1, 0);
   float depthScaledRadius = radius+clamp(-fragPos.z*0.1f, 0, 200000);
   float depthScaledStrength = strength+clamp(-fragPos.z*0.01f, 0, 8);
   float depthScaledBias = bias + bias*-fragPos.z*0.75f;
    // Create TBN change-of-basis matrix: from tangent-space to view-space
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);
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
        float sampleDepth =texture(gPosition, offset.xy).z; // Get depth value of kernel sample
        
        // range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, depthScaledRadius / abs(fragPos.z - sampleDepth ));
	  //float rangeCheck = abs(fragPos.z - sampleDepth) < radius ? 1.0 : 0.0;
        
        occlusion += (sampleDepth >= sample.z + depthScaledBias ? 1.0 : 0.0)*rangeCheck;
    }
    occlusion = 1.0 - (occlusion / kernelSize);

    FragColor = vec3(pow (occlusion, depthScaledStrength));

}
