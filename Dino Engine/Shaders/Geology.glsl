#ifndef DINO_GEOLOGY_GLSL
#define DINO_GEOLOGY_GLSL

// The world's bedrock as a field (World/Geology.cs): which layer of rock a point lies in, which bed that is, and what that rock is like. Rock.glsl draws
// stone with it; anything that only needs a colour, global illumination's voxels among them, reads it alone.

// Written by TerrainRenderer.SetGeology; the layout must match it.
layout(std140, binding = 10) uniform GeologyData {
    vec4 geologyDip;        // xy how far the beds climb along x and z, z the height of the first entry, w entries to a metre
    vec4 geologyPhase;      // xyz where the beds wander, w the material that's rock
    vec4 geologyColour;     // rgb the hue of the rock, as a tint with its brightness taken out, w the material that's broken rock
    vec4 geologyFrame;      // xy the way the strike runs, a unit vector on xz; z metres between joints across it for each metre a bed is thick, w along it
    vec4 geologyJoints;     // x the closest joints come and y the farthest, in metres; z metres a joint stands open, w metres a block's edge is rounded over
    vec4 geologyFace;       // x how far a block's face is tipped, y how deeply a soft bed is shadowed, z the kind of rock (World/Geology.cs's RockKind), w how much of it holds snow
    vec4 geologyMoss;       // rgb what grows on bare rock, an albedo, w how much does
    vec4 geologyGrain;      // x cross-bedding, y foliation, z varnish, w karst (JointStyle)
    uvec4 geologyLayers[512];   // Four entries to an element, each red, green, blue and hardness in a byte.
    uvec4 geologyBeds[256];     // Eight entries to an element, each how many entries its bed has run below it and has still to run above it, a byte each.
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
    i &= 2047;
    return unpackUnorm4x8(geologyLayers[i >> 2][i & 3]);
}

// Where in the table a layer is. The stack comes round again past either end, as beds that dip steeply reach far past it across a land (Geology.PlaceOf).
float geologyPlace(float layer)
{
    return mod((layer - geologyDip.z) * geologyDip.w, 2048.0);
}

// What the rock in a layer is like, its colour an albedo and its hardness in w.
vec4 geologyToneAt(float layer)
{
    float place = geologyPlace(layer);
    int i = int(floor(place));
    return mix(geologyEntry(i), geologyEntry(i + 1), place - float(i));
}

// Geology.BedAt: which bed a layer is in, as metres up from its foot and down to its head along the layers, and the entry its foot is in, the same for the whole bed.
void geologyBedAt(float layer, out float since, out float until, out int bed)
{
    float place = geologyPlace(layer);
    int i = clamp(int(floor(place)), 0, 2047);
    float within = clamp(place - float(i), 0.0, 1.0);
    uint bits = geologyBeds[i >> 3][(i >> 1) & 3] >> uint((i & 1) * 16);
    float below = float(bits & 0xFFu);
    float above = float((bits >> 8u) & 0xFFu);
    since = (below + within) / geologyDip.w;
    until = (above + 1.0 - within) / geologyDip.w;
    bed = i - int(below);
}

#endif
