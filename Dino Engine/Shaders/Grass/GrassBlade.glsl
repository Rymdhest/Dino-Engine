#ifndef DINO_GRASS_BLADE_GLSL
#define DINO_GRASS_BLADE_GLSL

// A grass blade as Grass_Generate.comp leaves it for the vertex shaders, and how they build its vertices. The blade is a
// cubic Bézier curve from its root (P0) through P1 and P2 to its tip (P3), already bent by the wind; the vertex shaders
// only walk along it. Needs Grass/GrassField.glsl included first.

// std430, 48 bytes. The control points are half floats relative to the root.
struct GrassBlade {
    vec4 root;      // xyz root (world), w width at the root (m)
    uvec4 curve;    // half pairs: (P1.x, P1.y), (P1.z, P2.x), (P2.y, P2.z), (P3.x, P3.y)
    uvec4 look;     // x half (P3.z, LOD morph), y half side direction xz, z unorm colour rgb + tip bleach, w unorm ground normal xz + root AO + gloss
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
};

// Triangle strips: `segments` rows of two vertices, then the tip.
const int GRASS_NEAR_SEGMENTS = 7;  // 15 vertices
const int GRASS_FAR_SEGMENTS = 3;   // 7 vertices

GrassBlade packGrassBlade(GrassBladeData d)
{
    GrassBlade b;
    b.root = vec4(d.root, d.width);
    b.curve = uvec4(packHalf2x16(d.p1.xy), packHalf2x16(vec2(d.p1.z, d.p2.x)), packHalf2x16(d.p2.yz), packHalf2x16(d.p3.xy));
    b.look = uvec4(packHalf2x16(vec2(d.p3.z, d.morph)), packHalf2x16(d.side),
                   packUnorm4x8(vec4(d.colour, d.tipBleach)),
                   packUnorm4x8(vec4(d.groundNormal * 0.5 + 0.5, d.rootAO, d.gloss)));
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
    return d;
}

// Where row s (0..1) of the strip sits along the blade. Rows bunch up towards the tip, where the blade curves most.
float grassRowT(float s)
{
    return s * (1.5 - 0.5 * s);
}

// Width along the blade relative to the root, narrowing to a point.
float grassWidthProfile(float t)
{
    return (1.0 - t * t) * (1.0 - 0.3 * t);
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
    acrossCoord = row == segments ? 0.0 : float(vertexID & 1) * 2.0 - 1.0;
    t = grassRowT(float(row) / float(segments));

    vec3 centre = grassBezier(blade, t);
    vec3 tangent = grassBezierTangent(blade, t);
    float halfWidth = 0.5 * blade.width * grassWidthProfile(t);

    // Near the LOD switch, near blades take on the far LOD's shape (straight between its rows) so the switch doesn't pop.
    if (blade.morph > 0.0 && segments > GRASS_FAR_SEGMENTS)
    {
        int low = 0;
        while (low < GRASS_FAR_SEGMENTS - 1 && t > grassRowT(float(low + 1) / float(GRASS_FAR_SEGMENTS))) low++;
        float t0 = grassRowT(float(low) / float(GRASS_FAR_SEGMENTS));
        float t1 = grassRowT(float(low + 1) / float(GRASS_FAR_SEGMENTS));
        float a = clamp((t - t0) / (t1 - t0), 0.0, 1.0);
        centre = mix(centre, mix(grassBezier(blade, t0), grassBezier(blade, t1), a), blade.morph);
        tangent = mix(tangent, mix(grassBezierTangent(blade, t0), grassBezierTangent(blade, t1), a), blade.morph);
        halfWidth = mix(halfWidth, 0.5 * blade.width * mix(grassWidthProfile(t0), grassWidthProfile(t1), a), blade.morph);
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
                    ? 0.5 * blade.width * grassWidthProfile(grassRowT(float(segments - 1) / float(segments)))
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
