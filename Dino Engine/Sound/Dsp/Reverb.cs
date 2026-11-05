namespace Dino_Engine.Sound.Dsp
{
    /// <summary>What a place does to sound. Outdoors there are few walls, so it's mostly in how the ground, trees and far-off rock answer.</summary>
    public struct ReverbSettings
    {
        public float DecayTime;     // Seconds to fall 60 dB in the mid range.
        public float DecayHigh;     // The same at the top, as air and soft things eat high sound first.
        public float PreDelay;      // Seconds before anything comes back: the distance of the nearest big surface.
        public float Wet;           // How much of it there is.
        public float Echo;          // Level of the far echo off cliffs and hills: 0 for none.
        public float EchoTime;      // Seconds until that answers.
        public float EchoBrightness;// Hz above which the echo has lost its top.

        public static ReverbSettings Lerp(in ReverbSettings a, in ReverbSettings b, float t) => new ReverbSettings
        {
            DecayTime = a.DecayTime + (b.DecayTime - a.DecayTime) * t,
            DecayHigh = a.DecayHigh + (b.DecayHigh - a.DecayHigh) * t,
            PreDelay = a.PreDelay + (b.PreDelay - a.PreDelay) * t,
            Wet = a.Wet + (b.Wet - a.Wet) * t,
            Echo = a.Echo + (b.Echo - a.Echo) * t,
            EchoTime = a.EchoTime + (b.EchoTime - a.EchoTime) * t,
            EchoBrightness = a.EchoBrightness + (b.EchoBrightness - a.EchoBrightness) * t,
        };
    }

    /// <summary>
    /// A feedback delay network reverb, in the form set out by Geraint Luff: first a diffuser, which turns each echo into many
    /// by passing it through steps of delays mixed by a Hadamard matrix, then eight delay lines fed back through a Householder
    /// matrix, which keeps energy exactly and needs no multiplications to speak of. Echo density comes from the diffuser, so
    /// the loop needs only to sustain it, and because the matrices lose nothing the decay is set entirely by one gain per
    /// line, worked out from the wanted time. Each line also loses its top on the way round, so the tail darkens as a real one does.
    /// </summary>
    public sealed class Reverb
    {
        private const int Channels = 8;
        private const int DiffusionSteps = 4;
        private const int Size = 1 << 15;           // Longest of any delay, in samples, and a power of two.
        private const int Mask = Size - 1;
        private const float Modulation = 7f;        // Samples either way the loop's delays drift, which keeps its tail from ringing.
        private static readonly float DampingCos = MathF.Cos(MathF.Tau * 5000f / AudioFormat.SampleRate);   // DecayHigh is met at 5 kHz.

        // Milliseconds. Lengths share no factors worth the name, or their echoes would pile up on the same moments.
        private static readonly float[] LoopDelays = { 47.3f, 57.1f, 67.9f, 79.3f, 89.9f, 103.7f, 113.9f, 127.1f };

        private readonly float[][][] _diffusers;
        private readonly int[][] _diffuserDelays = new int[DiffusionSteps][];
        private readonly bool[][] _flips = new bool[DiffusionSteps][];
        private readonly int[][] _shuffles = new int[DiffusionSteps][];
        private readonly float[][] _loops = new float[Channels][];
        private readonly float[] _loopDelays = new float[Channels];
        private readonly float[] _loopGains = new float[Channels];
        private readonly float[] _dampStates = new float[Channels];
        private readonly float[] _dampCoefficients = new float[Channels];
        private readonly float[] _phases = new float[Channels];
        private readonly float[] _phaseSteps = new float[Channels];
        private readonly float[] _preLeft = new float[Size];
        private readonly float[] _preRight = new float[Size];
        private readonly float[] _echoLeft = new float[Size * 2];
        private readonly float[] _echoRight = new float[Size * 2];
        private readonly float[] _channels = new float[Channels];
        private readonly float[] _scratch = new float[Channels];
        private OnePole _echoFilterLeft, _echoFilterRight;
        private OnePole _preDelaySmoothing, _echoTimeSmoothing;
        private ReverbSettings _settings;
        private int _write;
        private int _echoWrite;

        public Reverb(ulong seed = 1)
        {
            var random = new SoundRandom(seed);
            _diffusers = new float[DiffusionSteps][][];

            // The steps double in length, which spreads the echoes smoothly from the first millisecond to the loop's own delays.
            float range = 0.003f;
            for (int step = 0; step < DiffusionSteps; step++)
            {
                _diffusers[step] = new float[Channels][];
                _diffuserDelays[step] = new int[Channels];
                _flips[step] = new bool[Channels];
                _shuffles[step] = new int[Channels];
                var order = new List<int>(Enumerable.Range(0, Channels));
                for (int c = 0; c < Channels; c++)
                {
                    _diffusers[step][c] = new float[Size / 4];
                    float low = range * c / Channels;
                    float high = range * (c + 1) / Channels;
                    _diffuserDelays[step][c] = Math.Max((int)(random.Range(low, high) * AudioFormat.SampleRate), 1);
                    _flips[step][c] = random.Chance(0.5f);
                    int pick = random.Int(order.Count);
                    _shuffles[step][c] = order[pick];
                    order.RemoveAt(pick);
                }
                range *= 2f;
            }

            for (int c = 0; c < Channels; c++)
            {
                _loops[c] = new float[Size];
                _loopDelays[c] = LoopDelays[c] * 0.001f * AudioFormat.SampleRate;
                _phases[c] = random.Float() * MathF.Tau;
                _phaseSteps[c] = MathF.Tau * random.Range(0.11f, 0.37f) / AudioFormat.SampleRate;
            }

            _preDelaySmoothing.SetTime(0.5f);
            _echoTimeSmoothing.SetTime(0.5f);
            Configure(new ReverbSettings { DecayTime = 0.8f, DecayHigh = 0.3f, PreDelay = 0.01f, EchoTime = 0.3f, EchoBrightness = 3000f });
            _preDelaySmoothing.State = _settings.PreDelay;
            _echoTimeSmoothing.State = _settings.EchoTime;
        }

        /// <summary>Takes effect smoothly: the decay at once, as it can only be heard in what comes next, and the delays gliding.</summary>
        public void Configure(in ReverbSettings settings)
        {
            _settings = settings;
            float decay = MathF.Max(settings.DecayTime, 0.05f);
            float high = Math.Clamp(settings.DecayHigh, 0.02f, decay);

            for (int c = 0; c < Channels; c++)
            {
                float seconds = _loopDelays[c] / AudioFormat.SampleRate;
                _loopGains[c] = MathF.Pow(10f, -3f * seconds / decay);

                // How much more the top has to lose per trip round, as the pole of a one pole lowpass giving that at the reference
                // frequency: the positive root of r^2 |1 - p e^-jw|^2 = (1 - p)^2.
                float highGain = MathF.Pow(10f, -3f * seconds / high);
                float ratio = Math.Clamp(highGain / _loopGains[c], 0.02f, 0.9999f);
                float r2 = ratio * ratio;
                float a = r2 - 1f;
                float b = 2f * (1f - r2 * DampingCos);
                _dampCoefficients[c] = (-b + MathF.Sqrt(MathF.Max(b * b - 4f * a * a, 0f))) / (2f * a);
            }
            _echoFilterLeft.SetCutoff(settings.EchoBrightness);
            _echoFilterRight.SetCutoff(settings.EchoBrightness);
        }

        /// <summary>Adds the reverb of what's in the input to the output. Both pairs are the same length.</summary>
        public void Process(ReadOnlySpan<float> inLeft, ReadOnlySpan<float> inRight, Span<float> outLeft, Span<float> outRight)
        {
            float[] channels = _channels;
            float wet = _settings.Wet;
            float echo = _settings.Echo;

            for (int i = 0; i < inLeft.Length; i++)
            {
                // Before anything, the wait for the first surface.
                _preLeft[_write] = inLeft[i];
                _preRight[_write] = inRight[i];
                float pre = _preDelaySmoothing.Lowpass(_settings.PreDelay) * AudioFormat.SampleRate;
                int read = (_write - (int)pre) & Mask;
                float left = _preLeft[read];
                float right = _preRight[read];

                // Left and right go in with the signs alternating across the eight channels, to come out as wide as they went in.
                for (int c = 0; c < Channels; c++) channels[c] = ((c & 1) == 0 ? left : right) * (((c & 2) == 0) ? 1f : -1f);

                for (int step = 0; step < DiffusionSteps; step++) Diffuse(step, channels);

                float mix = 0f;
                for (int c = 0; c < Channels; c++)
                {
                    _phases[c] += _phaseSteps[c];
                    if (_phases[c] > MathF.Tau) _phases[c] -= MathF.Tau;
                    float delay = _loopDelays[c] + Modulation * MathF.Sin(_phases[c]);
                    float position = _write - delay;
                    int whole = (int)MathF.Floor(position);
                    float fraction = position - whole;
                    float[] line = _loops[c];
                    float a = line[whole & Mask];
                    float b = line[(whole + 1) & Mask];
                    float value = (a + (b - a) * fraction) * _loopGains[c];

                    _dampStates[c] += (value - _dampStates[c]) * (1f - _dampCoefficients[c]) + AudioFormat.Denormal;
                    value = _dampStates[c];
                    _scratch[c] = value;
                    mix += value;
                }

                // Householder: each line gets itself, less a quarter of them all. Nothing lost, and everything feeds everything.
                mix *= 2f / Channels;
                for (int c = 0; c < Channels; c++)
                {
                    _loops[c][_write] = _scratch[c] - mix + channels[c] * 0.35f;
                }

                float tailLeft = _scratch[0] - _scratch[2] + _scratch[4] - _scratch[6];
                float tailRight = _scratch[1] - _scratch[3] + _scratch[5] - _scratch[7];

                float early = 0.5f;
                float reverbLeft = (tailLeft + channels[0] * early) * wet * 0.5f;
                float reverbRight = (tailRight + channels[1] * early) * wet * 0.5f;

                // A distant cliff answers late and dull, and it comes back from a little way round.
                _echoLeft[_echoWrite] = inLeft[i];
                _echoRight[_echoWrite] = inRight[i];
                float time = _echoTimeSmoothing.Lowpass(_settings.EchoTime) * AudioFormat.SampleRate;
                int echoMask = _echoLeft.Length - 1;
                float farLeft = _echoFilterLeft.Lowpass(_echoRight[(_echoWrite - (int)time) & echoMask]);
                float farRight = _echoFilterRight.Lowpass(_echoLeft[(_echoWrite - (int)(time * 1.13f)) & echoMask]);

                outLeft[i] += reverbLeft + farLeft * echo;
                outRight[i] += reverbRight + farRight * echo;

                _write = (_write + 1) & Mask;
                _echoWrite = (_echoWrite + 1) & echoMask;
            }
        }

        private void Diffuse(int step, float[] channels)
        {
            float[][] lines = _diffusers[step];
            int[] delays = _diffuserDelays[step];
            int[] shuffle = _shuffles[step];
            bool[] flips = _flips[step];
            int mask = Size / 4 - 1;
            float[] delayed = _scratch;

            for (int c = 0; c < Channels; c++)
            {
                float[] line = lines[c];
                line[_write & mask] = channels[c];
                delayed[c] = line[(_write - delays[c]) & mask];
            }
            for (int c = 0; c < Channels; c++)
            {
                float value = delayed[shuffle[c]];
                channels[c] = flips[c] ? -value : value;
            }
            Hadamard(channels);
        }

        // Eight wide, by butterflies: orthogonal, so energy passes straight through, but every output now holds every input.
        private static void Hadamard(float[] v)
        {
            for (int span = 1; span < Channels; span *= 2)
            {
                for (int start = 0; start < Channels; start += span * 2)
                {
                    for (int i = start; i < start + span; i++)
                    {
                        float a = v[i];
                        float b = v[i + span];
                        v[i] = a + b;
                        v[i + span] = a - b;
                    }
                }
            }
            const float Scale = 0.35355339f;    // 1 / sqrt(8)
            for (int c = 0; c < Channels; c++) v[c] *= Scale;
        }
    }
}
