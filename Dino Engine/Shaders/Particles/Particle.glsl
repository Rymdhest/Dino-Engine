#ifndef DINO_PARTICLE_GLSL
#define DINO_PARTICLE_GLSL

// What every particle pass shares: a particle as the GPU keeps it, its style, its curves, and the frame its picture lies in. Needs Globals.glsl.

// One particle. Must match GpuParticle (Particles/ParticlePool.cs): std430, 96 bytes.
struct Particle {
    vec3 position;
    float age;          // Seconds.
    vec3 velocity;
    float life;         // Seconds in all; below 0 once it's gone early.
    vec3 axis;          // Where a Segment ends, from position. The ground's normal, once it has landed.
    float size;         // Metres across, at birth.
    float angle;
    float spin;         // Radians a second. The age it landed at, once it has.
    float seed;
    uint style;         // Its place in the style table, and PARTICLE_LANDED.
    vec4 ground;        // The plane of the ground under where it was born (normal, distance), or 0.
    vec4 tint;          // rgb times its colour, a times its strength.
};

// A style. Must match ParticleRenderer.UploadStyles: std430, six vec4s.
struct ParticleStyleData {
    vec4 motion;        // x drag (1/s), y gravity (m/s2), z buoyancy (m/s2), w 1 / buoyancy fade (1/s)
    vec4 air;           // x turbulence (m/s), y 1 / turbulence scale (1/m), z share of the wind, w spin drag (1/s)
    vec4 contact;       // x contact, y bounce, z friction, w spread on landing
    vec4 look;          // x sprite, y facing, z palette row, w 1 if lit
    vec4 draw;          // x softness, y stretch (s), z cover, w how far an Upright one stands up
    vec4 book;          // x flame shape or decal pattern, y frame rate, z flicker, w metres drawn towards the eye
};

const uint PARTICLE_STYLE_MASK = 0xFFFFu;
const uint PARTICLE_LANDED = 0x10000u;
const uint PARTICLE_DEAD_KEY = 0xFFFFFFFFu;

// ParticleSprite, ParticleFacing and ParticleContact, in their order.
const int SPRITE_GLOW = 0;
const int SPRITE_STREAK = 1;
const int SPRITE_GLINT = 2;
const int SPRITE_SMOKE = 3;
const int SPRITE_FLAME = 4;
const int SPRITE_DROPLET = 5;
const int SPRITE_ARC = 6;
const int SPRITE_DECAL = 7;

const int FACING_CAMERA = 0;
const int FACING_UPRIGHT = 1;
const int FACING_STRETCHED = 2;
const int FACING_GROUND = 3;
const int FACING_SEGMENT = 4;

// A flame's frame is this much taller than it is wide: FLAME_HEIGHT / FLAME_WIDTH in ParticleFlipbooks.glsl.
const float FLAME_ASPECT = 1.5;
// How far an arc's card reaches either side of it, in its widths: room for its glow.
const float ARC_GLOW = 3.0;
// How far an Upright particle leans for each metre a second it's moving sideways.
const float UPRIGHT_LEAN = 0.25;

const int CONTACT_NONE = 0;
const int CONTACT_BOUNCE = 1;
const int CONTACT_STICK = 2;
const int CONTACT_VANISH = 3;

// How far above or below the ground a surface can be and still be painted: high enough for the tips of tall grass, and no higher, so it doesn't climb whoever stands in it.
const float DECAL_THICKNESS = 1.6;
// How far a decal's card reaches past it at most, in DECAL_THICKNESS, for an eye looking along the ground (see particleFrame).
const float DECAL_MOST_SLANT = 2.5;

// A particle's two rows of curves: colour and opacity, then size, emission, heat and erosion. ParticleRenderer.CurveRows rows of Curve.Samples steps.
vec4 particleCurveA(sampler2D curves, uint style, float t, float rows)
{
    vec2 uv = vec2((0.5 + clamp(t, 0.0, 1.0) * 63.0) / 64.0, (float(style * 2u) + 0.5) / rows);
    return textureLod(curves, uv, 0.0);
}

vec4 particleCurveB(sampler2D curves, uint style, float t, float rows)
{
    vec2 uv = vec2((0.5 + clamp(t, 0.0, 1.0) * 63.0) / 64.0, (float(style * 2u + 1u) + 0.5) / rows);
    return textureLod(curves, uv, 0.0);
}

// Floats as unsigned integers that sort in the same order.
uint particleSortKey(float value)
{
    uint bits = floatBitsToUint(value);
    return (bits & 0x80000000u) != 0u ? ~bits : bits | 0x80000000u;
}

// The plane a particle's picture lies in: its middle, the ways across and up it, the way it faces, and half its width and height. Six-way lighting is worked out in this frame
// (right +x, up +y, facing +z), so the compute pass and the vertex shader have to build it the same way.
struct ParticleFrame {
    vec3 centre;
    vec3 right;
    vec3 up;
    vec3 normal;
    float halfWidth;
    float halfHeight;
};

vec3 particleGroundNormal(Particle p)
{
    if ((p.style & PARTICLE_LANDED) != 0u) return p.axis;
    return dot(p.ground.xyz, p.ground.xyz) > 0.5 ? p.ground.xyz : vec3(0.0, 1.0, 0.0);
}

ParticleFrame particleFrame(Particle p, int facing, int sprite, float size, float stand, float stretch)
{
    ParticleFrame f;
    vec3 eye = viewPosWorld;
    vec3 camRight = invViewMatrix[0].xyz;
    vec3 camUp = invViewMatrix[1].xyz;
    vec3 camBack = invViewMatrix[2].xyz;
    float c = cos(p.angle);
    float s = sin(p.angle);
    f.centre = p.position;
    f.halfWidth = size * 0.5;
    f.halfHeight = size * 0.5 * (sprite == SPRITE_FLAME ? FLAME_ASPECT : 1.0);

    if ((p.style & PARTICLE_LANDED) != 0u) facing = FACING_GROUND;

    if (facing == FACING_UPRIGHT)
    {
        // Its top up the screen and towards the sky, standing on where it is: a flame burns up from its fuel. Square to the eye, a card would lie nearly flat under a camera
        // this high and its flames would sink into the ground they stand on, so it stands part of the way up from that towards upright, and is seen a little foreshortened.
        // It leans with the air it's in (its axis, Particle_Simulate.comp) and the way it's going, as a tilt: what's seen from straight above has no up of its own to
        // lean, and the lean alone would lay it on its side.
        vec3 toEye = normalize(eye - p.position);
        vec3 lean = p.axis + p.velocity;
        vec3 up = normalize(mix(camUp, vec3(0.0, 1.0, 0.0), stand) + vec3(lean.x, 0.0, lean.z) * UPRIGHT_LEAN);
        // A little lean from its angle, so that no two stand quite alike.
        vec3 side = normalize(cross(up, toEye));
        up = normalize(up + side * (sin(p.angle) * 0.12));
        f.right = normalize(cross(up, toEye));
        f.up = up;
        f.normal = normalize(cross(f.right, f.up));
        f.centre = p.position + f.up * f.halfHeight;
    }
    else if (facing == FACING_STRETCHED)
    {
        // Along the way it's going as the eye sees it, drawn out over the last moment of its path, as a fast spark is a line to the eye.
        vec3 toEye = normalize(eye - p.position);
        vec3 across = p.velocity - toEye * dot(p.velocity, toEye);
        float speed = length(across);
        float trail = speed * stretch;
        vec3 up = speed > 1e-4 ? across / speed : camUp;
        f.right = normalize(cross(up, toEye));
        f.up = up;
        f.normal = toEye;
        f.halfHeight = 0.5 * (size + trail);
        f.centre = p.position - up * (trail * 0.5);
    }
    else if (facing == FACING_GROUND)
    {
        vec3 n = particleGroundNormal(p);
        vec3 t0 = abs(n.z) < 0.99 ? normalize(cross(n, vec3(0.0, 0.0, 1.0))) : vec3(1.0, 0.0, 0.0);
        vec3 t1 = cross(n, t0);
        f.right = t0 * c + t1 * s;
        f.up = cross(n, f.right);
        f.normal = n;
        f.centre = p.position + n * (sprite == SPRITE_DECAL ? 0.0 : 0.03);
        if (sprite == SPRITE_DECAL)
        {
            // A decal is drawn with no depth test and paints whatever the depth buffer holds within DECAL_THICKNESS of the ground, grass tips included. The eye sees
            // what's off the ground moved along it by as far off it as that is, times the slant it's seen at, so the card, lying on the ground, reaches out that far
            // and no further: its fragments are what the pass costs. Seen nearly edge on that would be without end, and what's cut off then is grass standing in
            // front of what's painted, which may well hide it.
            float square = max(abs(dot(normalize(eye - p.position), n)), 0.05);
            float margin = DECAL_THICKNESS * min(sqrt(1.0 - square * square) / square, DECAL_MOST_SLANT);
            f.halfWidth += margin;
            f.halfHeight += margin;
        }
    }
    else if (facing == FACING_SEGMENT)
    {
        // From where it is to where its axis ends, square to the eye about that line, wide enough for its glow and its jags.
        vec3 middle = p.position + p.axis * 0.5;
        vec3 toEye = normalize(eye - middle);
        vec3 across = p.axis - toEye * dot(p.axis, toEye);
        float length_ = length(across);
        vec3 up = length_ > 1e-4 ? across / length_ : camUp;
        f.right = normalize(cross(up, toEye));
        f.up = up;
        f.normal = toEye;
        f.centre = middle;
        f.halfHeight = length_ * 0.5 + size;
        f.halfWidth = size * ARC_GLOW;
    }
    else
    {
        f.right = camRight * c + camUp * s;
        f.up = camUp * c - camRight * s;
        f.normal = camBack;
    }
    return f;
}

// The light reaching a particle, packed in halves: what comes from each of six ways of its frame (+x, -x, +y, -y, +z, -z), which is the sun and local lights; what
// comes from all round, the sky above and the ground below, as the side facing the eye sees them; and how much sun gets to it.
struct ParticleLighting {
    vec3 light[6];
    vec3 ambient;
    float sun;
};

void particlePackLighting(ParticleLighting l, out uvec4 a, out uvec4 b, out uvec4 c)
{
    a = uvec4(packHalf2x16(l.light[0].xy), packHalf2x16(vec2(l.light[0].z, l.light[1].x)), packHalf2x16(l.light[1].yz), packHalf2x16(l.light[2].xy));
    b = uvec4(packHalf2x16(vec2(l.light[2].z, l.light[3].x)), packHalf2x16(l.light[3].yz), packHalf2x16(l.light[4].xy), packHalf2x16(vec2(l.light[4].z, l.light[5].x)));
    c = uvec4(packHalf2x16(l.light[5].yz), packHalf2x16(l.ambient.xy), packHalf2x16(vec2(l.ambient.z, l.sun)), 0u);
}

ParticleLighting particleUnpackLighting(uvec4 a, uvec4 b, uvec4 c)
{
    ParticleLighting l;
    vec2 a0 = unpackHalf2x16(a.x), a1 = unpackHalf2x16(a.y), a2 = unpackHalf2x16(a.z), a3 = unpackHalf2x16(a.w);
    vec2 b0 = unpackHalf2x16(b.x), b1 = unpackHalf2x16(b.y), b2 = unpackHalf2x16(b.z), b3 = unpackHalf2x16(b.w);
    vec2 c0 = unpackHalf2x16(c.x), c1 = unpackHalf2x16(c.y), c2 = unpackHalf2x16(c.z);
    l.light[0] = vec3(a0, a1.x);
    l.light[1] = vec3(a1.y, a2);
    l.light[2] = vec3(a3, b0.x);
    l.light[3] = vec3(b0.y, b1);
    l.light[4] = vec3(b2, b3.x);
    l.light[5] = vec3(b3.y, c0);
    l.ambient = vec3(c1, c2.x);
    l.sun = c2.y;
    return l;
}

#endif
