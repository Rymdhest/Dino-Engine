using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Content
{
    /// <summary>
    /// The light and weather a world is seen in. A place is remembered by its light as much as its shape, so every world draws
    /// its own from the time of day, the season of its biomes and a little chance.
    /// </summary>
    public sealed class WorldMood
    {
        public string Name = "";
        public Vector3 SunDirection;    // The way the light travels.
        public Vector3 SunColour;
        public float SunIntensity;
        public Vector3 SkyColour;
        public float SkyIntensity;
        public float WindSpeed;         // Metres per second.
        public float WindDirection;     // Degrees.
        public float FogDensity;
        public float FogHeight;
        public Vector3 FogColour;
        public float Unsettled;         // How much wetter than the climate alone the world's weather runs. See WeatherField.

        private sealed record Weather(string Name, FloatRange SunHeight, Vector3 Sun, float SunStrength, Vector3 Sky, float SkyStrength, float Fog, float Wind, float Unsettled);

        private static readonly Weather[] Weathers =
        {
            new("Clear morning", new FloatRange(0.45f, 0.65f), new Vector3(1f, 0.92f, 0.8f), 15f, new Vector3(0.36f, 0.6f, 1f), 0.75f, 0.8f, 3f, -0.1f),
            new("High summer", new FloatRange(0.8f, 1f), new Vector3(1f, 0.96f, 0.88f), 17f, new Vector3(0.38f, 0.62f, 1f), 0.8f, 0.5f, 2.5f, -0.22f),
            new("Golden evening", new FloatRange(0.2f, 0.32f), new Vector3(1f, 0.68f, 0.38f), 13f, new Vector3(0.62f, 0.52f, 0.72f), 0.65f, 1.1f, 2f, -0.12f),
            new("Misty dawn", new FloatRange(0.16f, 0.26f), new Vector3(1f, 0.74f, 0.58f), 8f, new Vector3(0.58f, 0.64f, 0.82f), 0.8f, 2.4f, 1.2f, -0.02f),
            new("Grey overcast", new FloatRange(0.55f, 0.8f), new Vector3(0.82f, 0.86f, 0.92f), 5.5f, new Vector3(0.62f, 0.68f, 0.76f), 1.25f, 1.5f, 5f, 0.2f),
            new("Windswept", new FloatRange(0.4f, 0.6f), new Vector3(1f, 0.9f, 0.76f), 13f, new Vector3(0.42f, 0.6f, 0.92f), 0.8f, 0.5f, 8f, 0.04f),
        };

        public static WorldMood Draw(IReadOnlyList<BiomeDefinition> biomes, WorldRandom random, WorldArchetype? archetype = null)
        {
            Weather weather = random.Pick(Weathers);

            float azimuth = random.Float() * MathF.Tau;
            float height = random.Range(weather.SunHeight);

            // A place that has to look one way is always seen in the same light.
            if (archetype?.Light is string light)
            {
                weather = Weathers.First(w => w.Name == light);
                azimuth = archetype.SunAzimuth;
                height = archetype.SunHeight;
            }
            // Sun height runs 0 (horizon) to 1 (overhead).
            float elevation = height * MathF.PI * 0.5f;
            Vector3 towardsSun = new Vector3(MathF.Cos(azimuth) * MathF.Cos(elevation), MathF.Sin(elevation), MathF.Sin(azimuth) * MathF.Cos(elevation));

            float fog = 0f;
            float wind = 0f;
            Vector3 fogColour = Vector3.Zero;
            foreach (BiomeDefinition biome in biomes)
            {
                fog += biome.FogDensity;
                wind += biome.WindStrength;
                fogColour += biome.FogColour;
            }
            float count = biomes.Count;

            return new WorldMood
            {
                Name = weather.Name,
                SunDirection = -towardsSun,
                SunColour = weather.Sun,
                SunIntensity = weather.SunStrength,
                SkyColour = weather.Sky,
                SkyIntensity = weather.SkyStrength,
                WindSpeed = weather.Wind * (wind / count) * random.Range(0.85f, 1.2f),
                WindDirection = random.Range(-180f, 180f),
                FogDensity = fog / count * weather.Fog,
                FogHeight = random.Range(5f, 11f),
                FogColour = fogColour / count,
                Unsettled = weather.Unsettled,
            };
        }
    }
}
