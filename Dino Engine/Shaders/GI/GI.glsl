#ifndef DINO_GI_GLSL
#define DINO_GI_GLSL

// Global illumination, as everything that takes part in it reads it (GlobalIlluminationRenderer, and read GI.md first). The world is
// kept around the camera as four nested levels of voxels, finest in the middle, each a window that slides along with the camera in
// steps and wraps around its edges, so that moving only ever builds the slab that has come into view. The levels are stacked along y
// in each texture, as a sampler can't be picked per thread. Light is gathered by radiance cascades on probes in the world, which only
// exist near what the camera sees. The layout of GiData must match GlobalIlluminationRenderer.UploadFrame.

#define GI_LEVELS 4
#define GI_CASCADES 5

const ivec3 GI_DIM = ivec3(192, 64, 128);       // Voxels in a level's window: wider than deep, as the view is.
const ivec3 GI_BRICKS = ivec3(48, 16, 32);      // The same in bricks of 4x4x4 voxels, a bit each.
const uint GI_POOL_CAPACITY = 262144u;          // Surface voxels a level can light.
const uint GI_POOL_STRIDE = 8u;                 // uints to a surface voxel: where it is, a radiance per face, which faces are open.
const int GI_COUNT_PENDING = 120;               // Where in the counts buffer each level keeps how many of its voxels wait for a sun and sky.
const float GI_PI = 3.14159265359;

layout(std140, binding = 11) uniform GiData {
    ivec4 giOrigin[GI_LEVELS];      // xyz the world voxel at the low corner of each level's window
    vec4 giVoxel[GI_LEVELS];        // x metres to a voxel, y its inverse
    vec4 giSunDirection;            // xyz towards the sun, w 1 when it shines
    vec4 giSunRadiance;             // rgb what the sun gives a surface facing it, before cloud shadow
    vec4 giSkyScale;                // rgb the light the air scatters everywhere, w what the sky's radiance is multiplied by
    vec4 giGroundRadiance;          // rgb what's seen below the horizon past the end of the voxels
    vec4 giParams;                  // x emission scale, y how much of a bounce bounces again, z frame, w local lights
    vec4 giCascade[GI_CASCADES];    // x metres between probes, y where its rays start, z where they end, w directions
    ivec4 giColumns[GI_CASCADES];   // xy world column at the low corner of the region with probes, zw columns across it
    vec4 giFocus;                   // xyz where the camera looks, w height map: metres to a texel
    vec4 giHeightMapping;           // xy world to height map uv: scale, zw offset
};

// ---- Faces, in this order everywhere: +x, -x, +y, -y, +z, -z. A face is the side of a voxel facing that way.

const vec3 GI_FACE_NORMAL[6] = vec3[](vec3(1, 0, 0), vec3(-1, 0, 0), vec3(0, 1, 0), vec3(0, -1, 0), vec3(0, 0, 1), vec3(0, 0, -1));
const ivec3 GI_FACE_STEP[6] = ivec3[](ivec3(1, 0, 0), ivec3(-1, 0, 0), ivec3(0, 1, 0), ivec3(0, -1, 0), ivec3(0, 0, 1), ivec3(0, 0, -1));

// ---- What each voxel holds (voxInfo, a uint).

const uint GI_INFO_FACES = 0x3Fu;           // Faces open to the air: only these can be struck, or lit.
const int GI_INFO_SUN_SHIFT = 6;            // 4 bits: how much of the sun reaches it, of 15.
const int GI_INFO_SKY_SHIFT = 10;           // 6 x 3 bits: how much sky each face sees, of 7.
const uint GI_INFO_PENDING = 1u << 28;      // Its sun and sky are still to be worked out.
const uint GI_INFO_SOLID = 1u << 29;
const uint GI_INFO_TERRAIN = 1u << 30;      // Its colour is the ground's, and is filled in again when the ground's textures arrive.

float giInfoSun(uint info) { return float((info >> GI_INFO_SUN_SHIFT) & 15u) / 15.0; }
float giInfoSky(uint info, int face) { return float((info >> (GI_INFO_SKY_SHIFT + 3 * face)) & 7u) / 7.0; }

// Which way the surface a voxel stands for faces: its open faces, together. A slope that voxels make into steps has the top and the side
// of every step open, and faces between them. 0 where they cancel, as the two sides of a thin wall do.
vec3 giOpenNormal(uint faces)
{
    vec3 sum = vec3(0.0);
    for (int f = 0; f < 6; f++)
    {
        if (((faces >> uint(f)) & 1u) != 0u) sum += GI_FACE_NORMAL[f];
    }
    float length_ = length(sum);
    return length_ > 0.5 ? sum / length_ : vec3(0.0);
}

// The surface an open face of a voxel belongs to, as the way it faces: every open face on its side of the voxel together. The top and
// the side of a step of a slope are the one slope and are lit as it is, as a ray that meets the side of a step meets the slope; the two
// sides of a thin wall are two surfaces.
vec3 giSurfaceNormal(uint faces, int face)
{
    vec3 n = GI_FACE_NORMAL[face];
    vec3 sum = vec3(0.0);
    for (int f = 0; f < 6; f++)
    {
        if (((faces >> uint(f)) & 1u) != 0u && dot(GI_FACE_NORMAL[f], n) > -0.01) sum += GI_FACE_NORMAL[f];
    }
    float length_ = length(sum);
    return length_ > 0.5 ? sum / length_ : n;
}

// Where a voxel's rays start, so that the steps of its own surface lie behind them: out of it along the way its surface faces, or for
// the faces on one side of a thin wall, along theirs. A ray from the middle of a step finds the next step up in its way, and a sunlit
// roof would shade itself.
vec3 giSurfaceStart(vec3 centre, float size, uint faces, vec3 towards)
{
    vec3 sum = vec3(0.0);
    for (int f = 0; f < 6; f++)
    {
        if (((faces >> uint(f)) & 1u) != 0u && dot(GI_FACE_NORMAL[f], towards) > -0.01) sum += GI_FACE_NORMAL[f];
    }
    float length_ = length(sum);
    vec3 out_ = length_ > 0.5 ? sum / length_ : towards;
    return centre + out_ * (size * 0.75);
}

// ---- Where a voxel is kept. World voxel coordinates can be negative; the bias keeps % on positive numbers, where it's defined.

int giWrap(int v, int size) { return (v + size * 65536) % size; }

ivec3 giTexel(ivec3 voxel, int level)
{
    return ivec3(giWrap(voxel.x, GI_DIM.x), giWrap(voxel.y, GI_DIM.y) + level * GI_DIM.y, giWrap(voxel.z, GI_DIM.z));
}

ivec3 giBrickTexel(ivec3 brick, int level)
{
    return ivec3(giWrap(brick.x, GI_BRICKS.x), giWrap(brick.y, GI_BRICKS.y) + level * GI_BRICKS.y, giWrap(brick.z, GI_BRICKS.z));
}

bool giInWindow(ivec3 voxel, int level)
{
    ivec3 r = voxel - giOrigin[level].xyz;
    return all(greaterThanEqual(r, ivec3(0))) && all(lessThan(r, GI_DIM));
}

ivec3 giVoxelOf(vec3 world, int level)
{
    return ivec3(floor(world * giVoxel[level].y));
}

vec3 giVoxelCentre(ivec3 voxel, int level)
{
    return (vec3(voxel) + 0.5) * giVoxel[level].x;
}

// The finest level whose window holds a point, or GI_LEVELS if none does.
int giLevelOf(vec3 world, int finest)
{
    for (int level = finest; level < GI_LEVELS; level++)
    {
        if (giInWindow(giVoxelOf(world, level), level)) return level;
    }
    return GI_LEVELS;
}

// Bit of a voxel in its brick's mask.
uint giBrickBit(ivec3 voxel) { ivec3 l = voxel & 3; return uint(l.x + 4 * l.y + 16 * l.z); }
bool giMaskHas(uvec2 mask, uint bit) { return (((bit < 32u) ? mask.x : mask.y) >> (bit & 31u) & 1u) != 0u; }

// ---- Packing. Radiance is three small floats in a uint (red and green 11 bits, blue 10, none signed), made from half floats by
// dropping the sign and the lowest bits of the mantissa, rounded.

uint giPackRadiance(vec3 c)
{
    c = clamp(c, vec3(0.0), vec3(60000.0));
    uint rg = packHalf2x16(c.rg);
    uint b = packHalf2x16(vec2(c.b, 0.0));
    uint r11 = min(((rg & 0x7FFFu) + 8u) >> 4, 0x7BFu);
    uint g11 = min((((rg >> 16) & 0x7FFFu) + 8u) >> 4, 0x7BFu);
    uint b10 = min(((b & 0x7FFFu) + 16u) >> 5, 0x3DFu);
    return r11 | (g11 << 11) | (b10 << 22);
}

vec3 giUnpackRadiance(uint p)
{
    float r = unpackHalf2x16((p & 0x7FFu) << 4).x;
    float g = unpackHalf2x16(((p >> 11) & 0x7FFu) << 4).x;
    float b = unpackHalf2x16(((p >> 22) & 0x3FFu) << 5).x;
    return vec3(r, g, b);
}

uvec2 giPackHalf4(vec4 v) { return uvec2(packHalf2x16(v.xy), packHalf2x16(v.zw)); }
vec4 giUnpackHalf4(uvec2 p) { return vec4(unpackHalf2x16(p.x), unpackHalf2x16(p.y)); }

// A voxel's place in a level, packed for the pool: where it's kept (8 bits each of x and z, 7 of y), then the level. The place it's
// kept doesn't move when the window does, and the window says which world voxel is kept there now.
uint giPackVoxel(ivec3 voxel, int level)
{
    ivec3 t = ivec3(giWrap(voxel.x, GI_DIM.x), giWrap(voxel.y, GI_DIM.y), giWrap(voxel.z, GI_DIM.z));
    return uint(t.x) | (uint(t.z) << 8) | (uint(t.y) << 16) | (uint(level) << 24);
}

ivec3 giUnpackVoxel(uint p, out int level)
{
    level = int(p >> 24);
    ivec3 t = ivec3(int(p & 0xFFu), int((p >> 16) & 0x7Fu), int((p >> 8) & 0xFFu));
    ivec3 o = giOrigin[level].xyz;
    return o + ivec3(giWrap(t.x - o.x, GI_DIM.x), giWrap(t.y - o.y, GI_DIM.y), giWrap(t.z - o.z, GI_DIM.z));
}

// ---- Cheap hashes, for anything that has to be the same every frame.

uint giHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

uint giHash3(ivec3 v) { return giHash(uint(v.x) * 73856093u ^ giHash(uint(v.y) * 19349663u ^ giHash(uint(v.z) * 83492791u))); }
float giUnit(uint h) { return float(h & 0xFFFFFFu) / 16777216.0; }

#endif
