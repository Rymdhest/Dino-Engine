#version 430

// A particle's picture, from the sorted keys, farthest first: four corners an instance with no vertex data. Everything that holds for the whole particle is worked out here
// once and handed on flat: its curves at its age, which flipbook frames it's between, its six lights. Something smaller than a pixel or two is drawn that big and that much
// fainter, as rain is, or it would sparkle as it crossed the pixel grid.

#include Globals.glsl
#include Particles/Particle.glsl
#include Particles/ParticleFlipbooks.glsl
#include Halls.glsl

layout(std430, binding = 0) readonly buffer ParticleBuffer {
    Particle particles[];
};
layout(std430, binding = 1) readonly buffer StyleBuffer {
    ParticleStyleData styles[];
};
layout(std430, binding = 2) readonly buffer KeyBuffer {
    uvec2 keys[];
};
layout(std430, binding = 3) readonly buffer LightingBuffer {
    uvec4 lighting[];
};

uniform sampler2D particleCurves;
uniform float curveRows;
uniform float emissionScale;
uniform float particleTime;
uniform int drawPass;       // 0 what lies on the ground, drawn first with no depth test; 1 everything else

out vec3 vQuad;             // xy where in its picture, -1 to 1; z how far in front of the eye (m)
flat out vec4 vTint;        // rgb its colour now, a how much of it there is now
flat out vec4 vLife;        // x share of its life gone, y its seed, z its sprite, w its flame shape or pattern
flat out vec4 vBook;        // x the first frame's layer, y the next's, z how far between them, w erosion
flat out vec4 vGlow;        // x how strongly it glows, y its heat, z its palette row, w how much its glow hides
flat out vec4 vLight0;
flat out vec4 vLight1;
flat out vec4 vLight2;
flat out vec4 vLight3;
flat out vec4 vLight4;
flat out vec4 vLight5;      // rgb the light from all round
flat out vec4 vFrameA;      // centre, half its width
flat out vec4 vFrameB;      // right, half its height
flat out vec4 vFrameC;      // up, softness (m)

const float MIN_PIXELS = 1.6;

void hide()
{
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    vQuad = vec3(0.0);
    vTint = vec4(0.0);
    vLife = vec4(0.0);
    vBook = vec4(0.0);
    vGlow = vec4(0.0);
    vLight0 = vec4(0.0);
    vLight1 = vec4(0.0);
    vLight2 = vec4(0.0);
    vLight3 = vec4(0.0);
    vLight4 = vec4(0.0);
    vLight5 = vec4(0.0);
    vFrameA = vec4(0.0);
    vFrameB = vec4(0.0);
    vFrameC = vec4(0.0);
}

void main()
{
    uvec2 key = keys[gl_InstanceID];
    if (key.x == PARTICLE_DEAD_KEY)
    {
        hide();
        return;
    }
    uint index = key.y;
    Particle p = particles[index];
    if (!(p.life > 0.0) || p.age >= p.life)
    {
        hide();
        return;
    }

    uint styleIndex = p.style & PARTICLE_STYLE_MASK;
    ParticleStyleData s = styles[styleIndex];
    int sprite = int(s.look.x + 0.5);
    int facing = int(s.look.y + 0.5);
    float t = p.age / p.life;
    vec4 curveA = particleCurveA(particleCurves, styleIndex, t, curveRows);
    vec4 curveB = particleCurveB(particleCurves, styleIndex, t, curveRows);

    float size = p.size * curveB.x;
    bool landed = (p.style & PARTICLE_LANDED) != 0u;
    if (landed)
    {
        // A drop spreads into a splat in the moment after it lands.
        float since = p.age - p.spin;
        size *= mix(1.0, s.contact.w, 1.0 - exp(-since * 14.0));
        sprite = SPRITE_DECAL;
    }
    bool lying = facing == FACING_GROUND || landed;
    if (size <= 0.0 || lying != (drawPass == 0))
    {
        hide();
        return;
    }

    ParticleFrame f = particleFrame(p, facing, sprite, size, s.draw.w, s.draw.y);

    // Never under a pixel or two across: what's smaller is drawn that big, and fainter by as much as it grew.
    float depth = max(-(viewMatrix * vec4(f.centre, 1.0)).z, 0.05);
    float pixelsPerMetre = 0.5 * resolution.y * projectionMatrix[1][1] / depth;
    float across = 2.0 * f.halfWidth * pixelsPerMetre;
    float faint = 1.0;
    if (across < MIN_PIXELS)
    {
        float grow = MIN_PIXELS / max(across, 1e-3);
        f.halfWidth *= grow;
        if (facing == FACING_STRETCHED || facing == FACING_SEGMENT)
        {
            faint = 1.0 / grow;
        }
        else
        {
            f.halfHeight *= grow;
            faint = 1.0 / (grow * grow);
        }
    }

    int corner = gl_VertexID & 3;
    vec2 q = vec2((corner & 1) == 0 ? -1.0 : 1.0, (corner >> 1) == 0 ? -1.0 : 1.0);
    vec3 world = f.centre + f.right * (q.x * f.halfWidth) + f.up * (q.y * f.halfHeight);
    // Nearer the eye along its own line of sight, which moves it in depth and nowhere on screen.
    world += normalize(viewPosWorld - world) * s.book.w;
    gl_Position = projectionMatrix * viewMatrix * vec4(world, 1.0);
    vQuad = vec3(q, -(viewMatrix * vec4(world, 1.0)).z);

    // A glow that won't hold still, each on a clock of its own.
    float flicker = 1.0;
    if (s.book.z > 0.0)
    {
        float clock = particleTime * (7.0 + p.seed * 5.0) + p.seed * 40.0;
        float wave = sin(clock) * 0.5 + sin(clock * 2.3 + p.seed * 9.0) * 0.3 + sin(clock * 5.1) * 0.2;
        flicker = 1.0 - s.book.z * (wave * 0.5 + 0.5);
    }

    // How much of it there is, and how strongly that glows. Whatever's in what the open hall has cut away goes with it (Halls.glsl): its chimney's smoke.
    float kept = 1.0 - cutByHallSoft(f.centre, 3.0);
    vTint = vec4(curveA.rgb * p.tint.rgb, curveA.a * p.tint.a * faint) * kept;
    vLife = vec4(t, p.seed, float(sprite), s.book.x);
    vGlow = vec4(curveB.y * flicker * emissionScale * kept, curveB.z, s.look.z, s.draw.z);

    // Which frames it's between. What goes on plays at its rate from a place of its own; what plays once plays over its life.
    float erosion = curveB.w;
    vBook = vec4(0.0, 0.0, 0.0, erosion);
    if (sprite == SPRITE_FLAME)
    {
        int shape = int(s.book.x + 0.5);
        if (shape == 2)
        {
            int variant = min(int(p.seed * float(FLAME_TONGUES)), FLAME_TONGUES - 1);
            float frame = t * float(FLAME_TONGUE_FRAMES - 1);
            float first = floor(frame);
            float base = float(FLAME_TONGUE_FIRST + variant * FLAME_TONGUE_FRAMES);
            vBook.xyz = vec3(base + first, base + min(first + 1.0, float(FLAME_TONGUE_FRAMES - 1)), frame - first);
        }
        else
        {
            float frame = p.seed * float(FLAME_LOOP_FRAMES) + p.age * s.book.y;
            float first = mod(floor(frame), float(FLAME_LOOP_FRAMES));
            float base = shape == 1 ? float(FLAME_WICK_FIRST) : float(FLAME_HEARTH_FIRST);
            vBook.xyz = vec3(base + first, base + mod(first + 1.0, float(FLAME_LOOP_FRAMES)), fract(frame));
        }
    }
    else if (sprite == SPRITE_SMOKE)
    {
        int variant = min(int(p.seed * float(PUFF_VARIANTS)), PUFF_VARIANTS - 1);
        float frame = (s.book.y > 0.0 ? min(p.age * s.book.y, float(PUFF_FRAMES - 1)) : t * float(PUFF_FRAMES - 1));
        float first = floor(frame);
        float base = float(variant * PUFF_FRAMES);
        vBook.xyz = vec3(base + first, base + min(first + 1.0, float(PUFF_FRAMES - 1)), frame - first);
    }

    if (s.look.w > 0.5)
    {
        ParticleLighting l = particleUnpackLighting(lighting[index * 3u], lighting[index * 3u + 1u], lighting[index * 3u + 2u]);
        vLight0 = vec4(l.light[0], l.light[1].r);
        vLight1 = vec4(l.light[1].gb, l.light[2].rg);
        vLight2 = vec4(l.light[2].b, l.light[3]);
        vLight3 = vec4(l.light[4], l.light[5].r);
        vLight4 = vec4(l.light[5].gb, l.sun, 1.0);
        vLight5 = vec4(l.ambient, 0.0);
    }
    else
    {
        vLight0 = vec4(0.0);
        vLight1 = vec4(0.0);
        vLight2 = vec4(0.0);
        vLight3 = vec4(0.0);
        vLight4 = vec4(0.0);
        vLight5 = vec4(0.0);
    }

    // A decal paints a patch of the ground the size the particle is, not its card, which stands off the ground and is bigger.
    float softness = s.draw.x * size;
    vFrameA = vec4(f.centre, sprite == SPRITE_DECAL ? size * 0.5 : f.halfWidth);
    vFrameB = vec4(f.right, sprite == SPRITE_DECAL ? size * 0.5 : f.halfHeight);
    vFrameC = vec4(f.up, softness);
}
