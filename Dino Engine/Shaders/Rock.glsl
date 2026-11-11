#ifndef DINO_ROCK_GLSL
#define DINO_ROCK_GLSL

// The world's bedrock, for the terrain and for every stone that stands on it (World/Geology.cs). Rock isn't mapped on to anything: it's seen from whichever side it's cut,
// so it's projected along the three axes in world space, and it's coloured by the layer of the world's rock each point lies in. That's what carries a cliff's bands across
// from the ground into a boulder lying against it. Needs Globals.glsl and textureUtil.glsl.

#include Geology.glsl

const float ROCK_TILE = 11.3;   // Metres across the large fractures of the texture.
const float ROCK_FINE = 3.7;    // Metres across the same texture over itself. Not a whole number of the first, or they'd beat.
const float ROCK_STREAKS = 0.34;

struct RockLook {
    vec3 albedo;
    vec3 normal;        // In world space.
    float roughness;
    float ambient;
    float height;       // 0..1, how far this point stands proud of the surface around it.
};

struct RockTap {
    vec3 albedo;
    vec2 slope;
    float ambient;
    float height;
};

// One plane of the projection: the large fractures and the fine ones laid over them. Their heights multiply, so that a crack in either is a crack.
RockTap rockTap(vec2 uv, vec2 dx, vec2 dy, float material)
{
    vec2 big = uv / ROCK_TILE;
    vec2 fine = uv / ROCK_FINE;
    vec3 bigCoord = vec3(big, material);
    vec3 fineCoord = vec3(fine + 0.37, material);

    vec2 bigDx = dx / ROCK_TILE;
    vec2 bigDy = dy / ROCK_TILE;
    vec2 fineDx = dx / ROCK_FINE;
    vec2 fineDy = dy / ROCK_FINE;

    vec4 normalBig = textureGrad(normalMapTextureArray, bigCoord, bigDx, bigDy);
    vec4 normalFine = textureGrad(normalMapTextureArray, fineCoord, fineDx, fineDy);
    float heightBig = textureGrad(materialMapTextureArray, bigCoord, bigDx, bigDy).a;
    float heightFine = textureGrad(materialMapTextureArray, fineCoord, fineDx, fineDy).a;

    RockTap tap;
    tap.albedo = textureGrad(albedoMapTextureArray, bigCoord, bigDx, bigDy).rgb * textureGrad(albedoMapTextureArray, fineCoord, fineDx, fineDy).rgb;
    // Well under the shape of the rock itself, which has to read first: from up here a busy surface looks flat.
    tap.slope = (normalBig.xy * 2.0 - 1.0) * 0.62 + (normalFine.xy * 2.0 - 1.0) * 0.36;
    tap.ambient = min(normalBig.a, normalFine.a);
    tap.height = heightBig * (0.6 + 0.4 * heightFine);
    return tap;
}

// `normal` is the surface's own, in world space. `dx` and `dy` are the screen derivatives of `position`, taken where control flow is still uniform.
RockLook rockLook(vec3 position, vec3 normal, vec3 dx, vec3 dy)
{
    float material = geologyPhase.w;

    // Tight, so that most pixels read one plane or two and the seam between them is short.
    vec3 share = pow(abs(normal), vec3(7.0));
    share /= share.x + share.y + share.z;

    vec3 albedo = vec3(0.0);
    vec3 turned = vec3(0.0);
    float ambient = 0.0;
    float height = 0.0;

    // "Whiteout" blending (Golus): each plane's slopes are added to the surface's own in that plane, so the bumps follow the rock round a corner.
    if (share.x > 0.01) {
        RockTap tap = rockTap(position.zy, dx.zy, dy.zy, material);
        vec3 n = vec3(tap.slope + normal.zy, abs(normal.x));
        turned += n.zyx * vec3(sign(normal.x), 1.0, 1.0) * share.x;
        albedo += tap.albedo * share.x;
        ambient += tap.ambient * share.x;
        height += tap.height * share.x;
    }
    if (share.y > 0.01) {
        RockTap tap = rockTap(position.xz, dx.xz, dy.xz, material);
        vec3 n = vec3(tap.slope + normal.xz, abs(normal.y));
        turned += n.xzy * vec3(1.0, sign(normal.y), 1.0) * share.y;
        albedo += tap.albedo * share.y;
        ambient += tap.ambient * share.y;
        height += tap.height * share.y;
    }
    if (share.z > 0.01) {
        RockTap tap = rockTap(position.xy, dx.xy, dy.xy, material);
        vec3 n = vec3(tap.slope + normal.xy, abs(normal.z));
        turned += n.xyz * vec3(1.0, 1.0, sign(normal.z)) * share.z;
        albedo += tap.albedo * share.z;
        ambient += tap.ambient * share.z;
        height += tap.height * share.z;
    }

    // A bed isn't ruled: its edge goes in and out with what's been broken off it.
    float layer = geologyLayerAt(position) + (height - 0.5) * 1.5;
    vec4 tone = geologyToneAt(layer);

    // Water has run down anything steep and left its marks there.
    float steep = smoothstep(0.75, 0.25, abs(normal.y));
    vec2 across = abs(normal.x) > abs(normal.z) ? position.zy : position.xy;
    float streak = textureLod(materialMapTextureArray, vec3(across * vec2(0.31, 0.021), material), 0.0).a;    // The arrays' first level, a material's second (TextureGenerator.MaterialLevelsDropped).
    float stains = 1.0 - ROCK_STREAKS * steep * smoothstep(0.42, 0.72, streak);

    RockLook look;
    look.albedo = albedo * tone.rgb * stains * mix(0.62, 1.0, smoothstep(0.1, 0.62, height));
    look.normal = normalize(turned);
    look.roughness = mix(0.94, 0.78, tone.a);
    look.ambient = ambient;
    look.height = height;
    return look;
}

#endif
