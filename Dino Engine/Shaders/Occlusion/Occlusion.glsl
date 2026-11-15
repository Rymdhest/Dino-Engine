#ifndef DINO_OCCLUSION_GLSL
#define DINO_OCCLUSION_GLSL

// What the occlusion passes share (OcclusionRenderer, OCCLUSION.md). Needs Globals.glsl.
//
// The depth they read is metres from the camera along its view, one texel a pixel with three coarser levels under it, negative where what's
// drawn is a canopy (leaves, blades, moss, fur: masses of thin things, Lighting/SubsurfaceKinds.glsl), which hides less of the sky than anything
// solid does, and OCCLUSION_SKY where nothing was drawn at all.
const float OCCLUSION_SKY = 1.0e6;

float occlusionDistance(float stored)
{
    return abs(stored);
}

bool occlusionThin(float stored)
{
    return stored < 0.0;
}

// Where in view space the middle of a pixel is, at a distance along the view. pixelCentre is in pixels, 0.5 the middle of the first.
vec3 occlusionViewPosition(vec2 pixelCentre, float distance, vec2 size)
{
    vec2 ndc = pixelCentre / size * 2.0 - 1.0;
    return vec3(ndc.x * distance / projectionMatrix[0][0], ndc.y * distance / projectionMatrix[1][1], -distance);
}

// How alike a pixel is to its four neighbours, left, right, up and down, 1 on one surface and 0 across an edge, by how far each is from where the
// slope through all of them says it would be (XeGTAO's, Intel 2021): a surface seen edge on is still one surface. tolerance scales how far apart
// counts as an edge: 1 for anything solid.
vec4 occlusionEdges(float centre, float left, float right, float up, float down, float tolerance)
{
    vec4 apart = vec4(left, right, up, down) - centre;
    float slopeAcross = (apart.y - apart.x) * 0.5;
    float slopeUp = (apart.z - apart.w) * 0.5;
    vec4 alongSlope = apart + vec4(slopeAcross, -slopeAcross, -slopeUp, slopeUp);
    apart = min(abs(apart), abs(alongSlope));
    return clamp(1.25 - apart / (centre * 0.011 * tolerance), 0.0, 1.0);
}

// Four edges in a byte, two bits each, as the denoise reads them.
float occlusionPackEdges(vec4 edges)
{
    edges = round(clamp(edges, 0.0, 1.0) * 3.0);
    return dot(edges, vec4(64.0, 16.0, 4.0, 1.0)) / 255.0;
}

vec4 occlusionUnpackEdges(float stored) // never "packed", a reserved word NVIDIA refuses
{
    uint bits = uint(stored * 255.0 + 0.5);
    return vec4(float((bits >> 6u) & 3u), float((bits >> 4u) & 3u), float((bits >> 2u) & 3u), float(bits & 3u)) / 3.0;
}

#endif
