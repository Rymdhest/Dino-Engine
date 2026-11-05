using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>A vocal tract: what shapes the source into a vowel. Frequencies in Hz, from the first formant up.</summary>
    public readonly struct Vowel
    {
        public readonly float F1, F2, F3, F4;

        public Vowel(float f1, float f2, float f3, float f4 = 3500f)
        {
            F1 = f1;
            F2 = f2;
            F3 = f3;
            F4 = f4;
        }

        public static Vowel Lerp(in Vowel a, in Vowel b, float t) => new Vowel(
            a.F1 + (b.F1 - a.F1) * t,
            a.F2 + (b.F2 - a.F2) * t,
            a.F3 + (b.F3 - a.F3) * t,
            a.F4 + (b.F4 - a.F4) * t);

        // Peterson and Barney's measurements of adult men.
        public static readonly Vowel Ah = new(730f, 1090f, 2440f);
        public static readonly Vowel Uh = new(640f, 1190f, 2390f);
        public static readonly Vowel Eh = new(530f, 1840f, 2480f);
        public static readonly Vowel Ee = new(270f, 2290f, 3010f);
        public static readonly Vowel Ih = new(390f, 1990f, 2550f);
        public static readonly Vowel Oh = new(570f, 840f, 2410f);
        public static readonly Vowel Oo = new(300f, 870f, 2240f);
        public static readonly Vowel Ae = new(660f, 1720f, 2410f);
        public static readonly Vowel Er = new(490f, 1350f, 1690f);
    }

    /// <summary>What a throat does over the length of an utterance.</summary>
    public struct VoiceSettings
    {
        public Curve Pitch;         // Hz.
        public Curve Loudness;      // 0..1 of the breath pushing through.
        public Func<float, Vowel> Tract;
        public float Size;          // 1 a man; above 1 a bigger throat, all formants lower together.
        public float Breath;        // 0..1 air leaking through the voice.
        public float Rough;         // 0..1 how irregular the folds are: strain, or a growl.
        public float Tension;       // 0 slack and soft, 1 pressed and bright. It sets how sharply the folds shut.
    }

    /// <summary>
    /// A voice made as a voice is: the vocal folds open and snap shut, and each closing is a pulse that the tract then shapes into
    /// a vowel. It's the closing that makes a voice bright, so the pulse is the Rosenberg shape with a sharpness that follows
    /// effort, with the jitter and shimmer of real folds, as a perfectly regular pulse train is what makes a voice sound like a machine.
    /// </summary>
    public static class Vocal
    {
        private const float Bandwidth1 = 90f;

        public static Signal Utter(Take take, float seconds, in VoiceSettings voice)
        {
            Signal output = take.Track(seconds);
            SoundRandom random = take.Random;

            float phase = 0f;
            float jitter = 0f;
            float shimmer = 1f;
            float[] samples = output.Samples;

            // Formants are resonators in a row, as the tract is one pipe.
            var f1 = new Svf();
            var f2 = new Svf();
            var f3 = new Svf();
            var f4 = new Svf();
            var breath = new Svf();
            breath.Set(2200f, 0.6f);
            var tilt = new OnePole();
            tilt.SetCutoff(5500f);

            float previous = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                float t = i * (1f / AudioFormat.SampleRate);
                float loudness = voice.Loudness.At(t);
                float pitch = voice.Pitch.At(t);

                if (i % 32 == 0)
                {
                    Vowel vowel = voice.Tract(t);
                    float scale = 1f / voice.Size;
                    float gain = 0.35f + 0.65f * loudness;
                    f1.Set(vowel.F1 * scale, vowel.F1 * scale / Bandwidth1 * (0.8f + gain * 0.5f));
                    f2.Set(vowel.F2 * scale, vowel.F2 * scale / 110f);
                    f3.Set(vowel.F3 * scale, vowel.F3 * scale / 160f);
                    f4.Set(vowel.F4 * scale, vowel.F4 * scale / 220f);
                }

                phase += pitch * (1f + jitter) * (1f / AudioFormat.SampleRate);
                if (phase >= 1f)
                {
                    phase -= 1f;

                    // Per cycle: pitch a little off, level a little off, more of both with strain. A rough voice also skips and doubles.
                    jitter = random.Gaussian() * (0.006f + voice.Rough * 0.05f);
                    shimmer = 1f + random.Gaussian() * (0.04f + voice.Rough * 0.2f);
                    if (voice.Rough > 0.5f && random.Chance((voice.Rough - 0.5f) * 0.5f)) shimmer *= 0.4f;
                }

                // Rosenberg: a slow opening and a quick closing. Tension shortens the closing, which is where the high harmonics are.
                float open = 0.6f - voice.Tension * 0.15f;
                float close = 0.3f - voice.Tension * 0.22f;
                float flow;
                if (phase < open) flow = 0.5f * (1f - MathF.Cos(MathF.PI * phase / open));
                else if (phase < open + close) flow = MathF.Cos(0.5f * MathF.PI * (phase - open) / close);
                else flow = 0f;

                // What radiates from the lips is the change in flow.
                float source = (flow - previous) * shimmer * 22f;
                previous = flow;

                float air = breath.BandpassNormal(random.Bipolar()) * (voice.Breath * 0.55f) * (0.6f + flow);
                float excitation = tilt.Lowpass(source) * (1f - voice.Breath * 0.6f) + air;

                float a = f1.Bandpass(excitation) * 0.09f;
                float b = f2.Bandpass(excitation) * 0.05f;
                float c = f3.Bandpass(excitation) * 0.028f;
                float d = f4.Bandpass(excitation) * 0.016f;
                samples[i] = (a + b + c + d) * loudness;
            }
            return output;
        }
    }
}
