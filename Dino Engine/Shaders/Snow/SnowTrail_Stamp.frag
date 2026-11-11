#version 330

// The shape of one press into the snow (SnowTrailRenderer, SnowPrint), written with a MAX blend, so that snow pressed down stays down whatever comes after.
// r how deep its floor goes, g how much of the snow's surface its floor replaces, b the snow heaped round it, a clumps of what it kicked up.
// Everything is in metres in the print's own frame: x across it, to the left as Frames.Left, and y along the way it points.

in vec2 world;
flat in vec4 place;
flat in vec4 size;
flat in vec4 more;
flat in float reach;

out vec4 trail;

const int BOOT = 0;
const int THREE_TOED = 1;
const int PAD = 2;
const int PAW = 3;
const int TRENCH = 4;
const int GROOVE = 5;

const float CLUMP_CELL = 0.075;     // Metres a cell of thrown clumps is, each with at most one clump in it.

float hash21(vec2 p)
{
    p = fract(p * vec2(0.1031, 0.1030));
    p += dot(p, p + vec2(19.19, 19.19));
    p = fract(vec2(p.x * p.y, p.y * p.x) * 19.19);
    return fract(p.x + p.y);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x), mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
}

float sq(float x) { return x * x; }

// Inigo Quilez's distance to an ellipse, near enough: exact on its axes and smooth everywhere, which is all a soft edge needs.
float ellipse(vec2 p, vec2 centre, vec2 radii)
{
    vec2 q = p - centre;
    float k0 = length(q / radii);
    float k1 = length(q / (radii * radii));
    return k1 > 1e-5 ? k0 * (k0 - 1.0) / k1 : -min(radii.x, radii.y);
}

// A toe: from a to b, as thick as ra at a and rb at b.
float toe(vec2 p, vec2 a, vec2 b, float ra, float rb)
{
    vec2 ab = b - a;
    float h = clamp(dot(p - a, ab) / dot(ab, ab), 0.0, 1.0);
    return length(p - a - ab * h) - mix(ra, rb, h);
}

float smoothUnion(float a, float b, float k)
{
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
}

void main(void)
{
    int kind = int(place.w + 0.5);
    float hw = size.x;
    float hl = size.y;
    vec2 forward = vec2(sin(place.z), cos(place.z));
    vec2 left = vec2(cos(place.z), -sin(place.z));
    vec2 rel = world - place.xy;
    vec2 p = vec2(dot(rel, left), dot(rel, forward));
    vec2 push = vec2(dot(more.xy, left), dot(more.xy, forward));
    float run = min(length(more.xy), 1.0);

    // d: metres outside the shape, negative inside. profile: how deep each part of its floor goes, of its depth.
    float d;
    float profile = 1.0;
    float wall = max(0.035, 0.12 * min(hw, hl));
    if (kind == BOOT)
    {
        float heel = ellipse(p, vec2(0.0, -0.62 * hl), vec2(0.7 * hw, 0.36 * hl));
        float ball = ellipse(p, vec2(0.06 * hw, 0.27 * hl), vec2(hw, 0.66 * hl));
        d = smoothUnion(heel, ball, 0.35 * hw);
        // The heel comes down hardest and the arch hardly at all, and a running foot digs its toes in as it pushes off.
        float heelPress = exp(-sq((p.y + 0.62 * hl) / (0.32 * hl)));
        float ballPress = exp(-sq((p.y - 0.3 * hl) / (0.38 * hl)));
        float toeDig = smoothstep(0.45 * hl, 0.85 * hl, p.y) * run;
        profile = 0.7 + 0.3 * max(heelPress * (1.0 - 0.4 * run), ballPress) + 0.25 * toeDig;
    }
    else if (kind == THREE_TOED)
    {
        vec2 base = vec2(0.0, -0.38 * hl);
        vec2 tipM = vec2(0.0, 0.96 * hl);
        vec2 tipL = vec2(0.84 * hw, 0.5 * hl);
        vec2 tipR = vec2(-0.84 * hw, 0.5 * hl);
        float pad = ellipse(p, vec2(0.0, -0.5 * hl), vec2(0.38 * hw, 0.3 * hl));
        float k = 0.12 * hw;
        d = smoothUnion(pad, toe(p, base, tipM, 0.17 * hw, 0.07 * hw), k);
        d = smoothUnion(d, toe(p, base, tipL, 0.15 * hw, 0.06 * hw), k);
        d = smoothUnion(d, toe(p, base, tipR, 0.15 * hw, 0.06 * hw), k);
        // The dew claw, a little behind and to one side, alone.
        d = min(d, length(p - vec2(0.3 * hw, -0.95 * hl)) - 0.075 * hw);
        // Each claw's point goes in deepest.
        float r = 0.12 * hw;
        float claws = max(exp(-dot(p - tipM, p - tipM) / (r * r)), max(exp(-dot(p - tipL, p - tipL) / (r * r)), exp(-dot(p - tipR, p - tipR) / (r * r))));
        profile = 0.8 + 0.12 * exp(-sq((p.y + 0.5 * hl) / (0.3 * hl))) + 0.4 * claws;
        wall = max(0.03, 0.07 * min(hw, hl));
    }
    else if (kind == PAD)
    {
        d = ellipse(p, vec2(0.0, -0.1 * hl), vec2(0.94 * hw, 0.84 * hl));
        float nubs = 0.0;
        for (int i = 0; i < 4; i++)
        {
            float angle = mix(-0.95, 0.95, float(i) / 3.0);
            vec2 c = vec2(sin(angle) * 0.8 * hw, cos(angle) * 0.82 * hl - 0.05 * hl);
            float nub = length(p - c) - 0.19 * hw;
            d = smoothUnion(d, nub, 0.07 * hw);
            nubs = max(nubs, 1.0 - smoothstep(-0.19 * hw, 0.0, nub));
        }
        profile = 0.9 + 0.22 * nubs;
    }
    else if (kind == PAW)
    {
        d = ellipse(p, vec2(0.0, -0.35 * hl), vec2(0.52 * hw, 0.4 * hl));
        d = min(d, ellipse(p, vec2(-0.64 * hw, 0.22 * hl), vec2(0.21 * hw, 0.25 * hl)));
        d = min(d, ellipse(p, vec2(-0.23 * hw, 0.62 * hl), vec2(0.21 * hw, 0.26 * hl)));
        d = min(d, ellipse(p, vec2(0.23 * hw, 0.62 * hl), vec2(0.21 * hw, 0.26 * hl)));
        d = min(d, ellipse(p, vec2(0.64 * hw, 0.22 * hl), vec2(0.21 * hw, 0.25 * hl)));
        wall = max(0.03, 0.08 * min(hw, hl));
    }
    else
    {
        // A furrow, rounded at its ends, and U-shaped across: deepest down its middle.
        d = length(vec2(p.x, max(abs(p.y) - max(hl - hw, 0.0), 0.0))) - hw;
        float across = clamp((d + hw) / hw, 0.0, 1.0);
        profile = 1.0 - 0.75 * across * across;
        wall = (kind == TRENCH ? 0.3 : 0.2) * hw;
    }

    float inside = 1.0 - smoothstep(-wall, 0.4 * wall, d);
    float floorDepth = size.z * profile * (1.0 - smoothstep(0.0, 1.2 * wall, d));
    float pressed = inside * size.w;

    // Snow pushed out of the way stands round the edge, broken into lumps, and heaped highest where the foot was going.
    float rimWidth = max(0.06, (kind == TRENCH ? 0.45 : 0.32) * min(hw, hl) * (kind == GROOVE ? 0.7 : 1.0));
    float rimShape = exp(-sq((d - 0.35 * rimWidth) / rimWidth)) * smoothstep(-0.2 * rimWidth, 0.25 * rimWidth, d);
    vec2 outward = length(p) > 1e-4 ? normalize(p) : vec2(0.0, 1.0);
    float facing = length(push) > 1e-3 ? dot(outward, normalize(push)) : 0.0;
    float heaped = 0.65 + 0.8 * run * max(facing, 0.0) - 0.35 * run * max(-facing, 0.0);
    float crumble = 0.45 + 1.1 * valueNoise(world / 0.07);
    float rim = more.z * rimShape * heaped * crumble;

    // Clumps of what it kicked up: thrown ahead of it the faster it went, one or none in each small cell, round, and fewer further out. A furrow is churned all through.
    // A furrow is laid again every frame, so it throws nothing beside it: only its floor is churned.
    float churn = more.w;
    float thrown = (0.35 + 1.5 * run) * max(hw, hl);
    float fan = mix(0.55, 0.2 + 0.8 * smoothstep(-0.3, 0.85, facing), run);
    float density = churn * fan * exp(-2.0 * max(d, 0.0) / thrown) * smoothstep(0.0, 0.04, d) * (1.0 - smoothstep(0.5 * thrown, thrown, d));
    if (kind == TRENCH || kind == GROOVE) density = churn * inside * 0.85;
    vec2 cell = floor(world / CLUMP_CELL);
    float h = hash21(cell + 17.0);
    vec2 centre = (cell + 0.25 + 0.5 * vec2(hash21(cell + 3.1), hash21(cell + 9.7))) * CLUMP_CELL;
    float radius = CLUMP_CELL * mix(0.22, 0.42, hash21(cell + 5.3));
    float clump = (1.0 - smoothstep(0.55 * radius, radius, length(world - centre))) * step(1.0 - density, h);
    float clumps = max(clump * mix(0.6, 1.0, h), density * 0.15);

    // Everything is gone well inside the square it's drawn on, which lies square to the world: anything left at its edge is a straight line in the snow.
    float window = 1.0 - smoothstep(0.7, 0.95, length(rel) / reach);
    trail = clamp(vec4(floorDepth, pressed, rim, clumps) * window, 0.0, 1.0);
}
