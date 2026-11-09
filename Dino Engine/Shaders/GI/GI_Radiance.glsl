#ifndef DINO_GI_RADIANCE_GLSL
#define DINO_GI_RADIANCE_GLSL

// What a ray sees where it strikes: the light leaving the face it came in through. Each surface voxel is lit once a frame
// (GI_Voxel_Light.comp), a radiance for each of its open faces, so that the two sides of a thin wall stay apart: a ray from inside a
// house meets the inside of its wall, however brightly the sun stands on the outside. Needs GI/GI.glsl and GI/GI_Voxels.glsl.

uniform usampler3D giIndex;     // Where each surface voxel is in its level's pool, plus one; 0 for anything else.

layout(std430, binding = 2) readonly buffer GiPoolRead { uint giPool[]; };

uint giPoolEntry(int level, uint index)
{
    return (uint(level) * GI_POOL_CAPACITY + index - 1u) * GI_POOL_STRIDE;
}

vec3 giHitRadiance(GiHit h, vec3 dir)
{
    uint index = texelFetch(giIndex, giTexel(h.voxel, h.level), 0).r;
    if (index == 0u) return vec3(0.0);
    uint e = giPoolEntry(h.level, index);
    uint faces = giPool[e + 7u];
    int face = h.face;
    if (face < 0 || ((faces >> uint(face)) & 1u) == 0u)
    {
        // In through a face that isn't open, or from inside: the open face that looks most back along the ray.
        float best = -2.0;
        face = -1;
        for (int f = 0; f < 6; f++)
        {
            if (((faces >> uint(f)) & 1u) == 0u) continue;
            float facing = -dot(GI_FACE_NORMAL[f], dir);
            if (facing > best)
            {
                best = facing;
                face = f;
            }
        }
        if (face < 0) return vec3(0.0);
    }
    return giUnpackRadiance(giPool[e + 1u + uint(face)]);
}

#endif
