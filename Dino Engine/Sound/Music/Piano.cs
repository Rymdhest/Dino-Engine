using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// A piano note as the modes of its strings, after Bank, Zambon and Fontana's modal piano. Each partial is a sine at the string's own stretched frequency, as a stiff string
    /// sharpens its overtones (Fletcher's f_n = n f0 sqrt(1 + B n^2)), with B from measurements across the keyboard: it's that stretch an ear misses first in a synthetic piano, which
    /// then sounds dull. The hammer decides how much of each partial there is: the place it strikes takes out the partials with a node there, and how long the felt stays on the string
    /// rounds off the top, so a hard blow is brighter as well as louder, which is how a real hammer's hardening felt behaves. The two or three strings of a note are never quite in tune,
    /// so the low partials each have a second string beside them, a fraction of a cent away, that dies more slowly: that's the piano's double decay, its fast first fall and long
    /// aftersound, and the slow beating within it. Higher partials die faster, as every measurement finds, and how hard a note is struck doesn't change how fast it dies.
    /// </summary>
    internal sealed class PianoVoice : OrchestraVoice
    {
        private const int MostPartials = 44;
        private const int Paired = 14;              // Partials that have an aftersound string beside them.
        private const int Oscillators = MostPartials + Paired;
        private const float HighestPartial = 11000f;
        private const float Quietest = 2e-5f;
        private const int DamperTop = 89;           // F6: above this a piano has no dampers, and a note rings on when the key comes up.
        private const float SampleTime = 1f / AudioFormat.SampleRate;

        // Inharmonicity, measured on a concert grand and interpolated by its logarithm (key: B).
        private static readonly (float key, float b)[] Stretch = { (21f, 3e-4f), (33f, 2.2e-4f), (45f, 2.5e-4f), (57f, 3.5e-4f), (69f, 6e-4f), (81f, 1.5e-3f), (93f, 6e-3f), (105f, 1.18e-2f), (108f, 1.39e-2f) };

        // Seconds a fundamental's aftersound takes to fall 60 dB, by key: long in the bass, short in the treble.
        private static readonly (float key, float seconds)[] Sustain = { (21f, 30f), (45f, 20f), (60f, 14f), (69f, 11f), (81f, 6f), (93f, 3f), (105f, 1.3f), (108f, 1f) };

        // Where the felt rounds off the top at a medium blow, by key, in Hz.
        private static readonly (float key, float hertz)[] Felt = { (21f, 900f), (60f, 2100f), (108f, 6500f) };

        private readonly float[] _re = new float[Oscillators];
        private readonly float[] _im = new float[Oscillators];
        private readonly float[] _stepRe = new float[Oscillators];
        private readonly float[] _stepIm = new float[Oscillators];
        private readonly float[] _amplitude = new float[Oscillators];
        private readonly float[] _fall = new float[Oscillators];
        private readonly float[] _damped = new float[Oscillators];
        private readonly SoundRandom _random;
        private int _count;
        private float _panLeft, _panRight;
        private float _knock, _knockFall, _knockLevel;
        private Svf _knockTone;
        private int _sinceNormal;

        public PianoVoice(SoundRandom random)
        {
            _random = random;
        }

        private static float Lookup((float key, float value)[] table, float key, bool logarithmic)
        {
            if (key <= table[0].key) return table[0].value;
            for (int i = 1; i < table.Length; i++)
            {
                if (key > table[i].key) continue;
                float t = (key - table[i - 1].key) / (table[i].key - table[i - 1].key);
                if (!logarithmic) return table[i - 1].value + (table[i].value - table[i - 1].value) * t;
                return MathF.Exp(MathF.Log(table[i - 1].value) + (MathF.Log(table[i].value) - MathF.Log(table[i - 1].value)) * t);
            }
            return table[^1].value;
        }

        /// <summary>How much the string stiffens its overtones, by key.</summary>
        public static float Inharmonicity(float key) => Lookup(Stretch, key, true);

        /// <summary>Frequency of a partial of a key, stretched as the string's stiffness has it.</summary>
        public static float PartialHertz(int key, int partial)
        {
            float hertz = 440f * MathF.Pow(2f, (key - 69f) / 12f);
            return partial * hertz * MathF.Sqrt(1f + Inharmonicity(key) * partial * partial);
        }

        public void Begin(in Note note)
        {
            Pitch = note.Pitch;
            Active = true;
            Releasing = false;
            Remaining = note.Length;
            _sinceNormal = 0;

            float key = note.Pitch;
            float loud = Math.Clamp(note.Loudness, 0.02f, 1f);
            float b = Inharmonicity(key);
            float fundamental = 440f * MathF.Pow(2f, (key - 69f) / 12f);

            // Where the hammer meets the string, about an eighth of the way along, and how much top the felt leaves: it stays on the string for a few milliseconds in the bass and
            // when soft, under one in the treble and when hard, and as felt hardens the harder it's pressed, a loud note is brighter as well as louder.
            float strike = _random.Range(0.11f, 0.135f);
            float corner = Lookup(Felt, key, true) * (0.4f + 1.5f * MathF.Pow(loud, 1.3f));

            float after = Lookup(Sustain, key, true);
            float prompt = after * _random.Range(0.2f, 0.3f);
            float level = (0.18f + 0.82f * MathF.Pow(loud, 1.4f)) * AudioFormat.DecibelsToGain(note.Fader) * 1.45f;

            _count = 0;
            float total = 0f;
            for (int n = 1; n <= MostPartials; n++)
            {
                float hertz = n * fundamental * MathF.Sqrt(1f + b * n * n);
                if (hertz > HighestPartial) break;

                float node = MathF.Abs(MathF.Sin(n * MathF.PI * strike));
                float felt = 1f / MathF.Sqrt(1f + MathF.Pow(hertz / corner, 4f));
                float amplitude = node * felt / MathF.Pow(n, 0.55f);
                if (amplitude < 1e-4f) continue;

                // Higher partials go sooner, whatever the note.
                float faster = 1f + (hertz - fundamental) / 1600f;
                bool paired = n <= Paired;
                float share = paired ? _random.Range(0.18f, 0.32f) : 0f;

                Add(hertz, amplitude * (1f - share), after / faster / (paired ? 1f : 1.6f) * (paired ? prompt / after : 1f));
                if (paired)
                {
                    // The other string of the unison, a fraction of a cent away, dying slowly: what's left when the first fall is over.
                    float cents = _random.Range(0.25f, 1.4f) * (_random.Chance(0.5f) ? 1f : -1f);
                    Add(hertz * MathF.Pow(2f, cents / 1200f), amplitude * share, after / faster);
                }
                total += amplitude;
            }

            float scale = level / MathF.Max(total, 1e-3f) * 1.6f;
            for (int k = 0; k < _count; k++) _amplitude[k] *= scale;

            // Bass on the left, treble on the right, as a pianist hears it.
            float pan = Math.Clamp((key - 64f) / 60f, -0.6f, 0.6f);
            float angle = (pan + 1f) * MathF.PI * 0.25f;
            _panLeft = MathF.Cos(angle) * 1.414f;
            _panRight = MathF.Sin(angle) * 1.414f;

            // The knock of the action and the board taking the blow, under the note for a moment, more of it the harder the key is struck.
            _knockLevel = level * (0.04f + 0.1f * loud);
            _knock = 1f;
            _knockFall = MathF.Exp(-SampleTime / 0.012f);
            _knockTone = Svf.Create(MathF.Min(fundamental * 1.5f + 120f, 900f), 0.9f);
        }

        // One partial, at a frequency, an amplitude and a time to fall 60 dB.
        private void Add(float hertz, float amplitude, float seconds)
        {
            if (_count >= Oscillators) return;
            int k = _count++;
            float phase = _random.Float() * MathF.Tau;
            _re[k] = MathF.Cos(phase);
            _im[k] = MathF.Sin(phase);
            float step = MathF.Tau * hertz * SampleTime;
            _stepRe[k] = MathF.Cos(step);
            _stepIm[k] = MathF.Sin(step);
            _amplitude[k] = amplitude;
            _fall[k] = MathF.Exp(-6.9078f * SampleTime / MathF.Max(seconds, 0.02f));

            // A damper on the string: gone in a few tenths of a second, the top first.
            _damped[k] = MathF.Exp(-6.9078f * SampleTime / MathF.Max(0.35f * 600f / (600f + hertz), 0.05f));
        }

        public override void Release()
        {
            if (Releasing) return;
            Releasing = true;
            if (Pitch > DamperTop) return;
            for (int k = 0; k < _count; k++) _fall[k] = MathF.Min(_fall[k], _damped[k]);
        }

        public override void Render(float[] left, float[] right, int count)
        {
            if (!Releasing)
            {
                Remaining -= count * SampleTime;
                if (Remaining <= 0f) Release();
            }

            int alive = 0;
            int k = 0;

            // Four partials at a time, so that each one's chain of multiplies doesn't hold up the next.
            for (; k + 3 < _count; k += 4)
            {
                float r0 = _re[k], i0 = _im[k], c0 = _stepRe[k], s0 = _stepIm[k], a0 = _amplitude[k], f0 = _fall[k];
                float r1 = _re[k + 1], i1 = _im[k + 1], c1 = _stepRe[k + 1], s1 = _stepIm[k + 1], a1 = _amplitude[k + 1], f1 = _fall[k + 1];
                float r2 = _re[k + 2], i2 = _im[k + 2], c2 = _stepRe[k + 2], s2 = _stepIm[k + 2], a2 = _amplitude[k + 2], f2 = _fall[k + 2];
                float r3 = _re[k + 3], i3 = _im[k + 3], c3 = _stepRe[k + 3], s3 = _stepIm[k + 3], a3 = _amplitude[k + 3], f3 = _fall[k + 3];
                for (int n = 0; n < count; n++)
                {
                    float t0 = r0 * c0 - i0 * s0; i0 = r0 * s0 + i0 * c0; r0 = t0;
                    float t1 = r1 * c1 - i1 * s1; i1 = r1 * s1 + i1 * c1; r1 = t1;
                    float t2 = r2 * c2 - i2 * s2; i2 = r2 * s2 + i2 * c2; r2 = t2;
                    float t3 = r3 * c3 - i3 * s3; i3 = r3 * s3 + i3 * c3; r3 = t3;
                    float value = i0 * a0 + i1 * a1 + i2 * a2 + i3 * a3;
                    a0 *= f0; a1 *= f1; a2 *= f2; a3 *= f3;
                    left[n] += value * _panLeft;
                    right[n] += value * _panRight;
                }
                _re[k] = r0; _im[k] = i0; _amplitude[k] = a0;
                _re[k + 1] = r1; _im[k + 1] = i1; _amplitude[k + 1] = a1;
                _re[k + 2] = r2; _im[k + 2] = i2; _amplitude[k + 2] = a2;
                _re[k + 3] = r3; _im[k + 3] = i3; _amplitude[k + 3] = a3;
            }
            for (; k < _count; k++)
            {
                float r = _re[k], i = _im[k], c = _stepRe[k], s = _stepIm[k], a = _amplitude[k], f = _fall[k];
                for (int n = 0; n < count; n++)
                {
                    float t = r * c - i * s; i = r * s + i * c; r = t;
                    float value = i * a;
                    a *= f;
                    left[n] += value * _panLeft;
                    right[n] += value * _panRight;
                }
                _re[k] = r; _im[k] = i; _amplitude[k] = a;
            }

            if (_knock > 1e-4f)
            {
                for (int n = 0; n < count; n++)
                {
                    float thud = _knockTone.Bandpass(_random.Bipolar()) * _knock * _knockLevel;
                    _knock *= _knockFall;
                    left[n] += thud;
                    right[n] += thud;
                }
            }

            // A rotation held in floats drifts off the unit circle, so it's put back now and then. Partials that have faded are dropped, which is most of them, soon.
            _sinceNormal += count;
            bool normalise = _sinceNormal > 2048;
            if (normalise) _sinceNormal = 0;
            for (int j = 0; j < _count; j++)
            {
                if (_amplitude[j] < Quietest) continue;
                if (normalise)
                {
                    float length = 1f / MathF.Sqrt(_re[j] * _re[j] + _im[j] * _im[j]);
                    _re[j] *= length;
                    _im[j] *= length;
                }
                if (alive != j)
                {
                    _re[alive] = _re[j]; _im[alive] = _im[j]; _stepRe[alive] = _stepRe[j]; _stepIm[alive] = _stepIm[j];
                    _amplitude[alive] = _amplitude[j]; _fall[alive] = _fall[j]; _damped[alive] = _damped[j];
                }
                alive++;
            }
            _count = alive;
            if (_count == 0 && _knock <= 1e-4f) Active = false;
        }
    }
}
