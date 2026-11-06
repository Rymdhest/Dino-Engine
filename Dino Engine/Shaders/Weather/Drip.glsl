#ifndef DINO_WEATHER_DRIP_GLSL
#define DINO_WEATHER_DRIP_GLSL

// Water coming off things. WeatherRenderer looks straight down on the world and finds every place something stops and the ground carries on below it (Drip_Sources.frag), so eaves, branches, walls and
// posts all drip with nothing placed by hand, and a world's drips are wherever its edges are. Each of those places keeps its own time, so a roof has a rhythm rather than a shimmer, and nothing is
// simulated or stored: a drop's height follows from the clock. Water keeps coming off things after the rain stops, thinning out as the ground dries. Needs Weather/Weather.glsl and Weather/SurfaceMap.glsl.

uniform sampler2D dripSources;  // A texel a tile: xy where a drop falls, z the height it leaves, w the height it lands. z is no more than w where nothing drips.

const float DRIP_GRAVITY = 9.81;
const float DRIP_HANG = 0.16;       // Seconds a drop swells before it lets go.
const float DRIP_RING_LIFE = 0.7;   // Seconds a ring is seen.
const float DRIP_LEAN = 0.1;        // How far a falling drop is carried: this times the wind times its seconds in the air, squared. Heavy drops take the wind's speed slowly.

struct DripSource
{
    vec2 at;
    float top;
    float landing;
    float falling;  // Seconds in the air.
    float period;   // Seconds between one chance of a drop here and the next, a whole number of quarters: WeatherSystem wraps the clock at a whole number of all of them.
    float offset;
    float rate;     // 0..1, how many of those chances come to a drop.
    uint seed;
};

uint dripHash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float dripRandom(inout uint state)
{
    state = dripHash(state);
    return float(state >> 8u) * (1.0 / 16777216.0);
}

bool readDripSource(int index, out DripSource source)
{
    int tiles = textureSize(dripSources, 0).x;
    vec4 texel = texelFetch(dripSources, ivec2(index % tiles, index / tiles), 0);
    source.at = texel.xy;
    source.top = texel.z;
    source.landing = texel.w;
    source.falling = 0.0;
    source.period = 1.0;
    source.offset = 0.0;
    source.rate = 0.0;
    source.seed = 0u;
    if (texel.z <= texel.w) return false;

    // While it rains water comes off as fast as it arrives, and after that as fast as what's soaked gives it up.
    vec4 here = weatherSurfaceAt(texel.xy);
    float rain = here.w * (1.0 - weatherKind.x) * (1.0 - weatherKind.y);
    source.rate = clamp(rain * 1.2 + here.x * here.x * 0.6, 0.0, 1.0) * dripLook.y;
    if (source.rate < 0.01) return false;

    // By the metre of the world, so that a place drips the same way each time the map comes round to it.
    ivec2 lattice = ivec2(floor(texel.xy));
    uint state = dripHash(uint(lattice.x) * 73856093u ^ uint(lattice.y) * 19349663u);
    source.seed = state;
    source.falling = sqrt(2.0 * (texel.z - texel.w) / DRIP_GRAVITY);
    int quarters = max(3 + int(dripRandom(state) * 10.0), int(ceil((DRIP_HANG + source.falling) * 4.0)));
    source.period = float(quarters) * 0.25;
    source.offset = dripRandom(state) * source.period;
    return true;
}

// Which turn this source was on `delay` seconds ago, how long ago that turn began, and whether a drop came of it. Asked with a drop's time in the air, this says what's landing now.
bool dripTurn(in DripSource source, float delay, out float age, out uint drop)
{
    float cycle = (dripLook.x + source.offset - delay) / source.period;
    float generation = floor(cycle);
    age = (cycle - generation) * source.period;
    drop = dripHash(source.seed ^ uint(int(generation)) * 2654435761u);
    return dripRandom(drop) < source.rate;
}

#endif
