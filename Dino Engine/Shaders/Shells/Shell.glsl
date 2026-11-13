#ifndef DINO_SHELL_GLSL
#define DINO_SHELL_GLSL

// What grows on a surface, as one height field that every layer of shells is cut from (SHELLS.md). Needs Shells/ShellStyle.glsl and AntiAliasing/Msaa.glsl.
//
// The field is fixed to the surface: it's read at the anchor, the point of the surface a layer stands over, in a space that moves with the surface (the world
// for what stands still, the bind pose for a body), so every layer agrees on where each clump is, and nothing swims as a body moves or a tree sways. It has two
// sizes. Clumps, moss's cushions or fur's locks, are domes standing on an even carpet; over them the fine strands thin out to nothing, a fringe whose wisps only
// show where they're a pixel or two across. Everything is band-limited by how much of the surface a pixel covers: what's finer than a pixel goes to what it comes
// to on average, so a field far off is a steady soft layer rather than noise that crawls. Its slopes are worked out with it, not taken by the screen, so they
// mean something wherever a pixel has been discarded beside it, and carry each MSAA sample's coverage out from the pixel's middle exactly.
//
// SHELL_PLANAR lays the clumps out on the world's xz plane rather than through space, for the ground, which is seen nearly square-on and covers most of the
// screen: four cells to a lattice instead of eight.

// Sixty-four bits for a cell: a lowbias32 mix of its coordinates (Wellons), and one more round for the second word. Every pixel of every layer takes a few of these.
uvec2 shellCellHash(ivec3 c, uint seed)
{
    uint h = (uint(c.x) * 0x8DA6B343u) ^ (uint(c.y) * 0xD8163841u) ^ (uint(c.z) * 0xCB1AB31Fu) ^ seed;
    h ^= h >> 16u;
    h *= 0x7FEB352Du;
    h ^= h >> 15u;
    h *= 0x846CA68Bu;
    h ^= h >> 16u;
    uint g = h * 0x9E3779B9u + 0x632BE5ABu;
    g ^= g >> 15u;
    g *= 0x2C1B3C6Du;
    g ^= g >> 12u;
    return uvec2(h, g);
}

// What a cell's hash says: where its clump stands in it, ten bits a way, whether it's born at all, how big, how tall and what shade.
vec3 shellJitter(uvec2 h)
{
    return vec3(float(h.x & 1023u), float((h.x >> 10u) & 1023u), float((h.x >> 20u) & 1023u)) * (1.0 / 1023.0);
}

float shellBirth(uvec2 h) { return float(h.y & 255u) * (1.0 / 255.0); }
float shellSize(uvec2 h) { return float((h.y >> 8u) & 255u) * (1.0 / 255.0); }
float shellPeak(uvec2 h) { return float((h.y >> 16u) & 255u) * (1.0 / 255.0); }
float shellTone(uvec2 h) { return float(h.y >> 24u) * (1.0 / 255.0) - 0.5; }

// One clump's say at a point, if it's taller than what's been found there: `along` is the point from the clump's middle, along the surface, in cells.
void shellDome(vec3 along, float r, vec3 comb, uvec2 h, float variety, float shape, float stretch, float taper,
               inout float height, inout vec3 slope, inout float crown, inout float tone)
{
    // Along the comb and across it; with no comb, all of it is across, and the clump is round.
    float a = dot(along, comb);
    vec3 across = along - comb * a;
    float x2 = (a * a + stretch * stretch * dot(across, across)) / (r * r);
    if (x2 >= 1.0) return;
    float peak = 1.0 - variety * shellPeak(h);
    float body = 1.0 - x2;
    float dome = pow(body, shape);
    // From its root end to its tip end, -1 to 1 along the comb.
    float laid = 1.0 - taper * 0.5 * (a / r + 1.0);
    float b = peak * dome * laid;
    if (b <= height) return;
    height = b;
    crown = peak * (1.0 - 0.5 * taper);   // As high as it stands over its middle: its root end is a little higher, and counts as all the way up.
    // How fast a dome falls at its rim is infinite for a cushion's power under one, and only ever seen through the carpet, so it's held to what it is a little in.
    vec3 x2Slope = (comb * a + across * (stretch * stretch)) * (2.0 / (r * r));
    vec3 domeSlope = x2Slope * (-shape * pow(max(body, 0.04), shape - 1.0));
    slope = peak * (domeSlope * laid - dome * (taper * 0.5 / r) * comb);
    tone = shellTone(h);
}

// The tallest clump over a point q, in cells. It's Worley's search cut to the cells whose middles are nearest: a clump is never more than half a cell wide and
// its middle never more than a quarter of one from its cell's, so nothing further can reach. A clump is a dome on the surface, so the clumps of a point are those
// whose middles lie within half a cell of the surface through it, each the smaller the further off it is, which keeps every clump of a curved surface whole.
// Neighbouring clumps overlap, and where one rises out of another is a crease, as between two cushions or a parting between locks of hair. A lock is `stretch`
// times as long along the comb as across it, its length the clump's width, in the frame of the comb where the point is: drawing the whole pattern out along the
// comb instead shears it by how far from the model's middle a point is, and a head three metres out came out in needles. A lock (`taper` over 0) is highest at
// its root, towards the head, and lies down towards its tip, so locks overlap as shingles do. `slope` is the height's gradient in cells, along the surface;
// `crown` is how high the clump over the point stands at its middle.
void shellClumps(vec3 q, vec3 n, vec3 comb, float width, float fill, float variety, float shape, float stretch, float taper, uint seed,
                 inout float height, inout vec3 slope, inout float crown, inout float tone)
{
#ifdef SHELL_PLANAR
    vec2 base = floor(q.xz - 0.5);
    for (int i = 0; i < 4; i++)
    {
        vec2 cell = base + vec2(float(i & 1), float(i >> 1));
        uvec2 h = shellCellHash(ivec3(int(cell.x), 0, int(cell.y)), seed);
        if (shellBirth(h) > fill) continue;
        vec2 d = q.xz - (cell + 0.25 + 0.5 * shellJitter(h).xy);
        float r = width * mix(0.78, 1.0, shellSize(h));
        shellDome(vec3(d.x, 0.0, d.y), r, comb, h, variety, shape, stretch, taper, height, slope, crown, tone);
    }
#else
    vec3 base = floor(q - 0.5);
    for (int i = 0; i < 8; i++)
    {
        vec3 cell = base + vec3(float(i & 1), float((i >> 1) & 1), float((i >> 2) & 1));
        uvec2 h = shellCellHash(ivec3(cell), seed);
        if (shellBirth(h) > fill) continue;
        vec3 d = q - (cell + 0.25 + 0.5 * shellJitter(h));
        float off = dot(d, n);
        float presence = 1.0 - 4.0 * off * off;
        if (presence <= 0.0) continue;
        float r = width * mix(0.78, 1.0, shellSize(h)) * sqrt(presence);
        shellDome(d - n * off, r, comb, h, variety, shape, stretch, taper, height, slope, crown, tone);
    }
#endif
}

// A corner's value for the noise, 0..1.
float shellCorner(ivec3 c, uint seed)
{
    return float(shellCellHash(c, seed).x >> 8u) * (1.0 / 16777215.0);
}

// Smooth value noise, 0..1, and its gradient (Quilez's form of it), for the wisps of the fringe.
float shellNoise(vec3 p, uint seed, out vec3 slope)
{
    vec3 i = floor(p);
    vec3 f = p - i;
    vec3 u = f * f * (3.0 - 2.0 * f);
    vec3 du = 6.0 * f * (1.0 - f);
    ivec3 c = ivec3(i);
    float a = shellCorner(c, seed);
    float b = shellCorner(c + ivec3(1, 0, 0), seed);
    float d = shellCorner(c + ivec3(0, 1, 0), seed);
    float e = shellCorner(c + ivec3(1, 1, 0), seed);
    float g = shellCorner(c + ivec3(0, 0, 1), seed);
    float k = shellCorner(c + ivec3(1, 0, 1), seed);
    float l = shellCorner(c + ivec3(0, 1, 1), seed);
    float m = shellCorner(c + ivec3(1, 1, 1), seed);

    float k1 = b - a, k2 = d - a, k3 = g - a;
    float k4 = a - b - d + e, k5 = a - d - g + l, k6 = a - b - g + k;
    float k7 = -a + b + d - e + g - k - l + m;
    slope = du * vec3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                      k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                      k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y);
    return a + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x + k7 * u.x * u.y * u.z;
}

struct ShellField {
    float core;         // Where the solid of what grows ends over this point, as a share of the length.
    float carpet;       // How high the even layer between clumps stands here.
    float raised;       // How far up the clump over this point it is, 0 down in a crease or on the carpet to 1 at its crown.
    vec3 coreSlope;     // Its gradient, per metre of the anchor's space.
    float reach;        // How far past that the fine strands go, thinning out, as a share of the length.
    vec3 reachSlope;
    float tone;         // The shade of the clump over this point, -0.5..0.5, and 0 where clumps are too small to see.
};

// The field over an anchor whose surface faces `n` and is combed along `comb` (zero for what isn't), where `cover` of the growth stands (0..1), seen by a
// layer at height `h` (a share of the length) through a pixel `footprint` metres across.
ShellField shellField(int style, vec3 anchor, vec3 n, vec3 comb, float cover, float h, float footprint)
{
    vec4 shape = shellRow(style, 0);
    vec4 build = shellRow(style, 1);
    vec4 fine = shellRow(style, 2);
    vec4 more = shellRow(style, 6);
    float cell = shape.y;
    float width = shape.z;
    float stretch = max(build.w, 1.0);
    float lattices = more.y;
    uint seed = uint(style) * 0x9E3779B9u + 0x7F4A7C15u;

    // The core stands at most this high, so that the strands over its tallest clumps reach the top layer and no further.
    float tallest = 1.0 - 0.7 * fine.x;
    // Where it thins, the carpet goes first, and then the clumps, from the smallest, so that a share of it covers about that share of the surface, as the colour
    // painted under it does: an edge of moss is the odd cushion going out into bare stone, and only thick moss is a carpet.
    float carpet = build.x * smoothstep(0.45, 0.9, cover) * tallest;
    float fill = shape.w * smoothstep(0.02, 0.6, cover);
    float grown = mix(0.5, 1.0, smoothstep(0.0, 1.0, cover)) * tallest;

    ShellField f;
    f.core = carpet;
    f.carpet = carpet;
    f.raised = 0.0;
    f.coreSlope = vec3(0.0);
    f.tone = 0.0;

    // Below the carpet nothing changes what can be seen: a clump over a point hides every layer under its top, and where there's none the carpet is all there is.
    if (h > carpet)
    {
        // A clump shows by its narrower side. Its edges are carried to each sample exactly (shellCoverage), so one about a pixel across holds still; under half
        // a pixel its samples would only catch it here and there, and it goes to its average.
        float detail = smoothstep(0.4, 1.1, cell * width / (stretch * max(footprint, 1e-6)));
        // What the clumps come to on average, which is all that's left of them where they're finer than a pixel or two: a cell's share under a clump (shrunk by
        // how far off the surface its middle tends to be, and by its stretch) times how far a dome stands over the carpet on average.
        float peak = (1.0 - 0.5 * build.y) * grown;
        float excess = max(peak - carpet, 0.0);
        float mean = carpet + min(3.14159 * width * width * 0.67 * fill * lattices / stretch, 1.0) * excess * excess / max(2.0 * peak, 1e-4);

        float height = 0.0;
        vec3 slope = vec3(0.0);
        float crown = 0.0;
        float tone = 0.0;
        if (detail > 0.0)
        {
            float taper = stretch > 1.0 ? 0.55 : 0.0;
            vec3 q = anchor / cell;
            shellClumps(q, n, comb, width, fill, build.y, build.z, stretch, taper, seed, height, slope, crown, tone);
            // A second lattice of clumps laid between the first's, so they crowd as the locks of a coat do, and the undercoat only shows here and there.
            if (lattices > 1.5) shellClumps(q + 0.5, n, comb, width, fill, build.y, build.z, stretch, taper, seed ^ 0x2545F491u, height, slope, crown, tone);
            height *= grown;
            crown *= grown;
            slope *= grown / cell;
        }
        bool clump = height > carpet;
        f.core = mix(mean, max(carpet, height), detail);
        f.coreSlope = clump ? slope * detail : vec3(0.0);
        f.tone = clump ? tone * detail : 0.0;
        // Far off, where the clumps are their average, so is how far up one a point is.
        float up = clump ? clamp((height - carpet) / max(crown - carpet, 1e-3), 0.0, 1.0) : 0.0;
        f.raised = mix(0.55, up, detail);
    }

    // The fine strands, in wisps that only show where one is more than a pixel across, and are their average elsewhere. They grow out of what's there, so they
    // reach as far as there's anything under them to grow from: over bare stone between two cushions there's nothing, and a veil there would be a carpet that isn't.
    const float SHELL_ROOTED = 0.12;
    float rooted = smoothstep(0.0, SHELL_ROOTED, f.core);
    float rootedSlope = f.core < SHELL_ROOTED ? 6.0 * f.core * (SHELL_ROOTED - f.core) / (SHELL_ROOTED * SHELL_ROOTED * SHELL_ROOTED) : 0.0;
    float reach = fine.x * smoothstep(0.0, 0.3, cover);
    float wave = 1.0;
    vec3 waveSlope = vec3(0.0);
    float wisps = 0.85 * smoothstep(0.6, 1.5, fine.y / max(footprint, 1e-6));
    if (wisps > 0.0 && h > f.core)
    {
        vec3 slope;
        float v = shellNoise(anchor / fine.y, seed ^ 0x68E31DA4u, slope);
        wave = 1.0 + wisps * (2.0 * v - 1.0);
        waveSlope = slope * (wisps * 2.0 / fine.y);
    }
    f.reach = reach * rooted * wave;
    f.reachSlope = reach * (rooted * waveSlope + wave * rootedSlope * f.coreSlope);
    return f;
}

// How much of a layer at height h is filled over this point: all of it inside the core, thinning to nothing where the strands end. Over 1 is inside.
float shellDensity(ShellField f, float h, out vec3 slope)
{
    float reach = max(f.reach, 1e-3);
    float over = h - f.core;
    slope = (f.coreSlope + f.reachSlope * (over / reach)) / reach;
    return 1.0 - over / reach;
}

// The samples of a pixel a layer covers. Each sample's density is carried from the pixel's middle along its slope on the screen, so an edge falls between
// samples as exactly as a polygon's would; and where the strands are too fine to see, a sample is covered when the density passes a threshold of its own, which
// turns with each layer, so that layers stacked over each other fill different samples. It's the same in every pixel and every frame, so it never crawls, and
// the resolve's average of a pixel's samples is the blend of the layers.
int shellCoverage(float density, vec2 screenSlope, int layer)
{
    int count = msaaSampleCount();
    float spin = float(layer) * 0.6180339887;
    int mask = 0;
    for (int i = 0; i < count; i++)
    {
        float d = density + dot(screenSlope, msaaSamplePosition(i) - 0.5);
        if (d > fract((float(i) + 0.5) / float(count) + spin)) mask |= 1 << i;
    }
    return msaaCoverage(mask);
}

#endif
