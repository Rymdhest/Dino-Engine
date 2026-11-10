#version 420

// An open way's light (WayRenderer, WAYGATES.md), added as radiance into the multisampled scene on the depth the geometry pass left. Everything here is in the gate's own frame.
//
// The way is a window onto a throat of light bored into the gate, which narrows as it goes down to the land the way leads to, standing at its far end under its own sky. The throat is
// the opening itself carried back along its axis and drawn in towards its heart, so its walls go on from the stone with no seam, and it's worked out exactly: each ray is followed in
// oblique coordinates (where it would cross the face, and how deep it is) in which the throat's walls are the opening's own edge, and a ray's way out of that edge is a straight line,
// so finding where it strikes the wall is a march through the opening's distance field in two dimensions. That's real parallax: walk past a way and its throat turns with you.
// Its axis is mostly the camera's own forward, so from the game's camera, high and looking down, the throat is looked straight down into, and the rest of it is the way to the way,
// so that it doesn't lie along the face of a gate seen from the side. Streams pour down its walls, glowing mist hangs in it, the edge where it meets the stone burns brightest,
// ripples cross the face, and the far land at the end is grown from the same look as all of it (WayLook).

#include Globals.glsl
#include Way/WayShape.glsl
#include Way/WayNoise.glsl

in vec3 wayLocal;
in vec3 wayWorld;
in vec3 wayRuneData;
in vec2 wayRuneUV;
in vec3 wayWorldNormal;

uniform vec3 wayEye;
uniform vec3 wayAxis;
uniform vec4 wayLook[12];
uniform float emissionScale;

layout (location = 0) out vec4 out_Colour;

// WayLook, as WaySystem packs it.
#define LOOK_RIM wayLook[0].rgb
#define LOOK_ARMS wayLook[0].a
#define LOOK_DEEP wayLook[1].rgb
#define LOOK_TWIST wayLook[1].a
#define LOOK_CORE wayLook[2].rgb
#define LOOK_SPIN wayLook[2].a
#define LOOK_ACCENT wayLook[3].rgb
#define LOOK_FLOW wayLook[3].a
#define LOOK_SKY_HIGH wayLook[4].rgb
#define LOOK_SHARP wayLook[4].a
#define LOOK_SKY_LOW wayLook[5].rgb
#define LOOK_MIST wayLook[5].a
#define LOOK_SUN wayLook[6].rgb
#define LOOK_UNREST wayLook[6].a
#define LOOK_HAZE wayLook[7].rgb
#define LOOK_SEED wayLook[7].a
#define LOOK_NEAR wayLook[8].rgb
#define LOOK_SUN_HEIGHT wayLook[8].a
#define LOOK_MIDDLE wayLook[9].rgb
#define LOOK_SUN_ACROSS wayLook[9].a
#define LOOK_FAR wayLook[10].rgb
#define LOOK_SEA wayLook[10].a
#define LOOK_RUGGED wayLook[11].x
#define LOOK_HEIGHT wayLook[11].y
#define LOOK_TERRACES wayLook[11].z
#define LOOK_SNOW wayLook[11].w

// How bright each part is, as radiance in a scene lit by a sun of about 13 (exposure 0.42, then an ACES fit, bloom from 4.5). Sunlit ground is about 1.2: the throat glows a little
// brighter than that, so that it keeps its colour once mapped instead of going white, and only the edge, the light flooding in as it opens and the far sun are bright enough to bloom.
const float RIM_PEAK = 12.0;
const float RIM_SOFT = 1.5;
const float WALL = 0.6;
const float STREAM = 2.6;
const float MIST = 0.6;
const float CORE_RING = 1.3;
const float SKY = 1.5;
const float SUN_DISC = 10.0;
const float LAND = 1.0;
const float FRONT = 8.0;
const float RUNE = 3.4;
const float FLASH = 0.9;

const float STARS = 7.0;
const float END_SHARE = 0.32;       // The far end is this share of the opening.
const float STARS_AROUND = 40.0;    // Cells of stardust round the throat, a whole number so that it closes on itself.
const float STARS_ALONG = 5.0;      // and down it, for each unit of its log depth.
const float DEPTH_SCALE = 1.6;      // Metres of depth to a unit of the throat's log depth.
const float TAU = 6.2831853;

// How far a ray goes, in oblique coordinates, before it leaves the opening from p along m: a march through its distance field, which from inside a convex shape never oversteps.
// Past `limit` it never does.
float wayExit(vec2 p, vec2 m, float limit)
{
    float speed = length(m);
    if (speed < 1e-6) return limit * 2.0;
    vec2 dir = m / speed;
    float lambda = 0.0;
    float most = limit * speed;
    for (int i = 0; i < 20; i++)
    {
        float room = -wayDistance(p + dir * lambda);
        if (room < 0.004) break;
        lambda += room;
        if (lambda > most) return limit * 2.0;
    }
    return lambda / speed;
}

// A ridge line across the far land, 0 to about 1.
float wayRidge(float x, float seed, float rugged)
{
    float h = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    for (int i = 0; i < 4; i++)
    {
        float n = wayNoise1(x * frequency + seed + float(i) * 13.1);
        n = mix(n, 1.0 - abs(2.0 * n - 1.0), rugged);
        h += n * amplitude;
        amplitude *= 0.5;
        frequency *= 2.1;
    }
    return h;
}

// The land at the end of the throat, at the hour it's always seen at: v is across and up, square to the throat so that it stands up to whoever looks down it, the end about a unit
// across, and aa how much v changes across a pixel.
vec3 wayVista(vec2 v, float aa)
{
    float age = wayState.x;
    const float horizon = -0.04;

    // The sky: warm and bright along the horizon where the light comes from, deepening overhead, with the sun low or high as that land's hour has it.
    float up = clamp((v.y - horizon) / 0.9, 0.0, 1.0);
    vec3 low = mix(LOOK_SKY_LOW, LOOK_SUN, 0.35);
    vec3 sky = mix(low, LOOK_SKY_HIGH * 0.65, pow(up, 0.55)) * SKY;
    vec2 sunAt = vec2(LOOK_SUN_ACROSS * 0.75, horizon + LOOK_SUN_HEIGHT * 0.7);
    float toSun = length(v - sunAt);
    sky += LOOK_SUN * (SUN_DISC * (1.0 - smoothstep(0.032, 0.04 + aa * 1.5, toSun)) + 0.9 * exp(-toSun * 9.0) + 0.35 * exp(-toSun * 2.5));
    vec3 colour = sky;

    bool sea = LOOK_SEA > 0.2;
    float seaLine = horizon - (0.12 + 0.1 * (1.0 - LOOK_SEA)) * LOOK_HEIGHT;
    float bottom = sea ? seaLine : -10.0;

    // Three ridges, far to near, as silhouettes in the colours of that land's ground: the far ones fading into the air of its horizon, the near ones darkest and strongest,
    // and the top of each catching the sky.
    for (int layer = 0; layer < 3; layer++)
    {
        float fl = float(layer);
        float base = horizon + (0.1 - fl * 0.09) * LOOK_HEIGHT;
        float rise = (0.36 - fl * 0.08) * LOOK_HEIGHT * (0.55 + LOOK_RUGGED * 0.8);
        float h = wayRidge(v.x * (1.4 + fl * 0.9) + fl * 7.3, LOOK_SEED * 0.37 + fl * 19.1, LOOK_RUGGED * (1.0 - fl * 0.3));
        h = mix(h, floor(h * 4.0 + 0.5) * 0.25, LOOK_TERRACES * 0.8);
        float top = base + h * rise;
        float share = (1.0 - smoothstep(top - aa, top + aa, v.y)) * smoothstep(bottom - aa, bottom + aa, v.y);
        vec3 ground = layer == 0 ? LOOK_FAR : (layer == 1 ? LOOK_MIDDLE : LOOK_NEAR);
        float hazed = layer == 0 ? 0.55 : (layer == 1 ? 0.28 : 0.06);
        vec3 lit = ground * (LAND * (0.85 - fl * 0.2));
        if (layer == 0) lit = mix(lit, vec3(LAND * 1.2), LOOK_SNOW * smoothstep(top - 0.05 - 0.05 * LOOK_RUGGED, top - 0.012, v.y));
        lit += low * ((0.5 - fl * 0.12) * exp(-max(top - v.y, 0.0) / 0.018));
        colour = mix(colour, mix(lit, low * SKY, hazed), share);
    }

    // The sea under it all, the sky darkened in it, and the sun's path across it.
    if (sea)
    {
        float share = 1.0 - smoothstep(seaLine - aa, seaLine + aa, v.y);
        vec3 water = mix(low, LOOK_SKY_HIGH, 0.5) * (SKY * 0.42);
        float glitter = 0.55 + 0.45 * wayNoise(vec3(v.x * 26.0, v.y * 55.0, age * 1.4 + LOOK_SEED));
        water += LOOK_SUN * (1.8 * exp(-abs(v.x - sunAt.x) * 7.0) * glitter * smoothstep(seaLine - 0.8, seaLine, v.y));
        colour = mix(colour, water, share);
    }
    return colour;
}

// A rune's light: it flares as its turn comes and settles, and a slow pulse runs up the gate through all of them.
vec3 wayRune(vec3 data, vec2 uv)
{
    float age = wayState.x;
    float since = age - data.y * WAY_RUNES;
    float kindle = smoothstep(0.0, 0.15, since) * (1.0 + 1.8 * exp(-max(since, 0.0) * 4.0));
    float wave = 0.5 + 0.5 * sin((data.y * 2.0 - age * 0.28) * TAU);
    float flicker = 0.93 + 0.07 * sin(age * 3.1 + data.z * 40.0);
    float across = 1.0 - 0.3 * uv.x * uv.x;
    return mix(LOOK_RIM, vec3(1.0), 0.12) * (RUNE * (0.7 + 0.5 * wave * wave) * kindle * flicker * across);
}

void main()
{
    bool drawn = wayDrawn(wayLocal, wayWorld, wayRuneData);
    float age = wayState.x;
    float side = wayEye.z >= 0.0 ? 1.0 : -1.0;
    vec2 p = wayLocal.xy;
    vec2 heart = wayHeart.xy;
    float inside = -wayDistance(p);

    // Ripples running out across the face from its heart, which bend what's seen through it.
    vec3 ray0 = normalize(wayLocal - wayEye);
    float grazing = 1.0 - abs(ray0.z);
    vec2 fromHeart = p - heart;
    float r = length(fromHeart);
    float phase = r * 2.3 - age * 1.7;
    float swell = exp(-r * 0.12);
    vec3 ray = ray0;
    ray.xy += fromHeart / max(r, 1e-3) * (cos(phase) * 0.035 * swell);
    ray.z = side * min(ray.z * side, -0.03);
    ray = normalize(ray);

    // The throat, in oblique coordinates: k is how deep a ray goes for each metre of it, w how its crossing point slides across the face, and the drawing in towards the heart, which
    // makes the walls a straight line out of the opening in these coordinates, is gamma.
    float k = -side * ray.z / max(abs(wayAxis.z), 0.05);
    vec2 w = ray.xy - wayAxis.xy * k;
    float opened = smoothstep(WAY_DEEP_FROM, WAY_DEEP_TO, age);
    float depth = wayHeart.z * mix(0.06, 1.0, opened);
    float beta = (1.0 - END_SHARE) / depth;
    float gamma = beta * k;
    vec2 m = w + gamma * (p - heart);
    float sigmaEnd = depth / (k * END_SHARE);
    float sigmaExit = wayExit(p, m, sigmaEnd);
    bool wall = sigmaExit < sigmaEnd;
    float sigma = min(sigmaExit, sigmaEnd);
    float tau = sigma / (1.0 + gamma * sigma);
    float reached = k * tau;
    vec2 at = p + m * sigma;

    // The walls: a few broad streams of light spiralling down them, each of fine threads, with dark between them, pouring down and turning as they go; the throat is brightest
    // where it leaves the stone, dims in the middle and brightens again into the land's light deep down, which is what makes it read as deep.
    float share = clamp(reached / depth, 0.0, 1.0);
    float logDepth = log(1.0 + reached / DEPTH_SCALE);
    vec2 fromAxis = at - heart;
    float theta = atan(fromAxis.y, fromAxis.x);
    float twist = LOOK_TWIST * logDepth * 2.2 + LOOK_SPIN * TAU * age;
    float pour = logDepth - age * LOOK_FLOW;
    float warp = LOOK_UNREST * (wayNoise(vec3(cos(theta) * 1.3, sin(theta) * 1.3, pour * 1.7 + LOOK_SEED)) - 0.5) * 2.4;
    float turned = theta + twist + warp;
    float body = wayNoise(vec3(cos(turned) * 1.2, sin(turned) * 1.2, pour * 1.5 + LOOK_SEED * 0.7));
    float arms = 0.5 + 0.5 * cos(LOOK_ARMS * (theta + twist * 0.8) + (body - 0.5) * 2.0);
    float course = smoothstep(0.42, 0.95, arms * 0.55 + body * 0.65);
    vec3 q = vec3(cos(turned) * 2.6, sin(turned) * 2.6, pour * 3.2 + LOOK_SEED);
    float threads = pow(1.0 - abs(2.0 * wayNoise(q) - 1.0), LOOK_SHARP * 2.2);
    float pulse = 0.75 + 0.25 * sin(pour * 9.0 + theta * 2.0);
    float detail = 1.0 - smoothstep(0.55, 0.95, share);
    float dim = mix(1.0, 0.5, smoothstep(0.08, 0.6, share));
    vec3 wallColour = mix(LOOK_RIM, LOOK_DEEP, smoothstep(0.0, 0.35, share));
    vec3 streamColour = mix(LOOK_RIM, LOOK_ACCENT, smoothstep(0.3, 0.7, wayNoise(q * 0.4 + 41.0)));
    float endward = smoothstep(0.7, 1.0, share);
    vec3 wallLight = wallColour * (WALL * dim * (0.55 + 0.6 * course))
        + streamColour * (STREAM * pulse * course * (threads * mix(0.3, 1.0, detail) + 0.22) * mix(0.55, 1.0, dim) * (1.0 - endward));

    // Stardust pouring down the walls with the streams, each mote twinkling, gone deep down where it would be smaller than a pixel.
    vec2 starAt = vec2((theta / TAU + 0.5) * STARS_AROUND, pour * STARS_ALONG);
    vec2 starCell = floor(starAt);
    starCell.x = mod(starCell.x, STARS_AROUND);
    vec2 inCell = fract(starAt) - vec2(wayHash(vec3(starCell, 3.1)), wayHash(vec3(starCell, 7.7))) * 0.7 - 0.15;
    float starSeed = wayHash(vec3(starCell, 11.3));
    float twinkle = 0.55 + 0.45 * sin(age * (2.0 + starSeed * 3.0) + starSeed * 40.0);
    float stars = step(0.62, starSeed) * exp(-dot(inCell, inCell) / 0.006) * twinkle * detail;
    wallLight += mix(LOOK_RIM, vec3(1.0), 0.45) * (STARS * stars);

    // The far land, square to the throat as far again past its end as the throat is deep, so that it stands up to whoever looks down it, seen through the end, which burns round
    // its edge.
    vec3 axis = wayAxis;
    vec3 farMiddle = vec3(heart, 0.0) + axis * (depth * 2.0);
    float tauFar = dot(farMiddle - wayLocal, axis) / max(dot(ray, axis), 0.05);
    vec3 onFar = wayLocal + ray * tauFar - farMiddle;
    vec3 farUp = normalize(vec3(0.0, 1.0, 0.0) - axis * axis.y + vec3(0.0, 0.0, 1e-4));
    vec3 farAcross = cross(farUp, axis) * side;
    vec2 v = vec2(dot(onFar, farAcross), dot(onFar, farUp)) / (END_SHARE * wayShape.x * 1.4);
    float aa = max(fwidth(v.y), 1e-4);
    vec3 vista = wayVista(v, aa);
    // No edge to the end: where it meets the walls it's exactly as dark as they are there (dim is 0.5 by then, and a stream's course averages 0.85), and the light at the end of the
    // throat is round, the land seen through a soft glow in its middle. Lit to the arch's own shape, a bright end read as a pale stone standing in the way.
    vec2 fromEnd = (at - heart) / vec2(wayShape.x * 0.8, (wayApex() - wayShape.y) * 0.4);
    float round_ = dot(fromEnd, fromEnd);
    vec3 edgeLight = LOOK_DEEP * (WALL * 0.5 * 0.85);
    vec3 endLight = mix(edgeLight, vista, smoothstep(0.9, 0.2, round_)) + LOOK_CORE * (CORE_RING * exp(-round_ * 3.5));

    // Glowing mist all the way down, thickest near the walls.
    vec3 mist = vec3(0.0);
    float through = 1.0;
    const int MIST_STEPS = 7;
    float step_ = tau / float(MIST_STEPS);
    for (int i = 0; i < MIST_STEPS; i++)
    {
        float tauI = step_ * (float(i) + 0.5);
        float deepI = k * tauI;
        float drawnIn = max(1.0 - beta * deepI, END_SHARE);
        vec2 atI = heart + (p + w * tauI - heart) / drawnIn;
        float nearWall = exp(wayDistance(atI) / 0.9);
        vec3 qI = vec3(atI * 0.42, log(1.0 + deepI / DEPTH_SCALE) * 2.0 - age * LOOK_FLOW * 1.6 + LOOK_SEED * 1.7);
        float puff = wayNoise(qI + vec3(0.0, 0.0, LOOK_UNREST * sin(age * 0.6 + atI.x)));
        float density = LOOK_MIST * smoothstep(0.35, 0.85, puff) * (0.35 + 0.65 * nearWall);
        mist += mix(LOOK_DEEP, LOOK_RIM, nearWall * 0.7) * (through * density * step_ * 0.22);
        through *= exp(-density * step_ * 0.05);
    }
    vec3 inner = (wall ? wallLight : endLight) * through + mist * MIST;

    // The face: its edge burning where it meets the stone, crackling along its length, the crests of the ripples, and a sheen where it's seen edge on.
    float rimLit = wayRimLit(p.y);
    float around = atan(fromHeart.y, fromHeart.x);
    float crackle = wayNoise(vec3(cos(around) * 4.0, sin(around) * 4.0, age * 1.3 + LOOK_SEED));
    float edge = RIM_PEAK * exp(-max(inside, 0.0) / 0.09) * (0.6 + 0.8 * crackle) + RIM_SOFT * exp(-max(inside, 0.0) / 0.6);
    vec3 film = LOOK_RIM * (edge * rimLit);
    film += LOOK_RIM * (0.6 * pow(max(sin(phase), 0.0), 6.0) * swell);
    film += LOOK_DEEP * (0.9 * pow(grazing, 5.0));

    // As it opens: the light flooding in from the edge with a bright front, and a flash as it meets in the middle.
    float fill = wayFill();
    float front = fill * wayDeepest() * 1.04;
    film += LOOK_RIM * (FRONT * exp(-abs(inside - front) / 0.14) * (1.0 - fill) * step(0.001, fill));
    float flash = smoothstep(WAY_FILL_TO - 0.35, WAY_FILL_TO, age) * exp(-max(age - WAY_FILL_TO, 0.0) * 2.2);

    vec3 face = (inner * 0.92 + film) * (1.0 + FLASH * flash);
    vec3 colour = wayRuneData.x > 0.5 ? wayRune(wayRuneData, wayRuneUV) : face;

    if (!drawn) discard;
    out_Colour = vec4(max(colour, vec3(0.0)) * (wayState.y * wayState.w * emissionScale), 0.0);
}
