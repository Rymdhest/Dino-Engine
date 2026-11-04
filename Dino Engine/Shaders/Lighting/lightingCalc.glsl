// How surfaces answer light, shared by the deferred light passes, the fog and the water. Colours are radiance: a white
// surface lit head-on by a light of colour c shines at c / pi.

const float LIGHT_PI = 3.14159265359;
const float LIGHT_SOURCE_RADIUS = 0.4;      // Metres: how close a local light behaves as a point. Keeps it finite up close.
const float MIN_ROUGHNESS = 0.045;          // Below this the highlight is narrower than half floats can hold.
const float TRANSMISSION_GAIN = 1.2;        // How much brighter a backlit leaf reads than the same leaf lit from the front.

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = LIGHT_PI * denom * denom;

    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// What reaches a point from a local light of intensity 1: inverse square from a source of some size, windowed to reach
// exactly zero at the light's radius, so that its volume can end there with no visible edge. The window is the gentler
// sibling of the usual (1 - (d/r)^4)^2 (Karis, "Real Shading in Unreal Engine 4", 2013): that one holds on until almost
// the end and then drops, and light at the edge of its reach meets the ground so flat that every bump frays the drop.
float lightFalloff(float distanceToLight, float radius)
{
    float ratio = distanceToLight / radius;
    float window = clamp(1.0 - ratio * ratio, 0.0, 1.0);
    return window * window / (distanceToLight * distanceToLight + LIGHT_SOURCE_RADIUS * LIGHT_SOURCE_RADIUS);
}

// What a surface reflects of a whole environment, from its colour at normal incidence (Karis, "Physically Based Shading
// on Mobile", 2014): the split-sum lookup of image-based lighting as a fit, with no texture.
vec3 environmentBRDF(vec3 F0, float roughness, float NdotV)
{
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572, 0.022);
    const vec4 c1 = vec4(1.0, 0.0425, 1.04, -0.04);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NdotV)) * r.x + r.y;
    vec2 scaleBias = vec2(-1.04, 1.04) * a004 + r.zw;
    return F0 * scaleBias.x + scaleBias.y;
}

struct Surface
{
    vec3 albedo;
    vec3 normal;
    vec3 view;              // Towards the camera.
    float roughness;
    float metallic;
    float translucency;     // How much light gets through: 0 for anything solid.
    float thickness;        // How far that light has to travel inside.
};

// The light a surface sends to the camera from one light. L points towards the light; radiance is what arrives there.
vec3 lightSurface(Surface s, vec3 L, vec3 radiance)
{
    vec3 N = s.normal;
    vec3 V = s.view;
    vec3 H = normalize(V + L);
    float roughness = max(s.roughness, MIN_ROUGHNESS);
    float NdotL = max(dot(N, L), 0.0);

    vec3 F0 = mix(vec3(0.04), s.albedo, s.metallic);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * NdotL, 0.0001);
    vec3 diffuse = (1.0 - F) * (1.0 - s.metallic) * s.albedo / LIGHT_PI;
    vec3 reflected = (diffuse + specular) * NdotL;

    // Thin things let light through, most of all seen against it. What comes out has lost the colours the leaf absorbs.
    float backLit = dot(V, normalize(N - L)) * 0.5 + 0.5;
    vec3 tint = max(mix(vec3(dot(s.albedo, vec3(0.2126, 0.7152, 0.0722))), s.albedo, 1.2), vec3(0.0));
    float absorption = 1.0 / (pow(s.thickness * 2.0, 3.0) + 1.0);
    vec3 transmitted = tint * (s.translucency * absorption * pow(backLit, 1.9) * TRANSMISSION_GAIN / LIGHT_PI);

    return (reflected + transmitted) * radiance;
}
