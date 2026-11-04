#ifndef DINO_GRASS_BLADE_GLSL
#define DINO_GRASS_BLADE_GLSL

// A grass blade as Grass_Generate.comp leaves it for the vertex shaders, and how they build its vertices. The blade is a
// cubic Bézier curve from its root (P0) through P1 and P2 to its tip (P3), already bent by the wind; the vertex shaders
// only walk along it. Not all are leaves: a plume is a stem carrying a feathery seed head, a bloom a stem carrying a flower
// that turns to face the eye. Needs Grass/GrassField.glsl included first.

// std430, 64 bytes (GrassRenderer.BladeStride). The control points are half floats relative to the root.
struct GrassBlade {
    vec4 root;      // xyz root (world), w width at the root (m)
    uvec4 curve;    // half pairs: (P1.x, P1.y), (P1.z, P2.x), (P2.y, P2.z), (P3.x, P3.y)
    uvec4 look;     // x half (P3.z, LOD morph), y half side direction xz, z unorm colour rgb + tip bleach, w unorm ground normal xz + root AO + gloss
    uvec4 field;    // x half (canopy height, sheen), y half field normal xz, z unorm tip colour rgb + kind, w unorm bloom colour rgb + head size
};

struct GrassBladeData {
    vec3 root;
    float width;
    vec3 p1;
    vec3 p2;
    vec3 p3;
    float morph;        // 0..1, how far a near blade has melted into the far LOD's shape
    vec2 side;          // horizontal direction across the blade
    vec3 colour;
    float tipBleach;    // 0..1, how much the tip fades towards the tip colour
    vec2 groundNormal;  // xz of the ground's normal
    float rootAO;       // ambient occlusion at the root
    float gloss;        // 0..1, strength of the glossy streak along the blade
    float canopy;       // metres, how high the field stands around this blade
    float sheen;        // 0..1, how much a passing gust lights this blade
    vec2 fieldNormal;   // xz of the normal of the field's top, as if it were one soft surface
    vec3 tipColour;
    int kind;           // GRASS_LEAF, GRASS_PLUME or GRASS_BLOOM
    vec3 headColour;    // of a plume's seed head or a bloom's petals
    float headSize;     // metres across a bloom's flower
};

const int GRASS_LEAF = 0;
const int GRASS_PLUME = 1;
const int GRASS_BLOOM = 2;

// Triangle strips: `segments` rows of two vertices, then the tip.
const int GRASS_NEAR_SEGMENTS = 7;  // 15 vertices
const int GRASS_FAR_SEGMENTS = 3;   // 7 vertices

// A plume's stem gives way to its seed head here, along the blade.
const float GRASS_PLUME_START = 0.56;

GrassBlade packGrassBlade(GrassBladeData d)
{
    GrassBlade b;
    b.root = vec4(d.root, d.width);
    b.curve = uvec4(packHalf2x16(d.p1.xy), packHalf2x16(vec2(d.p1.z, d.p2.x)), packHalf2x16(d.p2.yz), packHalf2x16(d.p3.xy));
    b.look = uvec4(packHalf2x16(vec2(d.p3.z, d.morph)), packHalf2x16(d.side),
                   packUnorm4x8(vec4(d.colour, d.tipBleach)),
                   packUnorm4x8(vec4(d.groundNormal * 0.5 + 0.5, d.rootAO, d.gloss)));
    b.field = uvec4(packHalf2x16(vec2(d.canopy, d.sheen)), packHalf2x16(d.fieldNormal),
                    packUnorm4x8(vec4(d.tipColour, float(d.kind) * (1.0 / 255.0))),
                    packUnorm4x8(vec4(d.headColour, d.headSize)));
    return b;
}

GrassBladeData unpackGrassBlade(GrassBlade b)
{
    GrassBladeData d;
    d.root = b.root.xyz;
    d.width = b.root.w;
    vec2 a = unpackHalf2x16(b.curve.x);
    vec2 c = unpackHalf2x16(b.curve.y);
    vec2 e = unpackHalf2x16(b.curve.z);
    vec2 f = unpackHalf2x16(b.curve.w);
    vec2 g = unpackHalf2x16(b.look.x);
    d.p1 = vec3(a, c.x);
    d.p2 = vec3(c.y, e);
    d.p3 = vec3(f, g.x);
    d.morph = g.y;
    d.side = unpackHalf2x16(b.look.y);
    vec4 colour = unpackUnorm4x8(b.look.z);
    d.colour = colour.rgb;
    d.tipBleach = colour.a;
    vec4 ground = unpackUnorm4x8(b.look.w);
    d.groundNormal = ground.xy * 2.0 - 1.0;
    d.rootAO = ground.z;
    d.gloss = ground.w;
    vec2 canopy = unpackHalf2x16(b.field.x);
    d.canopy = canopy.x;
    d.sheen = canopy.y;
    d.fieldNormal = unpackHalf2x16(b.field.y);
    vec4 tip = unpackUnorm4x8(b.field.z);
    d.tipColour = tip.rgb;
    d.kind = int(tip.a * 255.0 + 0.5);
    vec4 head = unpackUnorm4x8(b.field.w);
    d.headColour = head.rgb;
    d.headSize = head.a;
    return d;
}

// Where row s (0..1) of the strip sits along the blade. Rows bunch up towards the tip, where the blade curves most.
float grassRowT(float s)
{
    return s * (1.5 - 0.5 * s);
}

// Width along a blade relative to the root. A leaf swells out of its sheath and narrows to a point; a plume is a thin stem
// opening into a long feathery head; a bloom is only its stem, as the flower is built apart.
float grassWidthProfile(float t, int kind)
{
    if (kind == GRASS_BLOOM) return 0.3;
    if (kind == GRASS_PLUME)
    {
        float head = clamp((t - GRASS_PLUME_START) / (1.0 - GRASS_PLUME_START), 0.0, 1.0);
        return 0.2 + 0.85 * smoothstep(0.0, 0.25, head) * (1.0 - head * head * head);
    }
    return (0.66 + 0.34 * smoothstep(0.0, 0.32, t)) * (1.0 - t * t) * (1.0 - 0.3 * t);
}

vec3 grassBezier(GrassBladeData blade, float t)
{
    float s = 1.0 - t;
    return 3.0 * s * s * t * blade.p1 + 3.0 * s * t * t * blade.p2 + t * t * t * blade.p3;
}

vec3 grassBezierTangent(GrassBladeData blade, float t)
{
    float s = 1.0 - t;
    return 3.0 * s * s * blade.p1 + 6.0 * s * t * (blade.p2 - blade.p1) + 3.0 * t * t * (blade.p3 - blade.p2);
}

// The rows of a bloom's strip that make its flower: the last two of a far strip, the last four of a near one.
int grassHeadRows(int segments)
{
    return segments > GRASS_FAR_SEGMENTS ? 4 : 2;
}

// A vertex of a bloom's flower: a flat disc at the end of the stem, built across the line of sight so that it is a dab of
// colour from wherever it is seen. `row` counts from the disc's bottom edge to its top one, which is the strip's tip.
// t goes on past the stem, 1..2 up the disc, for Grass.frag to tell petals from stem.
void grassHeadVertex(GrassBladeData blade, int row, int rows, float side, vec4 eye,
                     out vec3 position, out vec3 normal, out vec3 across, out float acrossCoord, out float t)
{
    vec3 tip = blade.root + blade.p3;
    vec3 view = eye.w > 0.5 ? eye.xyz - tip : eye.xyz;
    float viewLength = length(view);
    view = viewLength > 1e-4 ? view / viewLength : vec3(0.0, 1.0, 0.0);
    across = cross(vec3(0.0, 1.0, 0.0), view);
    float acrossLength = length(across);
    across = acrossLength > 1e-3 ? across / acrossLength : vec3(blade.side.x, 0.0, blade.side.y);
    vec3 up = cross(view, across);

    float angle = 3.14159265 * float(row) / float(rows);
    float radius = 0.5 * blade.headSize;
    position = tip + up * (-cos(angle) * radius) + across * (side * sin(angle) * radius);
    // Turned a little to the sky, so a flower is lit like the field it stands in.
    normal = normalize(view + vec3(0.0, 1.0, 0.0));
    acrossCoord = side * sin(angle);
    t = 1.5 - 0.5 * cos(angle);
}

// Vertex `vertexID` of a blade drawn as a strip of `segments` rows.
// eye: the point the blade is seen from (w = 1), or the direction towards it (w = 0, for directional light shadows).
// minHalfWidthPerMetre: the least half width the blade may show across the line of sight, per metre from the eye (0 for
// none). Narrower blades are widened to it and coverage says how much of that width is really blade (1 otherwise).
// normal is the blade's front face (the side the strip's winding faces), across runs from its left edge to its right,
// acrossCoord is -1 on the left edge, 1 on the right and 0 at the tip, t runs from 0 at the root to 1 at the tip.
void grassBladeVertex(GrassBladeData blade, int vertexID, int segments, vec4 eye, float minHalfWidthPerMetre,
                      out vec3 position, out vec3 normal, out vec3 across, out float acrossCoord, out float t, out float coverage)
{
    int row = min(vertexID >> 1, segments);
    float edge = float(vertexID & 1) * 2.0 - 1.0;

    int stemRows = segments;
    if (blade.kind == GRASS_BLOOM)
    {
        int headRows = grassHeadRows(segments);
        stemRows = segments - headRows;
        if (row >= stemRows)
        {
            coverage = 1.0;
            grassHeadVertex(blade, row - stemRows, headRows, row == segments ? 0.0 : edge, eye, position, normal, across, acrossCoord, t);
            return;
        }
    }

    acrossCoord = row == segments ? 0.0 : edge;
    t = grassRowT(float(row) / float(stemRows));

    vec3 centre = grassBezier(blade, t);
    vec3 tangent = grassBezierTangent(blade, t);
    float halfWidth = 0.5 * blade.width * grassWidthProfile(t, blade.kind);

    // Near the LOD switch, near blades take on the far LOD's shape (straight between its rows) so the switch doesn't pop.
    if (blade.morph > 0.0 && segments > GRASS_FAR_SEGMENTS && blade.kind == GRASS_LEAF)
    {
        int low = 0;
        while (low < GRASS_FAR_SEGMENTS - 1 && t > grassRowT(float(low + 1) / float(GRASS_FAR_SEGMENTS))) low++;
        float t0 = grassRowT(float(low) / float(GRASS_FAR_SEGMENTS));
        float t1 = grassRowT(float(low + 1) / float(GRASS_FAR_SEGMENTS));
        float a = clamp((t - t0) / (t1 - t0), 0.0, 1.0);
        centre = mix(centre, mix(grassBezier(blade, t0), grassBezier(blade, t1), a), blade.morph);
        tangent = mix(tangent, mix(grassBezierTangent(blade, t0), grassBezierTangent(blade, t1), a), blade.morph);
        halfWidth = mix(halfWidth, 0.5 * blade.width * mix(grassWidthProfile(t0, blade.kind), grassWidthProfile(t1, blade.kind), a), blade.morph);
    }

    float tangentLength = length(tangent);
    vec3 T = tangentLength > 1e-6 ? tangent / tangentLength : vec3(0.0, 1.0, 0.0);
    // The cross section follows the curve: `across` stays as close to the blade's side direction as the tangent allows.
    vec3 side = vec3(blade.side.x, 0.0, blade.side.y);
    across = side - T * dot(side, T);
    float acrossLength = length(across);
    across = acrossLength > 1e-4 ? across / acrossLength : normalize(cross(T, vec3(0.0, 0.0, 1.0)) + vec3(1e-4, 0.0, 0.0));
    normal = cross(across, T);
    position = blade.root + centre + across * (acrossCoord * halfWidth);

    // Blades seen edge-on are widened across the line of sight (as in Ghost of Tsushima), so the field stays full and
    // thin blades don't flicker in and out. Widening is strongest exactly edge-on and gone face-on.
    coverage = 1.0;
    vec3 toEye = eye.w > 0.5 ? eye.xyz - position : eye.xyz;
    float eyeDistance = length(toEye);
    if ((grassLod.w > 0.0 || minHalfWidthPerMetre > 0.0) && eyeDistance > 1e-4)
    {
        vec3 view = toEye / eyeDistance;
        float facing = dot(normal, view);
        vec3 screenAcross = cross(view, T);
        float screenAcrossLength = length(screenAcross);
        if (screenAcrossLength > 1e-4)
        {
            // Oriented like the blade's own left-to-right as seen from the eye, which flips as the eye passes its edge.
            screenAcross *= (facing >= 0.0 ? -1.0 : 1.0) / screenAcrossLength;
            float thickening = grassLod.w * (1.0 - abs(facing));
            position += screenAcross * (acrossCoord * halfWidth * thickening);

            // A blade thinner than about a pixel would cover the MSAA samples it happens to straddle, popping in and out
            // as it sways. It is widened to the least width instead and covers that share of the samples (Grass.frag),
            // so it adds the same amount of colour wherever it moves (phone-wire anti-aliasing, after Persson). The
            // width that counts is across the line of sight: the blade's own, as much as it faces the eye, plus the
            // thickening. The tip vertex takes the coverage of the row below, whose width the tip triangle narrows from.
            if (minHalfWidthPerMetre > 0.0)
            {
                float rowHalfWidth = row == segments
                    ? 0.5 * blade.width * grassWidthProfile(grassRowT(float(segments - 1) / float(segments)), blade.kind)
                    : halfWidth;
                float seenHalfWidth = rowHalfWidth * (abs(facing) / screenAcrossLength + thickening);
                float leastHalfWidth = minHalfWidthPerMetre * eyeDistance;
                if (seenHalfWidth < leastHalfWidth)
                {
                    coverage = seenHalfWidth / leastHalfWidth;
                    position += screenAcross * (acrossCoord * (leastHalfWidth - seenHalfWidth));
                }
            }
        }
    }
}

#endif
