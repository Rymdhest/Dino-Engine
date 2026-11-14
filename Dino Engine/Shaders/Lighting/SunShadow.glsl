#ifndef DINO_LIGHTING_SUN_SHADOW_GLSL
#define DINO_LIGHTING_SUN_SHADOW_GLSL

// How much of a directional light reaches a point, through its cascades of shadow maps (SHADOWS.md). DirectionalLightRenderer binds them as plain
// depth, as each texel's own depth is needed and not only whether it's in the way, and with each a map of the nearest and farthest depth in every
// block of 16 x 16 texels (Shadows/SunShadow_Ranges.comp).
//
// The sun isn't a point. It's a disc, and how much of it a point sees is what's in the way of each part of the disc: right under what casts a shadow
// all of it is hidden and the shadow is hard, and further off the disc stands wider over what's in the way, so the edge softens (the penumbra), and
// where something small stands high it never hides all of the disc at all, and leaves only a faint soft patch (the antumbra). Seen from a point, a
// texel of the map h metres above it along the light hides the part of the disc it stands in front of, and the disc there is h times the tangent of
// its angular radius across: so each texel in the way is spread over a disc of its own that wide, and what all of them hide is the sum of those
// (Soler and Sillion's convolution, a texel at a time, rather than PCSS's one average height for everything round the point, which blurs a stone's
// contact shadow as widely as the tree's over it). Texels standing at the same height add up to exactly what one flat caster would hide; where
// several heights hide the same part of the disc it's counted more than once, which the clamp at all of it takes care of. A map has no sides,
// though: next to a step from something low to something tall, both in the way, the tall one's discs from beyond the step are missing and the low
// one's are too narrow to make up for them, and a solid shadow would let through as much as a quarter of the light along it. So each texel that
// isn't in the way has a disc too, as wide as the widest that reaches the point, and what's hidden is never less than the share of all the discs
// that are in the way: all of it wherever everything is, whatever their heights, and as the sum on a single caster's edge.
//
// Each texel's disc is a smooth kernel, (1 - (r/R)^2)^2, never narrower than the sharpest filter that doesn't step, and as wide as the sun's disc
// there with the same spread (so R^2 = sharpest^2 + 2 (tan(sun's radius) h / texel)^2), up to the softest, which bounds what a pixel costs. The
// kernel is close enough to a lattice's sum that a whole field of texels hides the whole disc to within a few tenths of a percent from its sharpest
// up, and a sliver at either end is taken as all or nothing to leave none.
//
// Nothing here is random: there's no temporal accumulation to average noise away, and a filter that turned its taps by a random angle each pixel once
// left that noise in the picture for good. Every texel that can reach the point is read, so the shadow is a function of where the point is alone: the
// same on every pixel that looks at it, smooth as it moves, and as still in the world as the maps, which are snapped to their texels. What can be
// known from the block map is known exactly, and costs a few reads: nothing within reach of the point is in the way (lit), or everything is (inside
// a shadow). A field of texels all in the way hides the whole disc wherever they stand at much the same height, and where they don't, nothing's left
// to see through: a gap in a crown that the sun comes through is a texel of whatever it falls on, which isn't in the way, and that point reads every
// texel. Within reach of such a texel the sum comes down from all of the disc smoothly, so the two meet with nothing to see between them. Only
// somewhere in between reads every texel within reach.

uniform int numberOfCascades;

uniform mat4 sunSpaceMatrices[5];
uniform sampler2D shadowMaps[5];
// Each cascade's nearest (x) and farthest (y) depth in each block of SHADOW_RANGE_BLOCK x SHADOW_RANGE_BLOCK texels.
uniform sampler2D shadowRanges[5];
// x the tangent of the sun's angular radius, y the sharpest filter's radius in texels, z the softest's, w 1 to soften with distance and 0 not to
// (SunShadowSettings).
uniform vec4 sunShadowShape;

// Texels to a block of the range maps: DirectionalLightRenderer.RangeBlock.
const int SHADOW_RANGE_BLOCK = 16;

// In the shadow map's depth, which runs 0 to 1 over a few hundred metres.
const float SHADOW_BIAS = 0.0001;

// Texels from a point within which each texel in the way is weighed on its own. Further out only a wide disc reaches the point, which hardly changes
// across a texel, so the four of a gather are weighed together, at the middle of those of them in the way and by the mean of their discs: right to
// the second order, and no gather is weighed both ways. Whether a gather is near or far is the same for every pixel of a group as they go over the
// texels round them in step, so it costs one way or the other and not both. It's the lift's own filter too, which reaches no further.
const float SHADOW_INNER = 4.0;

// The sliver of the disc at either end that's taken as all or nothing: a field of texels sums to the whole disc only to within a few tenths of a
// percent, and where they all stand in the way a shadow must be wholly dark, and wholly lit where none do.
const float SHADOW_SNAP = 0.01;

// Air fills a long shadow with light, and it matters here: a mountain can lay its shadow over everything for hundreds of metres in a low sun, and nobody can fight what they can't see.
// So a shadow gets lighter the further off whatever casts it. Close up it's as dark as ever, which is what gives a rock its shape, and what's standing on the ground keeps its own.
// Every texel says how far its own caster is, and what lifts a shadow is worked out from the texels in a small fixed filter round the point, the same
// whether the rest is read or known from the block map, so it follows the shadow exactly and leaves no seam where one gives way to the other.
const float SHADOW_FILL_NEAR = 16.0;    // Metres from a point to what's shading it, where the shadow starts to lift,
const float SHADOW_FILL_FAR = 96.0;     // and where it's lifted as far as it goes.
const float SHADOW_FILL_MOST = 0.5;
const float SHADOW_FILL_RADIUS = 4.0;   // Texels, of the filter what lifts a shadow is taken over: no further than SHADOW_INNER.

// How much each of four texels in the way lifts a shadow round it, by how far off its caster is (the fill a long shadow gets from the air), weighted
// by a small fixed filter round the point. rho2 is each texel's squared distance from the point, height how far it is above it.
vec4 sunShadowLift(vec4 rho2, vec4 height)
{
    const float inverseRadiusSquared = 1.0 / (SHADOW_FILL_RADIUS * SHADOW_FILL_RADIUS);
    vec4 weight = max(1.0 - rho2 * inverseRadiusSquared, 0.0);
    return weight * weight * (SHADOW_FILL_MOST * clamp((height - SHADOW_FILL_NEAR) / (SHADOW_FILL_FAR - SHADOW_FILL_NEAR), 0.0, 1.0));
}

// The filter's (1 - (r/R)^2)^2 over a disc of radius SHADOW_FILL_RADIUS: what the lift's weights come to over a whole field.
const float SHADOW_FILL_TOTAL = 3.14159265359 * SHADOW_FILL_RADIUS * SHADOW_FILL_RADIUS / 3.0;

// How much of what's in the way round a point lifts its shadow: the share of a small filter round it that's in the way, each texel by how far off
// its caster is. base and f locate the point in texels; reference is its depth less the bias. The splat loop works out the same as it goes.
float sunShadowFill(sampler2D map, vec2 size, vec2 base, vec2 f, float reference, float metresPerDepth)
{
    float lifted = 0.0;
    for (int y = -3; y <= 3; y += 2)
    {
        for (int x = -3; x <= 3; x += 2)
        {
            // The four texels from base + (x, y) to base + (x + 1, y + 1), in the order a gather gives them.
            vec4 depth = textureGather(map, (base + vec2(x, y) + 1.0) / size);
            vec4 dx = vec4(x, x + 1, x + 1, x) - f.x;
            vec4 dy = vec4(y + 1, y + 1, y, y) - f.y;
            vec4 height = (reference - depth) * metresPerDepth;
            vec4 lift = sunShadowLift(dx * dx + dy * dy, height);
            lifted += lift.x + lift.y + lift.z + lift.w;
        }
    }
    return lifted / SHADOW_FILL_TOTAL;
}

// What one cascade's shadow at a point needs to know, from its block map: where the point is in texels (base the texel at or below it, f how far on
// from there), its depth less the bias, and how far out anything reaches it. Shadows/SunShadow_Mask.comp reads the texels in reach from shared memory
// where it can and sunShadowCascade from the map, by the same sunShadowSplat.
struct SunShadowSetup
{
    vec2 size;              // Texels of the map.
    vec2 base;
    vec2 f;
    float reference;
    float metresPerDepth;
    float sharpest;         // Texels: the radius of the sharpest disc,
    float sharpest2;        // squared,
    float softest;          // and of the softest,
    float softest2;
    float spread;           // A texel h metres up spreads over R^2 = sharpest^2 + spread * h^2 texels squared, up to softest^2.
    float open2;            // Texels squared: the disc each texel that isn't in the way is given, as wide as the widest that reaches the point,
    float perOpen2;         // and one over it.
    float foot;             // Texels out to which anything reaches the point: 0 for none, when it's in the sun.
    bool lifts;             // Whether anything near enough to lift its shadow is far enough off to.
    bool deep;              // Whether everything within the softest reach is in the way: inside a shadow.
    int reach;              // Texels to either side of base that the texels in reach lie within: the square sunShadowSplat goes over.
};

// Where a point is in one cascade, and what's in reach of it (SunShadowSetup). point is in the cascade's space (0 to 1 on each axis); perMetre is how far
// its depth moves for a metre along the light and texelMetres how wide a texel is.
SunShadowSetup sunShadowSetup(sampler2D map, sampler2D ranges, vec3 point, float perMetre, float texelMetres)
{
    SunShadowSetup s;
    s.size = vec2(textureSize(map, 0));
    vec2 at = point.xy * s.size - 0.5;      // In texels, with texel i's centre at i.
    s.base = floor(at);
    s.f = at - s.base;
    s.reference = point.z - SHADOW_BIAS;
    s.metresPerDepth = 1.0 / perMetre;

    s.sharpest = max(sunShadowShape.y, 1.0);
    s.softest = max(sunShadowShape.z, s.sharpest);
    float sunTexels = sunShadowShape.w > 0.5 ? sunShadowShape.x / texelMetres : 0.0;
    s.spread = 2.0 * sunTexels * sunTexels;
    s.sharpest2 = s.sharpest * s.sharpest;
    s.softest2 = s.softest * s.softest;
    // The blocks within the softest reach of the point say what can reach it. A block's highest texel reaches furthest; if not even that reaches
    // the point, nothing in it does.
    ivec2 rangeSize = textureSize(ranges, 0);
    ivec2 firstBlock = ivec2(floor((at - s.softest) / float(SHADOW_RANGE_BLOCK)));
    ivec2 lastBlock = ivec2(floor((at + s.softest) / float(SHADOW_RANGE_BLOCK)));
    s.foot = 0.0;
    s.lifts = false;
    s.deep = true;
    for (int by = firstBlock.y; by <= lastBlock.y; by++)
    {
        for (int bx = firstBlock.x; bx <= lastBlock.x; bx++)
        {
            ivec2 block = ivec2(bx, by);
            vec2 first = vec2(block * SHADOW_RANGE_BLOCK);
            float apart = length(at - clamp(at, first, first + float(SHADOW_RANGE_BLOCK - 1)));
            if (any(lessThan(block, ivec2(0))) || any(greaterThanEqual(block, rangeSize)))
            {
                // Past the map's edge is its far end, where nothing is in the way.
                if (apart < s.softest) s.deep = false;
                continue;
            }
            vec2 range = texelFetch(ranges, block, 0).rg;
            float highest = (s.reference - range.x) * s.metresPerDepth;
            float lowest = (s.reference - range.y) * s.metresPerDepth;
            if (apart < s.softest && lowest <= 0.0) s.deep = false;
            if (apart < SHADOW_FILL_RADIUS && highest > SHADOW_FILL_NEAR) s.lifts = true;
            if (highest <= 0.0) continue;
            float reach = sqrt(min(s.sharpest2 + s.spread * highest * highest, s.softest2));
            if (apart >= reach) continue;
            s.foot = max(s.foot, reach);
        }
    }
    // The texels in reach, and the lift's own filter when it lifts, lie within this many of base either way (and one more on the far side).
    s.reach = max(int(ceil(s.foot)), s.lifts ? int(SHADOW_FILL_RADIUS) : 0);
    s.open2 = max(s.foot * s.foot, s.sharpest2);
    s.perOpen2 = 1.0 / s.open2;
    return s;
}

// What four texels hide of the sun's disc from the point, spread each over its own disc, and lift its shadow, added to hidden, lifted and spreadSum
// (what's hidden, each by the square of its disc's radius, for how soft it is), and what those that aren't in the way leave open, to open (each over
// the disc open2 gives it). depth is the four from base + (x, y) to base + (x + 1, y + 1), in the
// order a gather gives them. Within SHADOW_INNER of the point each texel is weighed on its own, and lifts the shadow as sunShadowFill has it; past it
// the four together, and nothing lifts.
void sunShadowSplat(SunShadowSetup s, vec4 depth, int x, int y, inout float hidden, inout float lifted, inout float spreadSum, inout float open)
{
    vec4 height = (s.reference - depth) * s.metresPerDepth;
    vec4 dx = vec4(x, x + 1, x + 1, x) - s.f.x;
    vec4 dy = vec4(y + 1, y + 1, y, y) - s.f.y;
    vec4 rho2 = dx * dx + dy * dy;
    vec4 inWay = vec4(greaterThan(height, vec4(0.0)));
    // What isn't in the way, over the one disc it's given.
    vec4 openKernel = max(1.0 - rho2 * s.perOpen2, 0.0);
    open += dot(openKernel * openKernel, 1.0 - inWay) * s.perOpen2;
    if (!any(greaterThan(height, vec4(0.0)))) return;
    vec4 radius2 = min(s.sharpest2 + s.spread * height * height, s.softest2);
    if (min(min(rho2.x, rho2.y), min(rho2.z, rho2.w)) >= SHADOW_INNER * SHADOW_INNER)
    {
        float count = dot(inWay, vec4(1.0));
        vec2 middle = vec2(dot(inWay, dx), dot(inWay, dy)) / count;
        float meanRadius2 = dot(inWay, radius2) / count;
        float perRadius2 = 1.0 / meanRadius2;
        float kernel = max(1.0 - dot(middle, middle) * perRadius2, 0.0);
        float share = count * kernel * kernel * perRadius2;
        hidden += share;
        spreadSum += share * meanRadius2;
    }
    else
    {
        vec4 perRadius2 = 1.0 / radius2;
        vec4 kernel = max(1.0 - rho2 * perRadius2, 0.0);
        vec4 share = kernel * kernel * perRadius2 * inWay;
        hidden += share.x + share.y + share.z + share.w;
        spreadSum += dot(share, radius2);
        if (s.lifts)
        {
            vec4 lift = sunShadowLift(rho2, height);
            lifted += lift.x + lift.y + lift.z + lift.w;
        }
    }
}

// What gets to the point once every texel in reach has been through sunShadowSplat: x what reaches it directly and y what fills its shadow. What's
// hidden is the larger of the sum of the discs in the way, which keeps a stone's shadow hard inside a soft one, and their share of all the discs,
// which is all of it wherever everything round the point is in the way.
vec2 sunShadowFinish(SunShadowSetup s, float hidden, float lifted, float spreadSum, float open, out float softness)
{
    softness = hidden > 1e-6 ? clamp((sqrt(spreadSum / hidden) - s.sharpest) / max(s.softest - s.sharpest, 1e-3), 0.0, 1.0) : 0.0;
    float share = hidden / max(hidden + open, 1e-12);
    // (1 - (r/R)^2)^2 over a disc of radius R comes to pi R^2 / 3.
    hidden = max(min(hidden * 3.0 / 3.14159265359, 1.0), share);
    float direct = clamp((1.0 - hidden - SHADOW_SNAP) / (1.0 - 2.0 * SHADOW_SNAP), 0.0, 1.0);
    return vec2(direct, (1.0 - direct) * lifted / SHADOW_FILL_TOTAL);
}

// What gets to a point that's in the sun, or inside a shadow, as known from the block map alone, without reading every texel in reach:
// whether it's one or the other (returns false if neither), and what gets there if it is. What lifts a deep shadow is worked out whenever anything in
// its filter could, so that it's never cut off where it isn't nothing.
bool sunShadowKnown(sampler2D map, SunShadowSetup s, out vec2 parts, out float softness)
{
    softness = 0.0;
    parts = vec2(1.0, 0.0);
    if (s.foot <= 0.0) return true;
    if (s.deep)
    {
        softness = clamp((s.foot - s.sharpest) / max(s.softest - s.sharpest, 1e-3), 0.0, 1.0);
        parts = vec2(0.0, s.lifts ? sunShadowFill(map, s.size, s.base, s.f, s.reference, s.metresPerDepth) : 0.0);
        return true;
    }
    return false;
}

// How much of the sun gets to a point through one cascade, as x what reaches it directly, 0 to 1, and y what fills its shadow from the air, so that
// the two add up to all that gets there. point is in the cascade's space (0 to 1 on each axis); perMetre is how far its depth moves for a metre along
// the light and texelMetres how wide a texel is. softness is how soft the shadow is here, 0 sharpest to 1 softest, for the debug picture. Every texel
// that can reach the point is read from the map, a gather of four at a time over the disc round it: each pair of rows only as far across as the disc
// is wide there, which leaves out a fifth to two fifths of the square round it, all of it out of reach.
vec2 sunShadowCascade(sampler2D map, sampler2D ranges, vec3 point, float perMetre, float texelMetres, out float softness)
{
    SunShadowSetup s = sunShadowSetup(map, ranges, point, perMetre, texelMetres);
    vec2 parts;
    if (sunShadowKnown(map, s, parts, softness)) return parts;

    float hidden = 0.0;
    float lifted = 0.0;
    float spreadSum = 0.0;
    float open = 0.0;
    float disc = float(s.reach) + 0.5;
    for (int y = -s.reach; y <= s.reach; y += 2)
    {
        // The nearer of the pair's two rows to the point, and how far either way the disc reaches along it.
        float rowY = clamp(s.f.y, float(y), float(y + 1)) - s.f.y;
        float across = sqrt(max(disc * disc - rowY * rowY, 0.0));
        int fromX = int(floor(s.f.x - across));
        int toX = int(floor(s.f.x + across));
        for (int x = fromX; x <= toX; x += 2)
        {
            sunShadowSplat(s, textureGather(map, (s.base + vec2(x, y) + 1.0) / s.size), x, y, hidden, lifted, spreadSum, open);
        }
    }
    return sunShadowFinish(s, hidden, lifted, spreadSum, open, softness);
}

// Where a point in view space is in cascade i's space, and how its depth and texels go: whether it's inside the cascade at all.
bool sunShadowInCascade(int i, vec3 positionViewSpace, out vec3 point, out float perMetre, out float texelMetres)
{
    mat4 sunSpace = sunSpaceMatrices[i];
    point = (sunSpace * vec4(positionViewSpace, 1.0)).xyz * 0.5 + 0.5;
    perMetre = 0.5 * length(vec3(sunSpace[0][2], sunSpace[1][2], sunSpace[2][2]));
    texelMetres = 2.0 / (length(vec3(sunSpace[0][0], sunSpace[1][0], sunSpace[2][0])) * float(textureSize(shadowMaps[i], 0).x));
    return all(greaterThanEqual(point, vec3(0.0))) && all(lessThanEqual(point, vec3(1.0)));
}

// What of the sun gets to a point in view space, as x what reaches it directly and y what fills its shadow from the air (sunShadowCascade), from the
// cascade that has it most in shadow. Outside them all, all of it.
vec2 calcShadowPartsSoftness(vec3 positionViewSpace, out float softness)
{
    vec2 parts = vec2(1.0, 0.0);
    softness = 0.0;
    for (int i = 0; i < numberOfCascades; i++)
    {
        vec3 point;
        float perMetre;
        float texelMetres;
        if (!sunShadowInCascade(i, positionViewSpace, point, perMetre, texelMetres)) continue;
        float cascadeSoftness;
        vec2 cascade = sunShadowCascade(shadowMaps[i], shadowRanges[i], point, perMetre, texelMetres, cascadeSoftness);
        if (cascade.x + cascade.y < parts.x + parts.y)
        {
            parts = cascade;
            softness = cascadeSoftness;
        }
    }
    return parts;
}

vec2 calcShadowParts(vec3 positionViewSpace)
{
    float softness;
    return calcShadowPartsSoftness(positionViewSpace, softness);
}

// All that gets to a point of the sun, through its shadow and the fill in it.
float calcShadow(vec3 positionViewSpace)
{
    vec2 parts = calcShadowParts(positionViewSpace);
    return parts.x + parts.y;
}

// The light flesh is measured over: texels from the point, past which they count for nothing. It takes in the four by four nearest, and no further, as how thick something is
// changes much faster across it than whether it's lit.
const float THICKNESS_FILTER_RADIUS = 2.25;
// Metres of flesh in front of the point at which a texel counts in full: less, and it's the point's own surface or next to it, which the light reaches directly.
const float THICKNESS_FULL = 0.01;
// Metres of the sun's own shadow map's offset, and of the two maps' different texels, that whatever's in front of the flesh is allowed before it counts as in the way.
const float THICKNESS_ENTRY_SLACK = 0.12;

// How much of the sun gets through flesh to a point, per colour, for every unit of it falling square on the flesh it went in by (Lighting/lightingCalc.glsl, SUBSURFACE.md).
// The flesh map holds the depth where the sun goes into skin and hide, and the point is where light comes out, so the depth between them is how far it went through flesh.
// Each texel counts by how squarely the sun met the flesh there, as light that meets a surface edge on hardly gets in, which is read from how fast the map's depth changes
// across it: that's also what makes it safe, as the only texels that would read thin where nothing is are those where the map grazes a body's outline from the sun or steps
// from one part to another, and there the depth runs away. Only flesh in front counts, so a point the sun lights directly gets nothing from it, and a texel's thickness goes
// through what it takes of each colour before they're averaged, so a thin edge isn't lost in a thick average. Then the sun's own shadow map says whether anything else,
// armour, a tree, a wall, stood between the sun and where it went in. fleshDepthMap is flesh alone from the sun, drawn with no offset (FleshDepth), and fleshSpace takes view
// space to it as sunSpaceMatrices do to the cascades; towardsSun is in view space; extinction is per metre, per colour (sssExtinction).
vec3 sunFleshTransmittance(sampler2D fleshDepthMap, mat4 fleshSpace, vec3 positionViewSpace, vec3 towardsSun, vec3 extinction)
{
    vec3 point = (fleshSpace * vec4(positionViewSpace, 1.0)).xyz * 0.5 + 0.5;
    if (any(lessThan(point, vec3(0.0))) || any(greaterThan(point, vec3(1.0)))) return vec3(0.0);

    float perMetre = 0.5 * length(vec3(fleshSpace[0][2], fleshSpace[1][2], fleshSpace[2][2]));
    vec2 size = vec2(textureSize(fleshDepthMap, 0));
    float texelMetres = 2.0 / (length(vec3(fleshSpace[0][0], fleshSpace[1][0], fleshSpace[2][0])) * size.x);
    // What a step in depth from one texel to the next comes to as a slope across the sun.
    float slopePerStep = 1.0 / (perMetre * texelMetres);
    vec2 at = point.xy * size - 0.5;
    vec2 base = floor(at);
    vec2 f = at - base;
    vec3 decay = extinction * 1.44269504;    // For exp2.
    const float inverseRadiusSquared = 1.0 / (THICKNESS_FILTER_RADIUS * THICKNESS_FILTER_RADIUS);

    vec3 through = vec3(0.0);
    float total = 0.0;
    float thickness = 0.0;
    float counted = 0.0;
    for (int y = -1; y <= 1; y += 2)
    {
        for (int x = -1; x <= 1; x += 2)
        {
            // Texels (0, 1), (1, 1), (1, 0) and (0, 0) of a square of four.
            vec4 depth = textureGather(fleshDepthMap, (base + vec2(x, y) + 1.0) / size);
            vec4 dx = vec4(x, x + 1, x + 1, x) - f.x;
            vec4 dy = vec4(y + 1, y + 1, y, y) - f.y;
            vec4 weight = max(1.0 - (dx * dx + dy * dy) * inverseRadiusSquared, 0.0);
            weight *= weight;
            total += weight.x + weight.y + weight.z + weight.w;

            // How squarely the sun met the flesh across these four: the cosine of a surface whose depth climbs by this much a texel either way. Where they step off a body
            // or from one part to another, the depth runs away and it's next to nothing.
            float slopeAcross = (depth.y - depth.x + depth.z - depth.w) * 0.5 * slopePerStep;
            float slopeAlong = (depth.x - depth.w + depth.y - depth.z) * 0.5 * slopePerStep;
            float square = inversesqrt(1.0 + slopeAcross * slopeAcross + slopeAlong * slopeAlong);

            // Metres of flesh from where the light went in to here.
            vec4 metres = (point.z - depth) / perMetre;
            vec4 flesh = weight * smoothstep(0.0, THICKNESS_FULL, metres) * square;
            metres = max(metres, 0.0);
            through += flesh.x * exp2(-decay * metres.x);
            through += flesh.y * exp2(-decay * metres.y);
            through += flesh.z * exp2(-decay * metres.z);
            through += flesh.w * exp2(-decay * metres.w);
            thickness += dot(flesh, metres);
            counted += flesh.x + flesh.y + flesh.z + flesh.w;
        }
    }
    if (counted <= 1e-4) return vec3(0.0);

    // Whether the sun reached where it went in: the cascade's first surface along the same ray mustn't be nearer the sun than the flesh's, past its own offset.
    vec3 entry = positionViewSpace + towardsSun * (thickness / counted);
    for (int i = 0; i < numberOfCascades; i++)
    {
        vec3 cascade = (sunSpaceMatrices[i] * vec4(entry, 1.0)).xyz * 0.5 + 0.5;
        if (any(lessThan(cascade, vec3(0.0))) || any(greaterThan(cascade, vec3(1.0)))) continue;
        mat4 sunSpace = sunSpaceMatrices[i];
        float cascadePerMetre = 0.5 * length(vec3(sunSpace[0][2], sunSpace[1][2], sunSpace[2][2]));
        vec4 first = textureGather(shadowMaps[i], cascade.xy);
        vec4 open = step(cascade.z - THICKNESS_ENTRY_SLACK * cascadePerMetre, first);
        return through / max(total, 1e-5) * (0.25 * (open.x + open.y + open.z + open.w));
    }
    return vec3(0.0);
}

#endif
