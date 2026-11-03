#ifndef DINO_FOCUS_CUTOUT_GLSL
#define DINO_FOCUS_CUTOUT_GLSL

// Thins out whatever stands between the camera and the focus point, so that tree crowns and roofs never hide the player.
// Needs Globals.glsl. focusPoint is xyz the point, w the radius of the opening at the point (0 turns it off).

// 0 = right in the opening, 1 = untouched.
float focusVisibility(vec3 worldPos)
{
    if (focusPoint.w <= 0.0) return 1.0;

    // Only what rises above the focus: the ground and everything at the character's own level stay as they are.
    if (worldPos.y < focusPoint.y) return 1.0;

    vec3 toFocus = focusPoint.xyz - viewPosWorld;
    float focusDistance = length(toFocus);
    vec3 direction = toFocus / focusDistance;
    vec3 fromCamera = worldPos - viewPosWorld;
    float along = dot(fromCamera, direction);
    if (along > focusDistance + 2.0) return 1.0;

    // The opening narrows towards the camera, following the view's own perspective.
    float radius = focusPoint.w * clamp(along / focusDistance, 0.35, 1.0);
    float offAxis = length(fromCamera - direction * along);
    return smoothstep(radius * 0.45, radius, offAxis);
}

// Interleaved gradient noise: a screen-space dither that reads as an even thinning.
float focusDither(vec2 pixel)
{
    return fract(52.9829189 * fract(dot(pixel, vec2(0.06711056, 0.00583715))));
}

// Call first thing in a fragment shader.
void applyFocusCutout(vec3 worldPos)
{
    if (focusVisibility(worldPos) <= focusDither(gl_FragCoord.xy)) discard;
}

#endif
