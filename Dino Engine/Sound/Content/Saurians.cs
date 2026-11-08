using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// Dinosaur throats. Nobody knows what one sounded like, and the bones say birds and crocodiles boom with their mouths shut, but these are the Reich's animals, bred and wrong, and they roar as
    /// the creatures of films do: a throat far bigger than any living thing's with a shriek riding on top of it, as those roars were built from an elephant's trumpet over a tiger and an alligator.
    /// A raptor honks and hisses, a skitter chirps, and anything big enough shakes the ground as it walks. Read LORE.md.
    /// </summary>
    [SoundCollection]
    public static class Saurians
    {
        // As loud as the beasts were, a little more for the big ones and a lot more for a tyrant, which is a land's boss. Raptors are most of what anyone meets, and a fight full of them once
        // sat on the limiter where one with the old enemies never touched it.
        public static readonly SoundDefinition TyrantRoar = Add("saurian.tyrant.roar", Roar, -13f, cooldown: 1.5f, priority: 94, presence: 7f, near: 20f, far: 160f);
        public static readonly SoundDefinition TyrantGrowl = Add("saurian.tyrant.growl", take => Growling(take, 2.7f, 36f, 50f, 0.55f, 0.85f), -17f, cooldown: 0.6f, priority: 85, presence: 2f, near: 16f, far: 110f);
        public static readonly SoundDefinition TyrantDeath = Add("saurian.tyrant.death", Dying, -11f, priority: 96, presence: 8f, near: 20f, far: 160f);
        public static readonly SoundDefinition RaptorCall = Add("saurian.raptor.call", Call, -21f, cooldown: 0.5f, near: 11f, far: 70f);
        public static readonly SoundDefinition RaptorDeath = Add("saurian.raptor.death", Shriek, -20f, near: 11f, far: 70f);
        public static readonly SoundDefinition Growl = Add("saurian.growl", take => Growling(take, 1.9f, 52f, 74f, 0.7f, 1.05f), -19f, cooldown: 0.5f, priority: 70, presence: 1f, near: 14f, far: 90f);
        public static readonly SoundDefinition Bellow = Add("saurian.bellow", Bellowing, -19f, cooldown: 0.8f, priority: 70, presence: 1f, near: 14f, far: 90f);
        public static readonly SoundDefinition Death = Add("saurian.death", take => Falling(take, 1.9f, 78f), -18f, near: 14f, far: 90f);
        public static readonly SoundDefinition Chirp = Add("saurian.skitter.chirp", Chirps, -23f, cooldown: 0.3f, near: 8f, far: 45f);
        public static readonly SoundDefinition Hiss = Add("saurian.hiss", take => Hissing(take, false), -21f, cooldown: 0.5f, near: 10f, far: 55f);
        public static readonly SoundDefinition RotHiss = Add("saurian.rot.hiss", take => Hissing(take, true), -21f, cooldown: 0.5f, near: 10f, far: 55f);

        /// <summary>A foot the size of a cart coming down: under everything else a step makes, and felt as much as heard.</summary>
        public static readonly SoundDefinition Tread = SoundCatalog.Add(new SoundDefinition("saurian.tread", Treading)
        {
            Bus = SoundBus.Foley,
            Takes = 6,
            PitchSpread = 1.2f,
            GainSpread = 1.5f,
            Spacing = 0.06f,
            MostAtOnce = 6,
            Priority = 55,
            ReverbSend = 0.15f,
            NearDistance = 16f,
            FarDistance = 120f,
            Mastering = Mastering.Default with { Target = -17f, Stage = -18f },
        });

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float cooldown = 0.1f, int priority = 62, float presence = 0f, float near = 11f, float far = 55f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Voice,
                Takes = 6,
                PitchSpread = 1.2f,
                GainSpread = 1.4f,
                Spacing = cooldown,
                MostAtOnce = 4,
                Priority = priority,
                Presence = presence,
                ReverbSend = 0.2f,
                NearDistance = near,
                FarDistance = far,
                Mastering = Mastering.Default with { Target = target, Stage = -15f },
            });
        }

        // The tyrant's roar: rising out of the chest, held, and falling away, with a scream on top of it for the first half and the ground of it an octave down.
        private static void Roar(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(2f, 2.6f);
            float pitch = random.Range(46f, 58f);
            var throat = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.75f), (seconds * 0.18f, pitch * 1.2f), (seconds * 0.55f, pitch * 1.05f), (seconds, pitch * 0.6f)),
                Loudness = Curve.Hump(seconds * 0.2f, seconds, 1f, 0.5f),
                Tract = t => Vowel.Lerp(Vowel.Ah, Vowel.Oh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = random.Range(2.8f, 3.3f),
                Breath = 0.6f,
                Rough = 1f,
                Tension = 1f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, throat).Drive(3.6f).Normalize(0f));

            float scream = seconds * random.Range(0.5f, 0.65f);
            float high = random.Range(330f, 470f);
            var shriek = new VoiceSettings
            {
                Pitch = Curve.Points((0f, high * 0.8f), (scream * 0.25f, high * 1.25f), (scream * 0.6f, high * 1.1f), (scream, high * 0.7f)),
                Loudness = Curve.Hump(scream * 0.25f, scream, 1f, 0.6f),
                Tract = t => Vowel.Lerp(Vowel.Ae, Vowel.Ah, Math.Clamp(t / scream, 0f, 1f)),
                Size = 0.72f,
                Breath = 0.4f,
                Rough = 0.85f,
                Tension = 1f,
            };
            take.Mix(Vocal.Utter(take, scream + 0.05f, shriek).Drive(2.2f).Bandpass(1500f, 0.7f).Normalize(0f), seconds * 0.04f, -7f);

            Signal under = take.Sine(seconds, Curve.Points((0f, pitch * 0.38f), (seconds * 0.18f, pitch * 0.6f), (seconds, pitch * 0.3f))).Shape(Curve.Hump(seconds * 0.2f, seconds)).Drive(2.6f).Normalize(0f);
            take.Mix(under, 0f, -6f);
            take.Mix(take.Pink(seconds).Bandpass(Curve.Glide(600f, 1800f, seconds * 0.4f), 0.9f).Shape(Curve.Hump(seconds * 0.25f, seconds)).Normalize(0f), 0f, -11f);
        }

        // Low and short, without the scream: what comes out with a blow.
        private static void Growling(Take take, float size, float low, float high, float shortest, float longest)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(shortest, longest);
            float pitch = random.Range(low, high);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.85f), (seconds * 0.3f, pitch * 1.25f), (seconds, pitch * 0.8f)),
                Loudness = Curve.Hump(seconds * 0.18f, seconds, 1f, 0.7f),
                Tract = t => Vowel.Lerp(Vowel.Uh, Vowel.Oh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = size,
                Breath = 0.65f,
                Rough = 1f,
                Tension = 0.95f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Drive(2.8f).Normalize(0f));
            take.Mix(take.Pink(seconds).Bandpass(Curve.Glide(500f, 1100f, seconds * 0.5f), 1.1f).Shape(Curve.Hump(seconds * 0.2f, seconds)).Normalize(0f), 0f, -12f);
        }

        // A tyrant going down: the roar falling the whole way, the scream breaking, and the weight of it hitting the ground.
        private static void Dying(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(2.2f, 2.8f);
            float pitch = random.Range(48f, 58f);
            var throat = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 1.15f), (seconds * 0.15f, pitch * 1.25f), (seconds * 0.6f, pitch * 0.8f), (seconds, pitch * 0.5f)),
                Loudness = Curve.Hump(seconds * 0.15f, seconds, 1f, 0.6f),
                Tract = t => Vowel.Lerp(Vowel.Ah, Vowel.Oo, Math.Clamp(t / seconds, 0f, 1f)),
                Size = 3.1f,
                Breath = 0.7f,
                Rough = 1f,
                Tension = 0.8f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, throat).Drive(3f).Normalize(0f));

            float scream = seconds * 0.45f;
            var shriek = new VoiceSettings
            {
                Pitch = Curve.Points((0f, 460f), (scream * 0.3f, 520f), (scream, 190f)),
                Loudness = Curve.Hump(scream * 0.2f, scream, 1f, 0.7f),
                Tract = t => Vowel.Lerp(Vowel.Ae, Vowel.Uh, Math.Clamp(t / scream, 0f, 1f)),
                Size = 0.75f,
                Breath = 0.5f,
                Rough = 0.9f,
                Tension = 0.9f,
            };
            take.Mix(Vocal.Utter(take, scream + 0.05f, shriek).Drive(2f).Bandpass(1400f, 0.7f).Normalize(0f), 0.05f, -8f);
            take.Mix(Impacts.Thump(take, 90f, 28f, 0.05f, 0.45f, 0.9f).Normalize(0f), seconds * 0.85f, -2f);
            take.Mix(take.Brown(1.2f).Lowpass(140f).Percussive(0.01f, 0.6f).Normalize(0f), seconds * 0.85f, -6f);
        }

        // A raptor: a honk from the back of the throat, a hiss after it, and now and then the clicking chatter they make to each other.
        private static void Call(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.3f, 0.55f);
            float pitch = random.Range(360f, 520f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.85f), (seconds * 0.3f, pitch * 1.3f), (seconds, pitch * 0.75f)),
                Loudness = Curve.Hump(seconds * 0.15f, seconds, 1f, 0.75f),
                Tract = t => Vowel.Lerp(Vowel.Eh, Vowel.Ah, Math.Clamp(t / seconds, 0f, 1f)),
                Size = random.Range(0.78f, 0.9f),
                Breath = 0.65f,
                Rough = 0.85f,
                Tension = 0.95f,
            };
            Signal honk = Vocal.Utter(take, seconds + 0.04f, voice).Drive(2f).Normalize(0f);

            // A tongue against the roof of the mouth, thirty times a second.
            if (random.Chance(0.4f))
            {
                float rate = random.Range(26f, 34f);
                honk = honk.Multiply(take.Sine(seconds + 0.04f, Curve.Points((0f, rate), (seconds, rate))).Fold(2.5f).Shape(Curve.Points((0f, 0.6f), (seconds, 0.6f))));
            }
            take.Mix(honk.Normalize(0f));
            take.Mix(take.Pink(0.35f).Bandpass(random.Range(2800f, 3800f), 1.4f).Shape(Curve.Hump(0.08f, 0.35f)).Normalize(0f), seconds * 0.6f, -9f);
        }

        // High and falling: a raptor's last.
        private static void Shriek(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.45f, 0.65f);
            float pitch = random.Range(520f, 680f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch), (seconds * 0.2f, pitch * 1.2f), (seconds, pitch * 0.35f)),
                Loudness = Curve.Hump(seconds * 0.12f, seconds, 1f, 0.65f),
                Tract = t => Vowel.Lerp(Vowel.Ae, Vowel.Uh, Math.Clamp(t / seconds, 0f, 1f)),
                Size = 0.78f,
                Breath = 0.55f,
                Rough = 0.9f,
                Tension = 0.9f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.04f, voice).Drive(1.8f).Normalize(0f));
        }

        // What a horned grazer does when it means to charge: long and low through a closed mouth, more horn than throat.
        private static void Bellowing(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.9f, 1.3f);
            float pitch = random.Range(70f, 92f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, pitch * 0.9f), (seconds * 0.4f, pitch * 1.15f), (seconds, pitch * 0.85f)),
                Loudness = Curve.Hump(seconds * 0.35f, seconds, 1f, 0.6f),
                Tract = t => Vowel.Lerp(Vowel.Oo, Vowel.Oh, MathF.Sin(t * 3f) * 0.5f + 0.5f),
                Size = 2.1f,
                Breath = 0.5f,
                Rough = 0.75f,
                Tension = 0.8f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, voice).Drive(2.2f).Normalize(0f));
            take.Mix(take.Sine(seconds, Curve.Points((0f, pitch * 0.5f), (seconds, pitch * 0.42f))).Shape(Curve.Hump(seconds * 0.35f, seconds)).Normalize(0f), 0f, -8f);
        }

        // Anything big going down: a groan falling away, and the ground taking it.
        private static void Falling(Take take, float size, float pitch)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.9f, 1.2f);
            float start = pitch * random.Range(0.9f, 1.1f);
            var voice = new VoiceSettings
            {
                Pitch = Curve.Points((0f, start * 1.2f), (seconds * 0.2f, start * 1.3f), (seconds, start * 0.55f)),
                Loudness = Curve.Hump(seconds * 0.15f, seconds, 1f, 0.65f),
                Tract = t => Vowel.Lerp(Vowel.Ah, Vowel.Oo, Math.Clamp(t / seconds, 0f, 1f)),
                Size = size,
                Breath = 0.6f,
                Rough = 0.95f,
                Tension = 0.8f,
            };
            take.Mix(Vocal.Utter(take, seconds + 0.05f, voice).Drive(2.4f).Normalize(0f));
        }

        // Little bird-sounds in a burst, two to four at a time, each sweeping up.
        private static void Chirps(Take take)
        {
            SoundRandom random = take.Random;
            int count = 2 + random.Int(3);
            float at = 0f;
            for (int i = 0; i < count; i++)
            {
                float length = random.Range(0.04f, 0.075f);
                float from = random.Range(1700f, 2400f);
                Signal chirp = take.Sine(length, Curve.Glide(from, from * random.Range(1.3f, 1.6f), length)).Drive(1.6f).Percussive(0.009f, length * 0.6f).Lowpass(7000f).Normalize(0f);
                take.Mix(chirp, at, -i * 1.5f);
                at += length + random.Range(0.03f, 0.07f);
            }
        }

        // Breath forced through a narrow throat, rattling in it. What's rotting gurgles under it as well.
        private static void Hissing(Take take, bool rotting)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.5f, 0.8f);
            Signal hiss = take.Pink(seconds).Bandpass(random.Range(2400f, 3400f), 1.8f).Lowpass(5000f).Shape(Curve.Hump(seconds * 0.2f, seconds, 1f, 0.7f));
            float rattle = random.Range(15f, 22f);
            hiss = hiss.Multiply(take.Sine(seconds, Curve.Points((0f, rattle), (seconds, rattle * 0.8f))).Fold(1.5f).Shape(Curve.Points((0f, 0.5f), (seconds, 0.5f))));
            take.Mix(hiss.Normalize(0f));
            if (!rotting) return;
            take.Mix(take.Brown(seconds).Lowpass(Curve.Glide(380f, 160f, seconds)).Multiply(take.Sine(seconds, Curve.Points((0f, 7f), (seconds, 5f))).Fold(2f)).Shape(Curve.Hump(seconds * 0.4f, seconds)).Normalize(0f), 0f, -6f);
        }

        // A thud far below a person's, and the ground ringing under it for a moment.
        private static void Treading(Take take)
        {
            SoundRandom random = take.Random;
            float pitch = random.Range(28f, 40f);
            take.Mix(Impacts.Thump(take, pitch * 2.6f, pitch, 0.03f, random.Range(0.22f, 0.32f), 0.6f).Normalize(0f));
            take.Mix(take.Brown(0.7f).Lowpass(150f).Percussive(0.006f, 0.35f).Normalize(0f), 0.005f, -5f);
            take.Mix(take.Pink(0.2f).Bandpass(420f, 0.8f).Percussive(0.002f, 0.06f).Normalize(0f), 0f, -14f);
        }
    }
}
