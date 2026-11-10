#ifndef DINO_RELIEF_GLSL
#define DINO_RELIEF_GLSL

// Parallax occlusion mapping for a tiling material on any surface (TEXTURES.md), and the shadows the relief casts on itself. The relief hangs below the mesh, as
// deep as the material says in metres at the size it's mapped (Surface/MaterialTable.glsl), so the mesh is its top and nothing ever stands out of a silhouette.
// A pixel's view ray is followed down into it and the texture is read where the ray first meets the height (Tatarchuk, "Dynamic Parallax Occlusion Mapping with
// Approximate Soft Shadows", 2006), with three things beyond the usual:
//   The ray is followed below the surface as it curves, not below a flat plane: over a bulge, a log or a tower, the surface falls away from the ray as it goes,
//   by half the curvature times the distance squared (after Oliveira and Policarpo's curved relief mapping, here with the shape operator of each pixel from
//   Surface/SurfaceFrame.glsl). Near a curved silhouette a ray now leaves the relief after a short way, where flat parallax would slide the texture a long way
//   sideways and make it swim; that's what keeps a round thing looking solid.
//   The relief comes up to nothing at a corner (edgeDistance, from MeshBuilder), where the next face begins and a ray would otherwise run past the edge of its
//   own face, and fades out where it would move nothing by a pixel, which is also what keeps it cheap far off, and on a surface curving round tighter than
//   a few of its depths, a twig or a thin branch, where it could only show as a rim.
//   How many steps are taken is how many pixels the ray crosses on screen, so a relief is followed as finely as it can be seen and no finer.
// Needs SurfaceFrame.glsl, with surfaceShape called on the frame, and the sampler2DArray materialMapTextureArray, whose alpha is the height.

const float RELIEF_MOST_SLANT = 3.0;        // The farthest a ray may run sideways, in depths of the relief: past about 72 degrees the relief flattens instead, as a
                                            // stone's sides, seen that edge on, are a few texels stretched across many pixels.
const float RELIEF_STEPS_PER_PIXEL = 1.0;
const int RELIEF_LEAST_STEPS = 3;
const int RELIEF_MOST_STEPS = 32;
const float RELIEF_SHOWN_FROM = 0.6;        // Pixels the parallax would move something by, below which there's none, as nothing would be seen to move,
const float RELIEF_SHOWN_AT = 1.6;          // and above which it's all there.
const float RELIEF_EDGE_REACH = 1.25;       // How far in from a corner the relief reaches its full depth, in how far the ray can run sideways through it.
const float RELIEF_PENUMBRA = 0.09;         // The tangent of the half-width of what casts a relief's shadow: soft, as the sky fills it in.
const float RELIEF_SHADOW_FROM = 1.0;       // Pixels deep a relief has to be for its own shadows to show, and how deep for them to be all there:
const float RELIEF_SHADOW_AT = 2.0;         // a shadow less than a pixel across is a cost and nothing else.
const float RELIEF_SHADOW_SMOOTHING = 1.5;  // Mips coarser than the march, for what casts a shadow: a stone does, the grain on its face doesn't.
const float RELIEF_SHADOW_BIAS = 0.06;      // Depths of the relief something has to stand above the ray before it hides any light. The chisel marks beside a
                                            // point, a few millimetres high and right next to it, otherwise black it out wherever light rakes across.
const int RELIEF_SHADOW_STEPS = 16;
const float RELIEF_ROUND_FROM = 2.0;        // Radii of curvature, in depths of the relief, below which a surface has none and above which it has all of it:
const float RELIEF_ROUND_AT = 4.0;          // a branch only as thick as its bark is deep curves away before its relief can show, and costs as much as a trunk.

struct Relief
{
    vec2 uv;                // Where the view ray meets the relief.
    float depth;            // How far below the mesh that is, in metres.
    float reliefMetres;     // How deep the relief was here, as it was faded at corners and far off: 0 where there was none.
};

float reliefDepthAt(vec2 uv, float layer, float lod, float reliefMetres)
{
    return (1.0 - textureLod(materialMapTextureArray, vec3(uv, layer), lod).a) * reliefMetres;
}

// The mip a footprint of this size wants, from the larger of its two screen steps. A march reads one mip throughout, as the ray's own texture coordinates jump at
// what it's hidden behind and their derivatives mean nothing.
float reliefLod(SurfaceFrame f, float textureSize)
{
    vec2 dx = f.uvDx * textureSize;
    vec2 dy = f.uvDy * textureSize;
    return max(0.5 * log2(max(dot(dx, dx), dot(dy, dy))), 0.0);
}

// Follows the view ray (`view`, unit, towards the camera) into a relief `reliefMetres` deep as it's mapped here: the material's ratio times the tile's metres
// (MaterialTable.glsl's x times f.tileMetres), which is what the normal map's slopes were made for. `edgeDistance` is metres to the nearest corner of the face.
Relief reliefMarch(SurfaceFrame f, vec2 uv, vec3 view, float layer, float reliefMetres, float edgeDistance, float lod)
{
    Relief r;
    r.uv = uv;
    r.depth = 0.0;
    r.reliefMetres = 0.0;
    float vn = dot(view, f.normal);
    if (!f.mapped || reliefMetres <= 0.0 || vn <= 0.01) return r;
    vec3 vt = view - f.normal * vn;
    float sideways = length(vt);
    float slant = sideways / vn;

    // Near a face's edge a ray would run off the face before it reached the bottom. It runs sideways at most the depth times the slant, so that, and no more,
    // is how far in the relief has to have come up to its full depth: a wall seen square keeps its relief nearly to its corner, and one seen edge on fades it
    // early enough that nothing slides round the corner.
    float depth = reliefMetres * smoothstep(0.0, RELIEF_EDGE_REACH * reliefMetres * max(slant, 0.5), edgeDistance);
    // The tightest the surface curves here, the larger principal curvature: on what's thinner for its relief than a few depths, the relief fades out. It goes by
    // the shape alone, not the view, so it holds still as the camera moves.
    float meanCurvature = 0.5 * (f.shape[0][0] + f.shape[1][1]);
    float spread = length(vec2(0.5 * (f.shape[0][0] - f.shape[1][1]), f.shape[0][1]));
    float tightest = abs(meanCurvature) + spread;
    depth *= smoothstep(RELIEF_ROUND_FROM, RELIEF_ROUND_AT, 1.0 / max(tightest * reliefMetres, 1e-6));
    if (slant > RELIEF_MOST_SLANT) depth *= RELIEF_MOST_SLANT / slant;
    depth *= smoothstep(RELIEF_SHOWN_FROM, RELIEF_SHOWN_AT, depth * max(slant, 0.5) * f.pixelsPerMetre);
    r.reliefMetres = depth;
    if (depth <= 0.0) return r;

    // Below the curving surface, the ray at distance t is vn t - k t^2 / 2 deep: it reaches the bottom of the relief where that's `depth`, or over a bulge too
    // sharp for that, turns back up at its deepest, vn / k.
    float k = surfaceCurvature(f, vt);
    float end;
    if (abs(k) < 1e-5)
    {
        end = depth / vn;
    }
    else
    {
        float disc = vn * vn - 2.0 * k * depth;
        end = disc >= 0.0 ? (vn - sqrt(disc)) / k : vn / k;
    }
    vec2 uvPerMetre = -vec2(dot(f.gradU, vt), dot(f.gradV, vt));
    int steps = clamp(int(sideways * end * f.pixelsPerMetre * RELIEF_STEPS_PER_PIXEL) + RELIEF_LEAST_STEPS, RELIEF_LEAST_STEPS, RELIEF_MOST_STEPS);
    float dt = end / float(steps);

    // How far the relief lies below the ray: positive while the ray is above it.
    float gapBefore = reliefDepthAt(uv, layer, lod, depth);
    if (gapBefore <= 0.0) return r;
    float tBefore = 0.0;
    float tAfter = end;
    float gapAfter = -1.0;
    bool crossed = false;
    for (int i = 1; i <= RELIEF_MOST_STEPS; i++)
    {
        if (i > steps) break;
        float t = dt * float(i);
        float gap = reliefDepthAt(uv + uvPerMetre * t, layer, lod, depth) - (vn * t - 0.5 * k * t * t);
        if (gap <= 0.0)
        {
            tAfter = t;
            gapAfter = gap;
            crossed = true;
            break;
        }
        tBefore = t;
        gapBefore = gap;
    }

    float t = end;
    if (crossed)
    {
        // Between the last step above the relief and the first below it, by false position: the gap's sign is all that's known for sure.
        for (int j = 0; j < 3; j++)
        {
            float between = tBefore + (tAfter - tBefore) * gapBefore / (gapBefore - gapAfter);
            float gap = reliefDepthAt(uv + uvPerMetre * between, layer, lod, depth) - (vn * between - 0.5 * k * between * between);
            if (gap > 0.0)
            {
                tBefore = between;
                gapBefore = gap;
            }
            else
            {
                tAfter = between;
                gapAfter = gap;
            }
        }
        t = tBefore + (tAfter - tBefore) * gapBefore / (gapBefore - gapAfter);
    }
    r.uv = uv + uvPerMetre * t;
    r.depth = max(vn * t - 0.5 * k * t * t, 0.0);
    return r;
}

// How much of a light along `light` (unit, towards it) reaches where the view ray met the relief, past the relief around it: 1 in the open. A ray goes from
// there towards the light until it's out of the relief, and the most the relief rises above it anywhere, as an angle seen from where it started, is how much of
// the light is hidden: a soft shadow that's sharp where what casts it is close, as a real one is.
float reliefShadow(SurfaceFrame f, Relief r, vec3 light, float layer, float lod)
{
    float depth = r.reliefMetres;
    float ln = dot(light, f.normal);
    float shown = smoothstep(RELIEF_SHADOW_FROM, RELIEF_SHADOW_AT, depth * f.pixelsPerMetre);
    if (depth <= 0.0 || r.depth <= 1e-5 || ln <= 0.0 || shown <= 0.0) return 1.0;
    vec3 lt = light - f.normal * ln;
    // The ray rises ln a metre, and over a bulge the surface falls away from it besides: it's out where ln t + k t^2 / 2 reaches the depth it began at.
    float bend = 0.5 * surfaceCurvature(f, lt);
    float end = r.depth / ln;
    if (abs(bend) > 1e-5)
    {
        float disc = ln * ln + 4.0 * bend * r.depth;
        if (disc >= 0.0) end = (-ln + sqrt(disc)) / (2.0 * bend);
    }
    float sideways = length(lt);
    end = min(end, RELIEF_MOST_SLANT * depth / max(sideways, 1e-4));
    vec2 uvPerMetre = vec2(dot(f.gradU, lt), dot(f.gradV, lt));
    int steps = clamp(int(sideways * end * f.pixelsPerMetre * 0.5) + 4, 4, RELIEF_SHADOW_STEPS);
    float casting = lod + RELIEF_SHADOW_SMOOTHING;

    float hidden = 0.0;
    for (int i = 1; i <= RELIEF_SHADOW_STEPS; i++)
    {
        if (i > steps) break;
        float t = end * (float(i) - 0.5) / float(steps);
        float rayDepth = r.depth - ln * t - bend * t * t;
        float above = rayDepth - reliefDepthAt(r.uv + uvPerMetre * t, layer, casting, depth) - RELIEF_SHADOW_BIAS * depth;
        hidden = max(hidden, above / (t * RELIEF_PENUMBRA + 1e-5));
    }
    return 1.0 - clamp(hidden, 0.0, 1.0) * shown;
}

#endif
