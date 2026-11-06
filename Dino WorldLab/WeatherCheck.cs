using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Runs every kind of world's weather through hours of play and fails it for what a player would catch: a sky that jumps, rain with no cloud, ground that's wet where
    /// nothing fell, deserts in a downpour, one world with the same weather everywhere. Each seed's numbers are printed, as how often it rains is a matter of taste.
    /// </summary>
    internal static class WeatherCheck
    {
        private const float Hours = 3f;
        private const float Step = 20f;

        private static int _failures;

        private readonly record struct Climate(string Id, float Clear, float Cloudy, float Falling, float Hard, float Storm, float Snowy, float Ashy, float Dusty, float Spread, int Strikes);

        public static int Run(int seeds)
        {
            _failures = 0;
            Console.WriteLine($"{"world",-12}{"clear",7}{"cloud",7}{"fall",7}{"hard",7}{"storm",7}{"snow",7}{"ash",7}{"dust",7}{"spread",8}{"bolts/h",9}");

            var climates = new Dictionary<string, List<Climate>>();
            foreach (WorldArchetype archetype in ArchetypeCatalog.All)
            {
                var all = new List<Climate>();
                for (int seed = 1; seed <= seeds; seed++)
                {
                    WorldData world = new WorldGenerator().Generate(new WorldRequest(seed * 37 + 5, archetype.Id));
                    var field = new WeatherField(world);
                    CheckField(world, field);
                    Climate climate = Measure(archetype.Id, world, field);
                    all.Add(climate);
                }
                climates[archetype.Id] = all;
                Print(Average(all));
            }

            Climate mire = Average(climates["mire"]);
            Climate badlands = Average(climates["badlands"]);
            Climate frost = Average(climates["frostmark"]);
            Climate ashen = Average(climates["ashen"]);
            Climate isle = Average(climates["isle"]);

            Require("A mire sees more of it than the badlands", mire.Falling > badlands.Falling * 2f);
            Require("The badlands are dusty and the isle isn't", badlands.Dusty > isle.Dusty * 2f + 0.01f);
            Require("What falls in the frozen march is mostly snow", frost.Snowy > frost.Falling * 0.4f);
            Require("On the ashen isle, ash falls", ashen.Ashy > 0.02f);
            Require("Worlds are fair more often than not", isle.Clear + isle.Cloudy > 0.5f);
            Require("It still rains in them", isle.Falling > 0.08f);
            Require("And not always gently", isle.Hard > 0.02f);
            Require("Worlds have storms", isle.Strikes > 0);
            Require("A world's weather is rarely the same all over", isle.Spread > 0.15f);

            Console.WriteLine(_failures == 0 ? "Weather holds." : $"{_failures} failed.");
            return _failures == 0 ? 0 : 1;
        }

        /// <summary>A row of maps of one world's weather, some minutes apart: land in grey, cloud in white, rain in blue, storms in violet, snow in cyan, dust in tan.</summary>
        public static int Map(int seed, string? archetype, string path, float clock, float minutes, int frames)
        {
            const int Size = 320;
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            var field = new WeatherField(world);
            int width = Size * frames;
            byte[] pixels = new byte[width * Size * 4];

            for (int frame = 0; frame < frames; frame++)
            {
                double time = clock + frame * minutes * 60f;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector2 at = (new Vector2(x, y) + new Vector2(0.5f)) * (world.Size / Size);
                        WeatherSample weather = field.Sample(at, time);
                        bool land = world.Height.Sample(at) > WorldPlan.SeaLevel;
                        Vector3 colour = land ? new Vector3(0.42f, 0.44f, 0.4f) : new Vector3(0.1f, 0.16f, 0.24f);
                        colour = Vector3.Lerp(colour, new Vector3(0.9f), weather.Cloud * 0.55f);
                        colour = Vector3.Lerp(colour, new Vector3(0.1f, 0.3f, 0.95f), weather.Rain);
                        colour = Vector3.Lerp(colour, new Vector3(0.45f, 0.95f, 1f), weather.Fall * weather.Snow);
                        colour = Vector3.Lerp(colour, new Vector3(0.3f, 0.25f, 0.25f), weather.Fall * weather.Ash);
                        colour = Vector3.Lerp(colour, new Vector3(0.8f, 0.6f, 0.3f), weather.Dust);
                        colour = Vector3.Lerp(colour, new Vector3(0.85f, 0.15f, 0.95f), weather.Storm);
                        int pixel = (y * width + frame * Size + x) * 4;
                        pixels[pixel] = (byte)(Math.Clamp(colour.X, 0f, 1f) * 255f);
                        pixels[pixel + 1] = (byte)(Math.Clamp(colour.Y, 0f, 1f) * 255f);
                        pixels[pixel + 2] = (byte)(Math.Clamp(colour.Z, 0f, 1f) * 255f);
                        pixels[pixel + 3] = 255;
                    }
                }
            }

            Dino_Engine.Util.PngWriter.Write(path, pixels, width, Size);
            Console.WriteLine($"Wrote {path}: {world.Plan.Name}, a {world.Plan.Archetype.Id}, {frames} maps {minutes} minutes apart.");
            return 0;
        }

        private static void CheckField(WorldData world, WeatherField field)
        {
            var again = new WeatherField(world);
            Vector2 middle = new Vector2(world.Size * 0.5f);

            bool same = true;
            float worstTime = 0f, worstSpace = 0f, cloudlessRain = 0f, wetNoRain = 0f;
            for (float t = 0f; t < Hours * 3600f; t += Step)
            {
                Vector2 at = middle + new Vector2(MathF.Sin(t * 0.0013f), MathF.Cos(t * 0.0017f)) * world.Size * 0.4f;
                WeatherSample a = field.Sample(at, t);
                WeatherSample b = again.Sample(at, t);
                same &= a.Cloud == b.Cloud && a.Fall == b.Fall && a.Wet == b.Wet;

                WeatherSample later = field.Sample(at, t + 1f);
                worstTime = MathF.Max(worstTime, MathF.Max(MathF.Abs(later.Fall - a.Fall), MathF.Abs(later.Cloud - a.Cloud)));

                WeatherSample beside = field.Sample(at + new Vector2(10f, 0f), t);
                worstSpace = MathF.Max(worstSpace, MathF.Max(MathF.Abs(beside.Fall - a.Fall), MathF.Abs(beside.Cloud - a.Cloud)));

                if (a.Fall > 0.05f) cloudlessRain = MathF.Max(cloudlessRain, a.Fall - a.Cloud * 1.2f);

                // Everything that wets this spot came down on it in the last twenty minutes.
                if (a.Wet > 0.02f)
                {
                    float fell = 0f;
                    for (float back = 0f; back <= 1200f; back += 15f) fell = MathF.Max(fell, field.Sample(at, t - back).Fall);
                    wetNoRain = MathF.Max(wetNoRain, a.Wet - fell * 40f);
                }
            }

            Require("One seed gives one sky", same);
            Require($"The sky changes gently over time (worst {worstTime:0.000} a second)", worstTime < 0.07f);
            Require($"And over distance (worst {worstSpace:0.000} in 10 m)", worstSpace < 0.18f);
            Require($"Nothing falls from a clear sky (worst {cloudlessRain:0.00})", cloudlessRain < 0.05f);
            Require($"Ground is only wet where it rained (worst {wetNoRain:0.00})", wetNoRain < 0.01f);

            var whole = new List<LightningStrike>();
            field.Strikes(1000.0, 4600.0, whole);
            var halves = new List<LightningStrike>();
            field.Strikes(1000.0, 2777.7, halves);
            field.Strikes(2777.7, 4600.0, halves);
            Require("Strikes don't depend on when they're asked for", whole.SequenceEqual(halves));
            Require("Bolts come in order", whole.Zip(whole.Skip(1)).All(pair => pair.First.Time <= pair.Second.Time));
            foreach (LightningStrike strike in whole)
            {
                if (field.Sample(strike.Position, strike.Time).Storm <= 0f) Require("Bolts come out of storms", false);
            }

            // Memory has to carry past the end of the rain. Walk to a place where it has just stopped, and it must still be wet.
            for (float t = 0f; t < Hours * 3600f; t += 30f)
            {
                WeatherSample now = field.Sample(middle, t);
                WeatherSample before = field.Sample(middle, t - 60f);
                if (before.Fall > 0.5f && now.Fall < 0.05f && now.Snow < 0.1f && now.Ash < 0.1f)
                {
                    Require("The ground stays wet after the rain has passed", now.Wet > 0.3f);
                    break;
                }
            }
        }

        private static Climate Measure(string id, WorldData world, WeatherField field)
        {
            int clear = 0, cloudy = 0, falling = 0, hard = 0, storm = 0, snowy = 0, ashy = 0, dusty = 0, samples = 0;
            float spread = 0f;
            int spreadSamples = 0;
            var random = new WorldRandom(11);

            for (float t = 0f; t < Hours * 3600f; t += 45f)
            {
                float least = 1f, most = 0f;
                for (int i = 0; i < 12; i++)
                {
                    Vector2 at = world.Sites[random.Int(world.Sites.Count)].Position;
                    WeatherSample weather = field.Sample(at, t);
                    samples++;
                    least = MathF.Min(least, weather.Cloud);
                    most = MathF.Max(most, weather.Cloud);
                    if (weather.Cloud < 0.3f) clear++;
                    else if (weather.Fall < 0.05f) cloudy++;
                    if (weather.Fall >= 0.05f) falling++;
                    if (weather.Fall > 0.6f) hard++;
                    if (weather.Storm > 0.3f) storm++;
                    if (weather.Fall >= 0.05f && weather.Snow > 0.5f) snowy++;
                    if (weather.Fall >= 0.05f && weather.Ash > 0.5f) ashy++;
                    if (weather.Dust > 0.3f) dusty++;
                }
                spread += most - least;
                spreadSamples++;
            }

            var strikes = new List<LightningStrike>();
            field.Strikes(0.0, Hours * 3600.0, strikes);

            float share = 1f / samples;
            return new Climate(id, clear * share, cloudy * share, falling * share, hard * share, storm * share, snowy * share, ashy * share, dusty * share,
                spread / spreadSamples, (int)(strikes.Count / Hours));
        }

        private static Climate Average(List<Climate> all)
        {
            return new Climate(all[0].Id, all.Average(c => c.Clear), all.Average(c => c.Cloudy), all.Average(c => c.Falling), all.Average(c => c.Hard), all.Average(c => c.Storm),
                all.Average(c => c.Snowy), all.Average(c => c.Ashy), all.Average(c => c.Dusty), all.Average(c => c.Spread), (int)all.Average(c => c.Strikes));
        }

        private static void Print(Climate c)
        {
            Console.WriteLine($"{c.Id,-12}{c.Clear,7:P0}{c.Cloudy,7:P0}{c.Falling,7:P0}{c.Hard,7:P0}{c.Storm,7:P0}{c.Snowy,7:P0}{c.Ashy,7:P0}{c.Dusty,7:P0}{c.Spread,8:0.00}{c.Strikes,9}");
        }

        private static void Require(string what, bool held)
        {
            if (held) return;
            _failures++;
            Console.WriteLine($"  FAILED: {what}");
        }
    }
}
