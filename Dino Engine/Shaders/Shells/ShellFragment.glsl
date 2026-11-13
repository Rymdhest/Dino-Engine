#ifndef DINO_SHELL_FRAGMENT_GLSL
#define DINO_SHELL_FRAGMENT_GLSL

// A layer of shells (SHELLS.md, Shells/ShellVertex.glsl): which of this pixel's samples the growth fills at this layer's height, and what it's like there, written to
// the G-buffer as any surface is, so that sun, sky, lamps, shadows, bounced light, fog and rain all see it with nothing more said. Every layer over a point
// writes the same normal, so the resolve finds a pixel's samples on one surface and lights it once, with its layers' colours averaged.

#if defined(SHELL_CHARACTER) || defined(SHELL_WILDLIFE)
#define SHELL_SKINNED
#endif

#include Globals.glsl
#include Shells/ShellStyle.glsl
#include Surface/MaterialTable.glsl
#include AntiAliasing/Msaa.glsl
#include Shells/Shell.glsl
#include gBufferUtil.glsl
#include FocusCutout.glsl
#include Halls.glsl
#include Geology.glsl
#include RockGrowth.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
// Nothing stands on what grows: moss drinks it, and what moves can't hold it (Model.frag). It also leaves out the rings of water standing, which every layer would pay for.
#define WEATHER_NOTHING_STANDS
#include Weather/Wetness.glsl

#ifndef SHELL_TERRAIN
in vec3 shellAnchor;
in vec3 shellAnchorNormal;
in vec3 shellColour;
in vec2 shellUV;
flat in float shellMaterial;
#endif
#ifdef SHELL_SKINNED
in vec3 shellComb;
in vec3 shellToWorldX;
in vec3 shellToWorldY;
in vec3 shellToWorldZ;
#endif
in vec3 shellViewPos;
in vec3 shellNormal;
in float shellAmount;
in float shellLength;
in float shellRock;
flat in int shellLayer;
flat in float shellHeight;
#ifdef SHELL_INSTANCED
flat in float shellFocusOccluder;
#endif
#ifdef SHELL_WILDLIFE
flat in float shellFade;
#endif

uniform int shellStyle;
uniform int numberOfMaterials;
uniform sampler2DArray albedoMapTextureArray;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

const float SHELL_LEAST_LIFT = 0.002;   // Metres off its surface below which a layer has nothing to show and would only fight it for the depth.
const float SHELL_MOST_TILT = 0.9;      // How far a clump's slope may turn the normal, as a tangent: keeps every layer of a pixel on one surface for the resolve.
const float HIT_SHINE = 7.0;            // Model.frag's.


void main()
{
    vec3 worldPosition = (invViewMatrix * vec4(shellViewPos, 1.0)).xyz;
#ifdef SHELL_TERRAIN
    // The ground's pattern is fixed where it's drawn.
    vec3 anchor = worldPosition;
    vec3 anchorNormal = shellNormal;
#else
    vec3 anchor = shellAnchor;
    vec3 anchorNormal = shellAnchorNormal;
#endif
    // The only derivatives taken, before anything can be discarded: every slope after this is worked out, not taken from neighbours.
    vec3 anchorDx = dFdx(anchor);
    vec3 anchorDy = dFdy(anchor);
    vec3 normalDx = dFdx(shellNormal);
    vec3 normalDy = dFdy(shellNormal);
#ifndef SHELL_TERRAIN
    vec2 uvDx = dFdx(shellUV);
    vec2 uvDy = dFdy(shellUV);
#endif

    // Whatever cuts the surface it grows on cuts it too, by the same rules (Model.frag, Terrain.frag).
#ifdef SHELL_INSTANCED
    if (shellFocusOccluder > 0.5) applyFocusCutout(worldPosition);
    if (cutByHall(worldPosition) * hallCutaway.y > focusDither(gl_FragCoord.xy)) discard;
#endif
#ifdef SHELL_CHARACTER
    if (characterSight.x > 0.5) applyFocusCutout(worldPosition);
    if (characterSight.y > 0.0 && 1.0 - characterSight.y <= focusDither(gl_FragCoord.xy)) discard;
#endif
#ifdef SHELL_WILDLIFE
    float shown = shellFade * (1.0 - cutByHallSoft(worldPosition, 2.0));
    if (shown < 1.0 && shown <= focusDither(gl_FragCoord.xy)) discard;
#endif
#ifdef SHELL_TERRAIN
    if (underHallFloor(worldPosition)) discard;
#endif

    float h = shellHeight;
    float len = shellLength;
    if (len * h < SHELL_LEAST_LIFT) discard;

    vec4 shape = shellRow(shellStyle, 0);
    vec4 fine = shellRow(shellStyle, 2);
    vec4 light = shellRow(shellStyle, 3);
    vec4 tip = shellRow(shellStyle, 4);
    int mask = int(shellRow(shellStyle, 5).w + 0.5);
    bool fur = fine.w > 0.5;

    vec3 n = normalize(anchorNormal);
    vec3 worldNormal = normalize(shellNormal);
    float cover = clamp(shellAmount, 0.0, 1.0);
    // What grows takes the colour of what it grows from, where it grows: its material's texture, as the surface under it is drawn, so the two are one at the
    // root and where the growth thins out.
#ifdef SHELL_TERRAIN
    vec3 colour = vec3(1.0);
#else
    vec3 colour = shellColour;
    if (shellMaterial >= 0.0 && shellMaterial < float(numberOfMaterials)) colour *= textureGrad(albedoMapTextureArray, vec3(shellUV, shellMaterial), uvDx, uvDy).rgb;
#endif

    // Moss on stone grows exactly where Rock.glsl paints it, in the colour it paints it (RockGrowth.glsl), and goes under its snow. A stone's anchor is the world.
#ifndef SHELL_TERRAIN
    if (mask == SHELL_MASK_ROCK)
    {
        // Moss only grows on rock that faces the sky at all, in a land whose rock grows any (RockGrowth.glsl), which is cheap to ask first.
        if (shellRock < 0.999 || n.y < 0.25 || geologyMoss.w <= 0.0) discard;
        RockGrowth growth = rockGrowthAt(anchor, n.y, rockSoftOf(rockLayerAt(anchor)), rockMiddling(anchor));
        cover *= growth.moss * (1.0 - growth.snow);
        colour = shellColour * geologyMoss.rgb * mix(0.75, 1.2, growth.patches);
    }
#endif
#ifdef SHELL_TERRAIN
    // On the ground it's the ground's own moss, and the moss on its rock by Rock.glsl's rule, as much as each is there (the vertices' shares, Shells/ShellVertex.glsl).
    {
        float ground = cover;
        float stone = clamp(shellRock, 0.0, 1.0);
        // The ground's moss, at its own size, as Terrain.frag reads it.
        float tile = max(materialTileMetres(groundMossMaterial, numberOfMaterials), 0.01);
        colour = textureGrad(albedoMapTextureArray, vec3(anchor.xz / tile, groundMossMaterial), anchorDx.xz / tile, anchorDy.xz / tile).rgb;
        float onStone = 0.0;
        if (stone > 0.0 && n.y >= 0.25 && geologyMoss.w > 0.0)
        {
            RockGrowth growth = rockGrowthAt(anchor, n.y, rockSoftOf(rockLayerAt(anchor)), rockMiddling(anchor));
            onStone = stone * growth.moss * (1.0 - growth.snow);
            colour = mix(colour, geologyMoss.rgb * mix(0.75, 1.2, growth.patches), onStone / max(onStone + ground, 1e-4));
        }
        cover = clamp(ground + onStone, 0.0, 1.0);
    }
#endif
    // Snow that's settled lies over moss: it doesn't stand up through it.
    if (!fur && weatherKind.w > 0.5) cover *= 1.0 - smoothstep(0.0, 0.5, weatherSurfaceAt(worldPosition.xz).z * smoothstep(0.1, 0.65, worldNormal.y));
    if (cover < 0.004) discard;

    // A pixel's footprint on the surface, which decides how fine a thing can be drawn without crawling: how much a pixel covers at this distance, widened by
    // how far the surface is turned from the eye. It's worked out, not taken from the screen, as the screen's are the same over a triangle and step from one
    // triangle to the next, and that stepped the clumps in and out in blocks.
    vec3 toEye = viewPosWorld - worldPosition;
    float eye = length(toEye);
    float pixel = eye * 2.0 / (projectionMatrix[1][1] * resolution.y);
    float facing = dot(worldNormal, toEye / max(eye, 1e-4));
    // What faces away is behind the surface it grows on, but for a sliver past its edge, which the layers facing the eye cover.
    if (facing < -0.1) discard;
    float footprint = max(pixel / max(abs(facing), 0.3), 1e-5);
    vec3 comb = vec3(0.0);
#ifdef SHELL_SKINNED
    if (fur)
    {
        comb = shellComb - n * dot(shellComb, n);
        float combLength = length(comb);
        comb = combLength > 1e-3 ? comb / combLength : vec3(0.0);
    }
#endif
    ShellField field = shellField(shellStyle, anchor, n, comb, cover, h, footprint);

    vec3 densitySlope;
    float density = shellDensity(field, h, densitySlope);
    vec2 screenSlope = vec2(dot(densitySlope, anchorDx), dot(densitySlope, anchorDy));
    int covered = shellCoverage(density, screenSlope, shellLayer);
    if (covered == 0) discard;
    gl_SampleMask[0] = covered;

    // How far up its own strands this layer is: dark and hidden at the root, pale and letting light through at the tips.
    float top = field.core + 0.5 * field.reach;
    float along = clamp(h / max(top, 1e-3), 0.0, 1.0);
    // How far up its clump a point is: down in a crease between cushions, or a parting between locks, it's in their shade, and older and darker, as far as the
    // style's creases go.
    float raised = mix(1.0, smoothstep(0.0, 0.85, field.raised), shellRow(shellStyle, 6).x);
    vec3 tint = mix(vec3(light.x), tip.rgb, smoothstep(0.0, 1.0, along));
    vec3 albedo = colour * tint * (1.0 + fine.z * field.tone * 2.0) * mix(0.72, 1.0, raised);
    float ambient = mix(light.y, 1.0, pow(along, 0.8)) * mix(0.55, 1.0, raised);
    // The sun gets down among strands less than the sky does: a root in the open is still in its neighbours' shade.
    float sunThrough = mix(0.5, 1.0, smoothstep(0.0, 0.9, along)) * mix(0.62, 1.0, raised);
    float through = light.z * smoothstep(0.35, 1.0, along);

    // The clumps' own slope turns the normal, the same for every layer over a point.
    vec3 slope = field.coreSlope - n * dot(field.coreSlope, n);
    vec3 tilt = slope * (len * (fur ? 0.6 : 1.0));
    float tiltLength = length(tilt);
    if (tiltLength > SHELL_MOST_TILT) tilt *= SHELL_MOST_TILT / tiltLength;
#ifdef SHELL_SKINNED
    tilt = mat3(shellToWorldX, shellToWorldY, shellToWorldZ) * tilt;
#endif
    vec3 normal = normalize(worldNormal - tilt);

    float roughness = light.w;
    weatherWetten(worldPosition, worldNormal, tip.w, 0.0, 0.0, along, albedo, roughness, normal);
    // Geometric specular anti-aliasing, as Wetness.glsl's weatherFilterRoughness does, from the derivatives taken while they still meant something.
    float kernel = min(0.5 * (dot(normalDx, normalDx) + dot(normalDy, normalDy)), 0.18);
    float alpha = roughness * roughness;
    roughness = sqrt(sqrt(min(alpha * alpha + kernel, 1.0)));

    float shine = 0.0;
#ifdef SHELL_CHARACTER
    albedo = mix(albedo, hitFlash.rgb, hitFlash.a);
    shine = hitFlash.a * HIT_SHINE;
#endif

    gAlbedo = vec4(albedo, transmittance(through, 0.0));
    gNormal = vec4(compressNormal(normalize(mat3(viewMatrix) * normal)), ambient);
    gMaterials = vec4(roughness, encodeEmission(shine), 0.0, sunThrough);
}

#endif
