#ifndef DINO_GEOLOGY_GLSL
#define DINO_GEOLOGY_GLSL

// The world's bedrock as a field (World/Geology.cs): which layer of rock a point lies in, and what that rock is like. Rock.glsl draws
// stone with it; anything that only needs a colour, global illumination's voxels among them, reads it alone.

// Written by TerrainRenderer.SetGeology; the layout must match it.
layout(std140, binding = 10) uniform GeologyData {
    vec4 geologyDip;        // xy how far the beds climb along x and z, z the height of the first entry, w entries to a metre
    vec4 geologyPhase;      // xyz where the beds wander, w the material that's rock
    vec4 geologyColour;     // rgb the hue of the rock, as a tint with its brightness taken out, w the material that's broken rock
    uvec4 geologyLayers[512];   // Four entries to an element, each red, green, blue and hardness in a byte.
};

bool isRock(float material)
{
    return abs(material - geologyPhase.w) < 0.5;
}

// Whether a material is the stuff a world's rock breaks down to, which takes on its colour.
bool isScree(float material)
{
    return abs(material - geologyColour.w) < 0.5;
}

// Geology.LayerAt: change them together.
float geologyLayerAt(vec3 p)
{
    float wander = 2.6 * sin(p.x * 0.0193 + 1.7 * sin(p.z * 0.0131 + geologyPhase.x))
        + 1.7 * sin(p.z * 0.0347 + 1.3 * sin(p.x * 0.0171 + geologyPhase.y))
        + 0.6 * sin((p.x + p.z) * 0.0871 + geologyPhase.z);
    return p.y + p.x * geologyDip.x + p.z * geologyDip.y + wander;
}

vec4 geologyEntry(int i)
{
    i = clamp(i, 0, 2047);
    return unpackUnorm4x8(geologyLayers[i >> 2][i & 3]);
}

// What the rock in a layer is like, its colour an albedo and its hardness in w.
vec4 geologyToneAt(float layer)
{
    float place = (layer - geologyDip.z) * geologyDip.w;
    int i = int(floor(place));
    return mix(geologyEntry(i), geologyEntry(i + 1), place - float(i));
}

#endif
