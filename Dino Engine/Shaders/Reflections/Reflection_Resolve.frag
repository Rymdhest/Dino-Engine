#version 420

// Turns rays into a reflection. A pixel also uses the rays its neighbours shot, each weighed by how likely this surface would have been to send light along it, against how likely the ray was
// to be shot at all (Stachowiak): four rays for the price of one, and sharp near what's touching the surface, where they all land together. What each ray saw comes from a blurred copy of
// the scene as wide as the lobe is at that distance, so a rough surface sees a soft image without needing the rays to find it.

#include Globals.glsl
#include gBufferUtil.glsl
#include Reflections/Reflection.glsl

layout (location = 0) out vec4 out_Reflection;  // rgb what's reflected, multiplied by its confidence; a that confidence

uniform sampler2D rays;
uniform sampler2D rayConfidence;
uniform sampler2D sceneBlur;
uniform sampler2D depthPyramid;
uniform sampler2D gNormal;
uniform sampler2D gMaterials;
uniform sampler2D gAlbedo;

uniform int frameIndex;

const int NEIGHBOURS = 5;
const vec2 RING[5] = vec2[](vec2(0.0, 0.0), vec2(1.0, 0.3), vec2(-0.3, 1.0), vec2(-1.0, -0.3), vec2(0.3, -1.0));

vec3 fromScreen(vec3 screen)
{
    vec4 view = invProjectionMatrix * vec4(screen * 2.0 - 1.0, 1.0);
    return view.xyz / view.w;
}

void main(void)
{
    out_Reflection = vec4(0.0);

    ivec2 cell = ivec2(gl_FragCoord.xy);
    ivec2 size = textureSize(rays, 0);
    ivec2 pixel = min(cell * 2, ivec2(resolution) - 1);

    float depth = texelFetch(depthPyramid, pixel, 0).r;
    if (depth >= 1.0) return;
    vec4 materials = texelFetch(gMaterials, pixel, 0);
    if (!reflectionWanted(materials.r, texelFetch(gAlbedo, pixel, 0).a)) return;
    float roughness = max(materials.r, 0.03);

    vec2 uv = (vec2(pixel) + 0.5) / resolution;
    vec3 position = fromScreen(vec3(uv, depth));
    vec3 N = normalize(unCompressNormal(texelFetch(gNormal, pixel, 0).xyz));
    vec3 V = normalize(-position);
    float NdotV = max(dot(N, V), 1e-3);
    float alpha = roughness * roughness;
    float alpha2 = alpha * alpha;

    bool mirror = roughness <= REFLECT_MIRROR_ROUGHNESS;
    float spread = mirror ? 0.0 : mix(1.5, 4.5, smoothstep(0.1, 0.55, roughness));
    float angle = reflectionNoise(cell, frameIndex).x * 6.2831853;
    float c = cos(angle), s = sin(angle);
    mat2 turn = mat2(c, s, -s, c);

    float coneTangent = reflectionConeTangent(roughness);
    float metresToPixels = 0.5 * float(size.y) * projectionMatrix[1][1];

    vec3 sum = vec3(0.0);
    float believed = 0.0;
    float weights = 0.0;
    for (int i = 0; i < NEIGHBOURS; i++)
    {
        ivec2 tap = clamp(cell + ivec2(round(turn * RING[i] * spread)), ivec2(0), size - 1);
        vec4 ray = texelFetch(rays, tap, 0);
        if (ray.w <= 0.0) continue;
        float confidence = texelFetch(rayConfidence, tap, 0).r;

        float weight = 1.0;
        if (!mirror)
        {
            float hitDepth = texelFetch(depthPyramid, ivec2(ray.xy * resolution), 0).r;
            vec3 hitView = fromScreen(vec3(ray.xy, hitDepth));
            // A ray that found nothing still says what this surface reflects: it goes out along its own line, by its neighbour's reckoning.
            vec3 L = confidence > 0.0 ? normalize(hitView - position) : reflect(-V, N);
            vec3 H = normalize(L + V);
            float NdotL = max(dot(N, L), 0.0);
            float brdf = reflectionD(max(dot(N, H), 0.0), alpha2) * reflectionG1(NdotL, alpha2) * reflectionG1(NdotV, alpha2) / (4.0 * NdotV);
            weight = clamp(brdf / ray.w, 0.0, 16.0);
            if (i == 0) weight = max(weight, 0.05);
        }
        if (weight <= 0.0) continue;

        weights += weight;
        if (confidence <= 0.0)
        {
            if (mirror) break;
            continue;
        }

        // As far across as the lobe has opened by the time it gets there, in pixels of this grid.
        float viewDepth = max(-position.z + ray.z, 0.5);
        float footprint = 2.0 * coneTangent * ray.z * metresToPixels / viewDepth;
        vec3 colour = textureLod(sceneBlur, ray.xy, clamp(log2(max(footprint, 1.0)), 0.0, 5.0)).rgb;

        float tone = reflectionTone(colour);
        sum += colour * (weight * confidence * tone);
        believed += weight * confidence;
        if (mirror) break;
    }

    if (weights <= 1e-5 || believed <= 0.0) return;
    // Back from the space fireflies are tamed in.
    vec3 average = sum / believed;
    average /= max(1.0 - max(average.r, max(average.g, average.b)), 0.02);
    float confidence = believed / weights;
    out_Reflection = vec4(average * confidence, confidence);
}
