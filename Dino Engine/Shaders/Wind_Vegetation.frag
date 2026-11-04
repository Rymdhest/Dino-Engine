#version 420

// Steps the vegetation response to the wind. Every texel is a patch of plants that behaves like a damped
// spring: the wind's drag and the waves going by set where it wants to be (vegetationTargetBend), and the spring
// lags behind them, a little past and back. It is close to critically damped: any less and gusts that happen to
// match its frequency ring it, and the field shivers. Contacts (feet, impacts) flatten plants into a layer that
// recovers slowly.
//
// out_Bend:    xy bend angle (radians, leaning towards +x / +z), zw angular velocity (rad/s)
// out_Contact: xy flattening by contacts (radians, same convention), zw unused

#include Wind.glsl

layout (location = 0) out vec4 out_Bend;
layout (location = 1) out vec4 out_Contact;

uniform sampler2D previousBend;
uniform sampler2D previousContact;
uniform sampler2D windMap;

uniform vec2 texelShift;
uniform int integrate;
uniform int resetState;
uniform float deltaTime;
uniform float naturalFrequency;
uniform float dampingRatio;
uniform float flattenRecoveryTime;

uniform int contactCount;
uniform vec4 contacts[32];
uniform float contactExponents[32];

const float STIFFNESS_PATCH = 5.0;  // Metres across a patch of plants of about the same stiffness.
const float CONTACT_RATE = 30.0;    // Per second: how fast plants go down under a contact.

vec2 clampLength(vec2 v, float maxLength)
{
    float len = length(v);
    return len > maxLength ? v * (maxLength / len) : v;
}

float cornerRandom(ivec2 cell)
{
    return float(windHash(uint(cell.x) + windHash(uint(cell.y))) & 0xffffu) / 65535.0;
}

// Smooth noise, 0..1: how stiff the plants are here.
float stiffnessPatch(vec2 xz)
{
    vec2 p = xz / STIFFNESS_PATCH;
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    return mix(mix(cornerRandom(c), cornerRandom(c + ivec2(1, 0)), u.x),
               mix(cornerRandom(c + ivec2(0, 1)), cornerRandom(c + ivec2(1, 1)), u.x), u.y);
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
    vec2 flatten = valid ? texelFetch(previousContact, source, 0).xy : vec2(0.0);

    vec2 rest = clampLength(vegetationTargetBend(windMap, xz) + flatten, VEGETATION_MAX_TOTAL_BEND);

    // Texels that just scrolled in (or a reset) start settled instead of springing up from upright.
    if (!valid) bend = vec4(rest, 0.0, 0.0);

    if (integrate == 1)
    {
        // Stiffness varies across the field (fixed to world position) so that it doesn't sway in lockstep, but smoothly, in
        // patches: neighbouring plants at their own pace would drift out of step with each other and the field would boil.
        float omega = WIND_TAU * naturalFrequency * (0.85 + 0.3 * stiffnessPatch(xz));
        float stiffness = omega * omega;
        float damping = 2.0 * dampingRatio * omega;

        // Implicit Euler: stable for any frame time.
        float dt = deltaTime;
        vec2 velocity = (bend.zw + dt * stiffness * (rest - bend.xy)) / (1.0 + dt * damping + dt * dt * stiffness);
        bend.xy = clampLength(bend.xy + dt * velocity, VEGETATION_MAX_TOTAL_BEND);
        bend.zw = velocity;

        flatten *= exp(-dt / max(flattenRecoveryTime, 1e-3));
    }

    for (int i = 0; i < contactCount; i++)
    {
        vec4 contact = contacts[i];
        vec2 offset = xz - contact.xy;
        float dist = length(offset);
        if (contact.z <= 0.0 || dist >= contact.z) continue;
        float push = contact.w * pow((contact.z - dist) / contact.z, contactExponents[i]);
        vec2 contactBend = clampLength((dist > 1e-4 ? offset / dist : vec2(0.0)) * push, VEGETATION_MAX_TOTAL_BEND);

        // Plants pressed further than they already are go down quickly, and stay flattened while they recover.
        if (dot(contactBend, contactBend) > dot(flatten, flatten)) flatten = contactBend;
        if (dot(contactBend, contactBend) > dot(bend.xy, bend.xy))
        {
            float pressed = 1.0 - exp(-deltaTime * CONTACT_RATE);
            bend.xy = mix(bend.xy, contactBend, pressed);
            bend.zw *= 1.0 - pressed;
        }
    }

    out_Bend = bend;
    out_Contact = vec4(flatten, 0.0, 0.0);
}
