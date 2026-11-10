#ifndef DINO_WAY_SHAPE_GLSL
#define DINO_WAY_SHAPE_GLSL

// A way's opening and how far it has opened, which both of its passes have to agree on to the pixel (WayRenderer). All in the gate's own frame, in metres. Needs Globals.glsl.

#include FocusCutout.glsl

uniform vec4 wayShape;      // x the opening's half width, y its sill, z where its arch springs, w the radius of the arch's two arcs (WaygateDesign)
uniform vec4 wayHeart;      // xy where its throat is centred, z the throat's depth, w 1 for the Haven's great gate
uniform vec4 wayState;      // x seconds since it opened, y how much of it is there, 0 to 1, z as wayHeart.w, w how bright its breathing makes it now (WayLook.Breathing)

// The opening sequence, in seconds from the moment it opens.
const float WAY_RUNES = 0.9;        // The runes light one after another over this long, from the treads to the keystone.
const float WAY_RIM_FROM = 0.25;    // The edge lights from the sill up both sides,
const float WAY_RIM_TO = 0.8;       // reaching the point of the arch here.
const float WAY_FILL_FROM = 0.55;   // The light floods in from the edge,
const float WAY_FILL_TO = 1.45;     // meeting in the middle here.
const float WAY_DEEP_FROM = 1.0;    // And the throat opens up behind it, from nothing
const float WAY_DEEP_TO = 2.9;      // to its full depth.
const float WAY_RIM_BAND = 0.2;     // Metres of the edge drawn as it lights, before the light floods in.

// Metres from the opening's edge, less than 0 inside: WaygateDesign.Distance. Exact inside, which is all that's asked of it.
float wayDistance(vec2 p)
{
    float offset = wayShape.w - wayShape.x;
    float across = abs(p.x);
    float d = max(across - wayShape.x, wayShape.y - p.y);
    if (p.y > wayShape.z) d = max(d, length(vec2(across + offset, p.y - wayShape.z)) - wayShape.w);
    return d;
}

float wayApex()
{
    float offset = wayShape.w - wayShape.x;
    return wayShape.z + sqrt(max(wayShape.w * wayShape.w - offset * offset, 0.0));
}

// How far the light has come in from the edge, 0 to 1.
float wayFill()
{
    float t = clamp((wayState.x - WAY_FILL_FROM) / (WAY_FILL_TO - WAY_FILL_FROM), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

// Whether the edge has lit at this height yet: it runs up both sides from the sill to the point.
float wayRimLit(float y)
{
    float reached = (wayState.x - WAY_RIM_FROM) / (WAY_RIM_TO - WAY_RIM_FROM);
    float height = (y - wayShape.y) / max(wayApex() - wayShape.y, 0.01);
    return clamp((reached - height) * 6.0 + 1.0, 0.0, 1.0);
}

// The deepest any point of the opening is from its edge, near enough.
float wayDeepest()
{
    return max(-wayDistance(wayHeart.xy), 0.5);
}

// Whether this fragment of the way is there yet: a rune once its turn has come, and the face as the light floods in from the edge, which it does once. A rune's light is on its stone, so
// it thins out with the stone where the focus cutout opens the gate for a hero behind it (FocusCutout.glsl); the way itself never does, as it hides what's behind it from either side.
bool wayDrawn(vec3 local, vec3 world, vec3 rune)
{
    if (rune.x > 0.5) return wayState.x >= rune.y * WAY_RUNES && focusVisibility(world) > focusDither(gl_FragCoord.xy);
    float fill = wayFill();
    if (fill >= 1.0) return true;
    float inside = -wayDistance(local.xy);
    float band = wayRimLit(local.y) > 0.0 ? WAY_RIM_BAND : 0.0;
    return inside <= max(fill * wayDeepest() * 1.04, band);
}

#endif
