#version 420

// FXAA 3.11, PC quality preset 39 (Timothy Lottes, NVIDIA). Finds edges by their contrast in luma, walks along each edge
// to both of its ends and blends every pixel across the edge by how far along the edge it lies; pixels inside a feature
// too small to have edges get a sub-pixel blend. Runs on the tone-mapped, gamma-encoded image, where luma contrast
// matches what is seen. Optional on top of MSAA (AntiAliasingSettings.Fxaa): it sees shading and texture aliasing
// that multisampling can't, at the cost of some sharpness.

#include Globals.glsl

uniform sampler2D l_tex;

layout (location = 0) out vec4 out_Colour;

// How much sub-pixel aliasing is removed: 1 softest, 0.75 the default, 0.5 sharper, 0 off.
const float SUBPIX = 0.75;
// Least local contrast, relative to the brightest neighbour, that counts as an edge.
const float EDGE_THRESHOLD = 0.125;
// Contrast below which dark areas are left alone.
const float EDGE_THRESHOLD_MIN = 0.0312;
// Steps of the walk along an edge, in pixels.
const int SEARCH_STEPS = 12;
const float SEARCH_STEP[SEARCH_STEPS] = float[](1.0, 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0, 8.0);

vec2 rcpFrame;

// Bilinear, clamped to the outermost texel centres: the buffer's border colour would read as an edge around the screen.
vec3 fetch(vec2 uv)
{
    return textureLod(l_tex, clamp(uv, 0.5 * rcpFrame, 1.0 - 0.5 * rcpFrame), 0.0).rgb;
}

float luma(vec3 colour)
{
    return dot(colour, vec3(0.299, 0.587, 0.114));
}

float lumaAt(vec2 uv)
{
    return luma(fetch(uv));
}

void main(void)
{
    rcpFrame = 1.0 / resolution;
    vec2 posM = gl_FragCoord.xy * rcpFrame;

    vec3 rgbM = fetch(posM);
    float lumaM = luma(rgbM);
    float lumaS = lumaAt(posM + vec2( 0.0,  1.0) * rcpFrame);
    float lumaE = lumaAt(posM + vec2( 1.0,  0.0) * rcpFrame);
    float lumaN = lumaAt(posM + vec2( 0.0, -1.0) * rcpFrame);
    float lumaW = lumaAt(posM + vec2(-1.0,  0.0) * rcpFrame);

    float maxSM = max(lumaS, lumaM);
    float minSM = min(lumaS, lumaM);
    float maxESM = max(lumaE, maxSM);
    float minESM = min(lumaE, minSM);
    float maxWN = max(lumaN, lumaW);
    float minWN = min(lumaN, lumaW);
    float rangeMax = max(maxWN, maxESM);
    float rangeMin = min(minWN, minESM);
    float range = rangeMax - rangeMin;
    if (range < max(EDGE_THRESHOLD_MIN, rangeMax * EDGE_THRESHOLD))
    {
        out_Colour = vec4(rgbM, 1.0);
        return;
    }

    float lumaNW = lumaAt(posM + vec2(-1.0, -1.0) * rcpFrame);
    float lumaSE = lumaAt(posM + vec2( 1.0,  1.0) * rcpFrame);
    float lumaNE = lumaAt(posM + vec2( 1.0, -1.0) * rcpFrame);
    float lumaSW = lumaAt(posM + vec2(-1.0,  1.0) * rcpFrame);

    // Is the edge horizontal or vertical?
    float lumaNS = lumaN + lumaS;
    float lumaWE = lumaW + lumaE;
    float subpixRcpRange = 1.0 / range;
    float subpixNSWE = lumaNS + lumaWE;
    float edgeHorz1 = (-2.0 * lumaM) + lumaNS;
    float edgeVert1 = (-2.0 * lumaM) + lumaWE;
    float lumaNESE = lumaNE + lumaSE;
    float lumaNWNE = lumaNW + lumaNE;
    float edgeHorz2 = (-2.0 * lumaE) + lumaNESE;
    float edgeVert2 = (-2.0 * lumaN) + lumaNWNE;
    float lumaNWSW = lumaNW + lumaSW;
    float lumaSWSE = lumaSW + lumaSE;
    float edgeHorz4 = (abs(edgeHorz1) * 2.0) + abs(edgeHorz2);
    float edgeVert4 = (abs(edgeVert1) * 2.0) + abs(edgeVert2);
    float edgeHorz3 = (-2.0 * lumaW) + lumaNWSW;
    float edgeVert3 = (-2.0 * lumaS) + lumaSWSE;
    float edgeHorz = abs(edgeHorz3) + edgeHorz4;
    float edgeVert = abs(edgeVert3) + edgeVert4;

    float subpixNWSWNESE = lumaNWSW + lumaNESE;
    float lengthSign = rcpFrame.x;
    bool horzSpan = edgeHorz >= edgeVert;
    float subpixA = subpixNSWE * 2.0 + subpixNWSWNESE;

    if (!horzSpan) lumaN = lumaW;
    if (!horzSpan) lumaS = lumaE;
    if (horzSpan) lengthSign = rcpFrame.y;
    float subpixB = (subpixA * (1.0 / 12.0)) - lumaM;

    // Which side of the pixel the edge lies on.
    float gradientN = lumaN - lumaM;
    float gradientS = lumaS - lumaM;
    float lumaNN = lumaN + lumaM;
    float lumaSS = lumaS + lumaM;
    bool pairN = abs(gradientN) >= abs(gradientS);
    float gradient = max(abs(gradientN), abs(gradientS));
    if (pairN) lengthSign = -lengthSign;
    float subpixC = clamp(abs(subpixB) * subpixRcpRange, 0.0, 1.0);

    // Walk along the edge, half a pixel over onto it, both ways until the luma pair changes.
    vec2 posB = posM;
    vec2 offNP;
    offNP.x = (!horzSpan) ? 0.0 : rcpFrame.x;
    offNP.y = ( horzSpan) ? 0.0 : rcpFrame.y;
    if (!horzSpan) posB.x += lengthSign * 0.5;
    if ( horzSpan) posB.y += lengthSign * 0.5;

    vec2 posN = posB - offNP * SEARCH_STEP[0];
    vec2 posP = posB + offNP * SEARCH_STEP[0];
    float subpixD = ((-2.0) * subpixC) + 3.0;
    float lumaEndN = lumaAt(posN);
    float subpixE = subpixC * subpixC;
    float lumaEndP = lumaAt(posP);

    if (!pairN) lumaNN = lumaSS;
    float gradientScaled = gradient * (1.0 / 4.0);
    float lumaMM = lumaM - lumaNN * 0.5;
    float subpixF = subpixD * subpixE;
    bool lumaMLTZero = lumaMM < 0.0;

    lumaEndN -= lumaNN * 0.5;
    lumaEndP -= lumaNN * 0.5;
    bool doneN = abs(lumaEndN) >= gradientScaled;
    bool doneP = abs(lumaEndP) >= gradientScaled;
    if (!doneN) posN -= offNP * SEARCH_STEP[1];
    if (!doneP) posP += offNP * SEARCH_STEP[1];
    bool doneNP = (!doneN) || (!doneP);

    for (int i = 2; i < SEARCH_STEPS && doneNP; i++)
    {
        if (!doneN) lumaEndN = lumaAt(posN) - lumaNN * 0.5;
        if (!doneP) lumaEndP = lumaAt(posP) - lumaNN * 0.5;
        doneN = abs(lumaEndN) >= gradientScaled;
        doneP = abs(lumaEndP) >= gradientScaled;
        if (!doneN) posN -= offNP * SEARCH_STEP[i];
        if (!doneP) posP += offNP * SEARCH_STEP[i];
        doneNP = (!doneN) || (!doneP);
    }

    // Blend across the edge by how close the nearer end is.
    float dstN = posM.x - posN.x;
    float dstP = posP.x - posM.x;
    if (!horzSpan) dstN = posM.y - posN.y;
    if (!horzSpan) dstP = posP.y - posM.y;

    bool goodSpanN = (lumaEndN < 0.0) != lumaMLTZero;
    float spanLength = (dstP + dstN);
    bool goodSpanP = (lumaEndP < 0.0) != lumaMLTZero;
    float spanLengthRcp = 1.0 / spanLength;

    bool directionN = dstN < dstP;
    float dst = min(dstN, dstP);
    bool goodSpan = directionN ? goodSpanN : goodSpanP;
    float subpixG = subpixF * subpixF;
    float pixelOffset = (dst * (-spanLengthRcp)) + 0.5;
    float subpixH = subpixG * SUBPIX;

    float pixelOffsetGood = goodSpan ? pixelOffset : 0.0;
    float pixelOffsetSubpix = max(pixelOffsetGood, subpixH);
    if (!horzSpan) posM.x += pixelOffsetSubpix * lengthSign;
    if ( horzSpan) posM.y += pixelOffsetSubpix * lengthSign;
    out_Colour = vec4(fetch(posM), 1.0);
}
