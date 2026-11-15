// How surfaces answer light, shared by the deferred light passes, the fog and the water. Colours are radiance: a white
// surface lit head-on by a light of colour c shines at c / pi.

#include Lighting/SubsurfaceKinds.glsl

const float LIGHT_PI = 3.14159265359;
const float LIGHT_SOURCE_RADIUS = 0.4;      // Metres: how close a local light behaves as a point. Keeps it finite up close.
const float MIN_ROUGHNESS = 0.045;          // Below this the highlight is narrower than half floats can hold.
const float WATER_SHEEN = 0.09;             // What a film of water adds to a surface's reflectance. See surfaceF0.

// How each kind of subsurface scattering behaves (SUBSURFACE.md), as SubsurfaceProfiles uploads it from SubsurfaceSettings, four rows to a kind in the order of the
// SSS_ kinds (Lighting/SubsurfaceKinds.glsl):
//   0  rgb  metres light goes through flesh or frost before it's down to a third, at an amount of 1      w  how far light let through wraps round to the side facing it
//   1  rgb  the colour light comes out in: of a thin thing a multiple of its own, of flesh or frost its own w  how much of the surface's own hue is in flesh's or frost's
//   2  x    how much of a light gets through       y  of the light from all round       z  of what leaves pass among themselves  w  how saturated a thin thing's light is
//   3  rgb  how far flesh's or frost's own diffuse light wraps past where the light stops, at an amount of 1, each colour as far as it gets under the surface
// sssSwitches: x 1 with subsurface scattering on and 0 with it off, y 1 with the sun through flesh measured in a map of flesh alone (FleshDepth), z the debug picture,
// 0 for none (Ambient_Light.frag).
layout(std140, binding = 14) uniform SubsurfaceData
{
    vec4 sssProfile[16];
    vec4 sssSwitches;
};

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

// GGX by the lobe's alpha squared, which is what a spread adds to.
float distributionGGXAlpha2(float NdotH, float alpha2)
{
    float denominator = NdotH * NdotH * (alpha2 - 1.0) + 1.0;
    return alpha2 / (LIGHT_PI * denominator * denominator);
}

// The masking and shadowing of GGX's microfacets as Heitz has it exactly ("Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs", 2014), height
// correlated, with the microfacet model's 4 N.L N.V taken into it: what GeometrySmith gives over that, but GeometrySmith is Schlick's fit with UE4's remapping of
// roughness for analytic lights, made to calm the roughest, which darkens every smooth surface at a grazing angle as if it were half rough: a low sun's glint on wet
// stone came out at two thirds of itself. The reflection pass's own masking (Reflections/Reflection.glsl) is this one's, so the two now agree.
float visibilitySmithCorrelated(float NdotV, float NdotL, float alpha2)
{
    float lightSide = NdotV * sqrt(NdotL * NdotL * (1.0 - alpha2) + alpha2);
    float viewSide = NdotL * sqrt(NdotV * NdotV * (1.0 - alpha2) + alpha2);
    return 0.5 / max(lightSide + viewSide, 1e-5);
}

// The split sum of image-based lighting for exact GGX (Rendering/Renderers/Lighting/SpecularLookup.cs): x what F0 is multiplied by and y what's added to it, so a surface
// reflects F0 x + y of its whole surroundings and a perfect mirror's microfacets x + y of what falls on them, once. Bound once to its own unit, which nothing else uses.
layout(binding = 31) uniform sampler2D specularLookup;

vec2 specularScaleBias(float roughness, float NdotV)
{
    return textureLod(specularLookup, vec2(clamp(NdotV, 0.0, 1.0), max(roughness, MIN_ROUGHNESS)), 0.0).rg;
}

// What GGX loses by letting light reflect only once: a rough surface's microfacets shade each other, and the light that would have bounced on between them and out is
// missing, so rough metal goes dark and dull, by half at the roughest (Kulla and Conty, "Revisiting Physically Based Shading at Imageworks", 2017). This puts it back as a
// multiple of the single-scattered highlight, by what the lobe's microfacets lose at all (one less the split sum's x + y), coloured by how much the surface reflects
// (Filament's energy compensation). For a dielectric it's a few percent at the most.
vec3 specularMultipleScattering(vec3 F0, vec2 scaleBias)
{
    return 1.0 + F0 * (1.0 / max(scaleBias.x + scaleBias.y, 0.1) - 1.0);
}

// The same for light from all round (Fdez-Agüera, "A Multiple-Scattering Microfacet Model for Real-Time Image-based Lighting", 2019): what bounces on between the
// microfacets and out again, as a share of what arrives from everywhere, which is lit by the surroundings' irradiance and not their reflection, as it's lost its way.
// singleScattered is F0 x + y. A diffuse surface's own light is what's left after both.
vec3 specularMultipleAmbient(vec3 F0, vec2 scaleBias, vec3 singleScattered)
{
    float lost = 1.0 - (scaleBias.x + scaleBias.y);
    vec3 average = F0 + (1.0 - F0) / 21.0;
    return singleScattered * average * lost / (1.0 - lost * average);
}

// How much a surface reflects head-on. A film of water would add almost nothing to this: seen from a low camera it's the grazing angle that makes wet ground shine, and this camera looks
// nearly straight down at it, where water reflects 2%. So anything as smooth as water gets more than physics gives it, as Ghost of Tsushima lets its look override what's correct. Smoothness
// is the sign of it, as the G-buffer has nowhere else to say so, and little that's dry is this smooth.
vec3 surfaceF0(vec3 albedo, float metallic, float roughness)
{
    return mix(vec3(0.04 + WATER_SHEEN * smoothstep(0.3, 0.07, roughness)), albedo, metallic);
}

// How much of what a surface reflects its own shape hides from it, from its ambient occlusion (Lagarde and de Rousiers, "Moving Frostbite to PBR").
float specularOcclusion(float NdotV, float ambient, float roughness)
{
    return clamp(pow(NdotV + ambient, exp2(-16.0 * roughness - 1.0)) - 1.0 + ambient, 0.0, 1.0);
}

// How big a local light looks in what reflects it: a lantern's glass and flame, not a point. A highlight is the reflection of the light, so on smooth wet ground a point light
// was a pin of a pixel that sparkled and jumped as the camera moved, where a real lantern shows as a soft glowing blob as big as itself.
const float LIGHT_GLOW_RADIUS = 0.2;

// What a local light at this distance adds to a surface's lobe as alpha squared (lightSurfaceSpread), as Karis's sphere lights widen it ("Real Shading in Unreal Engine 4",
// 2013): a sphere seen at an angle a across reflects through a lobe about a half of that wide, added in quadrature, which keeps what the light gives back the same and only
// spreads it, and leaves anything rough enough already as it was.
float lightSpreadAt(float distanceToLight)
{
    float across = min(LIGHT_GLOW_RADIUS / max(distanceToLight, 1e-3), 1.0);
    return 0.25 * across * across;
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

// What a surface reflects of a whole environment, once, from its colour at normal incidence: the split sum (specularScaleBias). It once came from Karis's analytic fit
// ("Physically Based Shading on Mobile", 2014), which was made for UE4's masking and read a fifth dark on half-rough metal.
vec3 environmentBRDF(vec3 F0, float roughness, float NdotV)
{
    vec2 scaleBias = specularScaleBias(roughness, NdotV);
    return F0 * scaleBias.x + scaleBias.y;
}

struct Surface
{
    vec3 albedo;
    vec3 normal;
    vec3 view;              // Towards the camera.
    float roughness;
    float metallic;
    int scatterKind;        // What light does once it's in under the surface (SSS_ in Lighting/SubsurfaceKinds.glsl).
    float scatter;          // How much: for a sheet or a canopy what gets through, for flesh or frost how far light goes in it, 0 to 1. 0 for anything solid.
    vec2 scaleBias;         // The split sum at its roughness and the angle it's seen at (specularScaleBias), looked up once for every light.
};

// ---------------------------------------------------------------- Subsurface scattering (SUBSURFACE.md)

// A kind's four rows. Each is read at an index known where it's written, and the right one chosen: read by a kind that differs from pixel to pixel, every row was a load
// of its own for every pixel, and for every lamp at every pixel, which made the point lights a tenth of a millisecond slower.
struct SssProfile
{
    vec4 reach;     // rgb metres, w back wrap
    vec4 colour;    // rgb colour, w hue
    vec4 gains;     // x direct, y ambient, z fill, w saturation
    vec4 wrap;      // rgb diffuse wrap
};

SssProfile sssProfileOf(int kind)
{
    SssProfile p;
    p.reach = kind == SSS_CANOPY ? sssProfile[4] : kind == SSS_FLESH ? sssProfile[8] : kind == SSS_FROST ? sssProfile[12] : sssProfile[0];
    p.colour = kind == SSS_CANOPY ? sssProfile[5] : kind == SSS_FLESH ? sssProfile[9] : kind == SSS_FROST ? sssProfile[13] : sssProfile[1];
    p.gains = kind == SSS_CANOPY ? sssProfile[6] : kind == SSS_FLESH ? sssProfile[10] : kind == SSS_FROST ? sssProfile[14] : sssProfile[2];
    p.wrap = kind == SSS_CANOPY ? sssProfile[7] : kind == SSS_FLESH ? sssProfile[11] : kind == SSS_FROST ? sssProfile[15] : sssProfile[3];
    return p;
}

// The colour a thin thing lets light through in: its own, deeper, as what crosses a leaf has been through more of it than what bounces off, times its kind's.
vec3 sssThinColour(vec3 albedo, SssProfile p)
{
    float luma = dot(albedo, vec3(0.2126, 0.7152, 0.0722));
    return max(mix(vec3(luma), albedo, p.gains.w), vec3(0.0)) * p.colour.rgb;
}

// How pale skin is, as an albedo's luminance, when what comes through flesh comes out as its kind says: a person's. Paler lets more out and darker less, up to SSS_PALEST times as
// much, as light coming out through skin crosses the same pigment it would have bounced off.
const float SSS_SKIN_LUMA = 0.47;
const float SSS_PALEST = 1.25;

// The colour light comes out of flesh in, before what its thickness takes: blood's, with something of the surface's hue in it, and as bright as the surface is pale, so a
// dark hide glows far less than a person's ear.
vec3 sssThickColour(vec3 albedo, SssProfile p)
{
    float luma = max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 0.02);
    return p.colour.rgb * mix(vec3(1.0), min(albedo / luma, vec3(3.0)), p.colour.w) * min(luma / SSS_SKIN_LUMA, SSS_PALEST);
}

// How a canopy passes light on, 1 straight through: leaves turned every way, each letting through what falls on its back as a Lambertian sheet does, send it on
// mostly the way it was going, a third as much sideways and almost none back (the area scattering phase function of a uniform spread of bi-Lambertian leaves:
// Ross 1981, Shultis and Myneni 1988). That's what a crown's light depends on, as a card's normal is the crown's and says nothing of its leaves. cosine is between
// the way the light travels and the way to the eye. Exactly it's (sin b + (3 - b) cos b) / 3 for the angle b between them, whose odd part is (1 - pi / 6) cos b and whose even
// part a quartic follows to within 0.002, so it costs a few multiplications rather than an acos and a sin for every light at every leaf.
float sssCanopyPhase(float cosine)
{
    float c = clamp(cosine, -1.0, 1.0);
    return 0.3336 + c * (0.4764 + c * (0.161 + 0.0269 * c * c));
}

// How much of a light falling on the far side of a thin thing comes out of the side facing the eye: a Lambertian sheet's back, wrapped a little as light that's
// crossed it is spread, and giving the same over a whole sphere of light as an unwrapped one.
float sssBackLit(vec3 N, vec3 L, float wrap)
{
    return clamp((wrap - dot(N, L)) / ((1.0 + wrap) * (1.0 + wrap)), 0.0, 1.0);
}

// Diffuse light that goes on past the terminator, as light that's got in under a surface comes out a little way on, each colour as far as it gets there (wrap, 0
// for Lambert). It's eased in with no edge where it starts, and gives what an unwrapped one does over a sphere of light: what reaches past the terminator is taken
// from the lit side.
vec3 sssWrappedDiffuse(float NdotL, vec3 wrap)
{
    vec3 lit = pow(clamp((NdotL + wrap) / (1.0 + wrap), 0.0, 1.0), 1.0 + wrap);
    return lit * (2.0 + wrap) / (2.0 + 2.0 * wrap);
}

// What a thin thing, a sheet or a canopy, lets through to the eye from a light, for every unit of light reaching it.
vec3 sssThinLight(Surface s, SssProfile p, vec3 L)
{
    vec3 colour = sssThinColour(s.albedo, p) * (s.scatter * p.gains.x / LIGHT_PI);
    if (s.scatterKind == SSS_CANOPY) return colour * (sssCanopyPhase(-dot(s.view, L)) + p.gains.z);
    return colour * sssBackLit(s.normal, L, p.reach.w);
}

// What snow and ice send back of what gets into them, before it's gone deep: a half-space of bright grains, each scattering once and mostly on the way the light was
// going (Lommel and Seeliger's law, with the canopy's forward-leaning phase), so a snowfield shines brighter looking towards the sun than away from it, and brightest
// where the light comes in low. Its own colour, cooled. For every unit of light reaching it.
vec3 sssFrostLight(Surface s, SssProfile p, vec3 L)
{
    float lit = max(dot(s.normal, L), 0.0);
    float law = lit / max(lit + max(dot(s.normal, s.view), 0.0), 1e-3);
    return sssThinColour(s.albedo, p) * (s.scatter * p.gains.x * sssCanopyPhase(-dot(s.view, L)) * law / LIGHT_PI);
}

// What flesh lets out towards the eye of a light that went in on its far side, given what of it got here (per colour, measured in the flesh map, with how squarely it went
// in): its kind's colour, and the same every way, as light is that comes out from under a surface. For every unit of light falling on it.
vec3 sssThickLight(Surface s, SssProfile p, vec3 throughThickness)
{
    return sssThickColour(s.albedo, p) * throughThickness * (p.gains.x * sssSwitches.x / LIGHT_PI);
}

// What a thin thing lets through of the light from all round it: what reaches its back from the half of the world behind it (behind, as radiance, like the sky and
// ground's), for each unit of its occlusion.
vec3 sssAmbientThin(Surface s, vec3 behind)
{
    SssProfile p = sssProfileOf(s.scatterKind);
    return sssThinColour(s.albedo, p) * behind * (s.scatter * p.gains.y * sssSwitches.x);
}

// Per metre and per colour, how fast light that's gone into flesh or frost dies away: one over how far each colour gets, which the amount scales.
vec3 sssExtinction(Surface s, SssProfile p)
{
    return 1.0 / max(p.reach.rgb * s.scatter, vec3(1e-4));
}

// The light a surface sends to the camera from one light, whose highlight is spread as if the light were `spread` wide: that much is added to the lobe's
// alpha squared, so the light given back stays the same and only goes further round. A point light has none. L points towards the light; radiance is
// what arrives there, for the highlight, and underRadiance what lights the light that goes in under the surface, which in snow is what's come in round about
// as well as here (Ambient_Light.frag's frostDiffusedSun) and anywhere else the same.
vec3 lightSurfaceSplit(Surface s, vec3 L, vec3 radiance, vec3 underRadiance, float spread)
{
    vec3 N = s.normal;
    vec3 V = s.view;
    vec3 H = normalize(V + L);
    float lobe = max(s.roughness, MIN_ROUGHNESS);
    float alpha2 = lobe * lobe * lobe * lobe + spread;
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = clamp(dot(N, V), 1e-4, 1.0);

    vec3 F0 = surfaceF0(s.albedo, s.metallic, s.roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    float lobeShape = distributionGGXAlpha2(max(dot(N, H), 0.0), alpha2) * visibilitySmithCorrelated(NdotV, NdotL, alpha2);
    vec3 specular = F * lobeShape * specularMultipleScattering(F0, s.scaleBias);
    vec3 diffuse = (1.0 - F) * (1.0 - s.metallic) * s.albedo / LIGHT_PI;

    // What gets in under the surface: flesh and frost carry it on past the terminator, snow and ice send it on forwards, and a thin thing lets it through to its other
    // side. What gets through flesh from a light on its far side is the sun's alone, as only the sun has a map of flesh to say how thick it is (Ambient_Light.frag).
    vec3 diffuseShape = vec3(NdotL);
    vec3 transmitted = vec3(0.0);
    if (s.scatter > 0.0 && sssSwitches.x > 0.0)
    {
        SssProfile p = sssProfileOf(s.scatterKind);
        if (s.scatterKind >= SSS_FLESH) diffuseShape = sssWrappedDiffuse(dot(N, L), p.wrap.rgb * s.scatter);
        transmitted = s.scatterKind == SSS_FROST ? sssFrostLight(s, p, L) : s.scatterKind == SSS_FLESH ? vec3(0.0) : sssThinLight(s, p, L);
    }
    return (diffuse * diffuseShape + transmitted) * underRadiance + specular * NdotL * radiance;
}

vec3 lightSurfaceSpread(Surface s, vec3 L, vec3 radiance, float spread)
{
    return lightSurfaceSplit(s, L, radiance, radiance, spread);
}

vec3 lightSurface(Surface s, vec3 L, vec3 radiance)
{
    return lightSurfaceSpread(s, L, radiance, 0.0);
}
