using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Works out how warm and how wet every place is. Temperature falls with height and drifts across the world; moisture
    /// follows water, gathers where drainage does and is lost in the rain shadow behind high ground. Both are then stretched
    /// so that the land covers the whole range from 0 to 1: a world is small next to real climate zones, and this is what
    /// lets it show all of its biomes rather than one.
    /// </summary>
    public sealed class ClimateStage : IWorldStage
    {
        private const int HistogramBins = 2048;

        public string Description => "Turning the seasons";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("climate");
            var temperatureNoise = new WorldNoise(random.Fork("temperature"));
            var moistureNoise = new WorldNoise(random.Fork("moisture"));

            float size = world.Size;
            ScalarField elevation = world.Elevation;
            ScalarField inland = world.Inland;
            ScalarField flow = world.Flow;
            float highest = MathF.Max(elevation.Range().max, 1f);

            float angle = random.Float() * MathF.Tau;
            Vector2 warmthDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float windAngle = random.Float() * MathF.Tau;
            Vector2 windDirection = new Vector2(MathF.Cos(windAngle), MathF.Sin(windAngle));

            ScalarField temperature = ScalarField.ForWorld(size, WorldData.ClimateCell);
            temperature.Generate((x, z) =>
            {
                float height = MathF.Max(elevation.Sample(x, z), 0f) / highest;
                Vector2 uv = new Vector2(x, z) / size - new Vector2(0.5f);
                return Vector2.Dot(uv, warmthDirection) * 1.15f
                    + temperatureNoise.Fbm(x, z, 0.5f * size, 4) * 0.7f
                    - height * 0.9f;
            });

            ScalarField moisture = ScalarField.ForWorld(size, WorldData.ClimateCell);
            moisture.Generate((x, z) =>
            {
                float nearWater = MathF.Exp(-inland.Sample(x, z) / 200f);
                float drainage = flow.Sample(x, z);
                float shadow = RainShadow(elevation, x, z, windDirection);
                return moistureNoise.Fbm(x, z, 0.42f * size, 4) * 0.75f
                    + nearWater * 0.45f
                    + drainage * 0.55f
                    - shadow * 0.5f;
            });

            Equalize(temperature, elevation);
            Equalize(moisture, elevation);

            world.Temperature = temperature;
            world.Moisture = moisture;
        }

        // How much high ground stands between this point and the wind.
        private static float RainShadow(ScalarField elevation, float x, float z, Vector2 windDirection)
        {
            float here = elevation.Sample(x, z);
            float blocked = 0f;
            for (int i = 1; i <= 6; i++)
            {
                float distance = i * 50f;
                float upwind = elevation.Sample(x - windDirection.X * distance, z - windDirection.Y * distance);
                blocked = MathF.Max(blocked, (upwind - here) / (18f + distance * 0.06f));
            }
            return Saturate(blocked);
        }

        // Replaces each value with its rank among the land's values, so land is spread evenly over 0..1.
        private static void Equalize(ScalarField field, ScalarField elevation)
        {
            (float min, float max) = field.Range();
            float span = MathF.Max(max - min, 1e-5f);
            int[] histogram = new int[HistogramBins];
            int total = 0;

            for (int z = 0; z < field.Depth; z++)
            {
                for (int x = 0; x < field.Width; x++)
                {
                    if (elevation.Sample(x * field.CellSize, z * field.CellSize) <= WorldPlan.SeaLevel) continue;
                    histogram[Bin(field[x, z], min, span)]++;
                    total++;
                }
            }
            if (total == 0) return;

            float[] rank = new float[HistogramBins];
            int running = 0;
            for (int i = 0; i < HistogramBins; i++)
            {
                rank[i] = (running + histogram[i] * 0.5f) / total;
                running += histogram[i];
            }

            float[] data = field.Data;
            for (int i = 0; i < data.Length; i++) data[i] = rank[Bin(data[i], min, span)];
        }

        private static int Bin(float value, float min, float span)
        {
            return Math.Clamp((int)((value - min) / span * (HistogramBins - 1)), 0, HistogramBins - 1);
        }
    }
}
