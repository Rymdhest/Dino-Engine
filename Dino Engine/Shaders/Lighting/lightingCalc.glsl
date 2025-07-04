float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = 3.14159265359 * denom * denom;

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

float calcAttunuation(vec3 lightPos, vec3 position, vec3 attenuation)
{
    float dist = length(lightPos - position.xyz);
    return clamp(1.0 / (attenuation.x + attenuation.y * dist + attenuation.z * dist * dist) - 1, 0, 999);
}

// all done in view space
vec3 getLightPBR(vec3 albedo, vec3 normal, float roughness, float metallic, vec3 lightColour, float attenuation, float ambient, vec3 viewDir, vec3 LightDir, float lightFactor)
{
    vec3 F0 = vec3(0.04);
    vec3 Lo = vec3(0.0);
    F0 = mix(F0, albedo, metallic);
    vec3 N = normalize(normal);
    vec3 V = viewDir;

    // calculate per-light radiance
    vec3 L = normalize(LightDir);
    vec3 H = normalize(V + L);
    vec3 radiance = lightColour* attenuation;

    // cook-torrance brdf
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);

    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    vec3 specular = numerator / denominator;

    // add to outgoing radiance Lo
    float NdotL = max(dot(N, L), 0.0);
    Lo += (kD * albedo / 3.14159265359 + specular) * radiance * NdotL;

    vec3 totalAmbient = vec3(albedo * lightColour)* attenuation* ambient;
    vec3 color = totalAmbient + Lo * lightFactor*(1.0- ambient);

    return color;
}