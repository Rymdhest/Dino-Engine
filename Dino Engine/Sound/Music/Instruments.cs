using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Music
{
    /// <summary>Everyone in the orchestra. A part is written for one of these.</summary>
    public enum Section
    {
        Flute,
        Oboe,
        Clarinet,
        Horns,
        Trombones,
        Trumpets,
        Violins,
        Violas,
        Cellos,
        Basses,
        Fiddle,
        Choir,
        Harp,
        Lute,
        Pizzicato,
        Celesta,
        Bells,
        Timpani,
        BigDrum,
        FrameDrum,
        Tambourine,
        Cymbal,
    }

    /// <summary>How a note is played.</summary>
    public enum Articulation
    {
        Legato,     // Joined to the next, with no fresh attack.
        Normal,
        Marcato,    // Leaned on.
        Staccato,   // Short and separate.
        Tremolo,    // A bow shaking on the string.
        Swell,      // Rises from nothing.
    }

    /// <summary>What's written down: a note for a section.</summary>
    public struct Note
    {
        public Section Section;
        public int Pitch;               // MIDI.
        public float Start;             // Seconds from the start of its bar.
        public float Length;            // Seconds.
        public float Loudness;          // 0 pianissimo to 1 fortissimo.
        public float Fader;             // Decibels. How far forward it sits in the mix, which isn't how hard it's played: that changes its sound, this doesn't.
        public Articulation Articulation;
        public float Tail;              // Seconds it takes to die away once let go, where that's longer than the player's usual release. What carries one style into the next.
    }

    /// <summary>How a kind of sustained instrument behaves, as measured or as players describe it.</summary>
    public sealed class SustainedStyle
    {
        public Timbre Timbre = null!;
        public int Players = 1;         // How many play each note.
        public float Scatter = 3f;      // Cents players differ by.
        public float Attack = 0.08f;    // Seconds, at mezzo forte. Louder is quicker.
        public float Release = 0.18f;
        public float VibratoRate = 5.2f;
        public float VibratoDepth = 15f;// Cents at fortissimo. Measured on a flute, depth goes from 7 at pp to 20 at ff.
        public float VibratoDelay = 0.25f;
        public float Scoop = 0f;        // Cents below the note a fresh attack starts from.
        public float Breath = 0.01f;    // Noise with the tone: air in a flute, hair on a bow.
        public float BreathPitch = 2f;  // Where that noise sits, as a multiple of the note.
        public float Chiff = 0.1f;      // The noise at the start of a note.
        public float Range = 20f;       // Decibels between pianissimo and fortissimo. A horn measured 13, a flute 10, strings far more.
        public float Tremor = 0.03f;    // How much loudness moves with the vibrato. On a flute vibrato is much more this than it is pitch.
    }

    /// <summary>A note sounding. Voices are made once and used again, so a long session allocates nothing.</summary>
    internal abstract class OrchestraVoice
    {
        public bool Active;
        public bool Releasing;
        public int Pitch;
        public float Remaining;         // Seconds until it lets go.
        public long Started;

        public abstract void Release();

        /// <summary>Adds its sound into a pair of buffers.</summary>
        public abstract void Render(float[] left, float[] right, int count);
    }

    /// <summary>
    /// A held note: winds, brass, bowed strings and voices. Each player reads the timbre at their own slightly wandering pitch, with their own vibrato and timing, so that a section
    /// sounds like several people and not one wave with a chorus on it: unison choirs measure up to about 14 cents apart before anyone objects. Everything that moves here
    /// moves slowly and at random, as nothing a person does is periodic, and steady sound is what gives a synthesizer away.
    /// </summary>
    internal sealed class SustainedVoice : OrchestraVoice
    {
        private const int Most = 5;
        private const float SampleTime = 1f / AudioFormat.SampleRate;
        private const int ControlStep = 32;
        private const float DecayConstants = 3f;    // Time constants in what's heard as a fade: three is down by 26 dB.

        private readonly float[] _phases = new float[Most];
        private readonly float[] _detune = new float[Most];
        private readonly float[] _vibratoPhase = new float[Most];
        private readonly float[] _vibratoRate = new float[Most];
        private readonly float[] _steps = new float[Most];
        private readonly float[] _panLeft = new float[Most];
        private readonly float[] _panRight = new float[Most];
        private readonly float[] _delays = new float[Most];
        private readonly float[] _drift = new float[Most];
        private readonly float[] _driftTarget = new float[Most];
        private readonly SoundRandom _random;
        private SustainedStyle _style = null!;
        private float _note, _bend, _bendFall;
        private float _level, _levelTarget, _levelRate, _swell, _releaseTime;
        private float _loudness, _age, _chiff, _chiffFall, _shimmer = 1f;
        private bool _tremolo;
        private float _tremoloPhase, _tremoloRate;
        private float[] _quiet = null!, _loud = null!;
        private float _blend;
        private int _untilControl;
        private Svf _breath;
        private OnePole _breathTone;

        public SustainedVoice(SoundRandom random)
        {
            _random = random;
        }

        public void Begin(SustainedStyle style, in Note note)
        {
            _style = style;
            Pitch = note.Pitch;
            Active = true;
            Releasing = false;
            Remaining = note.Length;
            _age = 0f;
            _note = note.Pitch;
            _loudness = note.Loudness;
            _level = 0f;
            _untilControl = 0;
            _tremolo = note.Articulation == Articulation.Tremolo;
            _tremoloRate = _random.Range(11f, 14f);

            // Harder means quicker.
            float speed = 1.6f - note.Loudness;
            float attack = style.Attack * speed;
            if (note.Articulation == Articulation.Marcato) attack *= 0.4f;
            else if (note.Articulation == Articulation.Staccato) attack *= 0.25f;
            else if (note.Articulation == Articulation.Swell) attack = note.Length * 0.6f;

            _levelTarget = AudioFormat.DecibelsToGain(note.Fader - style.Range * (1f - note.Loudness));

            // A short note is stopped by the player, not left to ring.
            _releaseTime = note.Articulation == Articulation.Staccato ? style.Release * 0.28f : style.Release;
            if (note.Tail > 0f) _releaseTime = note.Tail / DecayConstants;
            _levelRate = 1f - MathF.Exp(-1f / (MathF.Max(attack, 0.002f) * AudioFormat.SampleRate / ControlStep));
            _swell = note.Articulation == Articulation.Marcato ? 1.5f : 1f;

            _bend = -style.Scoop;
            _bendFall = 1f - MathF.Exp(-1f / (0.035f * AudioFormat.SampleRate / ControlStep));

            bool articulated = note.Articulation != Articulation.Swell;
            _chiff = articulated ? style.Chiff * (0.5f + note.Loudness) * (note.Articulation == Articulation.Marcato ? 1.6f : 1f) : 0f;
            _chiffFall = MathF.Exp(-1f / (0.012f * AudioFormat.SampleRate));

            _breath = Svf.Create(MathF.Min(Hertz(note.Pitch) * style.BreathPitch, 11000f), 1.4f);
            _breathTone.SetCutoff(9000f);

            int players = Math.Min(style.Players, Most);
            for (int p = 0; p < players; p++)
            {
                _phases[p] = _random.Float();
                _detune[p] = style.Players > 1 ? _random.Gaussian() * style.Scatter : 0f;
                _vibratoPhase[p] = _random.Float();
                _vibratoRate[p] = style.VibratoRate * _random.Range(0.9f, 1.12f);
                _delays[p] = style.Players > 1 ? _random.Range(0f, 0.03f) : 0f;
                _drift[p] = 0f;
                _driftTarget[p] = 0f;
                float pan = style.Players > 1 ? (p / (players - 1f) * 2f - 1f) * 0.75f : 0f;
                float angle = (pan + 1f) * MathF.PI * 0.25f;
                _panLeft[p] = MathF.Cos(angle) * 1.2f / MathF.Sqrt(players);
                _panRight[p] = MathF.Sin(angle) * 1.2f / MathF.Sqrt(players);
            }
        }

        /// <summary>Goes on to a new pitch without a new attack, for a tune that's slurred.</summary>
        public void Slur(in Note note)
        {
            // A slide across the gap, quick enough not to be heard as one, as the finger or lips move.
            _bend = (Pitch - note.Pitch) * 100f;
            _bendFall = 1f - MathF.Exp(-1f / (0.022f * AudioFormat.SampleRate / ControlStep));
            Pitch = note.Pitch;
            _note = note.Pitch;
            _loudness = note.Loudness;
            Remaining = note.Length;
            _levelTarget = AudioFormat.DecibelsToGain(note.Fader - _style.Range * (1f - note.Loudness));
        }

        /// <summary>Lets go slowly, over about this long, for when the band is making way and hasn't been stopped.</summary>
        public void Fade(float seconds)
        {
            _releaseTime = MathF.Max(_releaseTime, seconds / DecayConstants);
            Release();
        }

        public override void Release()
        {
            if (Releasing) return;
            Releasing = true;
            _levelTarget = 0f;
            _levelRate = 1f - MathF.Exp(-1f / (_releaseTime * AudioFormat.SampleRate / ControlStep));
        }

        private static float Hertz(float note) => 440f * MathF.Pow(2f, (note - 69f) / 12f);

        private void Control(int players)
        {
            const float Step = ControlStep * SampleTime;
            _age += Step;
            _level += (_levelTarget * _swell - _level) * _levelRate;
            _swell += (1f - _swell) * 0.02f;
            _bend *= 1f - _bendFall;

            _style.Timbre.Select(_note, MathF.Min(_loudness + _level * 0.1f, 1f), out _quiet, out _loud, out _blend);

            // Vibrato comes in after the note has settled, as a player only then begins it, and grows with how hard they're playing.
            SustainedStyle style = _style;
            float onset = Math.Clamp((_age - style.VibratoDelay) / 0.35f, 0f, 1f);
            float depth = style.VibratoDepth * (0.35f + 0.65f * _loudness) * onset;
            _shimmer = 1f + style.Tremor * onset * MathF.Sin(_vibratoPhase[0] * MathF.Tau + 0.8f);
            for (int p = 0; p < players; p++)
            {
                _vibratoPhase[p] += _vibratoRate[p] * Step;
                if (_vibratoPhase[p] >= 1f) _vibratoPhase[p] -= 1f;

                // The slow wandering that keeps a held pitch from sounding like a machine's.
                if (_random.Chance(0.02f)) _driftTarget[p] = _random.Gaussian() * 2.5f;
                _drift[p] += (_driftTarget[p] - _drift[p]) * 0.01f;

                float cents = _detune[p] + _drift[p] + _bend + depth * MathF.Sin(_vibratoPhase[p] * MathF.Tau);
                _steps[p] = 440f * MathF.Pow(2f, (_note + cents * 0.01f - 69f) / 12f) * SampleTime;
            }
        }

        public override void Render(float[] left, float[] right, int count)
        {
            SustainedStyle style = _style;
            int players = Math.Min(style.Players, Most);
            float[] phases = _phases;

            for (int i = 0; i < count; i++)
            {
                if (--_untilControl <= 0)
                {
                    _untilControl = ControlStep;
                    Control(players);
                    if (!Releasing)
                    {
                        Remaining -= ControlStep * SampleTime;
                        if (Remaining <= 0f) Release();
                    }
                    else if (_level < 0.0003f)
                    {
                        Active = false;
                        return;
                    }
                }

                float l = 0f, r = 0f;
                for (int p = 0; p < players; p++)
                {
                    if (_delays[p] > 0f)
                    {
                        _delays[p] -= SampleTime;
                        continue;
                    }
                    float phase = phases[p] + _steps[p];
                    if (phase >= 1f) phase -= 1f;
                    phases[p] = phase;

                    float a = Timbre.Read(_quiet, phase);
                    float value = a + (Timbre.Read(_loud, phase) - a) * _blend;
                    l += value * _panLeft[p];
                    r += value * _panRight[p];
                }

                float level = _level * _shimmer;
                if (_tremolo)
                {
                    _tremoloPhase += _tremoloRate * SampleTime;
                    if (_tremoloPhase >= 1f) _tremoloPhase -= 1f;
                    level *= 0.68f + 0.32f * MathF.Sin(_tremoloPhase * MathF.Tau);
                }

                float noise = _random.Bipolar();
                float air = _breath.BandpassNormal(noise) * style.Breath + _breathTone.Lowpass(noise) * _chiff;
                _chiff *= _chiffFall;

                left[i] += (l + air) * level;
                right[i] += (r + air) * level;
            }
        }
    }

    /// <summary>
    /// A plucked string: the Karplus-Strong loop as extended by Jaffe and Smith. A burst goes round a delay line as long as the string's period, losing its top a little more
    /// each time round, which is just what a real string does, so its sound comes out right with no envelope at all. Two loops a hair apart stand for the string's two planes of
    /// motion, which give a pluck its quick first fall and its long ring, and a comb sets where it was plucked, as plucking at one fifth of the length takes out the fifth harmonic.
    /// </summary>
    internal sealed class PluckedVoice : OrchestraVoice
    {
        private const int Longest = 2048;
        private const int Mask = Longest - 1;

        private readonly float[] _first = new float[Longest];
        private readonly float[] _second = new float[Longest];
        private readonly SoundRandom _random;
        private int _write;
        private int _delayOne, _delayTwo;
        private float _fractionOne, _fractionTwo;
        private float _allpassOne, _allpassTwo;
        private float _lowOne, _lowTwo;
        private float _damping, _gain, _level, _pan;
        private float _released;

        public PluckedVoice(SoundRandom random)
        {
            _random = random;
        }

        /// <param name="brightness">0 a soft thumb, 1 a nail.</param>
        /// <param name="ring">Seconds to die away in the mid range.</param>
        public void Begin(in Note note, float brightness, float ring)
        {
            Pitch = note.Pitch;
            Active = true;
            Releasing = false;
            Remaining = note.Length;
            _released = 1f;

            float hertz = 440f * MathF.Pow(2f, (note.Pitch - 69f) / 12f);
            float period = AudioFormat.SampleRate / hertz;

            // The loop's own filter adds half a sample to its length, which matters a lot up high.
            Tune(period - 0.5f, out _delayOne, out _fractionOne);
            Tune(period * 1.0007f - 0.5f, out _delayTwo, out _fractionTwo);

            // How much is lost each trip: more, for shorter strings and for a thing that doesn't ring long.
            float trips = ring * hertz;
            _gain = MathF.Pow(0.001f, 1f / MathF.Max(trips, 1f));

            float loud = note.Loudness;
            _damping = Math.Clamp(0.5f - brightness * 0.42f + (1f - loud) * 0.1f, 0.04f, 0.6f);
            _lowOne = _lowTwo = 0f;
            _allpassOne = _allpassTwo = 0f;
            _level = (0.35f + loud * 0.65f) * AudioFormat.DecibelsToGain(note.Fader);
            _write = 0;
            Array.Clear(_first);
            Array.Clear(_second);

            // The pluck. A string drawn aside and let go is a triangle, which reaches the bridge as a pulse: its harmonics fall as one over their number, and those with a node
            // where it was plucked go missing. The width of the finger then takes off the top, more for a soft touch. Noise would be far too bright, as a string has no such edge.
            int length = Math.Min((int)period + 1, Longest - 2);
            float position = _random.Range(0.11f, 0.2f);
            int pulse = Math.Max((int)(length * position), 1);
            float high = 1f - position;
            float low = -position;
            var finger = new OnePole();
            var flesh = new OnePole();
            float touch = 700f + brightness * 4200f * (0.35f + loud);
            finger.SetCutoff(touch);
            flesh.SetCutoff(touch * 1.5f);

            // No two plucks are quite alike, so a little of the finger's own unevenness goes in.
            var rough = new OnePole();
            rough.SetCutoff(touch);
            for (int i = 0; i < length; i++)
            {
                float shape = i < pulse ? high : low;
                _first[i] = flesh.Lowpass(finger.Lowpass(shape)) + rough.Lowpass(_random.Bipolar()) * 0.08f;
            }
            for (int i = 0; i < length; i++) _second[i] = _first[i] * 0.6f;

            float peak = 1e-6f;
            for (int i = 0; i < length; i++) peak = MathF.Max(peak, MathF.Abs(_first[i]));
            float scale = 0.6f / peak;
            for (int i = 0; i < length; i++)
            {
                _first[i] *= scale;
                _second[i] *= scale;
            }
            _write = length;
            _delayOne = Math.Min(_delayOne, Longest - 2);
            _delayTwo = Math.Min(_delayTwo, Longest - 2);
        }

        private static void Tune(float period, out int whole, out float allpass)
        {
            whole = (int)period - 1;
            if (whole < 2) whole = 2;
            float fraction = period - whole;

            // A first order allpass for what's left over, as linear interpolation would dull the string.
            allpass = (1f - fraction) / (1f + fraction);
        }

        public override void Release()
        {
            Releasing = true;
        }

        public override void Render(float[] left, float[] right, int count)
        {
            int write = _write;
            float gain = _gain, damping = _damping;
            for (int i = 0; i < count; i++)
            {
                float one = _first[(write - _delayOne) & Mask];
                float two = _second[(write - _delayTwo) & Mask];

                // Averaging with the last sample is the original algorithm's lowpass; weighted, it sets how much top a trip costs.
                float filteredOne = one * (1f - damping) + _lowOne * damping;
                _lowOne = one;
                float filteredTwo = two * (1f - damping) + _lowTwo * damping;
                _lowTwo = two;

                // Fractional length.
                float outOne = _fractionOne * filteredOne + _allpassOne;
                _allpassOne = filteredOne - _fractionOne * outOne;
                float outTwo = _fractionTwo * filteredTwo + _allpassTwo;
                _allpassTwo = filteredTwo - _fractionTwo * outTwo;

                _first[write & Mask] = outOne * gain;
                _second[write & Mask] = outTwo * gain * 0.9985f;
                write = (write + 1) & Mask;

                float value = (one + two * 0.7f) * _level * _released;
                left[i] += value;
                right[i] += value;

                if (Releasing)
                {
                    _released *= 0.9994f;
                    if (_released < 0.001f)
                    {
                        Active = false;
                        break;
                    }
                }
            }
            _write = write;

            if (!Releasing)
            {
                Remaining -= count / (float)AudioFormat.SampleRate;
                if (Remaining <= 0f) Releasing = true;
            }
        }
    }

    /// <summary>A note made in advance and played back: percussion and anything else that has no way of being held. Made with the same toolkit as every other sound.</summary>
    internal sealed class StruckVoice : OrchestraVoice
    {
        private float[]? _samples;
        private double _position;
        private float _rate, _gain;

        public void Begin(float[] samples, float rate, float gain, int pitch)
        {
            _samples = samples;
            _position = 0.0;
            _rate = rate;
            _gain = gain;
            Pitch = pitch;
            Active = true;
            Releasing = false;
        }

        public override void Release()
        {
        }

        public override void Render(float[] left, float[] right, int count)
        {
            float[] samples = _samples!;
            for (int i = 0; i < count; i++)
            {
                int whole = (int)_position;
                if (whole + 1 >= samples.Length)
                {
                    Active = false;
                    return;
                }
                float fraction = (float)(_position - whole);
                float value = (samples[whole] + (samples[whole + 1] - samples[whole]) * fraction) * _gain;
                left[i] += value;
                right[i] += value;
                _position += _rate;
            }
        }
    }
}
