#ifndef DINO_GRASS_BLADE_GLSL
#define DINO_GRASS_BLADE_GLSL

// Wind deformation of grass blades, shared by Grass.vert and Grass_Shadow.vert so blades and their shadows
// always bend identically. Needs Globals.glsl (time) and Wind.glsl included first.

const float GRASS_MAX_BEND = 1.55;
const float GRASS_TAU = 6.28318530718;

// How far a blade leans in the wind (radians, towards +x / +z): the simulated sway of its patch plus its own
// flutter. Flutter is the fast shimmer of single blades in turbulent air; it grows with the wind speed and each
// blade has its own frequency and phase.
vec2 grassWindBend(sampler2D bendMap, sampler2D windMap, vec2 xz, float bladeRandom)
{
    vec2 bend = vegetationBend(bendMap, windMap, xz);

    vec2 wind = sampleWindMap(windMap, xz).xz;
    float speed = length(wind);
    if (speed > 1e-3 && vegetationParams.z > 0.0 && vegetationParams.y > 0.0)
    {
        vec2 along = wind / speed;
        vec2 across = vec2(-along.y, along.x);
        float amplitude = vegetationParams.z * min(speed / vegetationParams.y, 1.5);
        float frequency = 2.0 + 2.0 * bladeRandom;
        float wave = time * frequency * GRASS_TAU + bladeRandom * 43.0;
        bend += along * (amplitude * sin(wave)) + across * (amplitude * 0.4 * sin(wave * 1.31 + 1.7));
    }
    return bend;
}

// Places a vertex of a blade standing at `base` and bent by `bend` (radians, towards +x / +z). The blade bends
// along a circular arc, which keeps its length exact however far it bends.
// t: 0 at the base to 1 at the tip. side: horizontal unit vector across the unbent blade.
// localOffset.x / .z: offset across / out of the blade's plane. localNormal: normal in (across, along, out) space.
void bendGrassVertex(vec3 base, float height, float t, vec2 bend, vec3 side, vec3 localOffset, vec3 localNormal,
                     out vec3 position, out vec3 normal)
{
    float angle = length(bend);
    vec3 bendDirection = angle > 1e-6 ? vec3(bend.x, 0.0, bend.y) / angle : vec3(0.0);
    angle = min(angle, GRASS_MAX_BEND);

    float arcAngle = angle * t;
    float alongUp;
    float alongBend;
    if (angle > 1e-4)
    {
        alongUp = sin(arcAngle) / angle;
        alongBend = (1.0 - cos(arcAngle)) / angle;
    }
    else
    {
        alongUp = t;
        alongBend = 0.5 * angle * t * t;
    }
    vec3 centre = base + height * (vec3(0.0, alongUp, 0.0) + bendDirection * alongBend);
    vec3 tangent = vec3(0.0, cos(arcAngle), 0.0) + bendDirection * sin(arcAngle);

    // The blade's cross section follows the arc: `across` stays as close to the original side as the tangent allows.
    vec3 across = side - tangent * dot(side, tangent);
    float acrossLength = length(across);
    across = acrossLength > 1e-4 ? across / acrossLength : normalize(cross(tangent, vec3(0.0, 0.0, 1.0)) + vec3(1e-4, 0.0, 0.0));
    vec3 outOfPlane = cross(across, tangent);

    position = centre + across * localOffset.x + outOfPlane * localOffset.z;
    normal = normalize(across * localNormal.x + tangent * localNormal.y + outOfPlane * localNormal.z);
}

#endif
