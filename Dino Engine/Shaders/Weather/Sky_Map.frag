#version 420

// The sky, one hemisphere of it, for everything that reflects it or looks up: the colour of the air and the clouds in it, as in Ghost of Tsushima (Patry, "Real-Time Samurai
// Cinema", 2021). Each ray marches the cloud layer. Whether a bit of cloud scatters forward or back depends on how deep in it the light has come: thin wisps send the sun on towards
// the eye, which is the silver lining of a backlit cloud, and the heart of one sends light back as if it were a white wall, which is its dark edge. The map is drawn at a low resolution
// and every few frames, as clouds are slow and everything that reads them is soft.

#include Globals.glsl
#include Weather/Weather.glsl
#include Weather/Clouds.glsl

in vec2 textureCoords;
layout (location = 0) out vec4 out_Colour;

const int VIEW_STEPS = 14;
const int LIGHT_STEPS = 3;
const float CLOUD_EXTINCTION = 0.03;        // Per metre, in the thickest of it.
const float LIGHT_EXTINCTION = 0.012;       // What the sun loses per metre on its way in. Less than it should be: this stands in for the light that gets through by many scatterings.
const float BACK_SCATTER_GAIN = 2.16;       // Patry: multiple scattering in a dense layer of albedo 0.9, from simulating one.
const float LOW_SUN_BAND = 5.0;             // How closely a low sun's glow keeps to the horizon: it's down to a third 12 degrees up.
const float LOW_SUN_SPREAD = 8.0;           // And to the sun's side of the sky: the higher, the narrower.
const vec2 LOW_SUN_ROUND = vec2(0.766, 0.9976);     // The cosines of how far from the sun its glow begins, 40 degrees, and is all there is, 4.
const float LOW_SUN_GLOW = 0.085;           // What the air it lights shines with, of the sun's own radiance: little enough to keep its colour, as anything brighter is white.
const vec3 LOW_SUN_TINT = vec3(1.0, 0.8, 0.5);      // What coming so far through the air does to its colour again, over what the sun's own already says.
const float LOW_SUN_DEEPENS = 0.42;         // How much darker the sky overhead is than it would be, as the light's all down along the horizon.
const float LOW_SUN_HALO = 0.03;            // Of the glow close round the sun, where every other sky has 0.12.
const float PI = 3.14159265359;

float henyeyGreenstein(float cosine, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * PI * pow(max(1.0 + g2 - 2.0 * g * cosine, 1e-4), 1.5));
}

// The sky behind the clouds: bright along the horizon and deep overhead under a clear sky, and the other way round when it's overcast, as the CIE overcast sky has it. Between clouds it's
// still the world's own blue, until there's nothing left of it to see.
vec3 airColour(vec3 direction, float cover)
{
    float up = max(direction.y, 0.0);
    vec3 clear = rippleLook.yzw * (1.0 - pow(up, 0.7) * 0.9);
    vec3 overcast = skyColour * ((1.0 + 2.0 * up) * (1.0 / 3.0) * 0.72);
    return mix(clear, overcast, smoothstep(0.55, 1.0, cover)) * weatherSunColour.w;
}

// What a low sun does to the air towards it, for a view that looks out at the sky (the picture behind the main menu; cloudMotion.z is nothing otherwise): its light has come a long
// way through the air by then, which lights all of it along the horizon that way and round the sun itself, and the air it lights is gold in place of blue, not gold on top of it:
// added to the sky's own colour it only comes to white. The rest of the sky is deeper for it. x is how much of the sky's own colour is left, and yzw what's there in place of it.
vec4 lowSunAir(vec3 direction, vec3 towardsSun, vec3 sun)
{
    float low = cloudMotion.z * smoothstep(0.5, 0.12, towardsSun.y);
    if (low <= 0.0) return vec4(1.0, 0.0, 0.0, 0.0);

    float up = max(direction.y, 0.0);
    float around = dot(normalize(direction.xz + vec2(1e-5)), normalize(towardsSun.xz + vec2(1e-5))) * 0.5 + 0.5;
    float along = exp(-up * LOW_SUN_BAND) * pow(around, LOW_SUN_SPREAD);
    float round = smoothstep(LOW_SUN_ROUND.x, LOW_SUN_ROUND.y, dot(direction, towardsSun));
    float lit = low * max(along, round * sqrt(round) * 0.9);
    float deepens = 1.0 - low * LOW_SUN_DEEPENS * smoothstep(0.02, 0.4, up);
    return vec4((1.0 - lit) * deepens, sun * LOW_SUN_TINT * (lit * LOW_SUN_GLOW));
}

void main(void)
{
    vec2 p = textureCoords * 2.0 - 1.0;
    float r2 = dot(p, p);
    // Past the horizon the map holds the horizon, so that its mips stay clean along that edge.
    if (r2 > 1.0)
    {
        p *= inversesqrt(r2);
        r2 = 1.0;
    }
    vec3 direction = normalize(vec3(2.0 * p.x, 1.0 - r2, 2.0 * p.y));

    vec2 focus = focusPoint.w > 0.0 ? focusPoint.xz : viewPosWorld.xz;
    float coverHere = cloudCoverAt(focus);
    vec3 sky = airColour(direction, coverHere);
    vec3 towardsSun = weatherSun.xyz;
    float sunCosine = dot(direction, towardsSun);

    // A clear sky has its glow around the sun, and dust and thin cloud spread it wider.
    vec3 sun = weatherSunColour.rgb;
    vec4 lowSun = lowSunAir(direction, towardsSun, sun);
    sky = sky * lowSun.x + lowSun.yzw * (1.0 - coverHere * 0.9);
    float haze = weatherAir.x * 0.5 + weatherAir.w * 0.5;
    // Looked at, the glow round the sun is kept close to it, or all that side of the sky is white.
    float halo = mix(0.12, LOW_SUN_HALO, cloudMotion.z * smoothstep(0.5, 0.12, towardsSun.y));
    sky += sun * (henyeyGreenstein(sunCosine, 0.86 - 0.3 * haze) * halo * (1.0 - coverHere * 0.9));

    float elevation = max(direction.y, 0.03);
    float start = cloudShape.x / elevation;
    float end = (cloudShape.x + cloudShape.y) / elevation;
    // Far off, the layer is a line along the horizon, and a long march through it would only sample it sparsely.
    end = min(end, start + cloudShape.y * 5.0);
    float stepLength = (end - start) / float(VIEW_STEPS);

    float transmittance = 1.0;
    vec3 light = vec3(0.0);
    vec3 ambient = skyColour * (weatherSunColour.w * 0.42);
    vec3 origin = vec3(focus.x, 0.0, focus.y);

    for (int i = 0; i < VIEW_STEPS; i++)
    {
        vec3 position = origin + direction * (start + (float(i) + 0.5) * stepLength);
        float cover = cloudCoverAt(position.xz);
        float density = cloudDensity(position, cover, 0.0);
        if (density <= 0.002) continue;

        float lightDepth = 0.0;
        float lightStep = cloudShape.y * 0.22;
        for (int k = 0; k < LIGHT_STEPS; k++)
        {
            vec3 towards = position + towardsSun * (lightStep * (float(k) + 0.5));
            lightDepth += cloudDensity(towards, cover, 1.0) * lightStep;
        }
        float toLight = exp(-lightDepth * LIGHT_EXTINCTION);
        float segment = exp(-density * CLOUD_EXTINCTION * stepLength);

        // Patry's asymmetry: forward in the wisps, backward in the heart of it.
        float depthCue = toLight * segment;
        float phase = mix(henyeyGreenstein(sunCosine, -0.15) * BACK_SCATTER_GAIN, henyeyGreenstein(sunCosine, 0.85), depthCue);

        // The underside only sees the ground; tops see the whole sky.
        float height = clamp((position.y - cloudShape.x) / cloudShape.y, 0.0, 1.0);
        vec3 source = sun * (phase * toLight) + ambient * mix(0.55, 1.15, height);
        light += transmittance * (1.0 - segment) * source;
        transmittance *= segment;
        if (transmittance < 0.01) break;
    }

    // Distance takes the contrast out of clouds near the horizon.
    float far = smoothstep(0.02, 0.22, direction.y);
    vec3 cloudy = sky * transmittance + light;
    out_Colour = vec4(mix(sky, cloudy, far), 1.0);
}
