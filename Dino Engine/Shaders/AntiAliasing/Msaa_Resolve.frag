#version 420

// Averages the samples of the HDR scene into one colour per pixel for post-processing. A plain average of HDR values
// lets a bright sample swamp the rest (a dark leaf against a bright sky resolves to nearly sky) and edges stair-step
// again once tone mapped. Weighting each sample by 1 / (1 + luminance) averages them as the tone mapper will show
// them: it is the exact inverse of averaging Reinhard-tone-mapped samples (Karis, "Tone mapping", 2013).

#include AntiAliasing/Msaa.glsl

uniform sampler2DMS sceneSamples;
uniform float exposure;     // the tone mapper's, so the weights see the samples as bright as they will look

layout (location = 0) out vec4 out_Colour;

void main(void)
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    vec3 total = vec3(0.0);
    float totalWeight = 0.0;
    for (int i = 0; i < msaaSampleCount(); i++)
    {
        vec3 colour = texelFetch(sceneSamples, pixel, i).rgb;
        // One broken sample (NaN or infinite) would otherwise blank the pixel.
        colour = any(isnan(colour)) || any(isinf(colour)) ? vec3(0.0) : max(colour, vec3(0.0));
        float weight = 1.0 / (1.0 + dot(colour, vec3(0.2126, 0.7152, 0.0722)) * exposure);
        total += colour * weight;
        totalWeight += weight;
    }
    out_Colour = vec4(total / totalWeight, 1.0);
}
