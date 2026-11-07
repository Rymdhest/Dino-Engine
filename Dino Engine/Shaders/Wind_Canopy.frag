#version 420

// Steps what trees do in the wind. Every texel is a metre of forest behaving as one heavy spring: as Wind_Vegetation.frag, but much slower and with less to
// hold it back, as a trunk has too much mass to answer a gust at once, and goes on rocking when one has passed. It takes the same wind and the same waves,
// so that a tree and the grass under it move with the same event, and a blast shows as a ring going out through the wood, tree by tree.
//
// out_Canopy: xy lean (radians, towards +x / +z), zw how fast it's changing (rad/s)

#include Wind.glsl

layout (location = 0) out vec4 out_Canopy;

uniform sampler2D previousCanopy;
uniform sampler2D windMap;

uniform vec2 texelShift;
uniform int resetState;
uniform float deltaTime;
uniform float naturalFrequency;
uniform float dampingRatio;

const float STIFFNESS_PATCH = 24.0;     // Metres across a stand of trees of about the same stiffness.

void main(void)
{
    ivec2 size = textureSize(previousCanopy, 0);
    ivec2 texel = ivec2(gl_FragCoord.xy);
    vec2 xz = canopyRegion.xy + (vec2(texel) + 0.5) * (canopyRegion.zw / vec2(size));

    ivec2 source = texel + ivec2(round(texelShift));
    bool valid = resetState == 0 && all(greaterThanEqual(source, ivec2(0))) && all(lessThan(source, size));
    vec2 rest = vegetationTargetBend(windMap, xz);
    vec4 canopy = valid ? texelFetch(previousCanopy, source, 0) : vec4(rest, 0.0, 0.0);

    float omega = WIND_TAU * naturalFrequency * (0.85 + 0.3 * vegetationStiffness(xz, STIFFNESS_PATCH));
    float stiffness = omega * omega;
    float damping = 2.0 * dampingRatio * omega;

    // Implicit Euler: stable for any frame time.
    float dt = deltaTime;
    vec2 velocity = (canopy.zw + dt * stiffness * (rest - canopy.xy)) / (1.0 + dt * damping + dt * dt * stiffness);
    out_Canopy = vec4(canopy.xy + dt * velocity, velocity);
}
