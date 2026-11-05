using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// The band: every section with its instruments, its place on the stage and its share of the hall. It's told what to play and when, to the sample, and plays it. Sound from the
    /// front of a stage reaches the ear mostly direct and sound from the back mostly by way of the hall, so distance here is in how much reverb each section has as well as in where it's panned.
    /// </summary>
    public sealed class Orchestra
    {
        private const int Block = 512;
        private const int Sustained = 44;
        private const int Plucked = 28;
        private const int Struck = 32;
        private const float StartGain = 0.2f;

        /// <summary>How one section sits in the band.</summary>
        private sealed class Desk
        {
            public SustainedStyle? Style;
            public float Pan;
            public float Gain = 1f;
            public float Send = 0.3f;
            public float Brightness = 0.5f;     // For plucked things.
            public float Ring = 3f;
            public float[][]? Takes;            // For struck things.
            public int BasePitch = 60;
            public bool Pitched;
            public Biquad[] Left = Array.Empty<Biquad>();
            public Biquad[] Right = Array.Empty<Biquad>();
            public int Melody = -1;             // The voice a slur would go on from.
            public readonly float[] BufferLeft = new float[Block];
            public readonly float[] BufferRight = new float[Block];
        }

        private struct Pending
        {
            public long At;
            public Note Note;
        }

        private readonly Desk[] _desks;
        private readonly SustainedVoice[] _sustained = new SustainedVoice[Sustained];
        private readonly PluckedVoice[] _plucked = new PluckedVoice[Plucked];
        private readonly StruckVoice[] _struck = new StruckVoice[Struck];
        private long _cut = long.MaxValue;
        private float _cutFade;
        private Biquad _rumbleLeft = Biquad.Highpass(38f, 0.71f);
        private Biquad _rumbleRight = Biquad.Highpass(38f, 0.71f);
        private Biquad _presenceLeft = Biquad.Peak(4000f, 0.9f, 2.5f);
        private Biquad _presenceRight = Biquad.Peak(4000f, 0.9f, 2.5f);
        private readonly Section[] _sustainedDesk = new Section[Sustained];
        private readonly Section[] _pluckedDesk = new Section[Plucked];
        private readonly Section[] _struckDesk = new Section[Struck];
        private readonly List<Pending> _pending = new();
        private readonly SoundRandom _random;
        private readonly Reverb _hall = new(11);
        private readonly Limiter _limiter = new(-2f, 0.002f, 0.2f);
        private readonly float[] _sendLeft = new float[Block];
        private readonly float[] _sendRight = new float[Block];
        private long _now;

        public float[] Gains { get; } = new float[Enum.GetValues<Section>().Length];

        /// <summary>Notes that took the voice of one still playing, as there were too few to go round.</summary>
        public int Stolen { get; private set; }

        /// <summary>Samples played so far.</summary>
        public long Now => _now;

        public Orchestra(ulong seed)
        {
            _random = new SoundRandom(seed);
            for (int i = 0; i < Sustained; i++) _sustained[i] = new SustainedVoice(_random.Fork());
            for (int i = 0; i < Plucked; i++) _plucked[i] = new PluckedVoice(_random.Fork());
            for (int i = 0; i < Struck; i++) _struck[i] = new StruckVoice();
            Array.Fill(Gains, 1f);

            _desks = new Desk[Gains.Length];
            for (int i = 0; i < _desks.Length; i++) _desks[i] = new Desk();
            Seat();

            _hall.Configure(new ReverbSettings { DecayTime = 2.3f, DecayHigh = 1.05f, PreDelay = 0.028f, Wet = 1f, EchoTime = 0.3f, EchoBrightness = 3000f });
        }

        private static Biquad[] Filters(params Biquad[] filters) => filters;

        private Desk Sit(Section section, SustainedStyle? style, float pan, float gain, float send)
        {
            Desk desk = _desks[(int)section];
            desk.Style = style;
            desk.Pan = pan;
            desk.Gain = gain;
            desk.Send = send;
            return desk;
        }

        private static void Shape(Desk desk, Func<Biquad[]> filters)
        {
            desk.Left = filters();
            desk.Right = filters();
        }

        private void Seat()
        {
            Instruments kit = Instruments.Shared;

            // Woodwinds in the middle, strings across the front, brass and drums at the back, as they sit on a real stage.
            Sit(Section.Flute, kit.Flute, -0.12f, 0.7f, 0.34f);
            Sit(Section.Oboe, kit.Oboe, 0.14f, 0.55f, 0.32f);
            Sit(Section.Clarinet, kit.Clarinet, 0.22f, 0.55f, 0.32f);
            Sit(Section.Violins, kit.Violins, -0.5f, 0.7f, 0.26f);
            Sit(Section.Violas, kit.Violas, 0.1f, 0.62f, 0.26f);
            Sit(Section.Cellos, kit.Cellos, 0.42f, 0.75f, 0.26f);
            Sit(Section.Basses, kit.Basses, 0.6f, 0.8f, 0.3f);
            Sit(Section.Horns, kit.Horns, -0.32f, 0.7f, 0.5f);
            Sit(Section.Trombones, kit.Trombones, 0.36f, 0.62f, 0.46f);
            Sit(Section.Trumpets, kit.Trumpets, 0.18f, 0.5f, 0.46f);
            Sit(Section.Fiddle, kit.Fiddle, 0.12f, 0.6f, 0.22f);
            Sit(Section.Choir, kit.Choir, 0f, 0.72f, 0.62f);

            // A horn's bell points away from the audience, so what's heard has come off the back wall, with the edge taken off.
            Shape(_desks[(int)Section.Horns], () => Filters(Biquad.HighShelf(3500f, -5f)));

            // The bridge hill: what makes a violin carry.
            Shape(_desks[(int)Section.Violins], () => Filters(Biquad.Peak(2600f, 1.1f, 3f), Biquad.Peak(420f, 1.5f, 2f), Biquad.HighShelf(7000f, -3f)));
            Shape(_desks[(int)Section.Violas], () => Filters(Biquad.Peak(1900f, 1.1f, 2f), Biquad.HighShelf(6000f, -3f)));
            Shape(_desks[(int)Section.Cellos], () => Filters(Biquad.LowShelf(150f, 4f), Biquad.Peak(240f, 1.2f, 2f), Biquad.Peak(1200f, 1f, 1.5f)));
            Shape(_desks[(int)Section.Fiddle], () => Filters(Biquad.Peak(280f, 2f, 3f), Biquad.Peak(500f, 2.5f, -2f), Biquad.Peak(2700f, 1.4f, 4f)));

            Desk harp = Sit(Section.Harp, null, -0.6f, 0.78f, 0.34f);
            harp.Brightness = 0.55f;
            harp.Ring = 4.2f;
            Shape(harp, () => Filters(Biquad.Peak(240f, 1f, 3f), Biquad.HighShelf(5000f, -2f)));

            Desk lute = Sit(Section.Lute, null, -0.24f, 0.74f, 0.2f);
            lute.Brightness = 0.62f;
            lute.Ring = 1.6f;

            // A lute's body is a bowl: a low air resonance, and wood above it.
            Shape(lute, () => Filters(Biquad.Peak(115f, 2f, 4f), Biquad.Peak(290f, 1.6f, 4f), Biquad.Peak(620f, 2f, 2f), Biquad.HighShelf(4000f, -3f)));

            Desk pizzicato = Sit(Section.Pizzicato, null, 0.32f, 0.8f, 0.3f);
            pizzicato.Brightness = 0.32f;
            pizzicato.Ring = 0.55f;
            Shape(pizzicato, () => Filters(Biquad.Peak(220f, 1f, 3f)));

            // Percussion is made by the same toolkit as everything else. Each is one note made ahead and bent to pitch.
            Strike(Section.Celesta, 0.58f, kit.Celesta, 72, -0.42f, 0.42f);
            Strike(Section.Bells, 0.7f, kit.Bells, 60, 0.3f, 0.6f);
            Strike(Section.Timpani, 0.9f, kit.Timpani, 45, 0.26f, 0.5f);
            Strike(Section.BigDrum, 0.84f, kit.BigDrum, 36, -0.1f, 0.5f);
            Strike(Section.FrameDrum, 0.6f, kit.FrameDrum, 48, 0.1f, 0.24f);
            Strike(Section.Tambourine, 0.3f, kit.Tambourine, 60, 0.32f, 0.26f);
            Strike(Section.Cymbal, 0.5f, kit.Cymbal, 60, 0.05f, 0.55f);

            // Measured against recorded orchestras, drums with nothing done to them were the whole of the bottom two octaves, and every stroke drove the limiter down on everyone else.
            // A drum carries by its body and its skin, so that's what is kept.
            Shape(_desks[(int)Section.Timpani], () => Filters(Biquad.LowShelf(110f, -9f), Biquad.Peak(320f, 1f, 3f)));
            Shape(_desks[(int)Section.BigDrum], () => Filters(Biquad.LowShelf(100f, -10f), Biquad.Peak(240f, 1f, 4f)));
        }

        private void Strike(Section section, float gain, float[][] takes, int basePitch, float pan, float send)
        {
            Desk desk = Sit(section, null, pan, gain, send);
            desk.Takes = takes;
            desk.BasePitch = basePitch;
            desk.Pitched = section is Section.Celesta or Section.Bells or Section.Timpani;
        }

        /// <summary>Queues notes. Their times are from <paramref name="at"/>, in samples since the band began.</summary>
        public void Schedule(in Note note, long at)
        {
            long when = at + (long)(note.Start * AudioFormat.SampleRate);
            _pending.Add(new Pending { At = when, Note = note });
        }

        /// <summary>
        /// Ends what was planned at a moment to come, for when it no longer fits: what comes before it still plays, and nothing after it does. Whatever's sounding then dies away over
        /// <paramref name="fadeSeconds"/>, as players do who've been waved off, and plucked strings are left to ring.
        /// </summary>
        public void Cut(long at, float fadeSeconds)
        {
            _pending.RemoveAll(pending => pending.At >= at);
            _cut = at;
            _cutFade = fadeSeconds;
        }

        private void FadeAll(float seconds)
        {
            foreach (SustainedVoice voice in _sustained)
            {
                if (voice.Active) voice.Fade(seconds);
            }
        }

        private void Start(in Note note)
        {
            Desk desk = _desks[(int)note.Section];

            if (desk.Style != null)
            {
                // A slur goes on from the note before, with no new attack: one breath, one bow.
                if (note.Articulation == Articulation.Legato && Slurrable(desk, note.Section))
                {
                    _sustained[desk.Melody].Slur(note);
                    return;
                }

                int slot = Free(_sustained);
                _sustained[slot].Begin(desk.Style, note);
                _sustainedDesk[slot] = note.Section;
                desk.Melody = slot;
                return;
            }

            if (desk.Takes == null)
            {
                int slot = Free(_plucked);
                _plucked[slot].Begin(note, desk.Brightness, desk.Ring * Ringing(note.Pitch));
                _pluckedDesk[slot] = note.Section;
                return;
            }

            int voice = Free(_struck);
            float rate = desk.Pitched ? MathF.Pow(2f, (note.Pitch - desk.BasePitch) / 12f) : 1f;
            float[] take = desk.Takes[_random.Int(desk.Takes.Length)];
            _struck[voice].Begin(take, rate, (0.25f + note.Loudness * 0.75f) * AudioFormat.DecibelsToGain(note.Fader), note.Pitch);
            _struckDesk[voice] = note.Section;
        }

        // High strings ring out sooner than low ones.
        private static float Ringing(int pitch) => Math.Clamp(1.7f - (pitch - 48) * 0.022f, 0.4f, 2f);

        // Its voice may have gone to another section since, and then it isn't this section's note to go on from.
        private bool Slurrable(Desk desk, Section section)
        {
            if (desk.Melody < 0 || _sustainedDesk[desk.Melody] != section) return false;
            SustainedVoice voice = _sustained[desk.Melody];
            return voice.Active && !voice.Releasing;
        }

        // A free voice if there is one. If not, the one that has been dying longest, and failing that the oldest note, which is the one least missed.
        private int Free<T>(T[] voices) where T : OrchestraVoice
        {
            int chosen = -1;
            int oldest = 0;
            long earliestDying = long.MaxValue;
            long earliest = long.MaxValue;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].Active)
                {
                    voices[i].Started = _now;
                    return i;
                }
                if (voices[i].Releasing && voices[i].Started < earliestDying)
                {
                    earliestDying = voices[i].Started;
                    chosen = i;
                }
                if (voices[i].Started < earliest)
                {
                    earliest = voices[i].Started;
                    oldest = i;
                }
            }
            if (chosen < 0)
            {
                chosen = oldest;
                Stolen++;
            }
            voices[chosen].Started = _now;
            return chosen;
        }

        /// <summary>Plays on, into stereo. Any number of frames can be asked for, and notes start to the sample.</summary>
        public void Render(float[] left, float[] right, int frames)
        {
            int done = 0;
            while (done < frames)
            {
                int count = Math.Min(Block, frames - done);

                if (_cut <= _now)
                {
                    FadeAll(_cutFade);
                    _cut = long.MaxValue;
                }

                // Up to the next note, so that it starts when it should and not at the next block.
                long next = _cut;
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    if (_pending[i].At <= _now)
                    {
                        Note note = _pending[i].Note;
                        _pending.RemoveAt(i);
                        Start(note);
                    }
                    else next = Math.Min(next, _pending[i].At);
                }
                if (next != long.MaxValue) count = (int)Math.Min(count, next - _now);

                MixBlock(left, right, done, count);
                done += count;
                _now += count;
            }
        }

        private void MixBlock(float[] left, float[] right, int offset, int count)
        {
            foreach (Desk desk in _desks)
            {
                Array.Clear(desk.BufferLeft, 0, count);
                Array.Clear(desk.BufferRight, 0, count);
            }

            for (int i = 0; i < Sustained; i++)
            {
                SustainedVoice voice = _sustained[i];
                if (!voice.Active) continue;
                Desk desk = _desks[(int)_sustainedDesk[i]];
                voice.Render(desk.BufferLeft, desk.BufferRight, count);
            }
            for (int i = 0; i < Plucked; i++)
            {
                PluckedVoice voice = _plucked[i];
                if (!voice.Active) continue;
                Desk desk = _desks[(int)_pluckedDesk[i]];
                voice.Render(desk.BufferLeft, desk.BufferRight, count);
            }
            for (int i = 0; i < Struck; i++)
            {
                StruckVoice voice = _struck[i];
                if (!voice.Active) continue;
                Desk desk = _desks[(int)_struckDesk[i]];
                voice.Render(desk.BufferLeft, desk.BufferRight, count);
            }

            Array.Clear(_sendLeft, 0, count);
            Array.Clear(_sendRight, 0, count);
            Array.Clear(left, offset, count);
            Array.Clear(right, offset, count);

            for (int d = 0; d < _desks.Length; d++)
            {
                Desk desk = _desks[d];
                float gain = desk.Gain * Gains[d] * StartGain;
                if (gain <= 0f) continue;

                float angle = (desk.Pan + 1f) * MathF.PI * 0.25f;
                float toLeft = MathF.Cos(angle) * 1.414f * gain;
                float toRight = MathF.Sin(angle) * 1.414f * gain;
                float send = desk.Send;

                for (int i = 0; i < count; i++)
                {
                    float l = desk.BufferLeft[i];
                    float r = desk.BufferRight[i];
                    for (int f = 0; f < desk.Left.Length; f++)
                    {
                        l = desk.Left[f].Process(l);
                        r = desk.Right[f].Process(r);
                    }
                    l *= toLeft;
                    r *= toRight;
                    left[offset + i] += l;
                    right[offset + i] += r;
                    _sendLeft[i] += l * send;
                    _sendRight[i] += r * send;
                }
            }

            _hall.Process(_sendLeft.AsSpan(0, count), _sendRight.AsSpan(0, count), left.AsSpan(offset, count), right.AsSpan(offset, count));

            // Nothing in a hall reaches a microphone from below this but rumble, which would only spend the limiter's headroom.
            for (int i = offset; i < offset + count; i++)
            {
                left[i] = _presenceLeft.Process(_rumbleLeft.Process(left[i]));
                right[i] = _presenceRight.Process(_rumbleRight.Process(right[i]));
            }
            _limiter.Process(left.AsSpan(offset, count), right.AsSpan(offset, count));
        }
    }

    /// <summary>
    /// Everything needed to play, built once on first use. The timbres come from measured spectra, voices from a formula of their own, and percussion from the same toolkit as every
    /// other sound, each made in several takes so that no drum is ever struck twice the same.
    /// </summary>
    internal sealed class Instruments
    {
        private static readonly Lazy<Instruments> Kit = new(() => new Instruments());

        public static Instruments Shared => Kit.Value;

        public readonly SustainedStyle Flute, Oboe, Clarinet, Horns, Trombones, Trumpets, Violins, Violas, Cellos, Basses, Fiddle, Choir;
        public readonly float[][] Celesta, Bells, Timpani, BigDrum, FrameDrum, Tambourine, Cymbal;

        private Instruments()
        {
            Timbre.Brightening wood = Timbre.WoodwindBrightening;
            Timbre.Brightening brass = Timbre.BrassBrightening;
            Timbre.Brightening strings = Timbre.StringBrightening;

            // The numbers are measured where they could be: flute vibrato at 4.7 to 5.1 Hz and 7 to 20 cents, a horn with almost none and a 100 ms attack.
            Flute = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Flute, wood, 58f, 98f, 1), Attack = 0.075f, Release = 0.14f, VibratoRate = 5f, VibratoDepth = 20f, VibratoDelay = 0.28f, Tremor = 0.26f, Breath = 0.085f, BreathPitch = 1.6f, Chiff = 0.1f, Range = 10f };
            Oboe = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Oboe, wood, 56f, 92f, 2), Attack = 0.05f, Release = 0.1f, VibratoRate = 5.2f, VibratoDepth = 11f, VibratoDelay = 0.3f, Tremor = 0.05f, Breath = 0.02f, BreathPitch = 3f, Chiff = 0.05f, Range = 12f };
            Clarinet = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Clarinet, wood, 50f, 92f, 3), Attack = 0.055f, Release = 0.1f, VibratoRate = 5f, VibratoDepth = 2f, VibratoDelay = 0.4f, Tremor = 0.03f, Breath = 0.02f, BreathPitch = 2.5f, Chiff = 0.04f, Range = 16f };
            Horns = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Horn, brass, 36f, 80f, 4), Players = 3, Scatter = 3f, Attack = 0.1f, Release = 0.2f, VibratoRate = 5f, VibratoDepth = 3f, VibratoDelay = 0.5f, Scoop = 28f, Breath = 0.016f, BreathPitch = 4f, Chiff = 0.05f, Range = 13f };
            Trombones = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Trombone, brass, 34f, 74f, 5), Players = 3, Scatter = 3f, Attack = 0.075f, Release = 0.17f, VibratoRate = 5f, VibratoDepth = 3f, VibratoDelay = 0.5f, Scoop = 22f, Breath = 0.03f, BreathPitch = 4f, Chiff = 0.06f, Range = 15f };
            Trumpets = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Trumpet, brass, 54f, 86f, 6), Players = 2, Scatter = 3f, Attack = 0.05f, Release = 0.13f, VibratoRate = 5.5f, VibratoDepth = 4f, VibratoDelay = 0.45f, Scoop = 18f, Breath = 0.02f, BreathPitch = 4f, Chiff = 0.08f, Range = 15f };

            // Sections scatter their pitch and don't agree on their vibrato: that's most of what an ensemble sounds like.
            Violins = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Violins, strings, 55f, 100f, 7), Players = 5, Scatter = 6f, Attack = 0.12f, Release = 0.28f, VibratoRate = 5.6f, VibratoDepth = 13f, VibratoDelay = 0.22f, Tremor = 0.08f, Breath = 0.035f, BreathPitch = 6f, Chiff = 0.05f, Range = 22f };
            Violas = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Violas, strings, 48f, 88f, 8), Players = 4, Scatter = 6f, Attack = 0.12f, Release = 0.28f, VibratoRate = 5.4f, VibratoDepth = 12f, VibratoDelay = 0.22f, Tremor = 0.08f, Breath = 0.03f, BreathPitch = 6f, Chiff = 0.05f, Range = 22f };
            Cellos = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Cellos, strings, 36f, 78f, 9), Players = 4, Scatter = 5f, Attack = 0.13f, Release = 0.3f, VibratoRate = 5.2f, VibratoDepth = 11f, VibratoDelay = 0.24f, Tremor = 0.08f, Breath = 0.015f, BreathPitch = 7f, Chiff = 0.05f, Range = 22f };
            Basses = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Basses, strings, 28f, 62f, 10), Players = 3, Scatter = 4f, Attack = 0.15f, Release = 0.3f, VibratoRate = 5f, VibratoDepth = 6f, VibratoDelay = 0.3f, Breath = 0.006f, BreathPitch = 9f, Chiff = 0.05f, Range = 22f };
            Fiddle = new SustainedStyle { Timbre = Timbre.Build(MeasuredSpectra.Fiddle, strings, 55f, 100f, 11), Attack = 0.05f, Release = 0.1f, VibratoRate = 5.8f, VibratoDepth = 11f, VibratoDelay = 0.26f, Tremor = 0.1f, Breath = 0.04f, BreathPitch = 6f, Chiff = 0.1f, Range = 18f };

            Choir = new SustainedStyle { Timbre = Timbre.Build(ChoirSpectrum, 43f, 84f, 12), Players = 5, Scatter = 7f, Attack = 0.28f, Release = 0.5f, VibratoRate = 5.2f, VibratoDepth = 22f, VibratoDelay = 0.4f, Breath = 0.03f, BreathPitch = 7f, Chiff = 0f, Range = 16f };

            Celesta = Takes(4, 21, MakeCelesta);
            Bells = Takes(3, 22, take => take.Mix(Bells2(take)));
            Timpani = Takes(5, 23, MakeTimpani);
            BigDrum = Takes(5, 24, MakeBigDrum);
            FrameDrum = Takes(6, 25, MakeFrameDrum);
            Tambourine = Takes(6, 26, MakeTambourine);
            Cymbal = Takes(3, 27, MakeCymbal);
        }

        private static float[][] Takes(int count, ulong seed, Action<Take> recipe) => Content.GrainCloud.Make(seed * 1000UL, count, recipe);

        // A voice as source and filter: folds that fall away at 12 dB an octave, and the formants of an open vowel, which don't move with the note.
        private static float ChoirSpectrum(float hertz, int harmonic)
        {
            float frequency = hertz * harmonic;
            float source = -12f * MathF.Log2(harmonic);
            float tract = Formant(frequency, 620f, 90f, 0f) + Formant(frequency, 1040f, 120f, -6f) + Formant(frequency, 2500f, 180f, -14f) + Formant(frequency, 3200f, 220f, -22f);
            return source + AudioFormat.GainToDecibels(tract) + 12f;
        }

        private static float Formant(float frequency, float centre, float width, float decibels)
        {
            float x = (frequency - centre) / width;
            return AudioFormat.DecibelsToGain(decibels) / (1f + x * x);
        }

        private static void MakeCelesta(Take take)
        {
            SoundRandom random = take.Random;

            // A small steel bar over a resonator: nearly pure, with a bright tick at the start and a slow glow after it.
            Signal note = take.Track(2.4f);
            float pitch = 523.25f;
            float[] ratios = { 1f, 2.0f, 3.9f, 5.4f, 8.1f };
            float[] levels = { 1f, 0.32f, 0.16f, 0.1f, 0.05f };
            for (int p = 0; p < ratios.Length; p++)
            {
                float decay = 0.9f / (1f + p * 1.6f);
                note.Add(take.Sine(2.4f, pitch * ratios[p], random.Range(0f, 6f)).Percussive(0.0007f, decay), 0f, levels[p]);
            }
            note.Add(take.White(0.01f).Highpass(2600f).Percussive(0.00005f, 0.0012f), 0f, 0.3f);
            take.Mix(note);
        }

        private static Signal Bells2(Take take) => Content.Bells.Toll(take, 261.63f, 5f);

        private static void MakeTimpani(Take take)
        {
            SoundRandom random = take.Random;

            // Measured modes run 0.85, 1, 1.5, 2, 2.5 and 2.9 of the note: air loading drags a drum's clashing modes into a nearly harmonic series, which is why it has a pitch at all.
            float pitch = 110f;
            float[] ratios = { 0.85f, 1f, 1.5f, 1.99f, 2.44f, 2.89f };
            float[] levels = { 0.45f, 1f, 0.55f, 0.32f, 0.16f, 0.09f };
            float[] decays = { 0.12f, 0.75f, 0.5f, 0.34f, 0.22f, 0.16f };
            Signal drum = take.Track(2.2f);
            for (int p = 0; p < ratios.Length; p++)
            {
                float f = pitch * ratios[p] * (1f + random.Gaussian() * 0.004f);
                Signal mode = take.Sine(2.2f, Curve.Settle(f * 1.02f, f, 0.03f), random.Range(0f, 6f)).Percussive(0.0015f, decays[p]);
                drum.Add(mode, 0f, levels[p]);
            }

            // The stick on the skin.
            drum.Add(take.White(0.05f).Bandpass(Curve.Glide(2400f, 500f, 0.02f), 0.8f).Percussive(0.0002f, 0.006f), 0f, 0.5f);
            take.Mix(drum);
        }

        private static void MakeBigDrum(Take take)
        {
            SoundRandom random = take.Random;

            // A great barrel drum: a low body falling in pitch, and the slap of a heavy stick.
            float pitch = random.Range(58f, 68f);
            Signal drum = take.Sine(1.5f, Curve.Settle(pitch * 2.3f, pitch, 0.03f), 0.2f).Percussive(0.002f, 0.32f);
            drum.Add(take.Sine(1.5f, Curve.Settle(pitch * 3.6f, pitch * 1.59f, 0.02f), 1f).Percussive(0.002f, 0.1f), 0f, 0.5f);
            drum.Add(take.Sine(1.5f, pitch * 2.14f, 2f).Percussive(0.002f, 0.07f), 0f, 0.3f);
            drum.Add(take.Brown(0.5f).Lowpass(260f).Percussive(0.002f, 0.09f), 0f, 1.4f);
            drum.Add(take.White(0.05f).Bandpass(Curve.Glide(1800f, 500f, 0.016f), 0.7f).Percussive(0.0002f, 0.009f), 0f, 0.55f);
            take.Mix(drum.Drive(1.5f));
        }

        private static void MakeFrameDrum(Take take)
        {
            SoundRandom random = take.Random;

            // A hand drum: a skin on a hoop, with no shell to speak of, so its membrane modes stay inharmonic and it's more a knock than a note.
            float pitch = random.Range(135f, 170f);
            float[] ratios = { 1f, 1.59f, 2.14f, 2.3f, 2.65f };
            Signal drum = take.Track(0.5f);
            for (int p = 0; p < ratios.Length; p++)
            {
                drum.Add(take.Sine(0.5f, pitch * ratios[p], random.Range(0f, 6f)).Percussive(0.0012f, 0.07f / (1f + p * 0.5f)), 0f, 1f / (1f + p * 0.8f));
            }
            drum.Add(take.White(0.03f).Bandpass(1400f, 0.8f).Percussive(0.0002f, 0.005f), 0f, 0.4f);
            take.Mix(drum);
        }

        private static void MakeTambourine(Take take)
        {
            // A hoop of small cymbals, each a little metal disc: Cook's particles again.
            var jingles = new GrainStyle { LowHz = 5200f, HighHz = 12000f, MinDecay = 0.006f, MaxDecay = 0.03f, Partials = 3, Noise = 0.1f, Spread = 12f };
            take.Mix(Grains.Settle(take, 0.22f, 22f, 0.05f, jingles).Normalize(0f));
        }

        private static void MakeCymbal(Take take)
        {
            // Swelling up, as a roll on a suspended cymbal does: a dense metal wash, opening up at the top.
            float seconds = 2.6f;
            Signal left = take.White(seconds).Bandpass(Curve.Glide(1600f, 7000f, 1.6f), 0.5f).Shape(new Curve(t => MathF.Pow(Math.Clamp(t / 1.6f, 0f, 1f), 2.4f))).Comb(1180f, 0.4f, 0.35f);
            take.Mix(left.Fade(0.01f, 0.8f).Normalize(0f));
        }
    }
}
