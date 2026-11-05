using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;
using OpenTK.Mathematics;

namespace Dino_Engine.Sound.Mixing
{
    /// <summary>Bending a recording's pitch without hurting it: an eight point windowed sinc, read from a table. Reading between samples is where a cheap mixer goes dull.</summary>
    internal static class Resampler
    {
        public const int Taps = 8;
        private const int Phases = 512;
        private static readonly float[] Table = Build();

        private static float[] Build()
        {
            var table = new float[(Phases + 1) * Taps];
            for (int phase = 0; phase <= Phases; phase++)
            {
                float fraction = phase / (float)Phases;
                float sum = 0f;
                for (int tap = 0; tap < Taps; tap++)
                {
                    float x = tap - (Taps / 2 - 1) - fraction;
                    float sinc = MathF.Abs(x) < 1e-6f ? 1f : MathF.Sin(MathF.PI * x) / (MathF.PI * x);
                    float along = x / (Taps / 2);
                    float window = MathF.Abs(along) >= 1f ? 0f : 0.42f + 0.5f * MathF.Cos(MathF.PI * along) + 0.08f * MathF.Cos(2f * MathF.PI * along);
                    table[phase * Taps + tap] = sinc * window;
                    sum += sinc * window;
                }
                for (int tap = 0; tap < Taps; tap++) table[phase * Taps + tap] /= sum;
            }
            return table;
        }

        /// <summary>The value of <paramref name="samples"/> at a position between two of them. Off either end it's silence.</summary>
        public static float Read(float[] samples, int length, int whole, float fraction)
        {
            int row = (int)(fraction * Phases) * Taps;
            int first = whole - (Taps / 2 - 1);
            float sum = 0f;
            if (first >= 0 && first + Taps <= length)
            {
                for (int tap = 0; tap < Taps; tap++) sum += samples[first + tap] * Table[row + tap];
            }
            else
            {
                for (int tap = 0; tap < Taps; tap++)
                {
                    int index = first + tap;
                    if ((uint)index < (uint)length) sum += samples[index] * Table[row + tap];
                }
            }
            return sum;
        }
    }

    /// <summary>One sound being played. Voices are made once and reused, so starting a sound allocates nothing on the audio thread.</summary>
    internal sealed class Voice
    {
        private const int DelayLength = 64;     // Longest gap between the ears, in samples, as a power of two.

        public int Id;                          // Changes each use, so an old handle can't reach a new sound.
        public bool Active;

        // What's playing.
        public SoundPool? Pool;
        public LiveDefinition? Live;
        public LiveSound? LiveSource;
        public SoundBuffer? Buffer;
        public double Position;
        public float Rate = 1f;
        public long Started;

        // How it's played.
        public SoundBus Bus;
        public int Priority;
        public float Gain = 1f;                 // Linear, from what was asked for.
        public float Presence;
        public bool Positional;
        public float NearDistance, FarDistance, ReverbSend;
        public bool Doppler;
        public Vector3 Location, Velocity;

        // Going away.
        public bool Stopping;
        public float FadeStep;
        public float Fade = 1f;

        // Where the last block left things, for ramping from. Nothing changes between samples in steps.
        public float GainLeft, GainRight, Send;
        public float DelayLeft, DelayRight;
        public bool Begun;
        public OnePole AirLeft, AirRight;
        public float DopplerRate = 1f;

        // Smeared across the ears: the delay line holding what the far ear hasn't heard yet.
        public readonly float[] History = new float[DelayLength];
        public int HistoryPosition;

        public const int HistoryMask = DelayLength - 1;

        public void Reset()
        {
            Pool = null;
            Live = null;
            LiveSource = null;
            Buffer = null;
            Position = 0.0;
            Rate = 1f;
            Stopping = false;
            Fade = 1f;
            FadeStep = 0f;
            Begun = false;
            GainLeft = GainRight = Send = 0f;
            DelayLeft = DelayRight = 0f;
            DopplerRate = 1f;
            AirLeft = default;
            AirRight = default;
            Array.Clear(History);
            HistoryPosition = 0;
        }
    }
}
