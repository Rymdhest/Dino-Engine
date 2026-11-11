#version 420 core

#include Globals.glsl

in vec2 fragUV;
in vec2 fragInstanceChunkPos;
in vec2 fragInstanceChunkSizeXZ;
in float fragInstanceHeightMapID;
in vec3 fragWorldPos;
in vec3 fragWorldNormal;
in vec3 COLOR_TEST;

uniform vec3 viewPos;
uniform int numberOfMaterials;
uniform float reliefScale;      // 1 for each material's own relief, 0 for none (Graphics quality).
uniform float textureTileSize;  // Metres fragUV counts in: each material is read at its own size from it.
uniform float textureMapOffset;

// The world's ground materials, four to a vec4; surfaceWeightsA holds the weights of the first four, and so on.
uniform vec4 surfaceMaterials[3];

uniform sampler2DArray normalHeightTextureArray;
uniform sampler2DArray grassTextureArray;
uniform sampler2DArray grassColourTextureArray;
uniform sampler2DArray surfaceWeightsA;
uniform sampler2DArray surfaceWeightsB;
uniform sampler2DArray surfaceWeightsC;
uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;
uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;    // Declared for textureUtil.glsl alone: the ground reads no cards, and their units carry the roads.
uniform sampler2DArray roadFrameTextureArray;   // Per texel: metres across the nearest road and along it, and the way it runs (World/RoadFrames.cs).
uniform sampler2DArray roadInfoTextureArray;    // Its half width, 0 where there's none, and how much of a junction or an end this is.
uniform float roadMaterial;     // A dirt road's earth, read along the road with its ruts sunk into it (roadShape).
uniform float settsMaterial;    // A street's setts, read across it from kerb to kerb and along it.
uniform float snowMaterial;     // Snow, which keeps what's walked through it (Snow/SnowTrail.glsl) and glitters.
uniform bool DEBUG_VIEW;

layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gMaterials;

// The parallax hit always lies further along the view ray than the rasterized surface, so depth only grows.
// Declaring that keeps early depth rejection working even though gl_FragDepth is written.
layout (depth_greater) out float gl_FragDepth;

#include textureUtil.glsl
#include gBufferUtil.glsl
#include procedural/fastHash.glsl
#include Rock.glsl
#include Surface/MaterialTable.glsl
#include Grass/GrassField.glsl
#include Weather/Weather.glsl
#include Weather/SurfaceMap.glsl
#include Weather/RainShadow.glsl
#include Weather/Wetness.glsl
#include Halls.glsl
#include Snow/SnowTrail.glsl

const float MATERIAL_SIZE = 2048.0;             // Texels across a material, for the mip a march reads.
const float TERRAIN_MOST_SLANT = 6.0;           // The farthest a ray may run sideways, in depths of the relief (Surface/Relief.glsl).
const float TERRAIN_STEPS_PER_PIXEL = 0.5;
const int TERRAIN_LEAST_STEPS = 3;
const int TERRAIN_MOST_STEPS = 32;
const float TERRAIN_SHOWN_FROM = 0.6;           // Pixels of parallax below which there's none, and above which it's all there.
const float TERRAIN_SHOWN_AT = 1.6;
const float TERRAIN_PENUMBRA = 0.09;            // As a relief's own shadows are elsewhere,
const float TERRAIN_SHADOW_FROM = 1.0;          // and shown only as far as they're more than a pixel deep.
const float TERRAIN_SHADOW_AT = 2.0;

void ConsiderMaterial(float matID, float weight, inout vec3 bestIDs, inout vec3 bestWeights) {
    if (weight <= 0.0) return;

    if (weight > bestWeights.x) {
        bestIDs.z = bestIDs.y;     bestWeights.z = bestWeights.y;
        bestIDs.y = bestIDs.x;     bestWeights.y = bestWeights.x;
        bestIDs.x = matID;         bestWeights.x = weight;
    } else if (weight > bestWeights.y) {
        bestIDs.z = bestIDs.y;     bestWeights.z = bestWeights.y;
        bestIDs.y = matID;         bestWeights.y = weight;
    } else if (weight > bestWeights.z) {
        bestIDs.z = matID;         bestWeights.z = weight;
    }
}

// ---------------------------------------------------------------- Roads
// A road's own materials are read in its frame: across it and along it, so ruts follow every bend and a street's courses lie square across it (TEXTURES.md,
// Roads). The frame is baked per texel on the CPU (World/RoadFrames.cs) and is a straight line within a texel's reach, so it's carried through the march
// exactly: across and along anywhere near are across and along here plus how far over and on that is.

struct RoadPlace {
    float across;       // Metres from the centre line at fragWorldPos, positive towards side.
    float along;        // Metres along the road there.
    vec2 way;           // The way it runs, unit, on the xz plane,
    vec2 side;          // and the way across runs: way turned a right angle, as RoadFrame.Side.
    float halfWidth;
    float junction;     // 1 where across and along jump, where roads meet or one ends, or where no road is near: there it's read the world's way.
};

RoadPlace gRoad;            // This pixel's, set before the march.
float gRutWater = 0.0;      // How much of a rut the hit is in, where rain stands first.

RoadPlace ReadRoad(vec3 coord, vec2 dx, vec2 dy) {
    vec4 frame = textureGrad(roadFrameTextureArray, coord, dx, dy);
    vec4 info = textureGrad(roadInfoTextureArray, coord, dx, dy);
    RoadPlace r;
    float wayLength = length(frame.zw);
    r.way = wayLength > 1e-4 ? frame.zw / wayLength : vec2(1.0, 0.0);
    r.side = vec2(-r.way.y, r.way.x);
    r.across = frame.x;
    r.along = frame.y;
    r.halfWidth = info.x;
    // A texel past every road's reach reads across as far off (RoadFrame.Far), so between it and a road's own texels nothing holds.
    bool found = info.x > 0.5 && wayLength > 0.7 && abs(frame.x) < info.x + 4.0;
    r.junction = found ? clamp(info.y, 0.0, 1.0) : 1.0;
    return r;
}

// Across and along at a point near this pixel, in metres on the xz plane.
vec2 RoadAt(vec2 xz) {
    vec2 d = xz - fragWorldPos.xz;
    return vec2(gRoad.across + dot(d, gRoad.side), gRoad.along + dot(d, gRoad.way));
}

// A dirt road's cross-section, World/RoadProfile.cs's, which lays the grass of a crown on the CPU: change the two together.
const float ROAD_RUTTED_FROM = 1.8;
const float ROAD_RUT_HALF = 0.3;
const float ROAD_TAU = 6.2831853;

float roadWander(float along) { return 0.22 * sin(along * (ROAD_TAU / 29.0) + 1.1) + 0.1 * sin(along * (ROAD_TAU / 11.3) + 2.7); }
float roadGauge(float along) { return 1.3 + 0.06 * sin(along * (ROAD_TAU / 17.0) + 0.4); }

struct RoadShape {
    float rut;          // In one of the two ruts carts have worn.
    float lip;          // On the earth they pushed up beside it.
    float crown;        // Between the ruts, where grass is let grow on a quiet track.
    float trodden;      // Down a footpath's middle, where feet go.
    float verge;        // Out at the edge, where the road frays into the land.
    float packed;       // Round the ruts, where wheels and hooves have pressed the earth hard and dark.
};

RoadShape roadShape(vec2 at, float halfWidth) {
    RoadShape s;
    float rutted = step(ROAD_RUTTED_FROM, halfWidth);
    float wander = roadWander(at.y);
    float x = abs(at.x - wander);
    float gauge = roadGauge(at.y);
    float d = abs(x - gauge);
    s.rut = rutted * (1.0 - smoothstep(0.4 * ROAD_RUT_HALF, ROAD_RUT_HALF, d));
    s.lip = rutted * smoothstep(ROAD_RUT_HALF, 1.35 * ROAD_RUT_HALF, d) * (1.0 - smoothstep(1.35 * ROAD_RUT_HALF, 2.3 * ROAD_RUT_HALF, d));
    float inner = gauge - ROAD_RUT_HALF * 1.6;
    s.crown = rutted * (1.0 - smoothstep(inner - 0.35, inner, x));
    s.trodden = (1.0 - rutted) * (1.0 - smoothstep(0.22, 0.6, abs(at.x - 0.3 * wander)));
    s.verge = smoothstep(halfWidth - 0.6, halfWidth + 0.3, abs(at.x));
    s.packed = rutted * (1.0 - smoothstep(ROAD_RUT_HALF, 2.8 * ROAD_RUT_HALF, d));
    return s;
}

// What the cross-section does to the height, apart from the road's own earth: sunk in the ruts, heaped at their lips, high on the crown, worn down a footpath.
float roadShapeHeight(RoadShape s) {
    return -0.36 * s.rut + 0.07 * s.lip + 0.05 * s.crown - 0.12 * s.trodden;
}

// A dirt road's height at a point, from its earth's (stones flattened in a rut, as wheels have driven them down) and its cross-section.
float roadDirtHeight(float earth, RoadShape s) {
    return clamp(earth * (1.0 - 0.5 * s.rut) + roadShapeHeight(s), 0.0, 1.0);
}

bool isRoadDirt(float mat) { return abs(mat - roadMaterial) < 0.5; }
bool isSetts(float mat) { return abs(mat - settsMaterial) < 0.5; }

// The three strongest materials of this pixel, their shares, and what each is: how much finer than fragUV it's read (its tile's metres against
// textureTileSize) and how deep its relief goes, in metres. Which materials make up the ground is decided on the CPU (TerrainSurface), and read once
// per pixel, not in every step of the parallax march.
struct TerrainMaterialSample {
    vec3 mats;
    vec3 weights;
    vec3 scales;        // uv of the material = fragUV * scale.
    vec3 reliefs;       // Metres.
    vec3 heights;
    vec2 uv;            // Where the heights were read, in fragUV's units.
};

TerrainMaterialSample GatherSurface(vec2 macroCoord, vec2 macroDX, vec2 macroDY) {
    vec3 coord = vec3(macroCoord, fragInstanceHeightMapID);
    vec4 a = textureGrad(surfaceWeightsA, coord, macroDX, macroDY);
    vec4 b = textureGrad(surfaceWeightsB, coord, macroDX, macroDY);
    vec4 c = textureGrad(surfaceWeightsC, coord, macroDX, macroDY);

    vec3 ids = vec3(0.0);
    vec3 weights = vec3(-1.0);
    ConsiderMaterial(surfaceMaterials[0].x, a.x, ids, weights);
    ConsiderMaterial(surfaceMaterials[0].y, a.y, ids, weights);
    ConsiderMaterial(surfaceMaterials[0].z, a.z, ids, weights);
    ConsiderMaterial(surfaceMaterials[0].w, a.w, ids, weights);
    ConsiderMaterial(surfaceMaterials[1].x, b.x, ids, weights);
    ConsiderMaterial(surfaceMaterials[1].y, b.y, ids, weights);
    ConsiderMaterial(surfaceMaterials[1].z, b.z, ids, weights);
    ConsiderMaterial(surfaceMaterials[1].w, b.w, ids, weights);
    ConsiderMaterial(surfaceMaterials[2].x, c.x, ids, weights);
    ConsiderMaterial(surfaceMaterials[2].y, c.y, ids, weights);
    ConsiderMaterial(surfaceMaterials[2].z, c.z, ids, weights);
    ConsiderMaterial(surfaceMaterials[2].w, c.w, ids, weights);

    weights = max(weights, vec3(0.0));
    float total = weights.x + weights.y + weights.z;

    TerrainMaterialSample s;
    s.mats = ids;
    s.weights = total > 0.0 ? weights / total : vec3(1.0, 0.0, 0.0);
    for (int i = 0; i < 3; i++)
    {
        float tile = max(materialTileMetres(s.mats[i], numberOfMaterials), 0.01);
        s.scales[i] = textureTileSize / tile;
        // Rock is read from the side (Rock.glsl), not through these coordinates, so it has no relief here: where it meets the ground the ground sinks
        // below it, and its own cliff of a relief never makes the blend swim.
        s.reliefs[i] = isRock(s.mats[i]) ? 0.0 : materialReliefRatio(s.mats[i], numberOfMaterials) * tile * reliefScale;
    }
    s.heights = vec3(0.0);
    s.uv = vec2(0.0);
    return s;
}

// One material's height at a point (uv in fragUV's units) at its own size; a road's own materials along the road, and the world's way where the road's frame
// fades out. A road's frame reads as many texels to the metre as the world's way does (a street's tile is its width), so the mip is the same.
float MaterialHeight(float mat, float scale, vec2 uv, float lod) {
    float level = lod + log2(scale);
    bool dirt = isRoadDirt(mat);
    bool street = isSetts(mat);
    if (!(dirt || street) || gRoad.junction >= 0.999) return textureLod(materialMapTextureArray, vec3(uv * scale, mat), level).a;

    vec2 at = RoadAt(uv * textureTileSize);
    float tile = textureTileSize / scale;
    float height;
    if (street) {
        // Across from kerb to kerb, and nothing at all past them, so that what lies beside a street meets its kerbs.
        float u = (at.x + gRoad.halfWidth) / (2.0 * gRoad.halfWidth);
        height = (u < 0.0 || u > 1.0) ? 0.0 : textureLod(materialMapTextureArray, vec3(u, at.y / tile, mat), level).a;
    } else {
        height = roadDirtHeight(textureLod(materialMapTextureArray, vec3(at / tile, mat), level).a, roadShape(at, gRoad.halfWidth));
    }
    if (gRoad.junction > 0.001) height = mix(height, textureLod(materialMapTextureArray, vec3(uv * scale, mat), level).a, gRoad.junction);
    return height;
}

// The heights of this pixel's materials at a point, each at its own size. `lod` is the mip fragUV's footprint wants; a finer material reads a coarser
// mip by as much finer as it is. Explicit levels, because this runs inside the parallax loop, where implicit derivatives are undefined.
TerrainMaterialSample SampleTerrainMaterials(TerrainMaterialSample s, vec2 uv, float lod) {
    // Most ground is one material or two, and a share of nothing counts for nothing whatever its height.
    s.heights.x = MaterialHeight(s.mats.x, s.scales.x, uv, lod);
    if (s.weights.y > 0.0) s.heights.y = MaterialHeight(s.mats.y, s.scales.y, uv, lod);
    if (s.weights.z > 0.0) s.heights.z = MaterialHeight(s.mats.z, s.scales.z, uv, lod);
    s.uv = uv;
    return s;
}

// ---------------------------------------------------------------- Snow that's been walked through
// Every foot, and every body ploughing through deep snow, presses its shape into a map around the camera (SnowTrailRenderer, Snow/SnowTrail.glsl), and it's part
// of the depth the march goes through, so a print is a real hollow: it hides what's behind its far wall, the sun shadows it, and the sky sees less of its floor.
// A print's floor is level, measured down from where the snow lies on average, as a sole presses whatever drifts were there flat. What's pushed out heaps round
// it and can only lessen the depth, so nothing ever stands out of the mesh. Read SNOW.md.

float gSnow = 0.0;          // How much of this pixel is snow, the ground's own or what the weather has settled on it.
float gSnowDeep = 0.0;      // How deep a print goes here, of one in the Snowfield's snow: a dusting takes a shallower one.
float gSnowLevel = 0.0;     // Metres below the mesh the snow lies on average, which a floor goes down from.
float gTrailReach = 0.0;    // Metres past the materials' own relief the march has to look, for a print it might meet.
float gTrod = 0.0;          // How much the hit has been walked on, which crushes the crystals that glitter.
bool gTrailing = false;     // Whether this pixel reads the trail at all.

// Metres below the mesh at xz, given what the materials alone make it there, with what's been pressed into the snow and heaped on it. `scale` is how deep a full print
// goes, of SNOW_TRAIL_DEEPEST.
float SnowTrailed(float depth, vec2 xz, float scale)
{
    vec4 t = snowTrailAt(xz);
    if (t.r + t.g + t.b + t.a < 0.003) return depth;
    float lumps = snowLumps(xz);
    float heap = (t.b * SNOW_TRAIL_HIGHEST * (1.0 - t.g) * (0.6 + 0.8 * lumps) + t.a * 0.07 * lumps) * scale;
    float raised = max(depth - heap, 0.0);
    // Never shallower than the snow already was there, as a foot can't lift a drift's hollow.
    float pressed = max(gSnowLevel + t.r * SNOW_TRAIL_DEEPEST * scale, depth);
    // A furrow's floor is churned into chunks, where a footprint's is pressed smooth.
    float churned = t.g * (1.0 - smoothstep(0.75, 0.97, t.g));
    float trodden = max(mix(raised, pressed, t.g) - churned * 0.16 * snowChunks(xz) * scale, 0.0);
    return mix(depth, trodden, gSnow);
}

// How much of this pixel is snow, and, if there's a trail here, everything the march needs to go through it.
void SetUpSnow(TerrainMaterialSample surface, vec3 N, vec3 V)
{
    float snowWeight = 0.0;
    for (int i = 0; i < 3; i++)
    {
        if (abs(surface.mats[i] - snowMaterial) < 0.5) snowWeight += surface.weights[i];
    }
    // As Weather/Wetness.glsl settles it.
    float settled = 0.0;
    if (weatherKind.w > 0.5) settled = smoothstep(0.0, 0.5, weatherSurfaceAt(fragWorldPos.xz).z * smoothstep(0.1, 0.65, N.y));
    gSnow = max(snowWeight, settled);
    if (gSnow < 0.01) return;

    gSnowDeep = clamp(snowWeight + (1.0 - snowWeight) * settled * 0.35, 0.0, 1.0);
    float relief = dot(surface.weights, surface.reliefs);
    gSnowLevel = 0.5 * relief;

    // A print the march could meet lies along the way it goes, under this pixel or past it by as far sideways as the deepest print would take it. Where none
    // of these few looks finds anything, which is most snow, nothing else here reads the trail at all.
    float most = gSnowLevel + SNOW_TRAIL_DEEPEST * gSnowDeep * reliefScale;
    float NdotV = max(dot(N, V), 0.05);
    vec2 sideways = -(V - N * NdotV).xz / NdotV;
    if (!snowTrailCovers(fragWorldPos.xz, fragWorldPos.xz + sideways * most)) return;
    float deepest = 0.0;
    float anything = 0.0;
    for (int k = 0; k < 4; k++)
    {
        vec4 t = snowTrailAt(fragWorldPos.xz + sideways * most * (float(k) / 3.0));
        deepest = max(deepest, t.r);
        anything = max(anything, t.r + t.g + t.b + t.a);
    }
    if (anything < 0.003) return;
    gTrailing = true;
    gTrailReach = max(gSnowLevel + deepest * SNOW_TRAIL_DEEPEST * gSnowDeep * reliefScale - relief, 0.0) * gSnow;
}

// What being walked on does to how snow looks at the hit: the slopes of what was pressed and heaped, a floor pressed smooth and glazed, blue the deeper it goes, as
// packed snow lets light further in before it comes back, its floor seeing less of the sky, and heaped and kicked-up snow crumbling, bright and rough.
void TreadSnow(vec2 xz, vec3 N, float materialDepth, inout vec3 albedo, inout float roughness, inout vec3 normal, inout float ambient)
{
    vec4 t = snowTrailAt(xz);
    if (t.r + t.g + t.b + t.a < 0.003) return;
    gTrod = clamp(max(max(t.g, t.a * 1.5), t.b * 2.0), 0.0, 1.0);

    // Read at full depth whatever the quality, so a print still shows where there's no relief to march through.
    const float STEP = 0.045;
    float dx = SnowTrailed(materialDepth, xz + vec2(STEP, 0.0), gSnowDeep) - SnowTrailed(materialDepth, xz - vec2(STEP, 0.0), gSnowDeep);
    float dz = SnowTrailed(materialDepth, xz + vec2(0.0, STEP), gSnowDeep) - SnowTrailed(materialDepth, xz - vec2(0.0, STEP), gSnowDeep);
    vec3 trodden = normalize(N + vec3(dx, 0.0, dz) / (2.0 * STEP));
    float footprint = smoothstep(0.75, 0.97, t.g);
    float churned = t.g * (1.0 - footprint);
    normal = normalize(mix(normal, N, (footprint + 0.5 * churned) * gSnow * 0.9) + (trodden - N));

    float deep = clamp(t.r * SNOW_TRAIL_DEEPEST * gSnowDeep / 0.35, 0.0, 1.0) * t.g;
    float blue = gSnow * t.g * (0.35 + 0.65 * deep);
    albedo *= mix(vec3(1.0), vec3(0.5, 0.68, 1.0), blue);
    roughness = mix(roughness, 0.36, footprint * gSnow * 0.85);
    albedo *= mix(vec3(1.0), vec3(0.8, 0.87, 0.97), churned * gSnow);
    ambient *= 1.0 - 0.65 * deep * gSnow;

    float loose = gSnow * clamp(max(t.b * 2.2 * (1.0 - t.g), t.a * (0.4 + 0.9 * snowLumps(xz))), 0.0, 1.0);
    albedo = mix(albedo, min(albedo * 1.14, vec3(0.95)), loose);
    roughness = mix(roughness, 0.94, loose);
}

// ---------------------------------------------------------------- Snow glitters
// The odd crystal lies square to the sun and the eye at once and flashes. Each is a tiny facet on a fixed spot of the ground, tipped at random and very smooth, and
// the sun decides which of them flash: the light pass only gives one a highlight while it's nearly a mirror between the sun and the eye, so they twinkle as the
// camera moves and go out under cloud by themselves. Trodden snow is crushed, and doesn't.

const float GLINT_CELL = 0.06;      // Metres of ground to a facet at most.
const float GLINT_SHARE = 0.14;     // How many cells have one.
const float GLINT_TILT = 0.6;       // How far one may be tipped, as a slope.
const float GLINT_ROUGHNESS = 0.17;

void SnowGlitter(vec2 xz, vec3 N, float share, inout vec3 normal, inout float roughness)
{
    if (share < 0.05) return;
    vec2 cell = floor(xz / GLINT_CELL);
    if (hash21(cell + 41.0) < 1.0 - GLINT_SHARE) return;
    vec2 centre = (hash22(cell + 7.0) - 0.5) * 0.4;
    float facet = (1.0 - smoothstep(0.2, 0.3, length(fract(xz / GLINT_CELL) - 0.5 - centre))) * share;
    if (facet <= 0.0) return;
    vec2 tip = hash22(cell) * 2.0 - 1.0;
    normal = normalize(mix(normal, normalize(N + vec3(tip.x, 0.0, tip.y) * GLINT_TILT), facet));
    roughness = mix(roughness, GLINT_ROUGHNESS, facet);
}

// ---------------------------------------------------------------- What the ground is at the hit

struct GroundLook {
    vec3 albedo;
    vec3 normal;        // World space, before the weather's.
    float roughness;
    float ambient;
    float height;
    float subSurface;
    float emission;
    float metalic;
};

// One material straight from the arrays, its normal map read in the frame (alongU, alongV, facing): the gradients of u and v along the slope, scaled by the
// material's tile, which keeps its slopes those of the relief the march went through (Surface/SurfaceFrame.glsl's surfaceNormal). The ground reads no cards,
// so this leaves textureUtil.glsl's card arrays unread, and their units free for the roads.
GroundLook ReadMaterial(vec2 uv, float mat, vec2 dx, vec2 dy, vec3 alongU, vec3 alongV, vec3 facing) {
    vec4 albedo = textureGrad(albedoMapTextureArray, vec3(uv, mat), dx, dy);
    vec4 normal = textureGrad(normalMapTextureArray, vec3(uv, mat), dx, dy);
    vec4 props = textureGrad(materialMapTextureArray, vec3(uv, mat), dx, dy);
    vec2 slope = normal.xy * 2.0 - 1.0;
    vec3 tangentNormal = vec3(slope, sqrt(1.0 - clamp(dot(slope, slope), 0.0, 1.0)));
    GroundLook g;
    g.albedo = albedo.rgb;
    g.normal = normalize(alongU * tangentNormal.x + alongV * tangentNormal.y + facing * tangentNormal.z);
    g.subSurface = normal.z;
    g.ambient = normal.a;
    g.roughness = props.r;
    g.emission = props.g;
    g.metalic = props.b;
    g.height = props.a;
    return g;
}

GroundLook MixLooks(GroundLook a, GroundLook b, float t) {
    GroundLook g;
    g.albedo = mix(a.albedo, b.albedo, t);
    g.normal = normalize(mix(a.normal, b.normal, t));
    g.roughness = mix(a.roughness, b.roughness, t);
    g.ambient = mix(a.ambient, b.ambient, t);
    g.height = mix(a.height, b.height, t);
    g.subSurface = mix(a.subSurface, b.subSurface, t);
    g.emission = mix(a.emission, b.emission, t);
    g.metalic = mix(a.metalic, b.metalic, t);
    return g;
}

// The winning material at the hit (hitUV in fragUV's units). Explicit gradients, because the hit's coordinates jump at occlusion edges and their own
// derivatives would pick the wrong mip there: the footprint is the surface's own, carried into whichever frame the material is read in.
GroundLook LookGround(float mat, vec2 hitUV, vec3 N, vec3 facing, vec2 uvDX, vec2 uvDY, vec3 positionDX, vec3 positionDY) {
    float tile = max(materialTileMetres(mat, numberOfMaterials), 0.01);
    float scale = textureTileSize / tile;
    // The world's way: u along +X and v along +Z, projected straight down onto the slope. The tile cancels: u's gradient is the slope's share of +X over
    // the tile, times the tile.
    vec3 worldU = vec3(1.0, 0.0, 0.0) - N * N.x;
    vec3 worldV = vec3(0.0, 0.0, 1.0) - N * N.z;
    bool dirt = isRoadDirt(mat);
    bool street = isSetts(mat);
    if (!(dirt || street) || gRoad.junction >= 0.999) return ReadMaterial(hitUV * scale, mat, uvDX * scale, uvDY * scale, worldU, worldV, facing);

    vec2 at = RoadAt(hitUV * textureTileSize);
    vec2 atDX = vec2(dot(positionDX.xz, gRoad.side), dot(positionDX.xz, gRoad.way));
    vec2 atDY = vec2(dot(positionDY.xz, gRoad.side), dot(positionDY.xz, gRoad.way));
    vec3 side = vec3(gRoad.side.x, 0.0, gRoad.side.y);
    vec3 way = vec3(gRoad.way.x, 0.0, gRoad.way.y);
    vec3 roadU = side - N * dot(N, side);
    vec3 roadV = way - N * dot(N, way);

    GroundLook road;
    if (street) {
        float width = 2.0 * gRoad.halfWidth;
        float u = clamp((at.x + gRoad.halfWidth) / width, 0.0005, 0.9995);
        // u counts the street's width and v the tile: where those differ, the slopes are the relief's stretched the same way.
        float stretch = sqrt(tile / width);
        road = ReadMaterial(vec2(u, at.y / tile), mat, vec2(atDX.x / width, atDX.y / tile), vec2(atDY.x / width, atDY.y / tile), roadU * stretch, roadV / stretch, facing);
        gRutWater = 0.0;
    } else {
        road = ReadMaterial(at / tile, mat, atDX / tile, atDY / tile, roadU, roadV, facing);
        RoadShape s = roadShape(at, gRoad.halfWidth);
        // The ruts' own slopes, which no texture holds: down into them and up out of them, across the road, at the road's relief in metres.
        const float STEP = 0.04;
        float slope = (roadShapeHeight(roadShape(at + vec2(STEP, 0.0), gRoad.halfWidth)) - roadShapeHeight(roadShape(at - vec2(STEP, 0.0), gRoad.halfWidth))) / (2.0 * STEP);
        float relief = materialReliefRatio(mat, numberOfMaterials) * tile * reliefScale;
        road.normal = normalize(road.normal - normalize(roadU) * slope * relief);
        // Packed dark and damp in the ruts, darker on the lips, dust pale where feet go, darker and greener at the verge.
        road.albedo *= mix(1.0, 0.66, s.rut) * mix(1.0, 0.88, s.packed) * mix(1.0, 0.92, s.lip) * mix(1.0, 1.08, s.trodden) * mix(1.0, 0.8, s.verge);
        road.roughness = mix(road.roughness, road.roughness * 0.8, max(s.rut, s.packed * 0.5));
        road.height = roadDirtHeight(road.height, s);
        gRutWater = s.rut * (1.0 - gRoad.junction);
    }
    if (gRoad.junction > 0.001) road = MixLooks(road, ReadMaterial(hitUV * scale, mat, uvDX * scale, uvDY * scale, worldU, worldV, facing), gRoad.junction);
    return road;
}

// Broad drifts of lighter and darker, warmer and cooler ground, far larger than any material's tile, so that no tile is ever seen repeating and a field
// isn't one flat colour: what's painted is never quite the same twice.
float groundNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x), mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
}

vec3 GroundDrift(vec2 xz) {
    float broad = groundNoise(xz / 31.0) * 0.6 + groundNoise(xz / 11.0 + 17.3) * 0.4;
    float warmth = groundNoise(xz / 47.0 + 5.1);
    return mix(0.88, 1.1, broad) * mix(vec3(1.03, 1.0, 0.95), vec3(0.96, 1.0, 1.04), warmth);
}

// How far below the mesh the ground is, in metres: each material's own depth there by its share, so the march sees no false cliffs where they blend.
// `fade` scales every relief alike, as it was faded far off.
float BlendedDepth(TerrainMaterialSample s, float fade) {
    float depth = dot(s.weights, (1.0 - s.heights) * s.reliefs);
    if (gTrailing) depth = SnowTrailed(depth, s.uv * textureTileSize, gSnowDeep * reliefScale);
    return depth * fade;
}

// The most the march may go down here, in metres, before it's faded: the materials' relief, and any print it might meet.
float DeepestOf(TerrainMaterialSample s) {
    return dot(s.weights, s.reliefs) + gTrailReach;
}

// The single sharp winning material at the resolved surface coordinate
float WinningMaterial(TerrainMaterialSample s) {
    vec3 scores = s.heights * s.weights;

    float winningMat = s.mats.x;
    float maxScore = scores.x;

    if (scores.y > maxScore) {
        winningMat = s.mats.y;
        maxScore = scores.y;
    }
    if (scores.z > maxScore) {
        winningMat = s.mats.z;
    }

    return winningMat;
}

// Parallax occlusion mapping, done in world space.
// The surface near the fragment is treated as its tangent plane (normal N), with the ground hanging up to its relief below it along -N. The view ray
// R(t) = fragWorldPos - V*t is t*dot(N, V) below that plane, and projecting it back onto the plane along N gives the point it lies under. Terrain UVs
// are a planar XZ projection (uv = worldXZ / textureTileSize), so that point's uv is exact on any slope, with no tangent frame involved.
// Returns the hit UV, in fragUV's units. rayT gets the distance along -V from fragWorldPos to the hit, for the depth write, and depth how far below the mesh
// the hit is.
vec2 ParallaxOcclusionMapping(vec2 uv, vec3 V, vec3 N, TerrainMaterialSample surface, float fade, float lod, float pixelsPerMetre, out float rayT, out float depth) {
    rayT = 0.0;
    depth = 0.0;

    float NdotV = dot(N, V);
    if (NdotV <= 1e-3) return uv;
    float deepest = DeepestOf(surface) * fade;
    if (deepest <= 0.0) return uv;

    vec3 sideways = V - N * NdotV;
    float tPerDepth = 1.0 / NdotV;
    float tEnd = deepest * tPerDepth;
    vec2 uvPerT = -sideways.xz / textureTileSize;
    int steps = clamp(int(length(sideways) * tEnd * pixelsPerMetre * TERRAIN_STEPS_PER_PIXEL) + TERRAIN_LEAST_STEPS, TERRAIN_LEAST_STEPS, TERRAIN_MOST_STEPS);
    float dt = tEnd / float(steps);

    float gapBefore = BlendedDepth(SampleTerrainMaterials(surface, uv, lod), fade);
    if (gapBefore <= 0.0) return uv;
    float tBefore = 0.0;
    float tAfter = tEnd;
    float gapAfter = -1.0;
    bool crossed = false;
    for (int i = 1; i <= TERRAIN_MOST_STEPS; i++)
    {
        if (i > steps) break;
        float t = dt * float(i);
        float gap = BlendedDepth(SampleTerrainMaterials(surface, uv + uvPerT * t, lod), fade) - t * NdotV;
        if (gap <= 0.0)
        {
            tAfter = t;
            gapAfter = gap;
            crossed = true;
            break;
        }
        tBefore = t;
        gapBefore = gap;
    }

    float t = tEnd;
    if (crossed)
    {
        for (int j = 0; j < 3; j++)
        {
            float between = tBefore + (tAfter - tBefore) * gapBefore / (gapBefore - gapAfter);
            float gap = BlendedDepth(SampleTerrainMaterials(surface, uv + uvPerT * between, lod), fade) - between * NdotV;
            if (gap > 0.0)
            {
                tBefore = between;
                gapBefore = gap;
            }
            else
            {
                tAfter = between;
                gapAfter = gap;
            }
        }
        t = tBefore + (tAfter - tBefore) * gapBefore / (gapBefore - gapAfter);
    }
    rayT = t;
    depth = t * NdotV;
    return uv + uvPerT * t;
}

// How much of the sun reaches the hit past the ground's own relief around it, 1 in the open: the same blended depth as the march, followed up towards the
// sun until it's out, and the most it rises above that ray, as an angle seen from the hit, hides as much of the light (Surface/Relief.glsl's reliefShadow).
float ReliefShadow(vec2 uv, float depth, vec3 L, vec3 N, TerrainMaterialSample surface, float fade, float lod, float pixelsPerMetre) {
    float LdotN = dot(L, N);
    float deepest = DeepestOf(surface) * fade;
    float shown = smoothstep(TERRAIN_SHADOW_FROM, TERRAIN_SHADOW_AT, deepest * pixelsPerMetre);
    if (depth <= 1e-5 || LdotN <= 0.0 || deepest <= 0.0 || shown <= 0.0) return 1.0;
    vec3 sideways = L - N * LdotN;
    float end = min(depth / LdotN, TERRAIN_MOST_SLANT * deepest / max(length(sideways), 1e-4));
    vec2 uvPerT = sideways.xz / textureTileSize;
    int steps = clamp(int(length(sideways) * end * pixelsPerMetre * 0.5) + 4, 4, 16);
    // As Surface/Relief.glsl's: what casts a shadow is read a little coarser, and has to stand clear of the ray by a little, or the grain right beside a point
    // blacks it out in specks wherever the sun rakes across.
    float casting = lod + 1.5;
    float hidden = 0.0;
    for (int i = 1; i <= 16; i++)
    {
        if (i > steps) break;
        float t = end * (float(i) - 0.5) / float(steps);
        float above = (depth - LdotN * t) - BlendedDepth(SampleTerrainMaterials(surface, uv + uvPerT * t, casting), fade) - 0.06 * deepest;
        hidden = max(hidden, above / (t * TERRAIN_PENUMBRA + 1e-5));
    }
    return 1.0 - clamp(hidden, 0.0, 1.0) * shown;
}

void main() {
    vec3 N = normalize(fragWorldNormal);
    vec3 V = normalize(viewPos - fragWorldPos);

    vec2 uvDX = dFdx(fragUV);
    vec2 uvDY = dFdy(fragUV);
    vec3 positionDX = dFdx(fragWorldPos);
    vec3 positionDY = dFdy(fragWorldPos);
    vec2 uvToMacro = textureTileSize / fragInstanceChunkSizeXZ * (1.0 - textureMapOffset);
    vec2 macroDX = uvDX * uvToMacro;
    vec2 macroDY = uvDY * uvToMacro;
    vec2 macroCoord = clamp((fragWorldPos.xz - fragInstanceChunkPos) / fragInstanceChunkSizeXZ, 0.0, 1.0) * (1.0 - textureMapOffset) + vec2(textureMapOffset / 2.0);
    TerrainMaterialSample surface = GatherSurface(macroCoord, macroDX, macroDY);
    gRoad = ReadRoad(vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY);
    SetUpSnow(surface, N, V);

    // The mip fragUV's footprint wants at the arrays' size, and how many pixels a metre of the ground covers here, the fewer way.
    float lod = max(0.5 * log2(max(dot(uvDX, uvDX), dot(uvDY, uvDY)) * MATERIAL_SIZE * MATERIAL_SIZE), 0.0);
    vec3 alongX = positionDX - N * dot(positionDX, N);
    vec3 alongY = positionDY - N * dot(positionDY, N);
    float pixelsPerMetre = 1.0 / max(max(length(alongX), length(alongY)), 1e-6);

    // 1. Ray march using smoothly blended heights to eliminate false cliffs and window artifacts. Rock has no use for it: it's projected from the side,
    //    and what stands out of it is really there. The relief fades where it would move nothing by a pixel, which is also what keeps far ground cheap.
    bool bareRock = isRock(surface.mats.x) && surface.weights.x > 0.995;
    float NdotV = max(dot(N, V), 1e-3);
    float slant = sqrt(max(1.0 - NdotV * NdotV, 0.0)) / NdotV;
    float deepest = DeepestOf(surface);
    float fade = slant > TERRAIN_MOST_SLANT ? TERRAIN_MOST_SLANT / slant : 1.0;
    fade *= smoothstep(TERRAIN_SHOWN_FROM, TERRAIN_SHOWN_AT, deepest * fade * max(slant, 0.5) * pixelsPerMetre);
    vec2 winningUV = fragUV;
    float rayT = 0.0;
    float depth = 0.0;
    float sunThrough = 1.0;
    if (!bareRock && fade > 0.0 && deepest > 0.0) {
        winningUV = ParallaxOcclusionMapping(fragUV, V, N, surface, fade, lod, pixelsPerMetre, rayT, depth);
        sunThrough = ReliefShadow(winningUV, depth, normalize(sunDirectionWorld.xyz), N, surface, fade, lod, pixelsPerMetre);
    }

    // 2. Determine the single sharp winning material at the final resolved surface coordinate
    float winningMat = surface.mats.x;
    float materialDepth = 0.0;     // How far below the mesh the materials alone put the hit, which what's been trodden into snow is measured against.
    if (!bareRock) {
        TerrainMaterialSample atHit = SampleTerrainMaterials(surface, winningUV, lod);
        winningMat = WinningMaterial(atHit);
        materialDepth = dot(atHit.weights, (1.0 - atHit.heights) * atHit.reliefs);
    }
    bool rock = isRock(winningMat);

    // 3. Write the depth of the parallax hit so trees/grass don't float. The hit is on this pixel's view ray, rayT
    //    behind the rasterized surface. gl_FragDepth is written on every path; leaving it unwritten is undefined.
    vec4 clipPos = projectionMatrix * viewMatrix * vec4(fragWorldPos - V * rayT, 1.0);
    gl_FragDepth = max(gl_FragCoord.z, (clipPos.z / clipPos.w) * 0.5 + 0.5);

    // 4. The crisp winning material at the hit, at its own size, or along the road for a road's own (LookGround).
    vec3 facing = gl_FrontFacing ? N : -N;
    GroundLook look = LookGround(winningMat, winningUV, N, facing, uvDX, uvDY, positionDX, positionDY);
    if (rock) {
        RockLook stone = rockLook(fragWorldPos, N, positionDX, positionDY);
        look.albedo = stone.albedo;
        look.roughness = stone.roughness;
        look.ambient = stone.ambient;
        look.height = stone.height;
        look.normal = stone.normal;
    }

    gAlbedo.rgb = look.albedo;
    if (isScree(winningMat)) gAlbedo.rgb *= geologyColour.rgb;
    // Rock has the world's own colours in it already (Rock.glsl).
    if (!rock) gAlbedo.rgb *= GroundDrift(fragWorldPos.xz);

    // Under the grass the ground takes on the colour of the field, so gaps between blades read as more grass.
    float tintRoad = textureGrad(normalHeightTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).z;

    // On rock this says how far it stands out of what's around it, -1 to 1 (RockSculpt.MeasureRelief): shadow gathers in what's sunk, and an edge is worn pale. It's the shape of a
    // rock at a size that neither the texture nor the sun's shadows can give it, and what makes it read from this far up. Nothing is 0, so it fades out by itself at the rock's edge.
    if (rock) {
        gAlbedo.rgb *= mix(0.4, 1.0, smoothstep(-0.72, 0.0, tintRoad)) * mix(1.0, 1.32, smoothstep(0.04, 0.72, tintRoad));
        look.ambient *= mix(0.55, 1.0, smoothstep(-0.8, 0.0, tintRoad));
    }
    vec4 tintGrass = textureGrad(grassTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY);
    vec3 tintColour = textureGrad(grassColourTextureArray, vec3(macroCoord, fragInstanceHeightMapID), macroDX, macroDY).rgb;
    float tintFertility = grassFertility(tintGrass.r, N.y, tintRoad);
    gAlbedo.rgb = grassGroundTint(gAlbedo.rgb, fragWorldPos.xz, tintFertility, length(viewPos - fragWorldPos), tintColour, tintGrass.b);

    if (DEBUG_VIEW) {
        gAlbedo.rgb = vec3(hash13(gl_PrimitiveID)) * COLOR_TEST;
    }
    // Nor under a hall's floor (Halls.glsl), which would be under the ground: it's after every derivative this takes, as a quad that's lost a fragment has none.
    if (underHallFloor(fragWorldPos)) discard;

    gAlbedo.a = transmittance(look.subSurface, look.height);
    vec3 worldNormal = look.normal;

    // The hollows of the ground are in the grass map's alpha (TerrainSurface), for the rain to fill, and a road's ruts fill first.
    float roughness = look.roughness;
    vec3 albedo = gAlbedo.rgb;
    float hollow = max(tintGrass.a, gRutWater * 0.8);
    weatherWetten(fragWorldPos, N, weatherPorosity(winningMat, numberOfMaterials), look.metalic, hollow, look.height, albedo, roughness, worldNormal);
    // Snow is walked through and glitters after the weather has had its say, as what settled is what's trodden.
    vec2 hitXZ = winningUV * textureTileSize;
    if (gTrailing && !rock) TreadSnow(hitXZ, N, materialDepth, albedo, roughness, worldNormal, look.ambient);
    if (!rock) SnowGlitter(hitXZ, N, gSnow * (1.0 - gTrod), worldNormal, roughness);
    gAlbedo.rgb = albedo;
    gNormal.a = look.ambient;

    vec3 normal = normalize(mat3(viewMatrix) * worldNormal);
    gNormal.xyz = compressNormal(normal);

    gMaterials.r = weatherFilterRoughness(roughness, normal);
    gMaterials.g = encodeEmission(look.emission * EMISSION_GAIN);
    gMaterials.b = look.metalic;
    gMaterials.a = sunThrough;
}
