#version 420

// Steps what plants do, each texel a patch of them. Two things, kept apart because only one of them reaches a tree:
//
// The wind. A damped spring: the wind's drag and the waves going by set where a patch wants to be (vegetationTargetBend), and the spring lags behind them, a
// little past and back. It is close to critically damped: any less and gusts that happen to match its frequency ring it, and the field shivers.
//
// Touch: what's walking through. After Ghost of Tsushima, whose grass doesn't snap back but loses and regains its push in smaller and smaller amounts, so that
// a wave of it runs along the trail behind you. A body parts what's ahead of it and drags what it goes through along with it, and when it's gone a patch
// is let go on a loose spring. That rings on a damped wave, and nothing else drives it, so no gust can set it off. It rings about where the body left the
// patch lying (`settled`), which goes back slowly: a trail stays combed the way you went for a while after you've gone. Pressing is apart from all this, as
// right under a foot there's no direction to push anything: `press` says how flat, and each plant goes over the way it was already leaning.
//
// out_Bend:   xy bend angle (radians, leaning towards +x / +z), zw angular velocity (rad/s)
// out_Touch:  xy lean from touch (radians, same convention), z pressed flat (radians), w how fast it's moving (rad/s). All a reader needs.
// out_Memory: xy angular velocity of the lean (rad/s), zw where it's settled (radians)

#include Wind.glsl

layout (location = 0) out vec4 out_Bend;
layout (location = 1) out vec4 out_Touch;
layout (location = 2) out vec4 out_Memory;

uniform sampler2D previousBend;
uniform sampler2D previousTouch;
uniform sampler2D previousMemory;
uniform sampler2D windMap;

uniform vec2 texelShift;
uniform int integrate;
uniform int resetState;
uniform float deltaTime;
uniform float naturalFrequency;
uniform float dampingRatio;
uniform float touchFrequency;
uniform float touchDamping;
uniform float flattenRecoveryTime;
uniform float pressRecoveryTime;

uniform int contactCount;
uniform vec4 contactPath[64];       // xy where it is, zw where it was last step
uniform vec4 contactShape[64];      // x radius, y lean, z press, w drag

const float STIFFNESS_PATCH = 5.0;  // Metres across a patch of plants of about the same stiffness.
const float EDGE_PATCH = 1.3;       // Metres across the unevenness of the edge of what a body parts: a circle in a field looks drawn on.
const float CONTACT_RATE = 30.0;    // Per second: how fast plants go down under a contact.
const float SOFT_MIDDLE = 0.3;      // Share of a contact's radius over which the push out from it fades, so that it passes smoothly through its middle.
const float SETTLED_SHARE = 0.8;    // How much of the push a patch keeps, once what pushed it has gone.

vec2 clampLength(vec2 v, float maxLength)
{
    float len = length(v);
    return len > maxLength ? v * (maxLength / len) : v;
}

// One step of a spring with less than critical damping, exactly: stepped any other way it would ring for longer at a higher frame rate.
void ring(inout vec2 x, inout vec2 v, float omega, float zeta, float dt)
{
    float decay = zeta * omega;
    float rate = omega * sqrt(1.0 - zeta * zeta);
    float fade = exp(-decay * dt);
    float c = cos(rate * dt);
    float s = sin(rate * dt);
    vec2 nextX = fade * (x * c + (v + decay * x) * (s / rate));
    vec2 nextV = fade * (v * c - (decay * v + omega * omega * x) * (s / rate));
    x = nextX;
    v = nextV;
}

void main(void)
{
    ivec2 size = textureSize(previousBend, 0);
    ivec2 texel = ivec2(gl_FragCoord.xy);
    vec2 texelSize = vegetationRegion.zw / vec2(size);
    vec2 xz = vegetationRegion.xy + (vec2(texel) + 0.5) * texelSize;

    // The region follows the camera in whole texels; read where this texel's world position was last step.
    ivec2 source = texel + ivec2(round(texelShift));
    bool valid = resetState == 0 && all(greaterThanEqual(source, ivec2(0))) && all(lessThan(source, size));
    vec4 bend = valid ? texelFetch(previousBend, source, 0) : vec4(0.0);
    vec4 touch = valid ? texelFetch(previousTouch, source, 0) : vec4(0.0);
    vec4 memory = valid ? texelFetch(previousMemory, source, 0) : vec4(0.0);
    vec2 lean = touch.xy;
    float press = touch.z;
    vec2 velocity = memory.xy;
    vec2 settled = memory.zw;

    vec2 rest = clampLength(vegetationTargetBend(windMap, xz), VEGETATION_MAX_TOTAL_BEND);

    // Texels that just scrolled in (or a reset) start settled instead of springing up from upright.
    if (!valid) bend = vec4(rest, 0.0, 0.0);

    float stiffnessHere = vegetationStiffness(xz, STIFFNESS_PATCH);
    if (integrate == 1)
    {
        // Stiffness varies across the field (fixed to world position) so that it doesn't sway in lockstep, but smoothly, in
        // patches: neighbouring plants at their own pace would drift out of step with each other and the field would boil.
        float omega = WIND_TAU * naturalFrequency * (0.85 + 0.3 * stiffnessHere);
        float stiffness = omega * omega;
        float damping = 2.0 * dampingRatio * omega;

        // Implicit Euler: stable for any frame time.
        float dt = deltaTime;
        vec2 windVelocity = (bend.zw + dt * stiffness * (rest - bend.xy)) / (1.0 + dt * damping + dt * dt * stiffness);
        bend.xy = clampLength(bend.xy + dt * windVelocity, VEGETATION_MAX_TOTAL_BEND);
        bend.zw = windVelocity;

        settled *= exp(-dt / max(flattenRecoveryTime, 1e-3));
        press *= exp(-dt / max(pressRecoveryTime, 1e-3));
    }

    float pressed = 1.0 - exp(-deltaTime * CONTACT_RATE);
    float edgeHere = 0.8 + 0.4 * vegetationStiffness(xz + 31.0, EDGE_PATCH);
    vec2 held = vec2(0.0);
    for (int i = 0; i < contactCount; i++)
    {
        vec4 path = contactPath[i];
        vec4 shape = contactShape[i];

        // Everywhere it's been this step, so that no speed leaves gaps.
        vec2 travel = path.xy - path.zw;
        float travelled = dot(travel, travel);
        bool moved = travelled > 1e-8;
        vec2 nearest = path.zw + travel * (moved ? clamp(dot(xz - path.zw, travel) / travelled, 0.0, 1.0) : 1.0);
        vec2 offset = xz - nearest;
        float dist = length(offset);
        float reach = shape.x * edgeHere;
        if (reach <= 0.0 || dist >= reach) continue;

        // Flat across the body and soft at the rim, which a push fading all the way to the middle wouldn't be: most of that is under the body, out of sight.
        float near = 1.0 - dist / reach;
        float weight = smoothstep(0.0, 0.55, near);
        float soft = SOFT_MIDDLE * shape.x;
        vec2 aside = offset * inversesqrt(dist * dist + soft * soft);
        vec2 along = moved ? travel * inversesqrt(travelled) : vec2(0.0);
        float drag = shape.w;

        vec2 wanted = clampLength((aside * (1.0 - 0.45 * drag) + along * (0.85 * drag)) * (shape.y * weight), VEGETATION_MAX_TOTAL_BEND);
        if (dot(wanted, wanted) > dot(held, held)) held = wanted;

        vec2 left = (aside * (1.0 - 0.75 * drag) + along * drag) * (shape.y * weight * SETTLED_SHARE);
        if (dot(left, left) > dot(settled, settled)) settled = left;

        press = max(press, mix(press, shape.z * smoothstep(0.3, 0.9, near), pressed));
    }

    // A patch swings about whatever's holding it, or where it was left once nothing is, and what's held goes over at once: a body doesn't wait for a spring.
    vec2 centre = dot(held, held) > dot(settled, settled) ? held : settled;
    if (integrate == 1)
    {
        vec2 swing = lean - centre;
        ring(swing, velocity, WIND_TAU * touchFrequency * (0.9 + 0.2 * stiffnessHere), touchDamping, deltaTime);
        lean = centre + swing;
    }
    if (dot(held, held) > dot(lean, lean))
    {
        lean = mix(lean, held, pressed);
        velocity *= 1.0 - pressed;
    }

    out_Bend = bend;
    out_Touch = vec4(lean, press, length(velocity));
    out_Memory = vec4(velocity, settled);
}
