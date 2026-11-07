using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// What's alive around a place, and what haunts it. Each is made fresh from a seed, so that a world has birds of its own: their
    /// songs are drawn when it's entered, and no other world has quite those.
    /// </summary>
    [SoundCollection]
    public static class Life
    {
        public static readonly LiveDefinition Birds = SoundCatalog.Add(new LiveDefinition("life.birds", seed => new BirdsongSound(seed))
        {
            Parameters = new[] { "Activity" },
            Defaults = new[] { 0.6f },
        });

        public static readonly LiveDefinition Crickets = SoundCatalog.Add(new LiveDefinition("life.crickets", seed => new CricketSound(seed))
        {
            Parameters = new[] { "Activity" },
            Defaults = new[] { 0.7f },
        });

        public static readonly LiveDefinition Frogs = SoundCatalog.Add(new LiveDefinition("life.frogs", seed => new FrogSound(seed))
        {
            Parameters = new[] { "Activity" },
            Defaults = new[] { 0.6f },
        });

        public static readonly LiveDefinition Haunting = SoundCatalog.Add(new LiveDefinition("life.haunting", seed => new HauntingSound(seed))
        {
            Parameters = new[] { "Depth" },
            Defaults = new[] { 0.8f },
        });

        public static readonly LiveDefinition Gate = SoundCatalog.Add(new LiveDefinition("place.waygate", seed => new HumSound(seed, 55f, 0.8f))
        {
            Positional = true,
            NearDistance = 8f,
            FarDistance = 46f,
            ReverbSend = 0.25f,
            Bus = SoundBus.Ambience,
            Parameters = new[] { "Awake" },
            Defaults = new[] { 1f },
        });

        public static readonly LiveDefinition ShrineGlow = SoundCatalog.Add(new LiveDefinition("place.shrine", seed => new HumSound(seed, 196f, 0.35f))
        {
            Positional = true,
            NearDistance = 6f,
            FarDistance = 32f,
            ReverbSend = 0.3f,
            Bus = SoundBus.Ambience,
            Parameters = new[] { "Awake" },
            Defaults = new[] { 1f },
        });

        /// <summary>A few birds going up, and a flock doing it: wings are mostly what's heard of either, and a flock going off together is one of the sounds of being outdoors.</summary>
        public static readonly SoundDefinition WingsFew = Wings("life.wings.few", 3, 0.9f, -31f);
        public static readonly SoundDefinition WingsFlock = Wings("life.wings.flock", 9, 1.5f, -27f);

        public static readonly SoundDefinition Caw = Call("life.caw", take =>
        {
            SoundRandom random = take.Random;
            float at = 0f;
            int caws = 2 + random.Int(3);
            float pitch = random.Range(430f, 520f);
            for (int i = 0; i < caws; i++)
            {
                float seconds = random.Range(0.2f, 0.3f);
                var voice = new VoiceSettings
                {
                    Pitch = Curve.Points((0f, pitch * 1.12f), (seconds * 0.3f, pitch), (seconds, pitch * 0.74f)),
                    Loudness = Curve.Hump(seconds * 0.16f, seconds, 1f, 0.7f),
                    Tract = t => Vowel.Lerp(Vowel.Ae, Vowel.Ah, Math.Clamp(t / seconds, 0f, 1f)),
                    Size = 0.56f,
                    Breath = 0.3f,
                    Rough = 0.92f,
                    Tension = 0.92f,
                };
                take.Mix(Vocal.Utter(take, seconds + 0.03f, voice).Normalize(0f), at);
                at += seconds + random.Range(0.13f, 0.28f);
            }
        }, -30f);

        public static readonly SoundDefinition GullCry = Call("life.gull", take =>
        {
            SoundRandom random = take.Random;
            float at = 0f;
            int cries = 2 + random.Int(4);
            float pitch = random.Range(1150f, 1450f);
            for (int i = 0; i < cries; i++)
            {
                float seconds = random.Range(0.17f, 0.34f);
                var voice = new VoiceSettings
                {
                    Pitch = Curve.Points((0f, pitch * 0.82f), (seconds * 0.28f, pitch), (seconds, pitch * 0.6f)),
                    Loudness = Curve.Hump(seconds * 0.26f, seconds, 1f, 0.8f),
                    Tract = t => Vowel.Lerp(Vowel.Ee, Vowel.Ah, Math.Clamp(t / seconds, 0f, 1f)),
                    Size = 0.34f,
                    Breath = 0.16f,
                    Rough = 0.34f,
                    Tension = 0.8f,
                };
                take.Mix(Vocal.Utter(take, seconds + 0.03f, voice).Normalize(0f), at);
                at += seconds + random.Range(0.05f, 0.16f);
            }
        }, -31f);

        /// <summary>Something small going into water: the surface breaking, and the one bubble of air it took down with it, which is what a plop is.</summary>
        public static readonly SoundDefinition Plop = Call("life.plop", take =>
        {
            SoundRandom random = take.Random;
            Signal cavity = take.Track(0.28f);
            Liquid.Bubble(cavity, 0.012f, random.LogRange(0.0065f, 0.0105f), 1f, 0.3f);
            take.Mix(cavity.Normalize(0f));
            take.Mix(take.Pink(0.1f).Bandpass(Curve.Glide(2000f, 520f, 0.05f), 0.8f).Percussive(0.001f, 0.022f).Normalize(0f), 0f, -9f);
        }, -29f);

        private static SoundDefinition Call(string id, SoundRecipe recipe, float target)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Ambience,
                Takes = 3,
                PlaysPerTake = 2,
                PitchSpread = 0.8f,
                GainSpread = 2f,
                NearDistance = 12f,
                FarDistance = 95f,
                ReverbSend = 0.36f,
                Spacing = 0.2f,
                MostAtOnce = 2,
                Mastering = Mastering.Default with { Target = target, Stage = -8f },
            });
        }

        // Each wing beat is a short rush of air through feathers, most of it between 500 Hz and 4 kHz, and each bird beats at its own pace and is further off with every beat. A pulse train
        // sets how it goes, and noise is what it's made of, which is why it isn't a hiss: it's many small events, with nothing between them, which is what keeps wind in leaves from being one too.
        private static SoundDefinition Wings(string id, int birds, float seconds, float target)
        {
            return SoundCatalog.Add(new SoundDefinition(id, take =>
            {
                SoundRandom random = take.Random;
                Signal beats = take.Track(seconds);
                float[] samples = beats.Samples;
                for (int bird = 0; bird < birds; bird++)
                {
                    float time = random.Range(0f, 0.28f);
                    float rate = random.Range(9f, 14f);
                    float level = random.Range(0.5f, 1f);
                    while (time < seconds)
                    {
                        float left = 1f - time / seconds;
                        Beat(samples, time, level * left * left);
                        time += random.Range(0.9f, 1.1f) / rate;
                    }
                }

                take.Mix(take.White(seconds).Bandpass(1900f, 0.6f).Multiply(beats).Normalize(0f));
                take.Mix(take.Pink(seconds).Bandpass(480f, 0.8f).Multiply(beats).Normalize(0f), 0f, -7f);

                // The first stroke of a bird that's in a hurry claps its wings together over its back.
                take.Mix(take.White(0.05f).Highpass(900f).Percussive(0.0008f, 0.011f).Normalize(0f), 0f, -10f);
            })
            {
                Bus = SoundBus.Ambience,
                Takes = 3,
                PlaysPerTake = 2,
                PitchSpread = 1.4f,
                GainSpread = 2f,
                NearDistance = 11f,
                FarDistance = 80f,
                ReverbSend = 0.22f,
                Spacing = 0.2f,
                MostAtOnce = 3,
                Mastering = Mastering.Default with { Target = target, Stage = -9f },
            });
        }

        private static void Beat(float[] samples, float at, float level)
        {
            const float Attack = 0.003f, Decay = 0.028f;
            int start = (int)(at * AudioFormat.SampleRate);
            int length = (int)((Attack + Decay * 4f) * AudioFormat.SampleRate);
            for (int i = 0; i < length && start + i < samples.Length; i++)
            {
                float t = i * (1f / AudioFormat.SampleRate);
                float shape = t < Attack ? t / Attack : MathF.Exp(-(t - Attack) / Decay);
                samples[start + i] += shape * level;
            }
        }

        /// <summary>A far bell tolling, for whoever is near a church.</summary>
        public static readonly SoundDefinition ChurchBell = SoundCatalog.Add(new SoundDefinition("place.bell", take => take.Mix(Bells.Toll(take, take.Random.Pick(new[] { 146.83f, 164.81f, 130.81f }), 6f).Normalize(0f)))
        {
            Bus = SoundBus.Ambience,
            Takes = 3,
            PlaysPerTake = 2,
            PitchSpread = 0.1f,
            NearDistance = 30f,
            FarDistance = 180f,
            ReverbSend = 0.5f,
            Mastering = Mastering.Default with { Target = -23f, Stage = -6f },
        });
    }

    /// <summary>
    /// Birdsong. Each species has a song made of a few notes, and sings it with small differences. Notes are fast sweeps, usually
    /// between 2 and 7 kHz, as a syrinx gives; some trill, by chopping a note up faster than the ear can count.
    /// </summary>
    public sealed class BirdsongSound : LiveSound
    {
        public const int Activity = 0;  // 0..1 birds a place has at this hour.

        private const int Species = 4;
        private const int Songs = 7;

        private readonly float[][][] _songs = new float[Species][][];
        private readonly float[] _pauses = new float[Species];
        private readonly float[] _waits = new float[Species];
        private readonly float[] _distance = new float[Species];
        private readonly GrainCloud _cloud = new(16);

        public BirdsongSound(ulong seed) : base(1, seed, 2f)
        {
            for (int s = 0; s < Species; s++)
            {
                // Everything about the species is settled here, and only small things vary from song to song.
                var kind = new SoundRandom(seed * 131UL + (ulong)s);
                float pitch = kind.LogRange(2000f, 6000f);
                int notes = 2 + kind.Int(6);
                float length = kind.Range(0.04f, 0.17f);
                float gap = kind.Range(0.02f, 0.11f);
                bool trills = kind.Chance(0.4f);
                float trill = kind.Range(30f, 90f);
                float fall = kind.Range(-0.5f, 0.5f);
                float[] shape = new float[notes];
                for (int n = 0; n < notes; n++) shape[n] = kind.Range(0.8f, 1.35f);
                _distance[s] = kind.Range(0.1f, 0.5f);
                _pauses[s] = kind.Range(2.5f, 9f);
                _waits[s] = kind.Range(0f, _pauses[s]);

                _songs[s] = GrainCloud.Make(seed + (ulong)s * 53UL, Songs, take => Song(take, pitch, shape, length, gap, trills, trill, fall));
            }
        }

        private static void Song(Take take, float pitch, float[] shape, float length, float gap, bool trills, float trill, float fall)
        {
            SoundRandom random = take.Random;
            float time = 0f;
            for (int n = 0; n < shape.Length; n++)
            {
                float start = pitch * shape[n] * random.Range(0.97f, 1.03f);
                float end = start * (1f + fall * random.Range(0.7f, 1.3f));
                float seconds = length * random.Range(0.85f, 1.2f);

                Signal note = take.Sine(seconds, Curve.Glide(start, end, seconds), random.Range(0f, 6f));
                note.Add(take.Sine(seconds, Curve.Glide(start * 2f, end * 2f, seconds), random.Range(0f, 6f)), 0f, 0.08f);
                if (trills) note.Shape(new Curve(t => 0.5f + 0.5f * MathF.Sin(MathF.Tau * trill * t)));
                note.Shape(Curve.Hump(seconds * 0.3f, seconds));
                take.Mix(note, time);
                time += seconds + gap * random.Range(0.8f, 1.3f);
            }
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            left.Clear();
            right.Clear();
            float activity = Value(Activity);
            float seconds = left.Length / (float)AudioFormat.SampleRate;

            for (int s = 0; s < Species; s++)
            {
                _waits[s] -= seconds * activity;
                if (_waits[s] > 0f) continue;
                _waits[s] = Random.Exponential(_pauses[s]) + 0.6f;

                // Somewhere in the trees: most are a way off, and off to one side or the other.
                float gain = _distance[s] * Random.Range(0.35f, 1f) * 0.2f;
                _cloud.Trigger(_songs[s][Random.Int(Songs)], gain, Random.Bipolar());
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>
    /// A summer night's insects: each cricket rubs out chirps of a few pulses at around 4.5 kHz, a little out of step with the others, and
    /// many together make the shimmer that never settles.
    /// </summary>
    public sealed class CricketSound : LiveSound
    {
        public const int Activity = 0;

        private const int Crickets = 7;

        private readonly float[][] _chirps;
        private readonly float[] _periods = new float[Crickets];
        private readonly float[] _waits = new float[Crickets];
        private readonly float[] _pans = new float[Crickets];
        private readonly float[] _gains = new float[Crickets];
        private readonly int[] _voices = new int[Crickets];
        private readonly GrainCloud _cloud = new(24);

        public CricketSound(ulong seed) : base(1, seed, 2f)
        {
            _chirps = GrainCloud.Make(seed, Crickets, Chirp);
            var kind = new SoundRandom(seed * 3UL + 1UL);
            for (int c = 0; c < Crickets; c++)
            {
                _periods[c] = kind.Range(0.3f, 0.65f);
                _waits[c] = kind.Range(0f, 1f);
                _pans[c] = kind.Bipolar();
                _gains[c] = kind.Range(0.012f, 0.06f);
                _voices[c] = c;
            }
        }

        private static void Chirp(Take take)
        {
            SoundRandom random = take.Random;
            float pitch = random.Range(4000f, 5100f);
            int pulses = 2 + random.Int(3);
            float spacing = random.Range(0.024f, 0.04f);
            float seconds = random.Range(0.012f, 0.02f);
            for (int p = 0; p < pulses; p++)
            {
                take.Mix(take.Sine(seconds, pitch * random.Range(0.995f, 1.005f), random.Range(0f, 6f)).Shape(Curve.Hump(seconds * 0.25f, seconds)), p * spacing);
            }
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            left.Clear();
            right.Clear();
            float activity = Value(Activity);
            float seconds = left.Length / (float)AudioFormat.SampleRate;
            for (int c = 0; c < Crickets; c++)
            {
                _waits[c] -= seconds;
                if (_waits[c] > 0f) continue;
                _waits[c] = _periods[c] * Random.Range(0.92f, 1.1f);

                // Now and then one falls silent for a bit, or there's no texture to it, only a clock.
                if (Random.Chance(0.1f)) _waits[c] += Random.Range(0.8f, 4f);
                _cloud.Trigger(_chirps[_voices[c]], _gains[c] * activity, _pans[c]);
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>Frogs at the edge of still water: a rapid pulse through a throat sac, each one answering the last.</summary>
    public sealed class FrogSound : LiveSound
    {
        public const int Activity = 0;

        private readonly float[][] _croaks;
        private readonly GrainCloud _cloud = new(16);
        private float _wait;

        public FrogSound(ulong seed) : base(1, seed, 2f)
        {
            _croaks = GrainCloud.Make(seed, 10, Croak);
        }

        private static void Croak(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.14f, 0.4f);
            float rate = random.Range(24f, 62f);
            Signal pulses = take.Track(seconds);
            for (float t = 0f; t < seconds; t += 1f / rate) pulses.Samples[(int)(t * AudioFormat.SampleRate)] = 1f;
            Signal throat = take.Track(seconds);
            float pitch = random.LogRange(620f, 1500f);
            throat.Add(pulses);
            throat.Bandpass(pitch, 5f);
            Signal upper = take.Track(seconds);
            upper.Add(pulses);
            upper.Bandpass(pitch * 2.2f, 7f);
            throat.Add(upper, 0f, 0.5f);
            throat.Shape(Curve.Hump(seconds * 0.3f, seconds));
            take.Mix(throat.Normalize(0f));
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            left.Clear();
            right.Clear();
            float activity = Value(Activity);
            float seconds = left.Length / (float)AudioFormat.SampleRate;
            _wait -= seconds * activity;
            if (_wait <= 0f)
            {
                _wait = Random.Exponential(0.55f) + 0.08f;
                _cloud.Trigger(_croaks[Random.Int(_croaks.Length)], Random.Range(0.02f, 0.09f), Random.Bipolar());
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>
    /// The air over a churchyard: wind in something hollow, a cold thin whistle wandering high up, and under it all a low pressure that's felt more
    /// than heard. No single thing is frightening, and it's better if nobody can say what's wrong.
    /// </summary>
    public sealed class HauntingSound : LiveSound
    {
        public const int Depth = 0;

        private readonly float[] _comb = new float[8192];
        private int _write;
        private PinkNoise _pinkLeft, _pinkRight;
        private Svf _hollowLeft, _hollowRight, _whistle;
        private OnePole _sub;
        private Wander _hollowDrift, _whistleDrift, _swell;
        private float _phase, _combDelay = 400f, _subGain, _whistleGain, _hollowGain;

        public HauntingSound(ulong seed) : base(1, seed, 2.5f)
        {
            _hollowDrift.Set(5f, ControlRate);
            _whistleDrift.Set(3f, ControlRate);
            _swell.Set(7f, ControlRate);
            _sub.SetCutoff(60f);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float depth = Value(Depth);
            float swell = 1f + _swell.Next(Random, Step) * 0.6f;
            float hollow = 180f + _hollowDrift.Next(Random, Step) * 60f;
            _hollowLeft.Set(hollow, 6f);
            _hollowRight.Set(hollow * 1.34f, 6f);
            _combDelay = AudioFormat.SampleRate / hollow;
            _hollowGain = 0.05f * depth * MathF.Max(swell, 0f);

            float whistle = 1500f + _whistleDrift.Next(Random, Step) * 480f;
            _whistle.Set(whistle, 34f);
            _whistleGain = 0.05f * depth * MathF.Max(swell - 0.6f, 0f);
            _subGain = 0.06f * depth;
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            int mask = _comb.Length - 1;
            for (int i = 0; i < left.Length; i++)
            {
                float pinkLeft = _pinkLeft.Next(Random.Bipolar());
                float pinkRight = _pinkRight.Next(Random.Bipolar());
                float breath = (pinkLeft + pinkRight) * 0.5f;

                int delayed = (_write - (int)_combDelay) & mask;
                float pipe = breath + _comb[delayed] * 0.82f;
                _comb[_write] = pipe;
                _write = (_write + 1) & mask;

                _phase += 31f / AudioFormat.SampleRate;
                if (_phase > 1f) _phase -= 1f;
                float sub = MathF.Sin(MathF.Tau * _phase) * _sub.Lowpass(breath + 0.8f) * _subGain;
                float whistle = _whistle.Bandpass(breath) * _whistleGain;

                left[i] = _hollowLeft.Bandpass(pinkLeft + pipe * 0.4f) * _hollowGain + sub + whistle * 0.7f;
                right[i] = _hollowRight.Bandpass(pinkRight + pipe * 0.4f) * _hollowGain + sub + whistle;
            }
        }
    }

    /// <summary>A place with power in it: a low note, its harmonics slowly turning over each other, and something bright high up in the air around it.</summary>
    public sealed class HumSound : LiveSound
    {
        public const int Awake = 0;

        private readonly float _pitch;
        private readonly float _depth;
        private readonly float[] _phases = new float[6];
        private readonly float[] _steps = new float[6];
        private readonly float[] _levels = { 1f, 0.5f, 0.32f, 0.2f, 0.1f, 0.06f };
        private readonly float[] _gains = new float[6];
        private readonly Wander[] _drift = new Wander[6];
        private PinkNoise _pink;
        private Svf _air;
        private float _airGain;

        public HumSound(ulong seed, float pitch, float depth) : base(1, seed, 1.5f)
        {
            _pitch = pitch;
            _depth = depth;
            for (int h = 0; h < _phases.Length; h++)
            {
                _steps[h] = pitch * (h + 1) * (1f + (h * 0.0009f)) / AudioFormat.SampleRate;
                _drift[h].Set(2f + h, ControlRate);
            }
            _air.Set(pitch * 34f, 3.5f);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float awake = Value(Awake);
            for (int h = 0; h < _gains.Length; h++)
            {
                float turn = 0.7f + 0.3f * _drift[h].Next(Random, Step);
                _gains[h] = 0.18f * awake * _levels[h] * turn * (0.5f + 0.5f * _depth);
            }
            _airGain = 0.05f * awake;
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                float sum = 0f;
                for (int h = 0; h < _phases.Length; h++)
                {
                    _phases[h] += _steps[h];
                    if (_phases[h] >= 1f) _phases[h] -= 1f;
                    sum += MathF.Sin(MathF.Tau * _phases[h]) * _gains[h];
                }
                float shimmer = _air.Bandpass(_pink.Next(Random.Bipolar())) * _airGain;
                left[i] = sum + shimmer;
                right[i] = sum - shimmer * 0.6f;
            }
        }
    }
}
