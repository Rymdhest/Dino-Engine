using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// The Haven's giants, which are nobody's: read LORE.md. Nothing of the Reich's is in them, their voices least of all. The Reich's beasts roar as the creatures of films do, but the bones say
    /// the great ones boomed with their mouths shut, as birds and crocodiles do, and these are as they were: a call is a boom that swells in the chest, carried a long way, low enough to be felt as
    /// much as heard, with nothing in it that snarls. A foot coming down is the ground taking a weight bigger than a house, and eating is a crown of leaves being pulled at.
    /// </summary>
    [SoundCollection]
    public static class Giants
    {
        /// <summary>The call, and as often as not the answer: a long boom through a shut mouth, now and then with a shorter one leading into it, and a breath let out after.</summary>
        public static readonly SoundDefinition Call = SoundCatalog.Add(new SoundDefinition("giant.call", Calling)
        {
            Bus = SoundBus.Voice,
            Takes = 2,
            PlaysPerTake = 1,
            PitchSpread = 0.8f,
            GainSpread = 1f,
            Spacing = 1.5f,
            MostAtOnce = 2,
            Priority = 60,
            ReverbSend = 0.35f,
            NearDistance = 30f,
            FarDistance = 320f,
            Mastering = Mastering.Default with { Target = -20f, Stage = -15f },
        });

        /// <summary>A foot as wide as a cartwheel set down on turf: a thud deeper than any other step, the ground ringing with it, and the grass crushed under it.</summary>
        public static readonly SoundDefinition Step = SoundCatalog.Add(new SoundDefinition("giant.step", Stepping)
        {
            Bus = SoundBus.Foley,
            Takes = 6,
            PitchSpread = 1f,
            GainSpread = 1.5f,
            Spacing = 0.05f,
            MostAtOnce = 6,
            Priority = 52,
            ReverbSend = 0.15f,
            NearDistance = 18f,
            FarDistance = 150f,
            Mastering = Mastering.Default with { Target = -18f, Stage = -18f },
        });

        /// <summary>A pull at the top of a tree: the leaves thrashing as the branch is dragged, the wood creaking, now and then a twig going, and the crown springing back.</summary>
        public static readonly SoundDefinition Crop = SoundCatalog.Add(new SoundDefinition("giant.crop", Cropping)
        {
            Bus = SoundBus.Foley,
            Takes = 4,
            PitchSpread = 1f,
            GainSpread = 1.5f,
            Spacing = 0.4f,
            MostAtOnce = 3,
            Priority = 40,
            ReverbSend = 0.2f,
            NearDistance = 16f,
            FarDistance = 80f,
            Mastering = Mastering.Default with { Target = -22f, Stage = -18f },
        });

        private static void Calling(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(2.6f, 3.8f);
            float pitch = random.Range(40f, 56f);
            float at = 0f;

            // Half the time a shorter boom, a little lower, leads into the long one.
            if (random.Chance(0.45f))
            {
                float first = random.Range(0.75f, 1.05f);
                Boom(take, 0f, first, pitch * random.Range(0.86f, 0.94f), -3f);
                at = first + random.Range(0.25f, 0.45f);
            }
            Boom(take, at, seconds, pitch, 0f);

            // The breath let out through the nose once it's done, low and soft.
            float breath = random.Range(0.6f, 0.95f);
            take.Mix(take.Pink(breath).Bandpass(Curve.Glide(random.Range(460f, 560f), 280f, breath), 0.8f).Shape(Curve.Hump(breath * 0.2f, breath, 1f, 0.8f)).Normalize(0f), at + seconds * 0.9f, -19f);
        }

        // A boom: the throat behind a shut mouth, muffled by the body it booms in, and the chest ringing with it, swelling and dying away, throbbing a little as the air in it pulses.
        private static void Boom(Take take, float at, float seconds, float pitch, float decibels)
        {
            SoundRandom random = take.Random;
            float swell = seconds * random.Range(0.25f, 0.4f);
            float throb = random.Range(3.2f, 4.6f);
            float depth = random.Range(0.1f, 0.2f);
            float drift = random.Range(0f, MathF.Tau);
            Curve tone = Curve.Points((0f, pitch * 0.88f), (swell, pitch), (seconds * 0.7f, pitch * random.Range(0.95f, 0.99f)), (seconds, pitch * 0.8f));
            Curve hump = Curve.Hump(swell, seconds, 1f, 0.8f);
            Curve loud = new Curve(t => hump.At(t) * (1f + depth * MathF.Sin(MathF.Tau * throb * t + drift)));

            var voice = new VoiceSettings
            {
                Pitch = tone,
                Loudness = loud,
                Tract = t => Vowel.Lerp(Vowel.Uh, Vowel.Oo, 0.3f + 0.7f * Math.Clamp(t / seconds, 0f, 1f)),
                Size = random.Range(3.4f, 4f),
                Breath = 0.12f,
                Rough = 0.12f,
                Tension = 0.3f,
            };
            Signal throat = Vocal.Utter(take, seconds + 0.05f, voice).Lowpass(random.Range(460f, 580f)).Fade(0.01f, 0.05f).Normalize(0f);
            Signal chest = take.Sine(seconds, tone)
                .Add(take.Sine(seconds, tone * 2f), 0f, random.Range(0.3f, 0.42f))
                .Add(take.Sine(seconds, tone * 3f), 0f, random.Range(0.1f, 0.18f))
                .Shape(loud).Drive(1.3f).Normalize(0f);
            take.Mix(chest, at, decibels);
            take.Mix(throat, at + 0.01f, decibels - 5f);
        }

        private static void Stepping(Take take)
        {
            SoundRandom random = take.Random;
            float pitch = random.Range(24f, 32f);

            // The weight arriving, deeper and longer than a tyrant's tread, and the ground ringing on after it, dying right away by the end.
            take.Mix(Impacts.Thump(take, pitch * 2.2f, pitch, 0.05f, random.Range(0.3f, 0.42f), 1.1f).Fade(0f, 0.25f).Normalize(0f));
            take.Mix(take.Brown(1.1f).Lowpass(Curve.Glide(random.Range(190f, 240f), 70f, 0.6f)).Percussive(0.008f, random.Range(0.32f, 0.45f)).Fade(0f, 0.3f).Normalize(0f), 0.006f, -3f);

            // Turf giving under it, which is what says it's soft ground and not stone,
            take.Mix(take.Pink(0.3f).Bandpass(random.Range(170f, 260f), 0.9f).Percussive(0.003f, random.Range(0.06f, 0.1f)).Normalize(0f), 0.002f, -7f);

            // and the grass crushed flat, many small breaks at once.
            Curve crush = Curve.Hump(0.03f, 0.26f);
            take.Mix(Grains.Stream(take, 0.26f, crush * random.Range(380f, 560f), crush, new GrainStyle { LowHz = 900f, HighHz = 4800f, MinDecay = 0.001f, MaxDecay = 0.004f, Partials = 1, Noise = 0.9f, Spread = 14f }).Normalize(0f), 0.004f, -17f);
        }

        private static void Cropping(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.9f, 1.4f);

            // The leaves thrashing as the branch is dragged, and again, less, as the crown springs back.
            var leaves = new GrainStyle { LowHz = 1300f, HighHz = 7000f, MinDecay = 0.0009f, MaxDecay = 0.006f, Partials = 1, Noise = 0.92f, Spread = 16f };
            Curve thrash = Curve.Hump(seconds * random.Range(0.2f, 0.32f), seconds, 1f, 0.8f);
            take.Mix(Grains.Stream(take, seconds, thrash * random.Range(900f, 1300f), thrash, leaves).Normalize(0f));
            float back = random.Range(0.5f, 0.75f);
            Curve spring = Curve.Hump(back * 0.15f, back, 1f, 1.2f);
            take.Mix(Grains.Stream(take, back, spring * random.Range(600f, 900f), spring, leaves).Normalize(0f), seconds * 0.82f, -6f);

            // The wood bending.
            take.Mix(Creak(take, random.Range(0.3f, 0.5f), random.Range(170f, 260f)).Normalize(0f), seconds * random.Range(0.12f, 0.28f), -13f);

            // Now and then a twig going.
            if (random.Chance(0.4f)) take.Mix(take.White(0.04f).Highpass(1800f).Percussive(0.0004f, 0.008f).Fade(0f, 0.01f).Normalize(0f), seconds * random.Range(0.3f, 0.6f), -9f);
        }

        // Wood bending is stick and slip: the fibres catch and let go, a click each time, coming quicker as it's pulled harder and slower as it eases, each ringing in the branch.
        private static Signal Creak(Take take, float seconds, float wood)
        {
            SoundRandom random = take.Random;
            Signal creak = take.Track(seconds);
            float[] samples = creak.Samples;
            float slowest = random.Range(35f, 55f);
            float quickest = slowest * random.Range(1.4f, 2f);
            float sign = 1f;
            for (float t = random.Range(0f, 0.01f); t < seconds; )
            {
                int i = (int)(t * AudioFormat.SampleRate);
                if (i >= creak.Length) break;
                float pulled = MathF.Sin(MathF.PI * t / seconds);
                samples[i] += sign * pulled * random.Range(0.6f, 1f);
                sign = -sign;
                t += random.Range(0.8f, 1.25f) / (slowest + (quickest - slowest) * pulled);
            }
            return creak.Comb(wood, 0.8f).Bandpass(wood * 2.2f, 1.1f).Fade(0.004f, 0.03f);
        }
    }
}
