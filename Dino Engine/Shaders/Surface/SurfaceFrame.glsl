#ifndef DINO_SURFACE_FRAME_GLSL
#define DINO_SURFACE_FRAME_GLSL

// How a tiling material lies on a surface, worked out for each pixel from where it is and what its texture coordinates are, with nothing stored on a vertex
// (TEXTURES.md). The gradients of u and v along the surface come from the two screen steps of the pixel (Schüler, "Followup: Normal Mapping Without Precomputed
// Tangents", 2013; Mikkelsen, "Surface Gradient Based Bump Mapping Framework", 2020): exact for whatever the mapping is, stretched, skewed or mirrored, which a
// tangent kept on a vertex can't be, and the same on a skinned body, a plant bending in the wind or a stretched fence as on anything else. From them come the
// metres a tile spans, which makes a material's relief a depth in metres, and the frame its normal map is read in. How the smooth normal turns across the pixel
// is the surface's curvature, which bends a parallax ray round a log or a tower (Surface/Relief.glsl); that's only worked out where a relief is followed
// (surfaceShape), as everything else, a crown's cards above all, is drawn many times over and has no use for it.

struct SurfaceFrame
{
    vec3 normal;            // The smooth surface's normal, unit, facing the side being seen.
    vec3 gradU;             // How fast u changes per metre along the surface.
    vec3 gradV;
    float tileMetres;       // Metres a tile spans here, the geometric mean of the two ways: 1 / sqrt(uv area per square metre).
    vec2 uvDx;              // Screen derivatives of the texture coordinates, for filtering.
    vec2 uvDy;
    vec3 positionDx;        // And of the position.
    vec3 positionDy;
    bool mapped;            // False where the texture coordinates don't change, so nothing can be known of a texture's slopes.

    // From surfaceShape, for a relief alone.
    vec3 axisA;             // Two unit directions in the tangent plane, at right angles,
    vec3 axisB;
    mat2 shape;             // and the shape operator in them: how far the normal turns per metre, positive where the surface bulges towards the viewer.
    float pixelsPerMetre;   // Screen pixels to a metre along the surface here, the fewer way.
};

// The most the normal is let turn per metre, as a mesh that's nothing but slivers can say anything: a radius of 5 cm.
const float SURFACE_MOST_CURVATURE = 20.0;

// `position` in any space where a unit is a metre (view space), `normal` the interpolated normal there, already turned to face the viewer. Call it where control
// flow is the same for the whole quad of pixels, before anything can be discarded, as it takes derivatives.
SurfaceFrame surfaceFrame(vec3 position, vec3 normal, vec2 uv)
{
    SurfaceFrame f;
    vec3 n = normalize(normal);
    f.normal = n;
    f.positionDx = dFdx(position);
    f.positionDy = dFdy(position);
    f.uvDx = dFdx(uv);
    f.uvDy = dFdy(uv);

    // The two screen steps' dual basis in the tangent plane: r1 is at right angles to the y step, r2 to the x step, and each meets its own step at det.
    vec3 r1 = cross(f.positionDy, n);
    vec3 r2 = cross(n, f.positionDx);
    float det = dot(f.positionDx, r1);
    f.mapped = abs(det) > 1e-24;
    float perDet = f.mapped ? 1.0 / det : 0.0;
    f.gradU = (r1 * f.uvDx.x + r2 * f.uvDy.x) * perDet;
    f.gradV = (r1 * f.uvDx.y + r2 * f.uvDy.y) * perDet;
    float density = abs(dot(cross(f.gradU, f.gradV), n));
    f.mapped = f.mapped && density > 1e-14;
    f.tileMetres = f.mapped ? inversesqrt(density) : 0.0;

    f.axisA = vec3(1.0, 0.0, 0.0);
    f.axisB = vec3(0.0, 1.0, 0.0);
    f.shape = mat2(0.0);
    f.pixelsPerMetre = 0.0;
    return f;
}

// The surface's curvature and its size on screen, which a relief needs and nothing else does. It takes derivatives of the normal, so its condition has to be the
// same across a quad of pixels: one triangle's material is.
void surfaceShape(inout SurfaceFrame f)
{
    vec3 n = f.normal;
    vec3 tx = f.positionDx - n * dot(f.positionDx, n);
    vec3 ty = f.positionDy - n * dot(f.positionDy, n);
    float lx = length(tx);
    f.axisA = lx > 1e-12 ? tx / lx : normalize(abs(n.y) < 0.99 ? cross(n, vec3(0.0, 1.0, 0.0)) : cross(n, vec3(1.0, 0.0, 0.0)));
    f.axisB = cross(n, f.axisA);
    f.pixelsPerMetre = 1.0 / max(max(lx, length(ty)), 1e-6);

    // dN = S dP for the two screen steps, so S = N P^-1, kept symmetric as a shape operator is.
    vec3 dnx = dFdx(n);
    vec3 dny = dFdy(n);
    mat2 steps = mat2(dot(f.positionDx, f.axisA), dot(f.positionDx, f.axisB), dot(f.positionDy, f.axisA), dot(f.positionDy, f.axisB));
    mat2 turns = mat2(dot(dnx, f.axisA), dot(dnx, f.axisB), dot(dny, f.axisA), dot(dny, f.axisB));
    float stepDet = determinant(steps);
    mat2 s = abs(stepDet) > 1e-24 ? turns * inverse(steps) : mat2(0.0);
    s = 0.5 * (s + transpose(s));
    f.shape = mat2(clamp(s[0], -SURFACE_MOST_CURVATURE, SURFACE_MOST_CURVATURE), clamp(s[1], -SURFACE_MOST_CURVATURE, SURFACE_MOST_CURVATURE));
}

// How far the surface curves away from a straight line along it, per metre of the line squared: positive over a bulge. `direction` needn't be unit; the answer
// goes with its length squared. Needs surfaceShape.
float surfaceCurvature(SurfaceFrame f, vec3 direction)
{
    vec2 d = vec2(dot(direction, f.axisA), dot(direction, f.axisB));
    return dot(d, f.shape * d);
}

// A material's normal map read in this frame: x along +u, y along +v, z out of the surface, as Synth_Normal.frag makes them. The axes are the gradients scaled
// by the tile's size, unit for an even mapping and shorter or longer as it's stretched, which keeps the slopes those of the relief the parallax marches through.
// `base` is what z points along: the surface's normal, or for a card the mass's it belongs to.
vec3 surfaceNormal(SurfaceFrame f, vec3 mapNormal, vec3 base)
{
    if (!f.mapped) return base;
    return normalize(f.gradU * (f.tileMetres * mapNormal.x) + f.gradV * (f.tileMetres * mapNormal.y) + base * mapNormal.z);
}

#endif
