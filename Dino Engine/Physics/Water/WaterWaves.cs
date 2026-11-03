using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>
    /// Wave physics shared by WaterSystem, the CPU water queries and the water shaders. The swell set and the constants
    /// marked with a shader name are mirrored in Shaders/Water: change both together.
    /// </summary>
    public static class WaterWaves
    {
        public const float Gravity = 9.81f;
        /// <summary>Surface tension over density of water, m^3/s^2 (capillary term of the dispersion relation).</summary>
        public const float SurfaceTension = 7.28e-5f;
        /// <summary>Waves break once they are this high relative to the depth (McCowan). WATER_BREAKING_RATIO.</summary>
        public const float BreakingRatio = 0.78f;
        /// <summary>Waves per set: shore wave heights rise and fall over this many waves. WATER_SET_LENGTH.</summary>
        public const float SetLength = 7f;
        /// <summary>Mean square slope of a clean sea surface per m/s of wind (Cox and Munk 1954, swell excluded).</summary>
        public const float CoxMunkSlopePerWind = 0.00512f;
        /// <summary>Largest factor the along-shore variation scales shore waves by. WATER_ALONGSHORE_MAX.</summary>
        public const float AlongShoreMax = 1.25f;
        /// <summary>The swash climbs at most this many wave heights up a steep shore. WATER_RUNUP_VERTICAL.</summary>
        public const float MaxRunUpHeight = 1.2f;

        /// <summary>Wavelength in deep water of waves with this period: g T^2 / (2 pi).</summary>
        public static float DeepWaterWavelength(float period) => Gravity * period * period / MathF.Tau;

        public const int SwellCount = 8; // SWELL_COUNT

        // The open-sea swell is a fixed set of waves; the wind only changes how big each one is, never its direction or
        // length, so a change of wind can't make the pattern slide. Directions in degrees like WindSingleton.Direction
        // (0 = +Z, 90 = +X), wavelengths in metres. Must match SWELL_DIRECTIONS and SWELL_WAVELENGTHS in Water_Ocean.frag.
        public static readonly float[] SwellDirections = { 0f, 47f, 88f, 136f, 178f, 223f, 271f, 316f };
        public static readonly float[] SwellWavelengths = { 26f, 17f, 33f, 12f, 21f, 15f, 29f, 10f };

        public static float SwellWavenumber(int wave) => MathF.Tau / SwellWavelengths[wave];

        /// <summary>Radians per second of swell wave <paramref name="wave"/> (deep water).</summary>
        public static float SwellAngularFrequency(int wave) => MathF.Sqrt(Gravity * SwellWavenumber(wave));

        public static Vector2 SwellDirection(int wave)
        {
            float angle = MathHelper.DegreesToRadians(SwellDirections[wave]);
            return new Vector2(MathF.Sin(angle), MathF.Cos(angle));
        }

        /// <summary>Radians per second of a wave with <paramref name="wavenumber"/> (rad/m), gravity and capillary terms.</summary>
        public static float AngularFrequency(float wavenumber)
        {
            return MathF.Sqrt(Gravity * wavenumber + SurfaceTension * wavenumber * wavenumber * wavenumber);
        }

        /// <summary>Significant wave height of a fully developed sea in this wind (Pierson-Moskowitz): Hs = 0.21 U^2 / g.</summary>
        public static float FullyDevelopedHeight(float windSpeed)
        {
            float u = Math.Clamp(windSpeed, 0f, 40f);
            return 0.21f * u * u / Gravity;
        }

        /// <summary>
        /// Height (crest to trough) of the waves breaking on an open coast: the swell from afar plus the sea the local
        /// wind raises. Wave energies add, so the heights add in quadrature.
        /// </summary>
        public static float ShoreBreakerHeight(in WaterSingleton water)
        {
            float swell = MathF.Max(water.ShoreWaveHeight, 0f);
            float windSea = FullyDevelopedHeight(water.SeaState) * MathF.Max(water.WindWaveHeight, 0f);
            return MathF.Sqrt(swell * swell + windSea * windSea);
        }

        /// <summary>
        /// Amplitudes in metres of the swell waves for the live sea state. Each wave gets the energy of the
        /// Pierson-Moskowitz spectrum around its frequency, spread around the wind direction (cos^2), scaled so the
        /// set has the significant height of the sea (Hs = 4 standard deviations of the elevation).
        /// </summary>
        public static void SwellAmplitudes(in WaterSingleton water, Span<float> amplitudes)
        {
            amplitudes.Clear();
            float windSpeed = water.SeaState;
            float significantHeight = FullyDevelopedHeight(windSpeed) * MathF.Max(water.WindWaveHeight, 0f);
            if (significantHeight <= 1e-5f) return;

            float peak = 0.877f * Gravity / MathF.Max(windSpeed, 0.5f);
            float total = 0f;
            for (int i = 0; i < SwellCount; i++)
            {
                float omega = SwellAngularFrequency(i);
                float ratio = peak / omega;
                // Spectral density ~ omega^-5 times a band that widens with omega (the set is log spaced).
                float spectrum = MathF.Pow(omega, -4f) * MathF.Exp(-1.25f * ratio * ratio * ratio * ratio);
                float alignment = MathF.Max(Vector2.Dot(SwellDirection(i), water.SeaDirection), 0f);
                amplitudes[i] = spectrum * alignment * alignment;
                total += amplitudes[i];
            }
            if (total <= 1e-12f)
            {
                amplitudes.Clear();
                return;
            }

            float variance = significantHeight * significantHeight / 16f;
            for (int i = 0; i < SwellCount; i++)
            {
                amplitudes[i] = MathF.Sqrt(2f * variance * amplitudes[i] / total);
            }
        }

        /// <summary>Standard deviation of the elevation of a set of sine waves.</summary>
        public static float StandardDeviation(ReadOnlySpan<float> amplitudes)
        {
            float sum = 0f;
            foreach (float a in amplitudes) sum += a * a * 0.5f;
            return MathF.Sqrt(sum);
        }

        /// <summary>
        /// Elevation of the open-sea swell above sea level at <paramref name="xz"/>: exactly what the ocean shader adds out
        /// at sea. Near the coast the shader also fades the swell into the shore waves, which the CPU doesn't model.
        /// </summary>
        public static float SwellElevation(in WaterSingleton water, Vector2 xz)
        {
            Span<float> amplitudes = stackalloc float[SwellCount];
            SwellAmplitudes(water, amplitudes);
            float elevation = 0f;
            for (int i = 0; i < SwellCount; i++)
            {
                if (amplitudes[i] <= 0f) continue;
                float phase = SwellWavenumber(i) * Vector2.Dot(SwellDirection(i), xz) - MathF.Tau * SwellPhase(water, i);
                elevation += amplitudes[i] * MathF.Sin(phase);
            }
            return elevation;
        }

        public static float SwellPhase(in WaterSingleton water, int wave)
        {
            return wave < 4 ? water.SwellPhasesA[wave] : water.SwellPhasesB[wave - 4];
        }

        /// <summary>Fraction of the sea covered by whitecaps in this wind (Monahan and O'Muircheartaigh 1980): 3.84e-6 U^3.41.</summary>
        public static float WhitecapCoverage(float windSpeed)
        {
            return 3.84e-6f * MathF.Pow(Math.Clamp(windSpeed, 0f, 40f), 3.41f);
        }

        /// <summary>
        /// How many standard deviations above the mean a Gaussian variable has to be for only <paramref name="fraction"/>
        /// of it to lie higher (Abramowitz and Stegun 26.2.23, error below 4.5e-4).
        /// </summary>
        public static float GaussianExceedance(float fraction)
        {
            float p = Math.Clamp(fraction, 1e-9f, 0.5f);
            float t = MathF.Sqrt(-2f * MathF.Log(p));
            return t - (2.515517f + 0.802853f * t + 0.010328f * t * t) / (1f + 1.432788f * t + 0.189269f * t * t + 0.001308f * t * t * t);
        }

        /// <summary>
        /// Mean square slope of the wind ripples on fully exposed water (Cox and Munk, without their swell term). Below
        /// about a metre per second there are no ripples at all and the water is glassy. Mirrors waterRipples in WaterCommon.glsl.
        /// </summary>
        public static float RippleMeanSquareSlope(float windSpeed, float scale)
        {
            float u = MathF.Max(windSpeed, 0f);
            float t = Math.Clamp((u - 0.3f) / 1.2f, 0f, 1f);
            float onset = t * t * (3f - 2f * t);
            return CoxMunkSlopePerWind * MathF.Max(scale, 0f) * u * onset;
        }
    }
}
