#ifndef DINO_ROCK_GLSL
#define DINO_ROCK_GLSL

// The world's bedrock, for the terrain and for every stone that stands on it (World/Geology.cs). Rock isn't mapped on to anything: it's seen from whichever side it's cut,
// so everything here is worked out in world space, and it's coloured by the layer of the world's rock each point lies in. That's what carries a cliff's bands across
// from the ground into a boulder lying against it.
//
// Its joints are geometry: every stone has them in its shape, and bare rock in the land is clad in blocks of its own (RockSkin), so what's drawn here is the finer
// things, its beds' colours and laminae, the fractures inside a block, drawn with the exact coverage of a pixel so that none of it crawls, and what weather and
// growing things do to rock. The ground under the skin is drawn with it too, as the floor of a crack.
// Needs Globals.glsl and textureUtil.glsl.

#include Geology.glsl

const float ROCK_TILE = 11.3;   // Metres across the texture's grain at the coarser of its two sizes.
const float ROCK_FINE = 3.7;    // Metres across the same texture over itself. Not a whole number of the first, or they'd beat.
const float ROCK_STREAKS = 0.3;

const int ROCK_GRANITE = 0;     // World/Geology.cs's RockKind.
const int ROCK_LIMESTONE = 1;
const int ROCK_SANDSTONE = 2;
const int ROCK_SCHIST = 3;
const int ROCK_BASALT = 4;

const float ROCK_FINE_SHUT = 0.5;       // The share of the fractures inside a block that die out.

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

// One plane of the projection: the texture's grain at two sizes laid over each other. Their heights multiply, so that a pit in either is a pit.
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
    // Well under the shape of the rock and its blocks, which have to read first: from up here a busy surface looks flat.
    tap.slope = (normalBig.xy * 2.0 - 1.0) * 0.5 + (normalFine.xy * 2.0 - 1.0) * 0.3;
    tap.ambient = min(normalBig.a, normalFine.a);
    tap.height = heightBig * (0.6 + 0.4 * heightFine);
    return tap;
}

// ---------------------------------------------------------------- Numbers of its own
// This is included both before and after procedural/fastHash.glsl, so it keeps to hashes of its own.

uint rockMix(uint h)
{
    h ^= h >> 16u;
    h *= 0x7FEB352Du;
    h ^= h >> 15u;
    h *= 0x846CA68Bu;
    h ^= h >> 16u;
    return h;
}

// Two mixes are enough to tell neighbours apart, and every rock pixel takes dozens of these.
uint rockHash(int a, int b, int c)
{
    return rockMix(uint(a) * 0x8DA6B343u + rockMix(uint(b) * 0xD8163841u ^ uint(c) * 0xCB1AB31Fu ^ 0x165667B1u));
}

float rockUnit(uint h)
{
    return float(h & 0xFFFFu) * (1.0 / 65535.0);
}

// Smooth noise, 0..1, for where joints wander and where things gather.
float rockNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    float a = rockUnit(rockMix(uint(c.x) * 0x8DA6B343u ^ uint(c.y) * 0xD8163841u ^ 0x27D4EB2Fu));
    float b = rockUnit(rockMix(uint(c.x + 1) * 0x8DA6B343u ^ uint(c.y) * 0xD8163841u ^ 0x27D4EB2Fu));
    float d = rockUnit(rockMix(uint(c.x) * 0x8DA6B343u ^ uint(c.y + 1) * 0xD8163841u ^ 0x27D4EB2Fu));
    float e = rockUnit(rockMix(uint(c.x + 1) * 0x8DA6B343u ^ uint(c.y + 1) * 0xD8163841u ^ 0x27D4EB2Fu));
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y);
}

// How much of a pixel `pixel` metres across, centred `d` metres from the middle of a line `width` metres wide, the line covers: the exact coverage of a box, so that a line
// finer than a pixel greys what's under it by as much as it would cover, rather than flickering in and out as the camera moves.
float rockLine(float d, float width, float pixel)
{
    float p = max(pixel, 1e-4);
    float lo = max(d - 0.5 * p, -0.5 * width);
    float hi = min(d + 0.5 * p, 0.5 * width);
    return clamp((hi - lo) / p, 0.0, 1.0);
}

// Metres along a surface (normal n) from a point to where it meets a plane `d` metres away, whose normal is j. A plane that lies nearly along the surface meets it far off.
float rockAcross(float d, vec3 j, vec3 n)
{
    float c = dot(j, n);
    return d / sqrt(max(1.0 - c * c, 0.0025));
}

// The way along a surface towards that plane, from the side of it `side` says: +1 is the side j points to.
vec3 rockToward(vec3 j, vec3 n, float side)
{
    vec3 t = j - n * dot(j, n);
    float l = length(t);
    return l > 1e-4 ? t * (-side / l) : vec3(0.0);
}

// How far a block's edge rounds into its joint at a distance from the joint's middle: all the way at its lip, and none past its bevel.
float rockBevel(float line, float gap, float bevel)
{
    float s = max(line - 0.5 * gap, 0.0) / max(bevel, 1e-4);
    float w = 1.0 - min(s, 1.0);
    return w * w;
}

// ---------------------------------------------------------------- Inside a block

struct RockBlocks {
    float fine;         // How much of the pixel is one of the finer fractures inside a block.
    float soft;         // 1 in a bed's soft band, which wears back under the hard one over it.
    float layer;        // The layer of rock, as the colours are read, wandering as the partings do.
    float head;         // Metres along the surface to the parting over this point.
    float laminae;      // How much the pixel is crossed by the fine sheets a bed is laid in, -1..1.
};

// Where the kth joint of a set lies, in spacings: a third of a spacing either side of k, so that joints are never evenly spaced, as real ones aren't.
float rockJointAt(int bed, int set, int k)
{
    return float(k) + (rockUnit(rockHash(bed, set, k)) - 0.5) * 0.6;
}

// Which gap between joints a coordinate falls in, and how far it is from the joints either side, in spacings.
void rockGap(float t, int bed, int set, out int index, out float below, out float above)
{
    int k = int(floor(t));
    float b0 = rockJointAt(bed, set, k);
    float b1 = rockJointAt(bed, set, k + 1);
    if (t < b0) { index = k - 1; below = t - rockJointAt(bed, set, k - 1); above = b0 - t; }
    else if (t < b1) { index = k; below = t - b0; above = b1 - t; }
    else { index = k + 1; below = t - b1; above = rockJointAt(bed, set, k + 2) - t; }
}

// A net of fractures through one bed at a point: one set running unbroken along the strike and the other crossing between them, staggered, leaning as they like and some
// of them not going through.
void rockJointsOfBed(vec3 q, vec3 bedNormal, vec3 along, vec3 across, float thick, int bed, uint bedHash, float shut, float scale,
    out float du, out float dv, out int row, out int column, out vec3 jointU, out vec3 jointV)
{
    float turn = (rockUnit(bedHash >> 20u) - 0.5) * 0.28;
    vec3 a = along * cos(turn) + across * sin(turn);
    vec3 c = cross(bedNormal, a);
    float sv = clamp(thick * geologyFrame.z * mix(0.8, 1.25, rockUnit(bedHash)), geologyJoints.x, geologyJoints.y) * scale;
    float v = dot(q, c) / sv + rockUnit(bedHash >> 8u);
    float belowV, aboveV;
    rockGap(v, bed, 61, row, belowV, aboveV);
    dv = min(belowV, aboveV) * sv;

    uint rowHash = rockHash(bed, row, 5 + int(scale * 16.0));
    float lean = (rockUnit(rowHash >> 8u) - 0.5) * 0.4;
    vec3 ar = a * cos(lean) + c * sin(lean);
    float su = clamp(thick * geologyFrame.w * mix(0.7, 1.35, rockUnit(rowHash)), geologyJoints.x, geologyJoints.y) * scale;
    float u = dot(q, ar) / su + rockUnit(rowHash >> 16u);
    int col;
    float belowU, aboveU;
    rockGap(u, bed, row * 3 + 2, col, belowU, aboveU);
    // A fracture that dies out does so for both sides of it, so it's known by its own number.
    bool shutLeft = rockUnit(rockHash(bed, row * 3 + 1, col)) < shut;
    bool shutRight = rockUnit(rockHash(bed, row * 3 + 1, col + 1)) < shut;
    du = min(shutLeft ? 1e4 : belowU * su, shutRight ? 1e4 : aboveU * su);
    column = shutLeft ? col - 1 : col;
    jointU = ar;
    jointV = c;
}

// What's inside a block of the world's rock at a point: which layer it's in, the fine fractures through it and the laminae it was laid in. Its joints are its shape (RockSkin).
RockBlocks rockBlocks(vec3 p, vec3 n, float pixel)
{
    RockBlocks r;
    int kind = int(geologyFace.z + 0.5);
    vec3 bedNormal = normalize(vec3(geologyDip.x, 1.0, geologyDip.y));
    float layerScale = length(vec3(geologyDip.x, 1.0, geologyDip.y));
    vec3 strike = vec3(geologyFrame.x, 0.0, geologyFrame.y);
    vec3 along = normalize(strike - bedNormal * dot(strike, bedNormal));
    vec3 across = cross(bedNormal, along);

    // Partings aren't ruled: a bed's edge goes in and out with what's come off it, and fractures wander across their own planes.
    float a = dot(p, along);
    float c = dot(p, across);
    // Granite has no beds, only sheets peeling off it, which follow the land far more loosely than a sea floor did.
    float sheeted = kind == ROCK_GRANITE ? 2.4 : 0.5;
    float layer = geologyLayerAt(p) + (rockNoise(vec2(a, c) * 0.29 + p.y * 0.07) - 0.5) * sheeted;
    vec3 q = p + across * (rockNoise(vec2(a * 0.075, p.y * 0.12) + 11.3) - 0.5) * 1.2
               + along * (rockNoise(vec2(c * 0.075, p.y * 0.12) - 5.9) - 0.5) * 1.2;

    float since, until;
    int bed;
    geologyBedAt(layer, since, until, bed);
    float thick = since + until;
    uint bedHash = rockHash(bed, 17, 3);
    float hard = geologyToneAt(layer).a;

    r.layer = layer;
    r.soft = 1.0 - smoothstep(0.4, 0.7, hard);
    r.head = rockAcross(until / layerScale, bedNormal, n);

    // The fractures inside a block, thin and mostly dying out, at a fraction of a joint's spacing.
    {
        float du, dv;
        int fineRow, fineColumn;
        vec3 fineU, fineV;
        rockJointsOfBed(q, bedNormal, along, across, thick, bed + 4096, rockHash(bed, 23, 9), ROCK_FINE_SHUT, 0.4, du, dv, fineRow, fineColumn, fineU, fineV);
        float fineGap = geologyJoints.z * 0.42;
        float lines = max(rockLine(rockAcross(du, fineU, n), fineGap, pixel), rockLine(rockAcross(dv, fineV, n), fineGap, pixel));
        float shown = rockUnit(rockHash(bed, fineRow, fineColumn * 7 + 3)) < 0.7 ? 1.0 : 0.0;
        r.fine = lines * shown;
    }

    // The fine sheets a bed is laid in: level in most rock, leaning in sets in sandstone, as wind and water laid it, and close and strong where it's been squeezed to schist.
    {
        float spacing = mix(0.22, 0.08, geologyGrain.y) * mix(0.8, 1.25, rockUnit(bedHash >> 4u));
        float lamina = since;
        if (geologyGrain.x > 0.0)
        {
            float turn = rockUnit(bedHash >> 12u) * 6.2831853;
            float lean = mix(0.25, 0.55, rockUnit(bedHash >> 20u));
            // Each set of laminae is cut off by the one over it, so a bed holds two or three, leaning their own ways.
            float set = floor(since / max(thick, 0.01) * 2.5);
            turn += set * 2.1;
            lamina += dot(p.xz, vec2(cos(turn), sin(turn))) * lean * geologyGrain.x;
        }
        float f = fract(lamina / spacing);
        float d = min(f, 1.0 - f) * spacing / layerScale;
        float width = spacing * 0.22;
        float line = rockLine(rockAcross(d, bedNormal, n), width, pixel);
        float strength = mix(0.35, 1.0, geologyGrain.y) * (1.0 - r.soft * 0.6);
        r.laminae = line * strength * (rockUnit(rockHash(bed, int(floor(lamina / spacing)), 31)) < 0.5 ? 1.0 : -0.6);
    }
    return r;
}

// ---------------------------------------------------------------- The look

// `normal` is the surface's own, in world space. `dx` and `dy` are the screen derivatives of `position`, taken where control flow is still uniform.
RockLook rockLook(vec3 position, vec3 normal, vec3 dx, vec3 dy)
{
    float material = geologyPhase.w;
    float pixel = max(max(length(dx), length(dy)), 1e-4);

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

    RockBlocks blocks = rockBlocks(position, normal, pixel);
    vec4 tone = geologyToneAt(blocks.layer + (height - 0.5) * 0.5);

    float up = normal.y;
    float steep = smoothstep(0.75, 0.25, abs(up));
    float top = smoothstep(0.5, 0.9, up);

    vec3 colour = albedo * tone.rgb;
    colour *= 1.0 + blocks.laminae * 0.09;
    // Weathering in broad patches, lighter and darker, warmer and cooler, so that no two faces of a mountain are quite the same colour, and the odd crust of lichen on a face.
    float broad = rockNoise(position.xz * 0.06 + position.y * 0.045);
    float middling = rockNoise(position.xz * 0.19 - position.y * 0.13 + 4.1);
    float weather = broad * 0.65 + middling * 0.35;
    colour *= mix(0.84, 1.12, weather) * mix(vec3(0.97, 0.99, 1.04), vec3(1.05, 1.0, 0.94), smoothstep(0.25, 0.75, middling));
    float lichen = smoothstep(0.76, 0.86, rockNoise(position.xz * 2.3 + position.y * 1.9)) * smoothstep(0.4, 0.8, broad);
    vec3 crust = mix(vec3(0.5, 0.45, 0.31), geologyMoss.rgb * 1.5, geologyMoss.w);
    colour = mix(colour, crust * mix(0.85, 1.1, weather), lichen * 0.45 * (1.0 - top * 0.5));
    // A soft bed wears back under the hard one over it, and lies in its shadow.
    float recess = geologyFace.y * blocks.soft * steep;
    colour *= 1.0 - 0.3 * recess;
    ambient *= 1.0 - 0.35 * recess;

    // Water has run down anything steep and left its marks there, most of all off the lip of a bed; a desert lacquers its streaks almost black.
    vec2 across = abs(normal.x) > abs(normal.z) ? position.zy : position.xy;
    float streak = textureLod(materialMapTextureArray, vec3(across * vec2(0.31, 0.021), material), 0.0).a;    // The arrays' first level, a material's second (TextureGenerator.MaterialLevelsDropped).
    float drip = mix(0.65, 1.0, 1.0 - smoothstep(0.0, 5.0, blocks.head));
    float stains = 1.0 - (ROCK_STREAKS + 0.3 * geologyGrain.z) * steep * drip * smoothstep(0.42, 0.72, streak);
    colour *= stains;
    colour = mix(colour, colour * vec3(0.78, 0.66, 0.56), geologyGrain.z * 0.5 * steep * drip * smoothstep(0.5, 0.8, streak));

    // What grows on rock: in the open on whatever faces the sky, and thickest on a soft bed's ledge.
    float patches = rockNoise(position.xz * 0.43 + position.y * 0.1) * 0.65 + middling * 0.35;
    float moss = geologyMoss.w * smoothstep(0.25, 0.85, up) * smoothstep(0.62 - 0.35 * geologyMoss.w, 0.86, patches + blocks.soft * 0.12);
    colour = mix(colour, geologyMoss.rgb * mix(0.75, 1.2, patches), moss * 0.85);
    colour *= 1.0 - 0.5 * blocks.fine;

    // Snow lies on whatever faces the sky where a land has snowfields, drifted into patches, thinnest on edges where the wind takes it and deepest on ledges.
    float snow = 0.0;
    if (geologyFace.w > 0.0)
    {
        float drifts = rockNoise(position.xz * 0.21 + position.y * 0.11 + 5.3) * 0.7 + patches * 0.3;
        snow = geologyFace.w * smoothstep(0.5, 0.86, up + (drifts - 0.5) * 0.4 + blocks.soft * 0.1);
        colour = mix(colour, vec3(0.82, 0.85, 0.9) * mix(0.92, 1.0, drifts), snow);
    }

    RockLook look;
    look.albedo = colour * mix(0.62, 1.0, smoothstep(0.1, 0.62, height));
    look.normal = normalize(turned);
    look.roughness = mix(mix(mix(0.94, 0.78, tone.a), 0.96, moss), 0.7, snow);
    look.ambient = ambient * (1.0 - 0.25 * blocks.fine);
    look.height = height;
    return look;
}

#endif
