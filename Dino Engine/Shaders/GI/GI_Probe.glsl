#ifndef DINO_GI_PROBE_GLSL
#define DINO_GI_PROBE_GLSL

// The probes of the radiance cascades (read GI.md). Cascade c has a probe at every point of a lattice of spacing giCascade[c].x, at
// the centres of its cells, but only near what the camera sees: the world is laid out in columns of the lattice, and each column keeps
// a bit for each of 32 heights above a floor a little under its ground, set where a probe is wanted. Its probes are numbered in a scan
// over all columns, so finding one is a bit count. Each finer probe has the eight of the next cascade around it as its parents.
// Needs GI/GI.glsl.

layout(std430, binding = 0) buffer GiCounts { uint giCounts[]; };
layout(std430, binding = 1) readonly buffer GiDirections { vec4 giDirections[]; };   // The centre of each cell of each cascade's sphere.
layout(std430, binding = 5) buffer GiColumnMask { uint giColumnMask[]; };
layout(std430, binding = 6) buffer GiColumnBase { int giColumnBase[]; };            // Height of each column's lowest bit, in lattice cells.
layout(std430, binding = 7) buffer GiColumnOffset { uint giColumnOffset[]; };        // Number of its first probe, over all cascades.
layout(std430, binding = 8) buffer GiProbeKey { uvec2 giProbeKey[]; };
layout(std430, binding = 9) buffer GiProbePlace { vec4 giProbePlace[]; };            // xyz where it is, out of anything solid; w flags.
layout(std430, binding = 10) buffer GiProbeParent { int giProbeParent[]; };          // Its eight parents, -1 where there's none.

// GlobalIlluminationRenderer's layout of the same: change them together.
const int GI_COLUMN_START[GI_CASCADES + 1] = int[](0, 131072, 163840, 172032, 174080, 174592);
const int GI_PROBE_CAPACITY[GI_CASCADES] = int[](196608, 49152, 12288, 3072, 768);
const int GI_PROBE_BASE[GI_CASCADES + 1] = int[](0, 196608, 245760, 258048, 261120, 261888);
const int GI_DIRECTION_COUNT[GI_CASCADES] = int[](12, 48, 192, 768, 3072);
const int GI_DIRECTION_BASE[GI_CASCADES] = int[](0, 12, 60, 252, 1020);
const int GI_TRACE_LEVEL[GI_CASCADES] = int[](0, 0, 1, 2, 3);     // The finest voxels each cascade's rays are walked through.
const float GI_COLUMN_FLOOR = 1.0;                                  // Metres under its lowest ground a column starts.

const int GI_COUNT_PROBE_START = 20;
const int GI_COUNT_PROBE_COUNT = 28;
const int GI_COUNT_DISPATCH = 36;   // Then for each cascade three dispatches of four: to set up, to trace, to merge.
const int GI_COUNT_FRESH = 96;      // How many probes are placed afresh this frame (GI_Probe_Init.comp),
const int GI_COUNT_FRESH_DISPATCH = 100;    // and two dispatches of four for them: to place them, and to ask which parents each can see.

const uint GI_PROBE_BURIED = 1u;    // Inside something with nowhere near to go: it sees nothing.
const uint GI_PROBE_STAMP = 0x7Eu;  // The voxels a probe was set up among (giProbeStamp), in the bits above.
const uint GI_MISSED = 0xFFFFFFFFu; // An interval that struck nothing: no light, and all of what's beyond let through. Packing never makes it.

int giProbeCount(int c) { return int(giCounts[GI_COUNT_PROBE_COUNT + c]); }

// The cascade a probe's slot belongs to.
int giCascadeOfSlot(int slot)
{
    int c = 0;
    while (c + 1 < GI_CASCADES && slot >= GI_PROBE_BASE[c + 1]) c++;
    return c;
}

int giColumnIndex(int c, ivec2 column)
{
    ivec2 r = column - giColumns[c].xy;
    if (any(lessThan(r, ivec2(0))) || any(greaterThanEqual(r, giColumns[c].zw))) return -1;
    return GI_COLUMN_START[c] + r.y * giColumns[c].z + r.x;
}

vec3 giLatticePoint(int c, ivec3 lattice)
{
    return (vec3(lattice) + 0.5) * giCascade[c].x;
}

// Which voxels a probe is set up among: the finest level its own rays walk through where it stands, and the finest its parents' do. Where
// it stands and which parents it sees hold for as long as these do, as nothing in the voxels moves; when a window slides over it, it
// stands among finer voxels or coarser ones, and is set up again (GI_Probe_Init.comp). Kept in a probe's flags.
uint giProbeStamp(int c, ivec3 lattice)
{
    vec3 p = giLatticePoint(c, lattice);
    uint own = uint(giLevelOf(p, GI_TRACE_LEVEL[c]));
    uint parents = c + 1 < GI_CASCADES ? uint(giLevelOf(p, GI_TRACE_LEVEL[c + 1])) : 0u;
    return (own | (parents << 3)) << 1;
}

// The cascade's number for the probe at a point of its lattice, or -1 if there isn't one.
int giProbeAt(int c, ivec3 lattice)
{
    int column = giColumnIndex(c, lattice.xz);
    if (column < 0) return -1;
    uint mask = giColumnMask[column];
    int k = lattice.y - giColumnBase[column];
    if (k < 0 || k > 31 || ((mask >> uint(k)) & 1u) == 0u) return -1;
    uint global = giColumnOffset[column] + uint(bitCount(mask & ((1u << uint(k)) - 1u)));
    int local = int(global) - int(giCounts[GI_COUNT_PROBE_START + c]);
    return local < GI_PROBE_CAPACITY[c] ? local : -1;
}

uvec2 giPackKey(int c, ivec3 lattice)
{
    return uvec2(uint(lattice.x + 32768) | (uint(lattice.z + 32768) << 16), uint(lattice.y + 32768) | (uint(c) << 16));
}

ivec3 giUnpackKey(uvec2 key)
{
    return ivec3(int(key.x & 0xFFFFu) - 32768, int(key.y & 0xFFFFu) - 32768, int(key.x >> 16) - 32768);
}

// The low corner of a probe's parents, and how much each counts: a probe lies a quarter of the way across its parents' cell.
ivec3 giParentLow(ivec3 lattice) { return (lattice - 1) >> 1; }

float giParentWeight(ivec3 lattice, int j)
{
    ivec3 corner = ivec3(j & 1, (j >> 1) & 1, j >> 2);
    ivec3 odd = lattice & 1;
    vec3 high = mix(vec3(0.75), vec3(0.25), vec3(odd));     // The weight of the upper parent on each axis.
    vec3 w = mix(1.0 - high, high, vec3(corner));
    return w.x * w.y * w.z;
}

#endif
