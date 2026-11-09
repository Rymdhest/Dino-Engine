#version 430

// What an object leaves in the voxels it passes through: how much of each it fills, its colour and its glow. The framebuffer has no
// attachments and asks for eight samples, and how many of them a triangle covers is how much of the voxel's face it fills, so a post
// too thin for a big voxel fills that much of it, and not all; a driver that gives one sample fills all of each voxel whose centre a
// triangle covers. A leaf card fills what its alpha does, read at the mip a voxel spans. What's
// filled is added up, as the triangles of one surface share a voxel between them and together fill it (taken as layers that let light
// through independently, two halves came to three quarters, and every tessellated roof had holes in it). GI_Voxel_Finalize.comp makes
// a voxel solid with that chance, the same way every time: a crown of leaves is a scatter of solid voxels that lets through about what
// the leaves would.

#include Globals.glsl
#include GI/GI.glsl

uniform int numberOfMaterials;
uniform sampler2DArray albedoMapTextureArray;
uniform sampler2DArray normalMapTextureArray;
uniform sampler2DArray materialMapTextureArray;
uniform sampler2DArray albedoMapModelTextureArray;
uniform sampler2DArray normalMapModelTextureArray;
uniform sampler2DArray materialMapModelTextureArray;

#include textureUtil.glsl
#include Rock.glsl
#include gBufferUtil.glsl

in GS_OUT
{
    vec3 voxel;
    vec3 colour;
    vec2 uv;
    flat float material;
    flat int axis;
} i;

layout(r32ui, binding = 2) uniform coherent uimage3D voxFilled;         // The level's voxIndex, used as scratch while it's built.
layout(r32ui, binding = 3) uniform coherent uimage3D scratchColour;     // Mean colour so far, and how many have added to it.
layout(r32ui, binding = 4) uniform coherent uimage3D scratchEmission;   // The brightest glow.

uniform int level;
uniform ivec3 regionMin;
uniform ivec3 regionMax;

const float GI_FILL_SCALE = 256.0;      // Fixed point, for the atomic sum.

void accumulateColour(ivec3 t, vec3 colour)
{
    uint next = packUnorm4x8(vec4(colour, 1.0 / 255.0));
    uint previous = 0u;
    uint current;
    for (int attempt = 0; attempt < 32; attempt++)
    {
        current = imageAtomicCompSwap(scratchColour, t, previous, next);
        if (current == previous) break;
        previous = current;
        vec4 old = unpackUnorm4x8(current);
        float count = old.a * 255.0;
        vec3 mean = (old.rgb * count + colour) / (count + 1.0);
        next = packUnorm4x8(vec4(mean, min(count + 1.0, 255.0) / 255.0));
    }
}

void main()
{
    vec2 uvDX = dFdx(i.uv);
    vec2 uvDY = dFdy(i.uv);
    vec4 albedo = lookupAlbedoGrad(i.uv, i.material, uvDX, uvDY);
    vec4 props = lookupMaterialGrad(i.uv, i.material, uvDX, uvDY);

    // A card fills what its alpha does; anything else is cut where the renderer cuts it, and is otherwise whole.
    bool card = int(i.material + 0.5) >= numberOfMaterials;
    // Of however many samples the rasterizer really took: a driver may not give an attachment-less framebuffer the samples it asked for,
    // and with one sample a fragment that exists covers its pixel's centre, so all of it counts.
    float covered = float(bitCount(gl_SampleMaskIn[0])) / float(max(gl_NumSamples, 1));
    float filled = covered * (card ? albedo.a : step(0.5, albedo.a));
    if (filled <= 0.003) return;

    vec3 origin = vec3(giOrigin[level].xyz);
    vec3 world = (i.voxel + origin) * giVoxel[level].x;

    // An emissive colour can be brighter than 1, as Model.frag has it: the hue is kept in the albedo and the rest is glow.
    float emission = props.g;
    float overbright = emission > 0.0 ? max(max(i.colour.r, max(i.colour.g, i.colour.b)), 1.0) : 1.0;
    vec3 colour = albedo.rgb * i.colour / overbright;
    if (!card && isRock(i.material)) colour = geologyToneAt(geologyLayerAt(world)).rgb;
    float shine = emission * EMISSION_GAIN * overbright;

    // The stretch of depth this pixel of the triangle spans, so that a slanting face leaves no gaps.
    float depth = i.voxel[i.axis];
    float spread = min(0.5 * (abs(dFdx(depth)) + abs(dFdy(depth))), 1.0);
    int first = int(floor(depth - spread));
    int last = int(floor(depth + spread));
    ivec3 cell = ivec3(floor(i.voxel));

    uint add = uint(filled * GI_FILL_SCALE + 0.5);
    uint glow = uint(clamp(encodeEmission(shine), 0.0, 1.0) * 255.0 + 0.5);
    for (int layer = first; layer <= last; layer++)
    {
        ivec3 relative = cell;
        relative[i.axis] = layer;
        ivec3 voxel = relative + giOrigin[level].xyz;
        if (any(lessThan(voxel, regionMin)) || any(greaterThanEqual(voxel, regionMax))) continue;
        imageAtomicAdd(voxFilled, giTexel(voxel, level), add);
        ivec3 s = giTexel(voxel, 0);
        accumulateColour(s, colour);
        if (glow > 0u) imageAtomicMax(scratchEmission, s, glow);
    }
}
