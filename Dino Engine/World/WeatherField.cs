using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>What the weather is doing at one place and moment. Everything is 0..1 unless it says otherwise.</summary>
    public struct WeatherSample
    {
        public float Cloud;     // How much of the sky is covered.
        public float Fall;      // How hard anything is coming down, whatever it is: a drizzle to a cloudburst.
        public float Storm;     // How much of that is a thunderstorm.
        public float Snow;      // Share of what falls that is snow.
        public float Ash;       // Share of what falls that is ash.
        public float Dust;      // Dust carried on the wind.
        public float Mist;      // Wet air lying over the land.
        public float Wet;       // How wet the ground has got. It takes a while to come and longer to go.
        public float Flood;     // How far the puddles have filled.
        public float SnowCover; // How far snow has settled.
        public float Gale;      // What this does to the wind: a multiplier on its speed.

        /// <summary>What's coming down that is water.</summary>
        public readonly float Rain => Fall * (1f - Snow) * (1f - Ash);

        /// <summary>Weather on order, by name, for a scripted run and the F3 window. False if there's no such weather.</summary>
        public static bool TryNamed(string name, out WeatherSample weather)
        {
            weather = name switch
            {
                "clear" => new WeatherSample { Gale = 1f },
                "cloudy" => new WeatherSample { Cloud = 0.55f, Gale = 1.1f },
                "overcast" => new WeatherSample { Cloud = 1f, Mist = 0.15f, Gale = 1.1f },
                "wet" => new WeatherSample { Cloud = 0.6f, Mist = 0.3f, Wet = 1f, Flood = 0.7f, Gale = 1f },
                "drizzle" => new WeatherSample { Cloud = 1f, Fall = 0.25f, Mist = 0.4f, Wet = 0.7f, Flood = 0.2f, Gale = 1.1f },
                "rain" => new WeatherSample { Cloud = 1f, Fall = 0.6f, Mist = 0.4f, Wet = 1f, Flood = 0.65f, Gale = 1.3f },
                "storm" => new WeatherSample { Cloud = 1f, Fall = 1f, Storm = 1f, Mist = 0.5f, Wet = 1f, Flood = 1f, Gale = 2.3f },
                "snow" => new WeatherSample { Cloud = 1f, Fall = 0.55f, Snow = 1f, Mist = 0.3f, SnowCover = 0.8f, Gale = 1f },
                "blizzard" => new WeatherSample { Cloud = 1f, Fall = 1f, Snow = 1f, Mist = 0.7f, SnowCover = 1f, Gale = 3f },
                "dust" => new WeatherSample { Cloud = 0.4f, Dust = 1f, Gale = 2.6f },
                "ash" => new WeatherSample { Cloud = 0.9f, Fall = 0.6f, Ash = 1f, Mist = 0.3f, Gale = 1.2f },
                "fog" => new WeatherSample { Cloud = 0.8f, Mist = 1f, Wet = 0.5f, Gale = 0.5f },
                _ => default,
            };
            return weather.Gale > 0f;
        }
    }

    /// <summary>A bolt of lightning. Everyone in a world sees the same ones, at the same moment.</summary>
    public readonly record struct LightningStrike(double Time, Vector2 Position, float Power, uint Seed);

    /// <summary>
    /// The weather of a world, as a pure function of place and time. Systems of wet air drift across the land on the world's own wind, changing shape as they go, so that
    /// it is always raining somewhere and clearing somewhere else, and walking out of a storm takes as long as a storm is wide. Where they drop what they carry is the world's
    /// climate: biomes say how wet and how warm a place is, wind climbing a slope wrings rain out of itself and leaves a rain shadow behind, what falls on cold ground is snow and
    /// on the ashlands ash, and a dry land's front brings dust and no water at all.
    /// Nothing here is stored or stepped. The same seed and clock give the same sky on every machine with nothing sent, and the ground's memory of rain is worked out by looking
    /// back along the same function, so a place first seen after a storm is already wet. Safe to call from any thread, and it has no GPU in it.
    /// </summary>
    public sealed class WeatherField
    {
        /// <summary>Texels along a side of the map <see cref="Bake"/> writes, which covers the whole world.</summary>
        public const int MapResolution = 64;
        /// <summary>Floats per texel of that map: wet, flood, snow cover, fall; then cloud, storm, mist, dust.</summary>
        public const int MapChannels = 8;

        private const float ClimateCell = 64f;          // Metres between nodes of the climate this reads.
        private const float LapseRate = 0.0025f;        // Warmth lost per metre of height.
        private const float LiftReach = 150f;           // Metres upwind that a slope is measured over.
        private const float LiftHeight = 110f;          // Metres of climb over that which wrings the most out of the air. Much less and every ridge wrings it dry and hands the sky straight over.
        private const int LandBlur = 8;                 // Cells of the coarse map, three times over: what the air feels of the land.
        private const float CellShare = 0.24f;          // How much showers break up what a system would do alone.
        private const float SurfaceMemory = 120f;       // Seconds for the ground to give up most of its water once it stops raining.
        private const float SurfaceFill = 40f;          // Seconds of hard rain to wet it through.
        private const float PuddleMemory = 300f;
        private const float PuddleFill = 170f;
        private const float StrikeSlot = 1.2f;          // Seconds per chance of a strike somewhere in the world.

        // Where a system's strength turns into each kind of weather. Systems sit near 0 and rarely pass 0.8.
        private static readonly FloatRange CloudForms = new FloatRange(-0.34f, 0.26f);
        private static readonly FloatRange RainStarts = new FloatRange(0.13f, 0.68f);
        private static readonly FloatRange StormBuilds = new FloatRange(0.5f, 0.88f);

        // How far back the ground remembers, in seconds.
        private static readonly float[] LooksBack = { 0f, 15f, 40f, 80f, 150f, 270f, 480f, 800f, 1200f };

        private readonly float _size;
        private readonly int _nodes;
        private readonly ClimateNode[] _climate;
        private readonly uint _seed;
        private readonly Vector2 _drift;
        private readonly float _systemScale;
        private readonly float _cellScale;
        private readonly float _evolution;
        private readonly double _start;

        private struct ClimateNode
        {
            public float Bias;      // How much wetter than the world's weather this place runs.
            public float Damp;      // 0 desert, 1 mire.
            public float Warmth;    // 0 ice, 1 furnace.
            public float Ash;       // How much of the land here is burning.
        }

        /// <summary>Metres a second that weather crosses the land.</summary>
        public Vector2 Drift => _drift;

        /// <summary>Side of the world in metres, which <see cref="Bake"/> covers.</summary>
        public float Size => _size;

        public WeatherField(WorldData world)
        {
            WorldRandom random = world.Random.Fork("weather");
            _seed = (uint)random.NextULong();
            _size = world.Size;
            _start = random.Range(0f, 100000f);

            // Weather comes from the way the world's wind blows, and a little off it.
            float heading = MathHelper.DegreesToRadians(world.Plan.Mood.WindDirection + random.Range(-25f, 25f));
            _drift = new Vector2(MathF.Sin(heading), MathF.Cos(heading)) * random.Range(2f, 4f);
            _systemScale = _size * random.Range(0.32f, 0.5f);
            _cellScale = random.Range(240f, 360f);
            _evolution = random.Range(600f, 1000f);
            float unsettled = world.Plan.Mood.Unsettled + random.Range(-0.06f, 0.06f);

            // A world is small next to real climate zones, so its coldest place feels the whole of it: a frozen march is cold all over.
            float worldWarmth = 0f;
            foreach (BiomeDefinition biome in world.Plan.Biomes) worldWarmth += biome.Temperature / world.Plan.Biomes.Count;

            _nodes = (int)MathF.Ceiling(_size / ClimateCell) + 1;
            _climate = new ClimateNode[_nodes * _nodes];
            Vector2 upwind = _drift.Normalized() * LiftReach;

            // Air answers to the lie of the land and not to a cliff: from one step to the next at the foot of a wall the sky doesn't change.
            ScalarField land = world.Elevation.Copy();
            land.Blur(LandBlur, 3);

            for (int z = 0; z < _nodes; z++)
            {
                for (int x = 0; x < _nodes; x++)
                {
                    _climate[z * _nodes + x] = Read(world, land, new Vector2(x, z) * ClimateCell, upwind, unsettled, worldWarmth);
                }
            }
            for (int pass = 0; pass < 3; pass++) Soften();
        }

        // Weather has no edge along a ridge or a biome's border: the air doesn't know they're there until it's over them.
        private void Soften()
        {
            var softened = new ClimateNode[_climate.Length];
            for (int z = 0; z < _nodes; z++)
            {
                for (int x = 0; x < _nodes; x++)
                {
                    ClimateNode sum = default;
                    float weight = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            ref ClimateNode node = ref _climate[Math.Clamp(z + dz, 0, _nodes - 1) * _nodes + Math.Clamp(x + dx, 0, _nodes - 1)];
                            float w = (dx == 0 ? 2f : 1f) * (dz == 0 ? 2f : 1f);
                            sum.Bias += node.Bias * w;
                            sum.Damp += node.Damp * w;
                            sum.Warmth += node.Warmth * w;
                            sum.Ash += node.Ash * w;
                            weight += w;
                        }
                    }
                    softened[z * _nodes + x] = new ClimateNode { Bias = sum.Bias / weight, Damp = sum.Damp / weight, Warmth = sum.Warmth / weight, Ash = sum.Ash / weight };
                }
            }
            Array.Copy(softened, _climate, _climate.Length);
        }

        private static ClimateNode Read(WorldData world, ScalarField land, Vector2 position, Vector2 upwind, float unsettled, float worldWarmth)
        {
            float damp = 0f, warmth = 0f, ash = 0f, total = 0f;
            for (int i = 0; i < world.BiomeWeights.Length; i++)
            {
                float share = world.BiomeWeights[i].Sample(position.X, position.Y);
                BiomeDefinition biome = world.Plan.Biomes[i];
                damp += biome.Moisture * share;
                warmth += biome.Temperature * share;
                if (biome.Motes == AmbientMotes.Embers) ash += share;
                total += share;
            }
            if (total > 1e-4f)
            {
                damp /= total;
                warmth /= total;
                ash /= total;
            }

            float height = MathF.Max(land.Sample(position.X, position.Y), 0f);
            float climb = (height - MathF.Max(land.Sample(position.X - upwind.X, position.Y - upwind.Y), 0f)) / LiftHeight;

            return new ClimateNode
            {
                Bias = unsettled + (damp - 0.5f) * 0.55f + Math.Clamp(climb, -1f, 1f) * 0.22f,
                Damp = damp,
                Warmth = warmth * 0.7f + worldWarmth * 0.3f - height * LapseRate,
                Ash = ash,
            };
        }

        private ClimateNode ClimateAt(float x, float z)
        {
            float gx = Math.Clamp(x / ClimateCell, 0f, _nodes - 1.001f);
            float gz = Math.Clamp(z / ClimateCell, 0f, _nodes - 1.001f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            ref ClimateNode a = ref _climate[iz * _nodes + ix];
            ref ClimateNode b = ref _climate[iz * _nodes + ix + 1];
            ref ClimateNode c = ref _climate[(iz + 1) * _nodes + ix];
            ref ClimateNode d = ref _climate[(iz + 1) * _nodes + ix + 1];
            float wa = (1f - fx) * (1f - fz), wb = fx * (1f - fz), wc = (1f - fx) * fz, wd = fx * fz;
            return new ClimateNode
            {
                Bias = a.Bias * wa + b.Bias * wb + c.Bias * wc + d.Bias * wd,
                Damp = a.Damp * wa + b.Damp * wb + c.Damp * wc + d.Damp * wd,
                Warmth = a.Warmth * wa + b.Warmth * wb + c.Warmth * wc + d.Warmth * wd,
                Ash = a.Ash * wa + b.Ash * wb + c.Ash * wc + d.Ash * wd,
            };
        }

        /// <summary>How strong the weather is at a point: below 0 fair, above it building. The systems are carried along by their drift and change as they go.</summary>
        private float Strength(float x, float z, double clock, float bias)
        {
            double t = clock + _start;
            double carriedX = x - _drift.X * t;
            double carriedZ = z - _drift.Y * t;
            float systems = Fbm(carriedX / _systemScale, carriedZ / _systemScale, t / _evolution, 2, _seed);
            float cells = Fbm(carriedX / _cellScale, carriedZ / _cellScale, t / (_evolution * 0.8), 1, _seed ^ 0x9E3779B9u);
            return systems + cells * CellShare + bias;
        }

        // Rain and snow alike. Ground too dry to have any water to give stays dry, however strong what passes over it.
        private static float FallFrom(float strength, in ClimateNode climate)
        {
            return Smoothstep(RainStarts.Min, RainStarts.Max, strength) * Smoothstep(0.03f, 0.3f, climate.Damp);
        }

        private static float SnowShare(in ClimateNode climate) => Smoothstep(0.36f, 0.24f, climate.Warmth);

        /// <summary>The weather at a point of the world, <paramref name="clock"/> seconds in.</summary>
        public WeatherSample Sample(Vector2 position, double clock)
        {
            float x = position.X, z = position.Y;
            ClimateNode climate = ClimateAt(x, z);
            float strength = Strength(x, z, clock, climate.Bias);
            float fall = FallFrom(strength, climate);

            // The ground's memory: what's fallen here lately, the latest counting most.
            float surface = 0f, puddles = 0f;
            for (int i = 0; i < LooksBack.Length; i++)
            {
                float back = LooksBack[i];
                float before = i > 0 ? LooksBack[i - 1] : back;
                float after = i + 1 < LooksBack.Length ? LooksBack[i + 1] : back;
                float span = (after - before) * 0.5f;
                float then = i == 0 ? fall : FallFrom(Strength(x, z, clock - back, climate.Bias), climate);
                surface += then * MathF.Exp(-back / SurfaceMemory) * span;
                puddles += then * MathF.Exp(-back / PuddleMemory) * span;
            }
            surface = Saturate(surface / SurfaceFill);
            puddles = Saturate(puddles / PuddleFill);

            float snow = SnowShare(climate);
            float ash = Saturate(climate.Ash * 1.4f);
            float water = (1f - snow) * (1f - ash);
            float dry = Smoothstep(0.34f, 0.14f, climate.Damp);
            float storm = Smoothstep(StormBuilds.Min, StormBuilds.Max, strength) * Smoothstep(0.28f, 0.5f, climate.Warmth) * Smoothstep(0.22f, 0.5f, climate.Damp) * water;
            float dust = Smoothstep(0.1f, 0.5f, strength) * dry;
            float wet = surface * water;

            return new WeatherSample
            {
                Cloud = Smoothstep(CloudForms.Min, CloudForms.Max, strength) * (1f - 0.5f * dry),
                Fall = fall,
                Storm = storm,
                Snow = snow,
                Ash = ash,
                Dust = dust,
                Mist = Saturate(fall * 0.5f + wet * 0.3f) * Smoothstep(0.15f, 0.6f, climate.Damp),
                Wet = wet,
                Flood = puddles * water,
                SnowCover = surface * snow,
                Gale = 1f + 0.3f * fall + 1.3f * storm + 1.6f * dust,
            };
        }

        /// <summary>Fills <paramref name="map"/> with the whole world's weather, in the layout of <see cref="MapResolution"/> and <see cref="MapChannels"/>.</summary>
        public void Bake(double clock, float[] map)
        {
            for (int z = 0; z < MapResolution; z++)
            {
                for (int x = 0; x < MapResolution; x++)
                {
                    Vector2 at = (new Vector2(x, z) + new Vector2(0.5f)) * (_size / MapResolution);
                    Write(Sample(at, clock), map, (z * MapResolution + x) * MapChannels);
                }
            }
        }

        /// <summary>Writes one texel of the map.</summary>
        public static void Write(in WeatherSample weather, float[] map, int offset)
        {
            map[offset] = weather.Wet;
            map[offset + 1] = weather.Flood;
            map[offset + 2] = weather.SnowCover;
            map[offset + 3] = weather.Fall;
            map[offset + 4] = weather.Cloud;
            map[offset + 5] = weather.Storm;
            map[offset + 6] = weather.Mist;
            map[offset + 7] = weather.Dust;
        }

        /// <summary>Adds every bolt that falls between two moments, in order. Each chance lands somewhere in the world, and comes to something only where a storm is.</summary>
        public void Strikes(double from, double to, List<LightningStrike> into)
        {
            long first = (long)Math.Floor((from + _start) / StrikeSlot);
            long last = (long)Math.Floor((to + _start) / StrikeSlot);
            for (long slot = first; slot <= last; slot++)
            {
                uint hash = Hash((uint)slot, (uint)(slot >> 32), 0x51EDu, _seed);
                double time = (slot + Unit(hash)) * StrikeSlot - _start;
                if (time < from || time >= to) continue;

                uint across = Mix(hash);
                uint along = Mix(across);
                uint chance = Mix(along);
                var position = new Vector2(Unit(across), Unit(along)) * _size;
                float storm = StormAt(position, time);
                if (Unit(chance) >= storm * storm * 0.85f) continue;
                into.Add(new LightningStrike(time, position, 0.5f + 0.5f * Unit(Mix(chance)), Mix(chance ^ 0xB01Du)));
            }
        }

        // The storm alone, without looking back: this runs for every chance of a strike.
        private float StormAt(Vector2 position, double clock)
        {
            ClimateNode climate = ClimateAt(position.X, position.Y);
            float strength = Strength(position.X, position.Y, clock, climate.Bias);
            float water = (1f - SnowShare(climate)) * (1f - Saturate(climate.Ash * 1.4f));
            return Smoothstep(StormBuilds.Min, StormBuilds.Max, strength) * Smoothstep(0.28f, 0.5f, climate.Warmth) * Smoothstep(0.22f, 0.5f, climate.Damp) * water;
        }

        // ---------------------------------------------------------------- noise

        // About -1..1, and rarely past +-0.8: scaled so that a system's strength reads the same whatever the octaves.
        private static float Fbm(double x, double y, double z, int octaves, uint seed)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Gradient(x, y, z, seed + (uint)i * 0x632BE5ABu) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.03;
                y *= 2.03;
                z *= 1.7;
            }
            return sum / total * 1.9f;
        }

        // Perlin's improved gradient noise, on a hash rather than a table, so that a world's lattice is its own.
        private static float Gradient(double x, double y, double z, uint seed)
        {
            double floorX = Math.Floor(x), floorY = Math.Floor(y), floorZ = Math.Floor(z);
            uint ix = (uint)(long)floorX, iy = (uint)(long)floorY, iz = (uint)(long)floorZ;
            float fx = (float)(x - floorX), fy = (float)(y - floorY), fz = (float)(z - floorZ);
            float u = Fade(fx), v = Fade(fy), w = Fade(fz);

            float a = Lerp(Corner(ix, iy, iz, seed, fx, fy, fz), Corner(ix + 1, iy, iz, seed, fx - 1f, fy, fz), u);
            float b = Lerp(Corner(ix, iy + 1, iz, seed, fx, fy - 1f, fz), Corner(ix + 1, iy + 1, iz, seed, fx - 1f, fy - 1f, fz), u);
            float c = Lerp(Corner(ix, iy, iz + 1, seed, fx, fy, fz - 1f), Corner(ix + 1, iy, iz + 1, seed, fx - 1f, fy, fz - 1f), u);
            float d = Lerp(Corner(ix, iy + 1, iz + 1, seed, fx, fy - 1f, fz - 1f), Corner(ix + 1, iy + 1, iz + 1, seed, fx - 1f, fy - 1f, fz - 1f), u);
            return Lerp(Lerp(a, b, v), Lerp(c, d, v), w);
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Corner(uint ix, uint iy, uint iz, uint seed, float x, float y, float z)
        {
            uint h = Hash(ix, iy, iz, seed) & 15u;
            float u = h < 8u ? x : y;
            float v = h < 4u ? y : h is 12u or 14u ? x : z;
            return ((h & 1u) == 0u ? u : -u) + ((h & 2u) == 0u ? v : -v);
        }

        private static uint Hash(uint x, uint y, uint z, uint seed)
        {
            return Mix(x ^ Mix(y ^ Mix(z ^ seed)));
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);
    }
}
