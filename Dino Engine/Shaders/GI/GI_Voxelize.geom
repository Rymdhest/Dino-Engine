#version 430

// Each triangle is drawn looking down the axis it faces most, a pixel to a voxel, so that it covers as many voxels as it can and leaves
// no gaps along its depth (GI_Voxelize.frag fills in the voxels each pixel's stretch of it passes through).

#include GI/GI.glsl

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

in VS_OUT
{
    vec3 world;
    vec3 colour;
    vec2 uv;
    float material;
} i[];

out GS_OUT
{
    vec3 voxel;         // Where it is, in voxels from the window's corner.
    vec3 colour;
    vec2 uv;
    flat float material;
    flat int axis;
} o;

uniform int level;
uniform ivec3 regionMin;
uniform ivec3 regionMax;

const float GI_VIEWPORT = 192.0;    // GlobalIlluminationRenderer.VoxelizeViewport: every plane of a window fits.

void main()
{
    vec3 a = i[0].world, b = i[1].world, c = i[2].world;
    float inv = giVoxel[level].y;
    vec3 low = min(min(a, b), c) * inv;
    vec3 high = max(max(a, b), c) * inv;
    if (any(greaterThan(low, vec3(regionMax) + 1.0)) || any(lessThan(high, vec3(regionMin) - 1.0))) return;

    vec3 n = abs(cross(b - a, c - a));
    int axis = (n.x >= n.y && n.x >= n.z) ? 0 : (n.y >= n.z ? 1 : 2);

    vec3 origin = vec3(giOrigin[level].xyz);
    for (int k = 0; k < 3; k++)
    {
        vec3 v = i[k].world * inv - origin;
        vec2 plane = axis == 0 ? v.zy : (axis == 1 ? v.xz : v.xy);
        gl_Position = vec4(plane / GI_VIEWPORT * 2.0 - 1.0, 0.0, 1.0);
        o.voxel = v;
        o.colour = i[k].colour;
        o.uv = i[k].uv;
        o.material = i[k].material;
        o.axis = axis;
        EmitVertex();
    }
    EndPrimitive();
}
