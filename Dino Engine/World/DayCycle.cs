using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>The light of one moment of a day. Colours are radiance, as the sun's and sky's own are.</summary>
    public readonly struct DaySample
    {
        public Vector3 Light { get; init; }         // The way the light that casts shadows travels: the sun's by day, the moon's by night.
        public Vector3 Sun { get; init; }           // What the sun shines with, above any cloud. Nothing once it's down.
        public float Moonlight { get; init; }       // How much of the moon's light there is, 0..1 of NightfallSingleton.Moon.
        public Vector3 Sky { get; init; }           // What the clear sky shines with, before night takes it (NightfallSingleton.Sky).
        public float Night { get; init; }           // 0 day, 1 full night.
        public float Golden { get; init; }          // How low and warm the sun is, 0..1: what a golden hour is.
        public float Dawn { get; init; }            // How near sunrise it is, 0..1, before and after.
        public float SunHeight { get; init; }       // Degrees above the horizon, below it negative.
        public float Hour { get; init; }            // 0..24, by where the sun is: 12 is noon.
    }

    /// <summary>
    /// A day and a night, as a pure function of time, for a world that has them (<see cref="Content.WorldArchetype.DayAndNight"/>). The sun goes round a real sky, at a
    /// latitude in summer, so it climbs high at noon and sets a little north of west, and it sets where the world's mood has its light. A day is <see cref="Length"/> seconds,
    /// and the sun lingers near the horizon, so that a golden hour and a dusk last long enough to be had. The moon stands half a day from the sun and lights the night once
    /// it's dark: the light that casts shadows only crosses from one to the other while both are out, so nothing jumps.
    /// Nothing is stored or stepped: the same clock gives the same hour on every machine. Headless, and safe on any thread.
    /// </summary>
    public sealed class DayCycle
    {
        /// <summary>Seconds in a day, night and all.</summary>
        public const float Length = 1200f;

        private const float Latitude = 45f;         // Degrees.
        private const float Declination = 23f;      // Midsummer: a long day and a short night.
        private const float Linger = 1.6f;          // How much slower the sun goes at the horizon than it would.
        private const float LingerWidth = 11f;      // Degrees of height either side of the horizon that it lingers over.
        private const float LowestShadow = 11f;     // Degrees: the light is never taken to come from lower than this, or a shadow runs the length of the land and a golden hour is all shade.
        private const int Steps = 2048;

        // How the sun's light runs with its height: degrees, then radiance.
        private static readonly (float height, Vector3 colour)[] SunByHeight =
        {
            (-1f, Vector3.Zero),
            (0f, new Vector3(1f, 0.42f, 0.18f) * 5f),
            (4f, new Vector3(1f, 0.58f, 0.3f) * 10f),
            (10f, new Vector3(1f, 0.72f, 0.45f) * 13f),
            (20f, new Vector3(1f, 0.88f, 0.72f) * 15f),
            (35f, new Vector3(1f, 0.95f, 0.88f) * 16f),
        };

        // And the clear sky's. Night takes it from below about -4 (NightfallSingleton.Sky).
        private static readonly (float height, Vector3 colour)[] SkyByHeight =
        {
            (-12f, new Vector3(0.3f, 0.3f, 0.45f) * 0.3f),
            (-4f, new Vector3(0.5f, 0.38f, 0.55f) * 0.45f),
            (2f, new Vector3(0.66f, 0.5f, 0.62f) * 0.62f),
            (8f, new Vector3(0.6f, 0.6f, 0.8f) * 0.68f),
            (20f, new Vector3(0.42f, 0.6f, 0.98f) * 0.76f),
            (40f, new Vector3(0.38f, 0.6f, 1f) * 0.8f),
        };

        private readonly Vector2 _east;
        private readonly Vector2 _north;
        private readonly float[] _elapsed = new float[Steps + 1];   // Share of the day gone by when the sun reaches each step of its hour angle from noon.

        /// <param name="sunsetAzimuth">Radians, as <see cref="Content.WorldMood"/> draws them: where the sun goes down is (cos, sin) of this, in x and z.</param>
        public DayCycle(float sunsetAzimuth)
        {
            // Where the sun sets, in a frame of east and north, and the world turned so that it sets where the mood says. East to north is clockwise in x and z, as the
            // camera looks along +z with -x on its right.
            float set = SunsetHourAngle();
            Vector3 local = Local(set);
            float sunsetLocal = MathF.Atan2(local.Z, local.X);
            float turn = sunsetAzimuth + sunsetLocal;
            _east = new Vector2(MathF.Cos(turn), MathF.Sin(turn));
            _north = new Vector2(MathF.Sin(turn), -MathF.Cos(turn));

            // The sun goes slower the nearer the horizon it is: each step of its angle takes the time it lingers there.
            float total = 0f;
            _elapsed[0] = 0f;
            for (int i = 0; i < Steps; i++)
            {
                float angle = (i + 0.5f) / Steps * MathF.Tau;
                float height = HeightOf(Local(angle));
                total += 1f + Linger * MathF.Exp(-(height / LingerWidth) * (height / LingerWidth));
                _elapsed[i + 1] = total;
            }
            for (int i = 0; i <= Steps; i++) _elapsed[i] /= total;
        }

        /// <summary>The hour angle at which the sun sets, in radians from noon.</summary>
        private static float SunsetHourAngle()
        {
            float lat = MathHelper.DegreesToRadians(Latitude), dec = MathHelper.DegreesToRadians(Declination);
            return MathF.Acos(Math.Clamp(-MathF.Tan(lat) * MathF.Tan(dec), -1f, 1f));
        }

        /// <summary>Towards the sun at an hour angle, as (east, up, north).</summary>
        private static Vector3 Local(float hourAngle)
        {
            float lat = MathHelper.DegreesToRadians(Latitude), dec = MathHelper.DegreesToRadians(Declination);
            float up = MathF.Sin(lat) * MathF.Sin(dec) + MathF.Cos(lat) * MathF.Cos(dec) * MathF.Cos(hourAngle);
            float east = -MathF.Cos(dec) * MathF.Sin(hourAngle);
            float north = MathF.Cos(lat) * MathF.Sin(dec) - MathF.Sin(lat) * MathF.Cos(dec) * MathF.Cos(hourAngle);
            return new Vector3(east, up, north);
        }

        private static float HeightOf(Vector3 local) => MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(local.Y, -1f, 1f)));

        private Vector3 World(Vector3 local)
        {
            Vector2 flat = _east * local.X + _north * local.Z;
            return new Vector3(flat.X, local.Y, flat.Y);
        }

        /// <summary>The sun's hour angle, radians from noon, at a share of the day since noon.</summary>
        private float HourAngleAt(float share)
        {
            int low = 0, high = Steps;
            while (high - low > 1)
            {
                int mid = (low + high) >> 1;
                if (_elapsed[mid] <= share) low = mid;
                else high = mid;
            }
            float span = _elapsed[high] - _elapsed[low];
            float t = span > 0f ? (share - _elapsed[low]) / span : 0f;
            return (low + t) / Steps * MathF.Tau;
        }

        /// <summary>The day, <paramref name="clock"/> seconds in. A clock of 0 is noon.</summary>
        public DaySample At(double clock)
        {
            double days = clock / Length;
            float share = (float)(days - Math.Floor(days));
            float hourAngle = HourAngleAt(share);

            Vector3 sunLocal = Local(hourAngle);
            Vector3 moonLocal = Local(hourAngle + MathF.PI);
            float sunHeight = HeightOf(sunLocal);
            float moonHeight = HeightOf(moonLocal);

            float sunUp = Smoothstep(-1.5f, 4f, sunHeight);
            float moonOut = Smoothstep(-3f, -10f, sunHeight) * Smoothstep(2f, 12f, moonHeight);
            bool byDay = sunHeight > -2.5f;
            Vector3 light = -World(Lowest(byDay ? sunLocal : moonLocal));

            float rising = hourAngle > MathF.PI ? 1f : 0f;
            return new DaySample
            {
                Light = light.Normalized(),
                Sun = Along(SunByHeight, sunHeight) * sunUp,
                Moonlight = moonOut,
                Sky = Along(SkyByHeight, sunHeight),
                Night = Smoothstep(-1f, -12f, sunHeight),
                Golden = Smoothstep(20f, 5f, sunHeight) * Smoothstep(-2f, 1f, sunHeight),
                Dawn = rising * Smoothstep(-14f, -2f, sunHeight) * Smoothstep(16f, 3f, sunHeight),
                SunHeight = sunHeight,
                Hour = (12f + hourAngle / MathF.Tau * 24f) % 24f,
            };
        }

        /// <summary>A clock, within the first day, at which the sun stands where it does at this hour (0..24, 12 noon).</summary>
        public double ClockAt(float hour)
        {
            float angle = ((hour - 12f) / 24f % 1f + 1f) % 1f * Steps;
            int low = Math.Clamp((int)angle, 0, Steps - 1);
            float t = angle - low;
            return (_elapsed[low] + (_elapsed[low + 1] - _elapsed[low]) * t) * Length;
        }

        // The light comes from no lower than a shadow can be cast from.
        private static Vector3 Lowest(Vector3 local)
        {
            float lowest = MathF.Sin(MathHelper.DegreesToRadians(LowestShadow));
            if (local.Y >= lowest) return local;
            Vector2 flat = new Vector2(local.X, local.Z);
            flat = flat.LengthSquared > 1e-8f ? flat.Normalized() * MathF.Sqrt(1f - lowest * lowest) : new Vector2(1f, 0f);
            return new Vector3(flat.X, lowest, flat.Y);
        }

        private static Vector3 Along((float height, Vector3 colour)[] keys, float height)
        {
            if (height <= keys[0].height) return keys[0].colour;
            for (int i = 1; i < keys.Length; i++)
            {
                if (height > keys[i].height) continue;
                float t = (height - keys[i - 1].height) / (keys[i].height - keys[i - 1].height);
                return Vector3.Lerp(keys[i - 1].colour, keys[i].colour, t);
            }
            return keys[^1].colour;
        }
    }
}
