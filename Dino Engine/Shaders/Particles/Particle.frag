#version 430

// What a particle looks like at a pixel, as premultiplied light and how much of what's behind it it hides: a glow hides little, smoke a lot. Blended (one, one minus source alpha)
// over the lit scene, farthest first.
// - Lit particles take the six lights their frame was given (Particle_Simulate.comp). Smoke reads them through its six-way lightmaps, so a puff's sunlit side is the one facing the
//   sun and a fire below lights it from underneath; a droplet reads them about a sphere.
// - Glowing ones look their heat up in their palette, and hide a share of what's behind them (cover), as light alone goes to white over a sunlit field.
// - Soft particles fade where they meet the scene's depth, and decals paint the scene's surface under them, so neither shows a hard line where it meets the ground.

#include Globals.glsl
#include gBufferUtil.glsl
#include Particles/Particle.glsl
#include Particles/ParticleFlipbooks.glsl
#include Particles/ParticleNoise.glsl

in vec3 vQuad;
flat in vec4 vTint;
flat in vec4 vLife;
flat in vec4 vBook;
flat in vec4 vGlow;
flat in vec4 vLight0;
flat in vec4 vLight1;
flat in vec4 vLight2;
flat in vec4 vLight3;
flat in vec4 vLight4;
flat in vec4 vLight5;
flat in vec4 vFrameA;
flat in vec4 vFrameB;
flat in vec4 vFrameC;

layout (location = 0) out vec4 out_Colour;

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D palettes;
uniform float paletteRows;
uniform sampler2DArray flameBook;
uniform sampler2DArray puffBookA;
uniform sampler2DArray puffBookB;
uniform vec3 sunTowards;
uniform vec3 sunIrradiance;
uniform float particleTime;
uniform float hazeDensity;
uniform vec3 skyLight;
uniform vec3 groundLight;

const float PI = 3.14159265;

vec3 palette(float heat)
{
    return textureLod(palettes, vec2((0.5 + clamp(heat, 0.0, 1.0) * 255.0) / 256.0, (vGlow.z + 0.5) / paletteRows), 0.0).rgb;
}

// The six lights: from +x, -x, +y, -y, +z and -z of the particle's frame.
vec3 lightPX() { return vLight0.xyz; }
vec3 lightNX() { return vec3(vLight0.w, vLight1.xy); }
vec3 lightPY() { return vec3(vLight1.zw, vLight2.x); }
vec3 lightNY() { return vLight2.yzw; }
vec3 lightPZ() { return vLight3.xyz; }
vec3 lightNZ() { return vec3(vLight3.w, vLight4.xy); }
float sunSeen() { return vLight4.z; }
bool isLit() { return vLight4.w > 0.5; }
vec3 ambient() { return vLight5.rgb; }

// What a white surface facing n would show: the sun and local lights from where they are, and the sky and ground all round.
vec3 lightFacing(vec3 n)
{
    vec3 w = n * n;
    return w.x * (n.x >= 0.0 ? lightPX() : lightNX()) + w.y * (n.y >= 0.0 ? lightPY() : lightNY()) + w.z * (n.z >= 0.0 ? lightPZ() : lightNZ()) + ambient();
}

// What a speck of something lit shows: mostly its face, and some of what comes from either side.
vec3 lightOfMote()
{
    return lightPZ() * 0.5 + (lightPX() + lightNX() + lightPY() + lightNY()) * 0.125 + ambient();
}

vec3 frameNormal()
{
    return normalize(cross(vFrameB.xyz, vFrameC.xyz));
}

// Light that glows, and what it hides, from how much of it there is here.
vec4 glowing(float amount, float heat)
{
    vec3 light = palette(heat) * vTint.rgb * (vGlow.x * amount * vTint.a);
    return vec4(light, vGlow.w * amount * vTint.a);
}

// Something lit that's this dense here, of its colour.
vec4 litMote(float amount)
{
    float alpha = amount * vTint.a;
    vec3 light = vTint.rgb * lightOfMote() * alpha;
    if (vGlow.x > 0.0) light += palette(vGlow.y) * (vGlow.x * alpha);
    return vec4(light, alpha);
}

vec4 shadeGlow(vec2 q)
{
    float r2 = dot(q, q);
    if (r2 >= 1.0) discard;
    float shape = (exp(-r2 * 4.0) + exp(-r2 * 18.0) * 0.6) * (1.0 - r2);
    return isLit() ? litMote(min(shape, 1.0)) : glowing(shape, vGlow.y * mix(0.7, 1.0, exp(-r2 * 10.0)));
}

vec4 shadeStreak(vec2 q)
{
    float across = exp(-q.x * q.x * 4.0) * (1.0 - q.x * q.x);
    float along = 1.0 - smoothstep(0.6, 1.0, abs(q.y));
    float head = mix(0.35, 1.0, q.y * 0.5 + 0.5);
    float shape = across * along * head;
    return isLit() ? litMote(shape) : glowing(shape, vGlow.y * mix(0.75, 1.0, head));
}

vec4 shadeGlint(vec2 q)
{
    float r2 = dot(q, q);
    float rays = exp(-abs(q.x) * 16.0) * (1.0 - abs(q.y)) + exp(-abs(q.y) * 16.0) * (1.0 - abs(q.x));
    float core = exp(-r2 * 22.0);
    float twinkle = 0.5 + 0.5 * sin(particleTime * (4.0 + vLife.y * 9.0) + vLife.y * 31.0);
    return glowing((core * 1.4 + rays * 0.6) * mix(0.35, 1.0, twinkle), vGlow.y);
}

// A bead of liquid, as a sphere: lit by the six lights, the sky at its rim, and the sun caught in it.
vec4 shadeDroplet(vec2 q)
{
    float r2 = dot(q, q);
    if (r2 >= 1.0) discard;
    vec3 n = vec3(q, sqrt(1.0 - r2));
    vec3 right = vFrameB.xyz;
    vec3 up = vFrameC.xyz;
    vec3 sun = vec3(dot(sunTowards, right), dot(sunTowards, up), dot(sunTowards, cross(right, up)));
    float fresnel = 0.04 + 0.96 * pow(1.0 - n.z, 5.0);
    vec3 halfway = normalize(sun + vec3(0.0, 0.0, 1.0));
    float highlight = pow(max(dot(n, halfway), 0.0), 90.0) * 9.0;
    vec3 light = vTint.rgb * lightFacing(n) * (1.0 - fresnel) + lightFacing(reflect(vec3(0.0, 0.0, -1.0), n)) * fresnel + sunIrradiance * (sunSeen() * highlight * 0.03);
    float alpha = smoothstep(1.0, 0.75, r2) * vTint.a;
    return vec4(light * alpha, alpha);
}

vec4 shadeSmoke(vec2 q)
{
    vec2 uv = q * 0.5 + 0.5;
    // The puff swells from one frame to the next, so each is read where what's here was in it.
    float frames = float(PUFF_FRAMES - 1);
    float grow = puffRadius(mod(vBook.y, float(PUFF_FRAMES)) / frames) / puffRadius(mod(vBook.x, float(PUFF_FRAMES)) / frames);
    float between = mix(1.0, grow, vBook.z);
    vec2 uv0 = 0.5 + (uv - 0.5) / between;
    vec2 uv1 = 0.5 + (uv - 0.5) * (grow / between);
    vec4 a = mix(texture(puffBookA, vec3(uv0, vBook.x)), texture(puffBookA, vec3(uv1, vBook.y)), vBook.z);
    vec4 b = mix(texture(puffBookB, vec3(uv0, vBook.x)), texture(puffBookB, vec3(uv1, vBook.y)), vBook.z);

    // Smoke thins as it spreads, letting more light through everywhere at once and softening at its edges with the rest of it. What its erosion thins is the depth
    // through it (a puff's alpha is 1 - e^-depth): a puff is nearly solid in the middle, so taking the same off its alpha everywhere only left a smaller puff with a
    // harder edge, and late in its life it read as a disc cut out of card.
    float density = a.a;
    float kept = 1.0 - pow(1.0 - min(density, 0.99), 1.0 - vBook.w);
    float scale = density > 1e-4 ? kept / density : 0.0;

    // The sun and local lights each through the lightmap of the way they come from. The sky comes from above, which from up here is mostly in front of the puff, and from
    // all round its sides, and the ground from behind it: through the lightmaps of those ways, a crease or the inside of a puff that little light from any of them
    // reaches stays dark, and the edges, which only light from behind would make glow, don't.
    vec3 lit = a.r * lightPX() + b.r * lightNX() + a.g * lightPY() + b.g * lightNY() + a.b * lightPZ() + b.b * lightNZ();
    float sides = a.r + b.r + a.g + b.g;
    lit += skyLight * (a.b * 0.55 + sides * 0.42) + groundLight * (b.b * 0.3 + sides * 0.1);
    vec3 light = vTint.rgb * lit * scale;
    if (vGlow.x > 0.0)
    {
        float core = b.a / max(density, 1e-4);
        light += palette(vGlow.y * core) * (vGlow.x * kept);
    }
    return vec4(light, kept) * vTint.a;
}

vec4 shadeFlame(vec2 q)
{
    vec2 uv = q * 0.5 + 0.5;
    vec4 here = texture(flameBook, vec3(uv, vBook.x));
    vec2 flow = here.zw;
    vec4 a = texture(flameBook, vec3(uv - flow * vBook.z, vBook.x));
    vec4 b = texture(flameBook, vec3(uv + flow * (1.0 - vBook.z), vBook.y));
    vec4 m = mix(a, b, vBook.z);

    float burning = m.r;
    float temperature = m.g / max(burning, 1e-4);
    float kept = clamp((burning - vBook.w) / max(1.0 - vBook.w, 1e-3), 0.0, 1.0);
    return glowing(kept, temperature * vGlow.y);
}

// A thread of lightning down the middle of its card, jagged and flickering, a white-hot core in a glow of its colour. Pieces meet end to start, so each draws a round cap at its
// start and none at its end, or the joints would be twice as bright.
vec4 shadeArc(vec2 q)
{
    float size = vFrameA.w / ARC_GLOW;
    float halfLength = max(vFrameB.w - size, 0.0);
    float along = q.y * vFrameB.w;
    if (along > halfLength) discard;
    float across = q.x * vFrameA.w;
    float u = clamp((along + halfLength) / max(2.0 * halfLength, 1e-4), 0.0, 1.0);
    float stroke = floor(particleTime * 22.0 + vLife.y * 7.0);
    float pinned = sin(PI * u);
    float jag = (particleValueNoise(vec2(u * 5.0 + vLife.y * 37.0, stroke)) - 0.5) * 1.8 * size * pinned;
    jag += (particleValueNoise(vec2(u * 19.0 + vLife.y * 11.0, stroke * 1.7)) - 0.5) * 0.5 * size * pinned;
    vec2 offset = vec2(across - jag, along - clamp(along, -halfLength, halfLength));
    float d = length(offset);
    float core = exp(-(d * d) / (size * size * 0.015));
    float glow = exp(-d / (size * 0.9)) * (1.0 - smoothstep(size * 2.2, size * ARC_GLOW, abs(across)));
    float strobe = 0.7 + 0.3 * particleHash21(vec2(stroke, vLife.y * 97.0));
    vec3 light = (palette(min(vGlow.y + 0.25, 1.0)) * core + palette(vGlow.y * 0.7) * (glow * 0.7)) * vTint.rgb * (vGlow.x * vTint.a * strobe);
    return vec4(light, vGlow.w * (core + glow * 0.3) * vTint.a);
}

// Light off a wet surface facing n, at a point of the scene: the sun caught in it, and the sky.
vec3 wetShine(vec3 scene, vec3 n, float gloss)
{
    vec3 view = normalize(viewPosWorld - scene);
    vec3 reflected = reflect(-view, n);
    float sun = pow(max(dot(reflected, sunTowards), 0.0), 50.0) * 5.0;
    float fresnel = 0.04 + 0.96 * pow(1.0 - max(dot(view, n), 0.0), 5.0);
    return (sunIrradiance * (sun * sunSeen() * 0.04) + skyColour * fresnel) * gloss;
}

// Painted on the scene's surface under the card: where the depth buffer puts it, as a deferred decal is, so it lies on grass, stones and slopes alike. It only paints what's
// near the ground and faces up, so it neither climbs whoever stands in it nor runs up a wall.
// Chunks of char to a metre in a bed of coals.
const float COAL_CHUNKS = 2.6;

vec4 shadeDecal(float sceneDepth)
{
    if (sceneDepth >= 1.0) discard;
    vec3 scene = ReconstructWorldSpacePosition(gl_FragCoord.xy, sceneDepth, invProjectionMatrix, invViewMatrix, resolution);
    vec3 n = frameNormal();
    vec3 local = scene - vFrameA.xyz;
    vec2 d = vec2(dot(local, vFrameB.xyz) / vFrameA.w, dot(local, vFrameC.xyz) / vFrameB.w);
    float r = length(d);
    float height = dot(local, n);
    if (r > 1.0 || abs(height) >= DECAL_THICKNESS) discard;
    vec3 surface = normalize(mat3(invViewMatrix) * unCompressNormal(texelFetch(gNormal, ivec2(gl_FragCoord.xy), 0).xyz));
    float thickness = (1.0 - smoothstep(DECAL_THICKNESS * 0.65, DECAL_THICKNESS, abs(height))) * smoothstep(0.25, 0.6, dot(surface, n));
    if (thickness <= 0.0) discard;

    int pattern = int(vLife.w + 0.5);
    float seed = vLife.y * 31.0;
    float edge = particleFbm(d * 2.3 + seed, 3) - 0.5;
    vec3 ground = lightPZ();
    vec3 light = vec3(0.0);
    float alpha = 0.0;

    if (pattern == 0)
    {
        // Spilt and splashed: a blob with a ragged, wandering edge and drops flung round it, dark and wet. Its edge only wanders inwards from the card's, or the card
        // would cut it round.
        float sprawl = particleFbm(d * 1.4 + seed * 2.3, 2) - 0.5;
        float blob = smoothstep(0.97, 0.72, r + (edge * 0.6 + sprawl * 0.5 + 0.55) * 0.55);
        vec3 w = particleWorley(d * 4.5 + seed);
        float drops = smoothstep(0.17, 0.07, w.x) * step(0.5, w.z) * smoothstep(1.0, 0.55, r) * (1.0 - blob);
        alpha = max(blob, drops);
        light = vTint.rgb * (ground + ambient()) * alpha + wetShine(scene, n, 1.0) * alpha;
    }
    else if (pattern == 1)
    {
        // A bed of coals: chunks of char the same size in any bed (COAL_CHUNKS), pushed out of true so that they don't read as tiles. Some glow from inside, mostly
        // along their cracks, breathing slowly, a few have gone to ash, and the seams between them burn in broken lines, hottest in the middle.
        vec2 m = d * vFrameA.w;
        m += (vec2(particleFbm(m * 1.7 + seed, 2), particleFbm(m * 1.7 - seed, 2)) - 0.375) * 0.35;
        float bed = smoothstep(1.0, 0.45, r + edge * 0.8);
        vec3 w = particleWorley(m * COAL_CHUNKS + seed);
        vec3 fine = particleWorley(m * (COAL_CHUNKS * 2.8) + seed * 1.7);
        float gap = w.y - w.x;
        float broken = smoothstep(0.35, 0.65, particleValueNoise(m * 2.2 + seed * 0.7));
        float seam = exp(-gap * 16.0) * broken * (0.55 + 0.45 * particleValueNoise(m * 6.0 + seed));
        float breathe = 0.7 + 0.3 * sin(particleTime * (1.2 + w.z * 2.2) + w.z * 31.0);
        float cracks = exp(-(fine.y - fine.x) * 9.0);
        float inside = smoothstep(0.45, 0.95, w.z) * breathe * smoothstep(0.02, 0.3, gap) * (0.25 + 0.75 * cracks);
        float heat = vGlow.y * bed * max(seam * 0.85, inside * 0.9) * mix(0.55, 1.0, smoothstep(1.0, 0.2, r));
        float ash = smoothstep(0.14, 0.04, w.z) * smoothstep(0.03, 0.2, gap) * (0.6 + 0.4 * particleValueNoise(m * 9.0 + seed));
        vec3 surface = mix(vec3(0.028, 0.024, 0.021), vec3(0.26, 0.25, 0.24), ash);
        alpha = bed * 0.92;
        light = surface * (ground + ambient()) * alpha + palette(heat) * vTint.rgb * (vGlow.x * bed);
    }
    else if (pattern == 2)
    {
        // Frost: crystals grown along the edges of cells, finer rime between them, and a glitter of the sun.
        float patch_ = smoothstep(1.0, 0.45, r + edge * 0.8);
        vec3 w = particleWorley(d * 7.0 + seed);
        float crystals = smoothstep(0.1, 0.0, w.y - w.x) * 0.7 + particleFbm(d * 15.0 + seed, 2) * 0.45;
        alpha = clamp(patch_ * (0.3 + crystals), 0.0, 1.0);
        float cell = particleHash21(floor(d * 46.0 + seed));
        float glitter = step(0.965, cell) * (0.5 + 0.5 * sin(particleTime * 3.0 + cell * 60.0));
        light = vTint.rgb * (ground + ambient()) * alpha + sunIrradiance * (sunSeen() * glitter * 0.06 * patch_) + palette(vGlow.y * alpha) * (vGlow.x * alpha);
    }
    else if (pattern == 3)
    {
        // A pool of venom, dark and oily with the sky on it, its light only in slow veins under its skin and in the bubbles coming up through it and bursting in
        // rings: lit all over, a pool read as green paint. Its edge wanders far, as a spill's does, so no pool is a circle, and only inwards from the card's.
        float sprawl = particleFbm(d * 1.3 + seed * 1.7, 2) - 0.5;
        float pool = smoothstep(0.98, 0.7, r + (edge * 0.7 + sprawl * 0.6 + 0.65) * 0.56);
        vec3 w = particleWorley(d * 6.0 + seed);
        float phase = fract(particleTime * (0.4 + w.z * 0.5) + w.z * 7.0);
        float bubble = smoothstep(0.05, 0.0, abs(w.x - phase * 0.38)) * (1.0 - phase) * step(0.35, w.z);
        float swirl = particleFbm(d * 3.0 + vec2(particleTime * 0.2, -particleTime * 0.15) + seed, 3);
        float veins = smoothstep(0.5, 0.56, swirl) * smoothstep(0.66, 0.58, swirl);
        float heat = min(vGlow.y * (0.8 + 0.6 * bubble), 1.0);
        alpha = pool * 0.92;
        light = (vTint.rgb * (ground + ambient()) + wetShine(scene, n, 0.9)) * alpha + palette(heat) * (vGlow.x * pool * (veins * 0.7 + bubble));
    }
    else if (pattern == 4)
    {
        // A ring going out, its edge ragged, a faint wash inside it.
        float angle = atan(d.y, d.x);
        float wobble = (particleFbm(vec2(cos(angle), sin(angle)) * 2.5 + seed, 3) - 0.5) * 0.14;
        float band = exp(-pow((r - 0.8 + wobble) / 0.08, 2.0));
        float inside = smoothstep(0.85, 0.0, r) * 0.12;
        float amount = band + inside;
        light = palette(vGlow.y * mix(0.6, 1.0, band)) * vTint.rgb * (vGlow.x * amount);
        alpha = vGlow.w * amount;
    }
    else
    {
        // Burnt black, ragged at the edge.
        float burn = smoothstep(1.0, 0.3, r + edge * 0.6);
        alpha = burn * 0.85;
        light = vTint.rgb * (ground + ambient()) * alpha;
    }
    // Whatever reaches the edge of the card fades out before it, rather than being cut off round.
    return vec4(light, alpha) * (vTint.a * thickness * smoothstep(1.0, 0.88, r));
}

void main()
{
    int sprite = int(vLife.z + 0.5);
    vec2 q = vQuad.xy;
    float sceneDepth = texelFetch(gDepth, ivec2(gl_FragCoord.xy), 0).r;

    vec4 colour;
    if (sprite == SPRITE_DECAL)
    {
        colour = shadeDecal(sceneDepth);
    }
    else
    {
        if (sprite == SPRITE_GLOW) colour = shadeGlow(q);
        else if (sprite == SPRITE_STREAK) colour = shadeStreak(q);
        else if (sprite == SPRITE_GLINT) colour = shadeGlint(q);
        else if (sprite == SPRITE_SMOKE) colour = shadeSmoke(q);
        else if (sprite == SPRITE_FLAME) colour = shadeFlame(q);
        else if (sprite == SPRITE_DROPLET) colour = shadeDroplet(q);
        else colour = shadeArc(q);

        // Soft where it meets the scene.
        if (vFrameC.w > 0.0)
        {
            float sceneDistance = -ReconstructViewSpaceZ(sceneDepth, invProjectionMatrix);
            colour *= clamp((sceneDistance - vQuad.z) / vFrameC.w, 0.0, 1.0);
        }
    }

    // The same haze as lies over everything else at this distance.
    float haze = exp(-hazeDensity * vQuad.z);
    colour.rgb = colour.rgb * haze + skyColour * ((1.0 - haze) * colour.a);

    if (colour.a <= 1e-4 && max(colour.r, max(colour.g, colour.b)) <= 1e-4) discard;
    out_Colour = colour;
}
