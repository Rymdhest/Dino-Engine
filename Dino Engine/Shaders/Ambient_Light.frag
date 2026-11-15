#version 420

// The light that comes from everywhere, the light surfaces give off themselves, and the sun. Ambient light is a hemisphere:
// the sky above and what the ground throws back below. Surfaces too rough to show what's around them reflect the sky's
// average here, for the cost of one lookup; anything smoother is the reflection pass's, which knows what it can really see
// (Reflections/Reflection_Composite.frag). The sun is here so that the scene is written once rather than twice, as nothing
// costs more than going over all of it, and a sunColour of 0 leaves it out. Nothing else here depends on a light entity, so
// with the sun and sky turned off only emissive surfaces remain. Drawn per pixel and per sample (MsaaRenderer.DrawSplit).

#include gBufferUtil.glsl
#include Lighting/lightingCalc.glsl
#include Globals.glsl
#include AntiAliasing/Msaa.glsl
#include Lighting/gBufferSample.glsl
#include Weather/Weather.glsl
#include Weather/Sky.glsl
#include Reflections/Reflection.glsl
#include Weather/Sun_Visibility.glsl
#include Lighting/SunShadow.glsl
#include Halls.glsl

layout (location = 0) out vec4 out_Colour;

uniform vec3 skyLight;
uniform vec3 groundLight;
uniform float emissionScale;
uniform vec3 sunDirectionViewSpace;
uniform vec3 sunColour;
// The sun's contact shadows, 1 lit and 0 shaded (ContactShadowRenderer, SHADOWS.md), per pixel like the occlusion.
uniform sampler2D contactShadows;
// The sun's soft shadow a pixel at a time, x what reaches it directly and y what fills its shadow (Shadows/SunShadow_Mask.comp), and how soft it is.
uniform sampler2D sunShadow;
uniform sampler2D sunShadowSoftness;

// How far, as a share of its depth, a sample can be from the pixel whose sun shadow it takes and still take it. A sample further from every pixel round
// it than that, on something thin and solid in front of all of them, a wire or a twig, works its own out (calcShadowParts); a canopy's, a blade or a
// leaf over the ground, takes the ground's, as the lighting of a field is its masses' and not its blades'.
const float SUN_SHADOW_APART = 0.006;
// The debug pictures: --render aodebug=1, contactdebug=1, shadowdebug=1 or 2 (OCCLUSION.md, SHADOWS.md). 0 for the picture.
uniform int occlusionDebug;
uniform int contactDebug;
uniform int shadowDebug;

// How wide the sun's highlight is, as alpha squared added to a surface's lobe (lightSurfaceSpread). Its disc is about half a degree, but a highlight that narrow on
// water as smooth as a puddle is a point, and rain rings a pixel across broke it into white fragments that jumped about whenever the camera moved: so it never
// reflects narrower than this, a glint that slides rather than glitters.
const float SUN_SPREAD = 0.0064;
// And how far clouds spread it on top of that, when they let none of it through as a disc. A sun behind cloud lights the whole sky; it doesn't stand in a puddle
// as a white hole, as it did in the Haven's rain, a dimmed sun still being a point.
const float CLOUD_SPREAD = 0.35;

// What the occluded part of a surface still gets of the light that bounces between it and what occludes it, so that occlusion doesn't grey out
// what's bright (Jimenez et al., "Practical Real-Time Strategies for Accurate Indirect Occlusion", 2016: a fit to the ground truth, by albedo).
vec3 occlusionMultiBounce(float visibility, vec3 albedo)
{
    vec3 a = 2.0404 * albedo - 0.3324;
    vec3 b = -4.7951 * albedo + 0.6417;
    vec3 c = 2.7552 * albedo + 0.6903;
    return max(vec3(visibility), ((visibility * a + b) * visibility + c) * visibility);
}

// Global illumination (GI.md): what GlobalIlluminationRenderer gathered, at half the resolution each way, a layer for each colour's
// irradiance A + B.n and a fourth with each texel's depth and how sure it is. 0 for giMix leaves the flat sky and ground.
uniform sampler2DArray giGathered;
uniform float giMix;
uniform float giStrength;
uniform int giDebug;

// The light gathered around this pixel, from the four texels about it, each by how near it is and how close its depth is to this one's,
// so that light never runs across the edge of what's in front. Returns the irradiance for a world-space normal, and in w how much of
// it GI has an answer for; `reflected` is the light coming along a direction, as blurred as the roughest surfaces see it, and `behind` the
// irradiance from the half of the world the surface turns its back on, for what a thin thing lets through.
vec4 giIrradiance(vec3 normal, float viewZ, vec3 direction, out vec3 reflected, out vec3 behind)
{
    vec2 at = gl_FragCoord.xy * 0.5 - 0.25;
    ivec2 low = ivec2(floor(at));
    vec2 f = at - vec2(low);
    ivec2 last = textureSize(giGathered, 0).xy - 1;
    vec4 red = vec4(0.0), green = vec4(0.0), blue = vec4(0.0);
    float known = 0.0, total = 0.0;
    for (int i = 0; i < 4; i++)
    {
        ivec2 corner = ivec2(i & 1, i >> 1);
        ivec2 q = clamp(low + corner, ivec2(0), last);
        vec4 guide = texelFetch(giGathered, ivec3(q, 3), 0);
        vec2 w2 = mix(1.0 - f, f, vec2(corner));
        float apart = abs(guide.x - viewZ) / (abs(viewZ) * 0.025 + 0.04);
        float w = w2.x * w2.y / (1.0 + apart * apart) + 1e-5;
        float sure = w * guide.y;
        red += texelFetch(giGathered, ivec3(q, 0), 0) * sure;
        green += texelFetch(giGathered, ivec3(q, 1), 0) * sure;
        blue += texelFetch(giGathered, ivec3(q, 2), 0) * sure;
        known += sure;
        total += w;
    }
    if (known <= 1e-6)
    {
        reflected = vec3(0.0);
        behind = vec3(0.0);
        return vec4(0.0);
    }
    red /= known;
    green /= known;
    blue /= known;
    vec3 a = vec3(red.x, green.x, blue.x);
    vec3 facing = vec3(dot(red.yzw, normal), dot(green.yzw, normal), dot(blue.yzw, normal));
    vec3 irradiance = max(a + facing, vec3(0.0));
    behind = max(a - facing, vec3(0.0));
    reflected = max(a / LIGHT_PI + (1.5 / LIGHT_PI) * vec3(dot(red.yzw, direction), dot(green.yzw, direction), dot(blue.yzw, direction)), vec3(0.0));
    return vec4(irradiance, clamp(known / total, 0.0, 1.0));
}

// Snow and ice (SUBSURFACE.md): nearly all the light they send back has gone in and come out again somewhere round about, and the further it goes the bluer it is,
// as ice takes red with every centimetre. Seen from this camera that's in two places, as the rest is only white: the edge of a shadow on snow runs blue into the
// shadow, and a hollow, a footprint or the joint between two blocks is blue where it's dark. The first is the sun's light on the snow round about, by how far
// each tap is from here in the world, in a Gaussian for each colour as wide as its reach (the profile's, times the amount): the screen's shadow at a fixed spiral
// of taps, with nothing random, as wide as blue's reach at this pixel's distance. A tap is any surface at all, which across a few tenths of a metre is the snow
// itself or something standing on it.
const int FROST_TAPS = 16;
// The taps' spiral reaches this many of blue's reaches out, and never more than this many pixels.
const float FROST_SPAN = 2.0;
const float FROST_MOST_PIXELS = 28.0;

// Where a pixel's middle is in view space, a distance along the view away (the occlusion's depth, which is that), for a perspective projection.
vec3 frostTapPosition(ivec2 pixel, float distance)
{
    vec2 ndc = (vec2(pixel) + 0.5) / resolution * 2.0 - 1.0;
    return vec3((ndc.x + projectionMatrix[2][0]) / projectionMatrix[0][0], (ndc.y + projectionMatrix[2][1]) / projectionMatrix[1][1], -1.0) * distance;
}

// How much of the sun lights the light that comes out of the snow here, per colour, given how much reaches it directly (lit).
vec3 frostDiffusedSun(Surface s, GBufferSample g, float lit)
{
    SssProfile p = sssProfileOf(SSS_FROST);
    vec3 sigma = p.reach.rgb * s.scatter;
    float distance = abs(texelFetch(occlusionDepth, g.screenPixel, 0).r);
    float metresPerPixel = 2.0 * distance / (projectionMatrix[1][1] * resolution.y);
    float span = min(FROST_SPAN * sigma.b / max(metresPerPixel, 1e-5), FROST_MOST_PIXELS);
    if (span < 1.0) return vec3(lit);

    // The sun at each tap first: where it's the same as here at all of them, as over most of a field in the sun or deep in a shadow, there's nothing to carry, and the
    // answer is exactly what it would be with the rest worked out.
    ivec2 last = textureSize(sunShadow, 0) - 1;
    ivec2 taps[FROST_TAPS];
    float sun[FROST_TAPS];
    float differs = 0.0;
    for (int i = 0; i < FROST_TAPS; i++)
    {
        // A golden-angle spiral, evenly over the disc, so that a straight shadow's edge is met at every distance rather than in rings.
        float radius = sqrt((float(i) + 0.5) / float(FROST_TAPS)) * span;
        float angle = float(i) * 2.39996323;
        taps[i] = clamp(g.screenPixel + ivec2(round(vec2(cos(angle), sin(angle)) * radius)), ivec2(0), last);
        vec2 shadow = texelFetch(sunShadow, taps[i], 0).rg;
        sun[i] = clamp(shadow.x + shadow.y, 0.0, 1.0);
        differs = max(differs, abs(sun[i] - lit));
    }
    if (differs < 1.0 / 1024.0) return vec3(lit);

    vec3 here = frostTapPosition(g.screenPixel, distance);
    vec3 falloff = 0.5 / max(sigma * sigma, vec3(1e-6));
    vec3 sum = vec3(lit);
    vec3 weights = vec3(1.0);
    for (int i = 0; i < FROST_TAPS; i++)
    {
        vec3 apart = frostTapPosition(taps[i], abs(texelFetch(occlusionDepth, taps[i], 0).r)) - here;
        vec3 w = exp(-dot(apart, apart) * falloff);
        sum += w * sun[i];
        weights += w;
    }
    return sum / weights;
}

// And the second: occlusion takes red from snow before blue, as what lights a hollow in it has come further through it, by how far each colour reaches (its
// reach's square root, 1 for blue). It stands for the albedo the occlusion's bounces between the hollow's sides see.
vec3 frostHollowAlbedo(Surface s)
{
    SssProfile p = sssProfileOf(SSS_FROST);
    vec3 deep = sqrt(p.reach.rgb / max(p.reach.b, 1e-4));
    return s.albedo * mix(vec3(1.0), deep, clamp(s.scatter / SNOW_SCATTER, 0.0, 1.0));
}

void main(void)
{
    GBufferSample g = readGBufferSample();
    if (g.depth >= 1.0) discard;

    Surface s = surfaceOf(g);
    float NdotV = max(dot(s.normal, s.view), 0.0);
    mat3 viewToWorld = mat3(invViewMatrix);
    vec3 worldNormal = viewToWorld * s.normal;
    float roughness = max(s.roughness, MIN_ROUGHNESS);
    vec3 worldReflection = viewToWorld * reflect(-s.view, s.normal);

    vec3 surrounding = mix(groundLight, skyLight, worldNormal.y * 0.5 + 0.5);
    // And what reaches its back, for a thin thing to let through.
    vec3 behind = mix(groundLight, skyLight, 0.5 - worldNormal.y * 0.5);
    vec3 environment = skyEnvironment(worldReflection, roughness, groundLight);
    // The flat sky and ground only stand in for what global illumination doesn't answer, and in a hall's room little of either comes in.
    float shelter = hallShelter((invViewMatrix * vec4(g.position, 1.0)).xyz);
    surrounding *= shelter;
    behind *= shelter;
    environment *= shelter;
    float gathered = 0.0;
    vec3 giLight = vec3(0.0);
    if (giMix > 0.0)
    {
        vec3 reflected;
        vec3 giBehind;
        vec4 gi = giIrradiance(worldNormal, g.position.z, worldReflection, reflected, giBehind);
        gathered = giMix * gi.w;
        giLight = gi.rgb / LIGHT_PI * giStrength;
        surrounding = mix(surrounding, giLight, gathered);
        behind = mix(behind, giBehind / LIGHT_PI * giStrength, gathered);
        environment = mix(environment, reflected * giStrength, gathered);
    }

    // A hall's room glows a little with the daylight let in, whatever GI made of it.
    surrounding += hallRoomLight((invViewMatrix * vec4(g.position, 1.0)).xyz, skyLight, sunColour);

    vec3 F0 = surfaceF0(s.albedo, s.metallic, s.roughness);
    vec3 specular = environmentBRDF(F0, roughness, NdotV);
    vec3 diffuse = s.albedo * (1.0 - s.metallic) * (1.0 - specular);

    bool frost = s.scatterKind == SSS_FROST && s.scatter > 0.0 && sssSwitches.x > 0.0;
    vec3 ambient = diffuse * surrounding * occlusionMultiBounce(g.ambient, frost ? frostHollowAlbedo(s) : s.albedo);
    // A leaf, a blade or a cloth lets through what lights its back: in the shade and under cloud it's most of what makes it glow (SUBSURFACE.md).
    if (s.scatter > 0.0 && s.scatterKind <= SSS_CANOPY) ambient += sssAmbientThin(s, behind) * g.ambient;
    if (!reflectionWanted(s.roughness, g.albedo.a))
    {
        ambient += specular * environment * specularOcclusion(NdotV, g.ambient, roughness);
    }
    vec3 colour = ambient + decodeEmission(s.albedo, g.materials.g) * emissionScale;

    // --render sssdebug=1: what each surface says of light under it (SUBSURFACE.md), as bright as how much: sheets blue, canopies green, flesh red, frost cyan. 2: nothing
    // here, for Flesh_Light.frag to draw what of the sun gets through flesh over. 3 is drawn with the sun, below.
    if (sssSwitches.z > 0.5 && sssSwitches.z < 2.5)
    {
        vec3 kindColour = s.scatterKind == SSS_SHEET ? vec3(0.1, 0.25, 1.0) : s.scatterKind == SSS_CANOPY ? vec3(0.1, 1.0, 0.15) : s.scatterKind == SSS_FLESH ? vec3(1.0, 0.12, 0.08) : vec3(0.1, 0.9, 1.0);
        vec3 shown = kindColour * (0.15 + 0.85 * s.scatter) * step(1e-3, s.scatter) + vec3(0.02);
        out_Colour = vec4(sssSwitches.z > 1.5 ? vec3(0.02) : shown, 1.0);
        return;
    }

    // --render gidebug=3: the light GI gives each surface, on white and with nothing else; 4: where it has an answer (green) and not (red).
    if (giDebug == 3 || giDebug == 5)
    {
        out_Colour = vec4(giLight * 0.6 * g.ambient, 1.0);
        return;
    }
    if (giDebug == 4)
    {
        out_Colour = vec4(mix(vec3(1.2, 0.05, 0.02), vec3(0.05, 0.9, 0.1), gathered / max(giMix, 1e-4)), 1.0);
        return;
    }

    // --render aodebug=1: the screen-space occlusion alone, on white.
    if (occlusionDebug == 1)
    {
        out_Colour = vec4(vec3(texelFetch(ambientOcclusion, g.screenPixel, 0).r) * 2.4, 1.0);
        return;
    }
    // --render contactdebug=1: the sun's contact shadows alone, on white.
    if (contactDebug == 1)
    {
        out_Colour = vec4(vec3(texelFetch(contactShadows, g.screenPixel, 0).r) * 2.4, 1.0);
        return;
    }

    if (sunColour != vec3(0.0) || shadowDebug != 0)
    {
        // How much of the sun gets here: the soft shadow (x) with what fills a long shadow from the air (y, Lighting/SunShadow.glsl), a pixel at a
        // time; the contact shadows only take what reaches it directly. The relief's own shadow is the sun's alone: it was cast along the sun's way,
        // and no other light's.
        vec2 shadow = texelFetch(sunShadow, g.screenPixel, 0).rg;
#ifdef PER_SAMPLE
        // Nothing to say for a face turned away, unless light gets through it from behind.
        bool facing = dot(s.normal, sunDirectionViewSpace) > 0.0 || s.scatter > 0.0;
        if (g.screenApart > SUN_SHADOW_APART && s.scatterKind != SSS_CANOPY && facing) shadow = calcShadowParts(g.position);
#endif
        float contact = texelFetch(contactShadows, g.screenPixel, 0).r;
        float lit = clamp(min(shadow.x, contact) + shadow.y, 0.0, 1.0);
        // Snow's own light comes from round about it (frostDiffusedSun); a sample on something thin over its pixel, which worked its shadow out for itself, isn't snow's.
        vec3 litUnder = vec3(lit);
        if (frost && g.screenApart <= SUN_SHADOW_APART) litUnder = frostDiffusedSun(s, g, lit);
        // --render sssdebug=3: what snow carries of the sun into its shadows, blue where it gains and red where it gives, over its own shadow in grey.
        if (sssSwitches.z > 2.5)
        {
            vec3 moved = litUnder - vec3(lit);
            out_Colour = vec4(vec3(lit * 0.3) + max(moved, vec3(0.0)) * 3.0 + vec3(max(-moved.b, 0.0) * 3.0, 0.0, 0.0), 1.0);
            return;
        }
        // --render shadowdebug=1: how soft the shadow here is, black sharpest and white softest; 2: the sun's shadow alone, on white.
        if (shadowDebug == 1)
        {
            out_Colour = vec4(vec3(texelFetch(sunShadowSoftness, g.screenPixel, 0).r) * 2.4, 1.0);
            return;
        }
        if (shadowDebug == 2)
        {
            out_Colour = vec4(vec3(lit) * 2.4, 1.0);
            return;
        }
        float through = weatherSunVisibility((invViewMatrix * vec4(g.position, 1.0)).xz);
        vec3 radiance = sunColour * lit * g.materials.a * through;
        vec3 underRadiance = sunColour * litUnder * g.materials.a * through;
        colour += lightSurfaceSplit(s, sunDirectionViewSpace, radiance, underRadiance, SUN_SPREAD + CLOUD_SPREAD * (1.0 - clamp(through, 0.0, 1.0)));
    }
    out_Colour = vec4(colour, 1.0);
}
