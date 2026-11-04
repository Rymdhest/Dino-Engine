using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Puts the wind through what a game puts it through, and fails if it does anything an eye would read as a fault. A field
    /// of plants shows up any jump in what moves it, and the faults hide: this one only came out once the wind changed, late
    /// in a session, far from the world's origin. So every check runs there. Shaders aren't run here: Wind.glsl mirrors
    /// WindField, so what holds for one holds for the other as long as they are kept the same.
    /// </summary>
    internal static class WindCheck
    {
        private const float Step = 1f / 60f;
        private const float Hour = 3600f;

        // How fast the crest can honestly change at any point, per second. Waves in a gale come by about once a second, so
        // this has room to spare, and is still far below what a pattern gone wrong does.
        private const float CrestRateLimit = 6f;

        // Near the origin, where the hero starts in a typical world, and as far out as worlds go.
        private static readonly Vector2[] Points =
        {
            new Vector2(0.5f, 0.5f), new Vector2(441f, 1442f), new Vector2(-1800f, 900f), new Vector2(3000f, -2500f), new Vector2(3900f, 3900f),
        };

        private static int _failures;

        public static int Run()
        {
            _failures = 0;

            float fresh = WorstCrestRate(Settled(0f), ChangeSpeed(3f, 6f));
            Report("A rising wind moves the waves at a sensible pace", fresh, CrestRateLimit);
            Report("A falling wind does", WorstCrestRate(Settled(0f, speed: 6f), ChangeSpeed(6f, 3f)), CrestRateLimit);

            float late = WorstCrestRate(Settled(Hour), ChangeSpeed(3f, 6f));
            Report("An hour into a session it's no different", MathF.Abs(late - fresh), 0.5f);

            Report("A wind turning a third of the way round doesn't make the waves slide", WorstCrestRate(Settled(0f), Turn(120f)), CrestRateLimit);
            Report("Nor does one turning right round", WorstCrestRate(Settled(0f), Turn(180f)), CrestRateLimit);
            Report("Nor the guiding wind, which picks up as it turns", WorstCrestRate(Settled(0f), Guide()), CrestRateLimit);

            Report("A wind turning right round keeps its strength", LowestSpeedThroughReversal(), -0.95f * 3f);
            Report("Waves fade between directions of the rose without a step", WorstCrestStepAcrossHeadings(), 0.01f);
            Report("Waves run down the wind, whichever way it blows", LeastDownwind(), -0.88f);
            Report("Wrapping the waves' running total never shows", WorstCrestStepOverWrap(), 0.02f);
            Report("Nor does the gust pattern change abruptly", WorstSpeedRate(), 3f);

            WindSingleton defaults = new WindSingleton();
            float resonance = 1f / (2f * defaults.VegetationDamping * MathF.Sqrt(MathF.Max(1f - defaults.VegetationDamping * defaults.VegetationDamping, 1e-3f)));
            Report("Gusts can't ring the plants' spring", resonance, 1.1f);

            Console.WriteLine(_failures == 0 ? "The wind holds up." : $"{_failures} wind problem(s).");
            return _failures == 0 ? 0 : 1;
        }

        private static void Report(string what, float value, float limit)
        {
            bool held = value <= limit;
            if (!held) _failures++;
            Console.WriteLine($"{(held ? "ok  " : "FAIL")} {what}: {MathF.Abs(value):0.000} (limit {MathF.Abs(limit):0.000})");
        }

        // A wind that has blown steadily for this long.
        private static WindSingleton Settled(float seconds, float speed = 3f)
        {
            WindSingleton wind = new WindSingleton { Speed = speed, Direction = 40f };
            for (float t = 0f; t < MathF.Max(seconds, 12f); t += Step) WindSystem.Advance(ref wind, Step);
            return wind;
        }

        private delegate void Change(ref WindSingleton wind, float time);

        // As crossing into a windier land does: AmbienceSystem eases Speed to the new region's wind.
        private static Change ChangeSpeed(float from, float to)
        {
            return (ref WindSingleton wind, float time) => wind.Speed += (to - wind.Speed) * (1f - MathF.Exp(-0.5f * Step));
        }

        private static Change Turn(float degrees)
        {
            return (ref WindSingleton wind, float time) => wind.Direction = 40f + degrees;
        }

        // As AmbienceSystem does: straight to a strong wind towards the goal, and back after nine seconds.
        private static Change Guide()
        {
            return (ref WindSingleton wind, float time) =>
            {
                bool guiding = time < 9f;
                wind.Direction = guiding ? -150f : 40f;
                wind.Speed = guiding ? 9f : wind.Speed + (3f - wind.Speed) * (1f - MathF.Exp(-0.5f * Step));
            };
        }

        private static float WorstCrestRate(WindSingleton wind, Change change)
        {
            float[] crests = new float[Points.Length];
            for (int i = 0; i < Points.Length; i++) crests[i] = WindField.Crest(wind, Points[i]);

            float worst = 0f;
            for (float t = 0f; t < 20f; t += Step)
            {
                change(ref wind, t);
                WindSystem.Advance(ref wind, Step);
                for (int i = 0; i < Points.Length; i++)
                {
                    float crest = WindField.Crest(wind, Points[i]);
                    worst = MathF.Max(worst, MathF.Abs(crest - crests[i]) / Step);
                    crests[i] = crest;
                }
            }
            return worst;
        }

        private static float LowestSpeedThroughReversal()
        {
            WindSingleton wind = Settled(0f);
            wind.Direction += 180f;
            float lowest = float.MaxValue;
            for (float t = 0f; t < 15f; t += Step)
            {
                WindSystem.Advance(ref wind, Step);
                lowest = MathF.Min(lowest, wind.Velocity.Length);
            }
            return -lowest;
        }

        private static float WorstCrestStepAcrossHeadings()
        {
            WindSingleton wind = Settled(0f);
            float worst = 0f;
            const float turn = 0.0005f;
            foreach (Vector2 point in Points)
            {
                wind.Heading = -MathF.PI - 0.1f;
                float last = WindField.Crest(wind, point);
                for (float heading = wind.Heading; heading < MathF.PI + 0.1f; heading += turn)
                {
                    wind.Heading = heading;
                    float crest = WindField.Crest(wind, point);
                    worst = MathF.Max(worst, MathF.Abs(crest - last));
                    last = crest;
                }
            }
            return worst;
        }

        // How far along the wind the waves run at the worst heading: 1 is straight down it. Negative, so that lower is better.
        private static float LeastDownwind()
        {
            WindSingleton wind = Settled(0f);
            float least = 1f;
            for (float heading = -MathF.PI; heading < MathF.PI; heading += 0.01f)
            {
                wind.Heading = heading;
                Vector2 along = WindSingleton.Towards(heading);
                float downwind = 0f;
                float total = 0f;
                for (int i = 0; i < WindField.WaveTrains; i++)
                {
                    Vector4 train = WindField.WaveTrain(wind, i);
                    downwind += train.W * Vector2.Dot(train.Xy.Normalized(), along);
                    total += train.W;
                }
                least = MathF.Min(least, downwind / total);
            }
            return -least;
        }

        private static float WorstCrestStepOverWrap()
        {
            WindSingleton wind = Settled(0f);
            float worst = 0f;
            foreach (Vector2 point in Points)
            {
                wind.WaveCycles = WindField.WaveCyclesPeriod - 0.0005f;
                float before = WindField.Crest(wind, point);
                wind.WaveCycles = 0.0005f;
                worst = MathF.Max(worst, MathF.Abs(WindField.Crest(wind, point) - before));
            }
            return worst;
        }

        // How fast the wind at one spot changes in a steady breeze, in m/s per second.
        private static float WorstSpeedRate()
        {
            WindSingleton wind = Settled(0f, speed: 8f);
            float[] speeds = new float[Points.Length];
            for (int i = 0; i < Points.Length; i++) speeds[i] = WindField.SampleAmbient(wind, Points[i]).Velocity.Length;

            float worst = 0f;
            for (float t = 0f; t < 30f; t += Step)
            {
                WindSystem.Advance(ref wind, Step);
                for (int i = 0; i < Points.Length; i++)
                {
                    float speed = WindField.SampleAmbient(wind, Points[i]).Velocity.Length;
                    worst = MathF.Max(worst, MathF.Abs(speed - speeds[i]) / Step);
                    speeds[i] = speed;
                }
            }
            return worst;
        }
    }
}
