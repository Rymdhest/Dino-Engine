using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// Throats: people and things that shouldn't have one. A synthetic voice gives itself away most easily of all sounds, so these stay short and
    /// breathy and busy, and in a fight they're heard among other things. Words are left out on purpose: speech here is the sound of it, a murmur,
    /// which every language in the world can read as talk.
    /// </summary>
    [SoundCollection]
    public static class Voices
    {
        public static readonly SoundDefinition HeroEffort = Add("voice.hero.effort", take => Effort(take, 138f, 1f), -22f, cooldown: 0.5f);
        public static readonly SoundDefinition HeroHurt = Add("voice.hero.hurt", take => Hurt(take, 150f, 1f), -19f, cooldown: 0.35f, priority: 80);
        public static readonly SoundDefinition HeroDeath = Add("voice.hero.death", take => Dying(take, 142f, 1f), -16f, priority: 95);
        public static readonly SoundDefinition OutlawHurt = Add("voice.outlaw.hurt", take => Hurt(take, 118f, 1.1f), -21f, cooldown: 0.2f);
        public static readonly SoundDefinition OutlawDeath = Add("voice.outlaw.death", take => Dying(take, 110f, 1.1f), -19f);
        public static readonly SoundDefinition OutlawShout = Add("voice.outlaw.shout", Shout, -20f, cooldown: 0.8f);
        public static readonly SoundDefinition DeadHurt = Add("voice.dead.hurt", take => Moan(take, 0.3f), -22f, cooldown: 0.3f);
        public static readonly SoundDefinition DeadDeath = Add("voice.dead.death", DeadCollapse, -18f);
        public static readonly SoundDefinition DeadMoan = Add("voice.dead.moan", take => Moan(take, 1f), -24f, cooldown: 1f);
        public static readonly SoundDefinition BeastSnarl = Add("voice.beast.snarl", Snarl, -21f, cooldown: 0.5f);
        public static readonly SoundDefinition BeastDeath = Add("voice.beast.death", Yelp, -20f);
        public static readonly SoundDefinition WarlordRoar = Add("voice.warlord.roar", Roar, -15f, presence: 4f, priority: 90);
        public static readonly SoundDefinition WarlordDeath = Add("voice.warlord.death", take => { Roar(take); Dying(take, 74f, 1.45f); }, -14f, presence: 6f, priority: 92);

        /// <summary>A few syllables of talk with no words in them, for someone of this pitch.</summary>
        public static readonly SoundDefinition Murmur = Add("voice.murmur", take => Babble(take, 1f), -24f, cooldown: 0.3f);
        public static readonly SoundDefinition MurmurHigh = Add("voice.murmur.high", take => Babble(take, 1.5f), -24f, cooldown: 0.3f);
        public static readonly SoundDefinition MurmurOld = Add("voice.murmur.old", take => Babble(take, 0.8f), -25f, cooldown: 0.3f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float cooldown = 0.1f, int priority = 60, float presence = 0f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Voice,
                Takes = 8,
                PitchSpread = 1f,
                GainSpread = 1.4f,
                Spacing = cooldown,
                MostAtOnce = 4,
                Priority = priority,
                Presence = presence,
                ReverbSend = 0.18f,
                NearDistance = 11f,
                FarDistance = 55f,
                Mastering = Mastering.Default with { Target = target, Stage = -15f },
            });
        }

        private static void Effort(Take take, float pitch, float size)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.14f, 0.24f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 1.35f), (seconds * 0.3f, pitch * 1.5f), (seconds, pitch * 0.95f)),
                Loudness = Curve.Hump(seconds * 0.22f, seconds, 1f, 0.8f),
                Tract = t => Vowel.Lerp(Vowel.Uh, Vowel.Ah, Math.Clamp(t / seconds, 0f, 1f)),
                Size = size,
                Breath = 0.35f,
                Rough = 0.35f,
                Tension = 0.8f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Normalize(0f));
        }

        // A voice held low in the throat and shaken, with its mouth barely open.
        private static void Snarl(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.3f, 0.5f);
            float pitch = random.Range(88f, 112f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.9f), (seconds * 0.3f, pitch * 1.35f), (seconds, pitch * 0.85f)),
                Loudness = Curve.Hump(seconds * 0.12f, seconds, 1f, 0.8f),
                Tract = t => Vowel.Lerp(Vowel.Uh, Vowel.Oh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = 0.82f,
                Breath = 0.6f,
                Rough = 0.95f,
                Tension = 0.95f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Normalize(0f));
        }

        // High and short, falling away. Nothing else in the world makes this sound.
        private static void Yelp(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.2f, 0.32f);
            float pitch = random.Range(420f, 560f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch), (seconds * 0.25f, pitch * 1.3f), (seconds, pitch * 0.6f)),
                Loudness = Curve.Hump(seconds * 0.1f, seconds, 1f, 0.7f),
                Tract = t => Vowel.Lerp(Vowel.Eh, Vowel.Uh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = 0.62f,
                Breath = 0.3f,
                Rough = 0.4f,
                Tension = 0.9f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Normalize(0f));
        }

        private static void Hurt(Take take, float pitch, float size)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.16f, 0.3f);
            Vowel vowel = random.Pick(new[] { Vowel.Uh, Vowel.Ah, Vowel.Eh });
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 1.8f), (seconds * 0.2f, pitch * 2.05f), (seconds, pitch * 1.05f)),
                Loudness = Curve.Hump(seconds * 0.16f, seconds, 1f, 0.75f),
                Tract = t => Vowel.Lerp(vowel, Vowel.Uh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = size * random.Range(0.95f, 1.08f),
                Breath = 0.42f,
                Rough = 0.55f,
                Tension = 0.9f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Normalize(0f));

            // The air forced out by the blow, before any voice.
            take.Mix(take.Pink(0.05f).Bandpass(1600f, 0.9f).Percussive(0.001f, 0.012f).Normalize(0f), 0f, -12f);
        }

        private static void Dying(Take take, float pitch, float size)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.55f, 0.85f);
            Vowel start = random.Pick(new[] { Vowel.Ah, Vowel.Uh });
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 1.75f), (seconds * 0.15f, pitch * 1.9f), (seconds * 0.6f, pitch * 1.15f), (seconds, pitch * 0.72f)),
                Loudness = Curve.Hump(seconds * 0.14f, seconds, 1f, 0.7f),
                Tract = t => Vowel.Lerp(start, Vowel.Oh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = size,
                Breath = 0.5f,
                Rough = 0.72f,
                Tension = 0.72f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, voice).Normalize(0f));

            // The last of the breath going out.
            take.Mix(take.Pink(0.5f).Bandpass(Curve.Glide(1500f, 500f, 0.45f), 1.1f).Shape(Curve.Hump(0.05f, 0.5f)).Normalize(0f), seconds * 0.8f, -13f);
        }

        private static void Shout(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.3f, 0.48f);
            float pitch = random.Range(135f, 175f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch), (seconds * 0.35f, pitch * 1.4f), (seconds, pitch * 1.05f)),
                Loudness = Curve.Hump(seconds * 0.25f, seconds, 1f, 0.6f),
                Tract = t => Vowel.Lerp(Vowel.Eh, Vowel.Ah, Math.Clamp(t / (seconds * 0.5f), 0f, 1f)),
                Size = random.Range(1f, 1.15f),
                Breath = 0.28f,
                Rough = 0.5f,
                Tension = 1f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Normalize(0f));
        }

        // There's nothing in a dead throat to set vibrating, so this is breath through a dry pipe, and the bones of it clicking.
        private static void Moan(Take take, float length)
        {
            SoundRandom random = take.Random;
            float seconds = (0.45f + 0.9f * length) * random.Range(0.8f, 1.25f);
            float pitch = random.Range(58f, 82f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch), (seconds * 0.5f, pitch * 1.35f), (seconds, pitch * 0.8f)),
                Loudness = Curve.Hump(seconds * 0.4f, seconds, 1f, 0.8f),
                Tract = t => Vowel.Lerp(Vowel.Oo, Vowel.Oh, MathF.Sin(t * 2.3f) * 0.5f + 0.5f),
                Size = 1.25f,
                Breath = 0.85f,
                Rough = 0.95f,
                Tension = 0.3f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, voice).Normalize(0f));
            take.Mix(take.Pink(seconds).Bandpass(Curve.Glide(900f, 420f, seconds), 3f).Shape(Curve.Hump(seconds * 0.35f, seconds)).Normalize(0f), 0f, -10f);
            take.Mix(Grains.Settle(take, seconds, 8f, seconds * 0.5f, new GrainStyle { LowHz = 900f, HighHz = 3200f, MinDecay = 0.0015f, MaxDecay = 0.006f, Partials = 2, Noise = 0.2f, Spread = 10f }).Normalize(0f), 0f, -15f);
        }

        private static void DeadCollapse(Take take)
        {
            SoundRandom random = take.Random;

            // Everything that was holding it together stops doing so. Bone on bone, then bone on ground, a rattle running down.
            var style = new GrainStyle { LowHz = 620f, HighHz = 3600f, MinDecay = 0.002f, MaxDecay = 0.013f, Partials = 3, Noise = 0.1f, Spread = 12f };
            take.Mix(Grains.Settle(take, 1.15f, 36f, 0.3f, style).Normalize(0f), 0.02f, -2f);
            for (int i = 0; i < 3; i++)
            {
                var knock = new Blow { Material = Material.Bone, Shape = ModalShape.Bar, Fundamental = random.LogRange(240f, 460f), Modes = 12, Tilt = -2f, Rattle = 0.8f, Noise = -4f, NoiseLow = 160f, Bounces = 2 };
                take.Mix(Impact.Of(take, knock, 0.22f).Normalize(0f), random.Range(0.03f, 0.5f), -5f - i * 2f);
            }
            take.Mix(Impacts.Thump(take, 170f, 76f, 0.02f, 0.06f).Normalize(0f), 0.04f, -7f);
            take.Mix(take.Pink(0.4f).Bandpass(Curve.Glide(800f, 300f, 0.36f), 2.5f).Shape(Curve.Hump(0.08f, 0.4f)).Normalize(0f), 0f, -14f);
        }

        private static void Roar(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.95f, 1.45f);
            float pitch = random.Range(64f, 86f);

            // A chest far bigger than a man's, with the rattling low in it of folds driven past what they can carry.
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.9f), (seconds * 0.2f, pitch * 1.35f), (seconds * 0.7f, pitch * 1.1f), (seconds, pitch * 0.72f)),
                Loudness = Curve.Hump(seconds * 0.22f, seconds, 1f, 0.55f),
                Tract = t => Vowel.Lerp(Vowel.Ah, Vowel.Oh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = 1.6f,
                Breath = 0.55f,
                Rough = 1f,
                Tension = 1f,
            };
            Signal roar = Vocal.Utter(take, seconds + 0.05f, voice).Drive(3f).Normalize(0f);
            take.Mix(roar);

            // Lower still, an octave down: the subharmonic of a voice in a very large body.
            Signal under = take.Sine(seconds, Curve.Points((0f, pitch * 0.45f), (seconds * 0.2f, pitch * 0.67f), (seconds, pitch * 0.36f))).Shape(Curve.Hump(seconds * 0.22f, seconds)).Drive(2.4f).Normalize(0f);
            take.Mix(under, 0f, -7f);
            take.Mix(take.Pink(seconds).Bandpass(Curve.Glide(900f, 2400f, seconds * 0.4f), 0.9f).Shape(Curve.Hump(seconds * 0.25f, seconds)).Normalize(0f), 0f, -11f);
        }

        private static readonly Vowel[] SpeechVowels = { Vowel.Ah, Vowel.Eh, Vowel.Ih, Vowel.Oh, Vowel.Uh, Vowel.Er, Vowel.Ae };

        private static void Babble(Take take, float pitchScale)
        {
            SoundRandom random = take.Random;
            int syllables = 2 + random.Int(4);
            float total = 0f;
            var lengths = new float[syllables];
            var vowels = new Vowel[syllables];
            for (int i = 0; i < syllables; i++)
            {
                lengths[i] = random.Range(0.09f, 0.2f);
                vowels[i] = random.Pick(SpeechVowels);
                total += lengths[i];
            }

            // Talk rises and falls in phrases, and comes down at the end of a sentence, in every language there is.
            float pitch = random.Range(108f, 130f) * pitchScale;
            float rise = random.Range(1.08f, 1.32f);
            Curve melody = Curve.Points((0f, pitch), (total * 0.3f, pitch * rise), (total, pitch * 0.82f));

            var starts = new float[syllables + 1];
            for (int i = 0; i < syllables; i++) starts[i + 1] = starts[i] + lengths[i];

            Curve loudness = new Curve(t =>
            {
                int s = 0;
                while (s < syllables - 1 && t >= starts[s + 1]) s++;
                float x = (t - starts[s]) / lengths[s];
                if (x < 0f || x > 1f) return 0f;

                // A closing consonant at each end, and the voice shutting off with it.
                return MathF.Pow(MathF.Sin(MathF.PI * x), 0.7f) * (s == 0 ? 1f : 0.8f);
            });

            var voice = new VoiceSettings
            {
                Pitch = melody,
                Loudness = loudness,
                Tract = t =>
                {
                    int s = 0;
                    while (s < syllables - 1 && t >= starts[s + 1]) s++;
                    int next = Math.Min(s + 1, syllables - 1);
                    float x = Math.Clamp((t - starts[s]) / lengths[s], 0f, 1f);
                    return Vowel.Lerp(vowels[s], vowels[next], x * x);
                },
                Size = 1f / MathF.Sqrt(pitchScale),
                Breath = 0.3f,
                Rough = 0.22f,
                Tension = 0.45f,
            };
            take.Mix(Vocal.Utter(take, total + 0.05f, voice).Lowpass(3600f).Normalize(0f));
        }
    }
}
