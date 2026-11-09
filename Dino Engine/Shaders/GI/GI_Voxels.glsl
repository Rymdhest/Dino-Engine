#ifndef DINO_GI_VOXELS_GLSL
#define DINO_GI_VOXELS_GLSL

// Rays through the voxels. A level is walked brick by brick, and a brick with nothing in it costs one fetch to cross: only bricks that
// hold something are walked voxel by voxel, on the bits of their mask. A ray that leaves a level's window goes on in the next, coarser
// one, which is the same world in bigger voxels: a far stretch of a ray costs no more steps than a near one. Needs GI/GI.glsl.

uniform usampler3D giBricks;    // A uvec2 to a brick of 4x4x4 voxels, a bit to a voxel, solid or not.

#define GI_MAX_STEPS 192

struct GiHit
{
    bool hit;
    float t;        // Metres along the ray to where it struck, or to where it stopped.
    int level;
    ivec3 voxel;    // The world voxel struck.
    int face;       // The face it came in through, -1 if that isn't known (it started there, or there in a coarser level).
};

bool giSolid(ivec3 voxel, int level)
{
    uvec2 mask = texelFetch(giBricks, giBrickTexel(voxel >> 2, level), 0).xy;
    return giMaskHas(mask, giBrickBit(voxel));
}

// Whether a point is inside anything, in the finest level from `finest` up that holds it. Outside every window nothing is solid.
bool giSolidAt(vec3 world, int finest)
{
    int level = giLevelOf(world, finest);
    return level < GI_LEVELS && giSolid(giVoxelOf(world, level), level);
}

// Marches from tStart to tEnd metres along a unit direction, starting in `level`. The first `escape` solid voxels it finds itself in
// don't count, for a ray that leaves from a surface whose own voxel holds it; any empty voxel ends that.
GiHit giTrace(vec3 origin, vec3 dir, float tStart, float tEnd, int level, int escape)
{
    GiHit h;
    h.hit = false;
    h.t = tEnd;
    h.level = level;
    h.voxel = ivec3(0);
    h.face = -1;

    vec3 d = vec3(abs(dir.x) < 1e-6 ? (dir.x < 0.0 ? -1e-6 : 1e-6) : dir.x,
                  abs(dir.y) < 1e-6 ? (dir.y < 0.0 ? -1e-6 : 1e-6) : dir.y,
                  abs(dir.z) < 1e-6 ? (dir.z < 0.0 ? -1e-6 : 1e-6) : dir.z);
    vec3 inv = 1.0 / d;
    ivec3 stp = ivec3(d.x > 0.0 ? 1 : -1, d.y > 0.0 ? 1 : -1, d.z > 0.0 ? 1 : -1);
    vec3 forward = vec3(greaterThan(stp, ivec3(0)));

    float t = tStart;
    int steps = 0;
    int escaping = escape;

    while (level < GI_LEVELS && t < tEnd && steps < GI_MAX_STEPS)
    {
        float size = giVoxel[level].x;
        float invSize = giVoxel[level].y;
        ivec3 low = giOrigin[level].xyz;
        vec3 o = origin * invSize - vec3(low);      // The origin in voxels from the window's corner.

        // Where the ray is inside this window, in metres along it.
        vec3 tLow = -o * inv * size;
        vec3 tHigh = (vec3(GI_DIM) - o) * inv * size;
        vec3 tNear = min(tLow, tHigh);
        vec3 tFar = max(tLow, tHigh);
        float enter = max(max(tNear.x, tNear.y), tNear.z);
        float leave = min(min(tFar.x, tFar.y), tFar.z);
        if (t < enter - 1e-4 || t >= leave)
        {
            level++;
            continue;
        }
        float end = min(tEnd, leave);

        vec3 p = o + d * (t * invSize);
        ivec3 v = clamp(ivec3(floor(p)), ivec3(0), GI_DIM - 1);
        vec3 tDelta = abs(inv) * size;
        vec3 tMax = (vec3(v) + forward - o) * inv * size;
        int face = -1;
        ivec3 brickBase = low >> 2;
        ivec3 brick = ivec3(-1000000);
        uvec2 mask = uvec2(0u);
        bool outside = false;

        while (steps < GI_MAX_STEPS)
        {
            steps++;
            ivec3 b = v >> 2;
            if (b != brick)
            {
                brick = b;
                mask = texelFetch(giBricks, giBrickTexel(brickBase + b, level), 0).xy;
                if (mask == uvec2(0u))
                {
                    // Nothing in this brick: straight to where the ray leaves it.
                    vec3 exits = (vec3(b * 4) + forward * 4.0 - o) * inv * size;
                    float out_ = min(min(exits.x, exits.y), exits.z);
                    escaping = 0;
                    if (out_ >= end)
                    {
                        t = end;
                        break;
                    }
                    int axis = (exits.x <= exits.y && exits.x <= exits.z) ? 0 : (exits.y <= exits.z ? 1 : 2);
                    t = out_;
                    vec3 q = o + d * (t * invSize);
                    v = clamp(ivec3(floor(q)), b * 4, b * 4 + 3);
                    v[axis] = stp[axis] > 0 ? b[axis] * 4 + 4 : b[axis] * 4 - 1;
                    face = axis * 2 + (stp[axis] > 0 ? 1 : 0);
                    if (any(lessThan(v, ivec3(0))) || any(greaterThanEqual(v, GI_DIM)))
                    {
                        outside = true;
                        break;
                    }
                    tMax = (vec3(v) + forward - o) * inv * size;
                    continue;
                }
            }

            if (giMaskHas(mask, giBrickBit(v)))
            {
                if (escaping == 0)
                {
                    h.hit = true;
                    h.t = t;
                    h.level = level;
                    h.voxel = v + low;
                    h.face = face;
                    return h;
                }
                escaping--;
                face = -1;
            }
            else
            {
                escaping = 0;
            }

            if (tMax.x < tMax.y && tMax.x < tMax.z)
            {
                t = tMax.x;
                tMax.x += tDelta.x;
                v.x += stp.x;
                face = stp.x > 0 ? 1 : 0;
            }
            else if (tMax.y < tMax.z)
            {
                t = tMax.y;
                tMax.y += tDelta.y;
                v.y += stp.y;
                face = stp.y > 0 ? 3 : 2;
            }
            else
            {
                t = tMax.z;
                tMax.z += tDelta.z;
                v.z += stp.z;
                face = stp.z > 0 ? 5 : 4;
            }
            if (t >= end) break;
            if (any(lessThan(v, ivec3(0))) || any(greaterThanEqual(v, GI_DIM)))
            {
                outside = true;
                break;
            }
        }

        if (t >= tEnd) break;
        // Out of this window, or at the end of its stretch: the rest is the next level's. The finer voxels have shown the way clear to
        // here, so if the ray finds itself inside a coarse voxel at once, that's the coarse voxel standing proud of what's really there:
        // it may pass the one it starts in.
        t = max(t, outside ? t : end);
        if (t < leave - 1e-4 && !outside) break;
        level++;
        escaping = max(escaping, 1);
    }
    h.t = min(t, tEnd);
    return h;
}

// Whether nothing stands between two points less than a few voxels apart, by looking at points along the way closer together than a
// voxel, which can't step over one: cheaper than walking, for the many short looks of the gather. Points at the start may be inside
// the surface the first stands on, up to `escape` of them, until one isn't.
bool giClearNear(vec3 from, vec3 to, int finest, int escape)
{
    int level = giLevelOf(from, finest);
    if (level >= GI_LEVELS) return true;
    vec3 along = to - from;
    float stride = giVoxel[level].x * 0.6;
    int steps = min(int(ceil(length(along) / stride)), 12);
    bool inside = true;
    for (int i = 1; i <= steps; i++)
    {
        vec3 p = from + along * (float(i) / float(steps));
        ivec3 voxel = giVoxelOf(p, level);
        bool solid = giInWindow(voxel, level) && giSolid(voxel, level);
        if (!solid)
        {
            inside = false;
            continue;
        }
        if (inside && i <= escape) continue;
        return false;
    }
    return true;
}

// Whether nothing stands between two points, the first `escape` solid voxels at the start aside.
bool giClear(vec3 from, vec3 to, int finest, int escape)
{
    vec3 along = to - from;
    float length_ = length(along);
    if (length_ < 1e-4) return true;
    int level = giLevelOf(from, finest);
    if (level >= GI_LEVELS) return true;
    return !giTrace(from, along / length_, 0.0, length_, level, escape).hit;
}

#endif
