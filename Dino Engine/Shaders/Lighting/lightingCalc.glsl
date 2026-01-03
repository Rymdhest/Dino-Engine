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
vec3 getLightPBROLD(vec3 albedo, vec3 normal, float roughness, float metallic, vec3 lightColour, float attenuation, float ambient, vec3 viewDir, vec3 LightDir, float lightFactor)
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

    float subSurfaceAmount = 0.35;

    vec3 saturated = mix(vec3(dot(albedo, vec3(0.2126, 0.7152, 0.0722))), albedo, 1.4);
    vec3 subColor = saturated*radiance*lightFactor;
    
    
    float backLit = clamp(dot(-N, L), 0.0, 1.0);
    float scatterProfile = pow(backLit, 2.0);

    vec3 scatter = saturated;
    vec3 subSurfaceScatterRadius = vec3 (1.0);
    //subSurfaceScatterRadius *= scatter;
    vec3 SubSurfaceScatter = exp(-3.0*abs(NdotL)/(subSurfaceScatterRadius+0.001));
    return color*(1.0)+subColor*subSurfaceAmount*scatterProfile;
}

vec3 shiftHueFast(vec3 color, float hueShift) {
    const mat3 rgb2yiq = mat3(
         0.299,  0.587,  0.114,
         0.595716, -0.274453, -0.321263,
         0.211456, -0.522591,  0.311135
    );
    const mat3 yiq2rgb = mat3(
         1.0,  0.9563,  0.6210,
         1.0, -0.2721, -0.6474,
         1.0, -1.1070,  1.7046
    );

    vec3 yiq = rgb2yiq * color;
    float hue = atan(yiq.z, yiq.y); 
    float chroma = sqrt(yiq.y * yiq.y + yiq.z * yiq.z);

    hue += hueShift * 6.2831853; 
    yiq.y = chroma * cos(hue);
    yiq.z = chroma * sin(hue);

    return clamp(yiq2rgb * yiq, 0.0, 1.0);
}

vec3 getLightPBR(
    vec3 albedo,
    vec3 normal,
    float roughness,
    float metallic,
    vec3 lightColour,
    float attenuation,
    float ambient,
    vec3 viewDir,
    vec3 lightDir,
    float lightFactor,
    float lightFactorEntry,
    float materialTransparancy,
    float geometricDepth
) {
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    vec3 N = normalize(normal);
    vec3 V = normalize(viewDir);
    vec3 L = normalize(lightDir);
    vec3 H = normalize(V + L);

    vec3 radiance = lightColour * attenuation;

    // ----- PBR FRONT LIGHTING -----
    float NDF = DistributionGGX(N, H, roughness);
    float G   = GeometrySmith(N, V, L, roughness);
    vec3  F   = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 kS = F;
    vec3 kD = (1.0 - kS) * (1.0 - metallic);

    float NdotL = max(dot(N, L), 0.0);
    vec3  specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * NdotL, 0.0001);

    vec3 diffuse = kD * albedo / 3.14159265359;
    vec3 LoFront = (diffuse + specular) * radiance * NdotL;


    // ----- BACK LIGHTING TRANSMISSION -----
    float backLit = clamp(dot(V, normalize(-L+N*1.0)), -1.0, 1.0);
    backLit = backLit * 0.5+0.5;
    backLit = pow(backLit,1.0);
    // Boost saturation for transmitted light
    vec3 avg = vec3(dot(albedo, vec3(0.2126, 0.7152, 0.0722)));
    vec3 saturated = mix(avg, albedo, 1.0); // 1.0 = no boost
    saturated = shiftHueFast(saturated, 1.0);
    // Simple absorption through the leaf
    float depthFactor = 1.0/(pow(geometricDepth*2.0, 3.0)+1.0);
    vec3 transmission = saturated * radiance * backLit*depthFactor;
    //transmission /= (pow(geometricDepth*2.0, 2.0)+1.0);
    // Sharpen the backlight falloff
    float backBlend = pow(backLit, 1.0);

    // ----- AMBIENT -----
    vec3 totalAmbient = albedo * lightColour * attenuation * ambient;

    // ----- COMBINE -----
    // Blend between front PBR and back transmission based on light direction
    //vec3 litColor = LoFront*lightFactor+ transmission * materialTransparancy;
    vec3 litColor = mix (LoFront*lightFactor, transmission*lightFactorEntry, materialTransparancy*backBlend);

    return totalAmbient + litColor * (1.0 - ambient);
}