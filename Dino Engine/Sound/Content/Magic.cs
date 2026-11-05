using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>The kinds of magic a sound can be, each with its own character, so that a player can tell one from another with their eyes shut.</summary>
    public enum Element
    {
        Fire,
        Frost,
        Lightning,
        Poison,
        Holy,
        Shadow,
        Arcane,
    }

    /// <summary>
    /// Spells have no real sound to imitate, which is a freedom and a trap, as most end up as whooshes with reverb. Each element here
    /// keeps a foot in a real thing: fire is real fire, frost is ice cracking, poison is a liquid eating at something, so that an unreal
    /// event still sounds like it's happening in the world. Each element has the same four moments, so a new spell is a new line.
    /// </summary>
    [SoundCollection]
    public static class Magic
    {
        private static readonly SoundDefinition[,] Spells = new SoundDefinition[Enum.GetValues<Element>().Length, 4];

        public static SoundDefinition Cast(Element element) => Spells[(int)element, 0];
        public static SoundDefinition Hit(Element element) => Spells[(int)element, 1];
        public static SoundDefinition Burst(Element element) => Spells[(int)element, 2];
        public static SoundDefinition Aura(Element element) => Spells[(int)element, 3];

        public static readonly SoundDefinition Heal = Add("magic.heal", Healing, -18f, stereo: true);
        public static readonly SoundDefinition Buff = Add("magic.buff", Strengthen, -19f, stereo: true);
        public static readonly SoundDefinition Curse = Add("magic.curse", Weaken, -19f, stereo: true);
        public static readonly SoundDefinition Poisoned = Add("status.poisoned", Sick, -22f);
        public static readonly SoundDefinition Burning = Add("status.burning", Scorched, -22f);
        public static readonly SoundDefinition Frozen = Add("status.frozen", Iced, -20f);
        public static readonly SoundDefinition Stunned = Add("status.stunned", Dazed, -20f, stereo: true);
        public static readonly SoundDefinition GuidingWind = Add("magic.guide", GuideCalled, -17f, stereo: true);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, bool stereo = false, float presence = 0f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Combat,
                Stereo = stereo,
                Takes = 4,
                PlaysPerTake = 2,
                Priority = 72,
                Presence = presence,
                NearDistance = 12f,
                FarDistance = 70f,
                ReverbSend = 0.3f,
                Mastering = Mastering.Default with { Target = target, Stage = -10f },
            });
        }

        static Magic()
        {
            foreach (Element element in Enum.GetValues<Element>())
            {
                string name = element.ToString().ToLowerInvariant();
                Spells[(int)element, 0] = Add($"magic.{name}.cast", take => CastSpell(take, element), -17f, stereo: true);
                Spells[(int)element, 1] = Add($"magic.{name}.hit", take => HitSpell(take, element, 0.5f), -14f, presence: 2f);
                Spells[(int)element, 2] = Add($"magic.{name}.burst", take => HitSpell(take, element, 1f), -12f, stereo: true, presence: 5f);
                Spells[(int)element, 3] = Add($"magic.{name}.aura", take => AuraSpell(take, element), -23f, stereo: true);
            }
        }

        private static void CastSpell(Take take, Element element)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.42f, 0.62f);
            Curve gather = new Curve(t => MathF.Pow(Math.Clamp(t / (seconds * 0.8f), 0f, 1f), 1.8f));
            Curve release = Curve.Hump(seconds * 0.8f, seconds * 1.3f, 1f);

            // All casting is alike in shape: something is drawn together, and let go.
            Whoosh(take, seconds * 1.3f, seconds * 0.8f, element);
            switch (element)
            {
                case Element.Fire:
                    take.Mix(Flare(take, seconds * 1.3f).Shape(release).Normalize(0f), 0f, -4f);
                    take.Mix(Spit(take, seconds, 170f).Shape(gather).Normalize(0f), 0f, -10f);
                    break;
                case Element.Frost:
                    take.Mix(Crackling(take, seconds, Curve.Line(30f, 340f, seconds), 2200f, 10000f, 0.002f).Normalize(0f), 0f, -6f);
                    take.Mix(Glass(take, seconds * 1.4f, random.LogRange(1800f, 2600f)).Normalize(0f), seconds * 0.7f, -9f);
                    break;
                case Element.Lightning:
                    take.Mix(Arc(take, seconds * 1.3f, gather).Normalize(0f), 0f, -3f);
                    break;
                case Element.Poison:
                    take.Mix(Bubbling(take, seconds * 1.2f, 0.4f).Normalize(0f), 0f, -5f);
                    take.Mix(Sizzle(take, seconds * 1.2f).Shape(release).Normalize(0f), 0f, -12f);
                    break;
                case Element.Holy:
                    take.Mix(Choir(take, seconds * 1.5f, random.Pick(new[] { 293.66f, 329.63f }), 0.2f).Normalize(0f), 0f, -3f);
                    break;
                case Element.Shadow:
                    take.Mix(Breath(take, seconds * 1.4f, gather).Normalize(0f), 0f, -4f);
                    take.Mix(Sub(take, seconds * 1.4f, 46f).Normalize(0f), 0f, -6f);
                    break;
                default:
                    take.Mix(Motes(take, seconds * 1.4f, 1100f).Normalize(0f), 0f, -3f);
                    break;
            }
        }

        private static void HitSpell(Take take, Element element, float size)
        {
            SoundRandom random = take.Random;
            float seconds = 0.5f + 1.4f * size;

            // What it lands on feels it, whatever it is.
            take.Mix(Impacts.Crack(take, 0.0016f + 0.0016f * size).Normalize(0f), 0f, -2f);
            take.Mix(Impacts.Thump(take, 240f, 62f, 0.02f, 0.05f + 0.13f * size, seconds).Normalize(0f), 0f, -3f);

            switch (element)
            {
                case Element.Fire:
                    take.Mix(Flare(take, seconds).Percussive(0.002f, 0.11f + 0.24f * size).Normalize(0f), 0f, -2f);
                    take.Mix(Spit(take, seconds, 260f).Normalize(0f), 0.01f, -9f);
                    break;
                case Element.Frost:
                    var ice = new Blow { Material = Material.Ice, Shape = ModalShape.Plate, Fundamental = random.LogRange(900f, 1600f), Modes = 34, Tilt = -1f, Rattle = 1f, Noise = -3f, NoiseLow = 400f };
                    take.Mix(Impact.Of(take, ice, seconds * 0.5f).Normalize(0f), 0f, -2f);
                    var shards = new GrainStyle { LowHz = 2000f, HighHz = 12000f, MinDecay = 0.003f, MaxDecay = 0.025f, Partials = 3, Noise = 0f, Spread = 14f };
                    take.Mix(Grains.Settle(take, seconds, 26f + 40f * size, seconds * 0.26f, shards).Normalize(0f), 0.008f, -4f);
                    break;
                case Element.Lightning:
                    take.Mix(Arc(take, seconds * 0.6f, Curve.Settle(1f, 0f, 0.05f + 0.1f * size)).Normalize(0f), 0f, -2f);
                    take.Mix(take.White(0.06f).Highpass(240f).Percussive(0.00002f, 0.01f).Drive(4f).Normalize(0f), 0f, -1f);
                    break;
                case Element.Poison:
                    take.Mix(Bubbling(take, seconds, 1f).Normalize(0f), 0.01f, -4f);
                    take.Mix(Sizzle(take, seconds).Percussive(0.003f, 0.2f + 0.4f * size).Normalize(0f), 0f, -7f);
                    take.Mix(take.Pink(seconds * 0.5f).Bandpass(Curve.Glide(1500f, 300f, 0.2f), 1.5f).Percussive(0.002f, 0.04f).Normalize(0f), 0f, -8f);
                    break;
                case Element.Holy:
                    take.Mix(Choir(take, seconds * 1.3f, random.Pick(new[] { 392f, 440f }), 0.02f).Normalize(0f), 0f, -3f);
                    take.Mix(Bells.Toll(take, 196f, seconds).Normalize(0f), 0f, -7f);
                    break;
                case Element.Shadow:
                    take.Mix(Breath(take, seconds, Curve.Settle(1f, 0f, 0.1f + 0.3f * size)).Normalize(0f), 0f, -3f);
                    take.Mix(Sub(take, seconds, 40f).Normalize(0f), 0f, -3f);
                    break;
                default:
                    take.Mix(Motes(take, seconds, 1400f).Normalize(0f), 0f, -3f);
                    take.Mix(take.Sine(seconds, Curve.Glide(1800f, 240f, 0.09f)).Percussive(0.0008f, 0.06f + 0.1f * size).Normalize(0f), 0f, -9f);
                    break;
            }
        }

        private static void AuraSpell(Take take, Element element)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(1.6f, 2.4f);
            Curve swell = Curve.Hump(seconds * 0.5f, seconds);
            switch (element)
            {
                case Element.Fire:
                    take.Mix(Flare(take, seconds).Shape(swell).Normalize(0f));
                    break;
                case Element.Frost:
                    take.Mix(Crackling(take, seconds, swell * 70f, 2600f, 10000f, 0.003f).Normalize(0f));
                    take.Mix(take.White(seconds).Bandpass(6500f, 3f).Shape(swell).Normalize(0f), 0f, -13f);
                    break;
                case Element.Lightning:
                    take.Mix(Arc(take, seconds, swell * 0.7f).Normalize(0f));
                    break;
                case Element.Poison:
                    take.Mix(Bubbling(take, seconds, 0.7f).Normalize(0f));
                    break;
                case Element.Holy:
                    take.Mix(Choir(take, seconds, 349.23f, seconds * 0.4f).Normalize(0f));
                    break;
                case Element.Shadow:
                    take.Mix(Breath(take, seconds, swell).Normalize(0f));
                    break;
                default:
                    take.Mix(Motes(take, seconds, 900f).Normalize(0f));
                    break;
            }
        }

        // --- Pieces: what each element is made of. ---

        private static void Whoosh(Take take, float seconds, float peak, Element element)
        {
            float low = element is Element.Shadow or Element.Fire ? 180f : 420f;
            float high = element is Element.Frost or Element.Lightning or Element.Arcane ? 4800f : 2200f;
            Curve sweep = Curve.Glide(low, high, peak);
            Curve shape = Curve.Hump(peak, seconds);
            Signal left = take.Pink(seconds).Bandpass(sweep, 1f).Shape(shape).Normalize(0f);
            Signal right = take.Pink(seconds).Bandpass(sweep, 1f).Shape(shape).Normalize(0f);
            take.MixStereo(left, right, 0f, -6f);
        }

        // Rushing flame: turbulent gas burning, a roar with a fluttering edge, wider and rougher than wind.
        private static Signal Flare(Take take, float seconds)
        {
            Curve flutter = take.Drift(seconds, 24f);
            Signal flame = take.Pink(seconds).Lowpass(Curve.Glide(2800f, 420f, seconds * 0.8f));
            flame.Shape(new Curve(t => 0.35f + flutter.At(t) * 1.3f));
            flame.Add(take.Brown(seconds).Lowpass(280f), 0f, 2.2f);
            return flame.Drive(2f);
        }

        private static Signal Spit(Take take, float seconds, float rate)
        {
            return Grains.Stream(take, seconds, rate, Curve.Settle(1f, 0.2f, seconds * 0.4f), new GrainStyle { LowHz = 1500f, HighHz = 11000f, MinDecay = 0.00015f, MaxDecay = 0.001f, Partials = 1, Noise = 0.9f, Spread = 24f });
        }

        private static Signal Crackling(Take take, float seconds, Curve rate, float low, float high, float decay)
        {
            return Grains.Stream(take, seconds, rate, 1f, new GrainStyle { LowHz = low, HighHz = high, MinDecay = decay * 0.4f, MaxDecay = decay * 2.4f, Partials = 3, Noise = 0.05f, Spread = 16f });
        }

        private static Signal Glass(Take take, float seconds, float pitch)
        {
            var blow = new Blow { Material = Material.Glass, Shape = ModalShape.Shell, Fundamental = pitch, Modes = 16, Tilt = -1.4f, Ringing = 0.7f, Rattle = 0.5f, Noise = -9f, NoiseLow = 1000f };
            return Impact.Of(take, blow, seconds);
        }

        // Electricity in air: a harsh buzz at the mains of nature, jumping in pitch as the arc finds a new path.
        private static Signal Arc(Take take, float seconds, Curve energy)
        {
            SoundRandom random = take.Random;
            Signal arc = take.Track(seconds);
            float[] samples = arc.Samples;
            float phase = 0f;
            float pitch = random.LogRange(90f, 180f);
            float until = 0f;
            float level = 1f;
            for (int i = 0; i < arc.Length; i++)
            {
                float t = i * (1f / AudioFormat.SampleRate);
                until -= 1f / AudioFormat.SampleRate;
                if (until <= 0f)
                {
                    until = random.Exponential(0.018f);
                    pitch = random.LogRange(80f, 320f);
                    level = random.Range(0.25f, 1f);
                }
                phase += pitch / AudioFormat.SampleRate;
                if (phase >= 1f) phase -= 1f;
                samples[i] = (phase < 0.08f ? 1f : -0.08f) * level * energy.At(t);
            }
            arc.Add(take.White(seconds).Highpass(3200f).Shape(energy), 0f, 0.22f);
            return arc.Highpass(200f).Lowpass(11000f).Drive(3f);
        }

        private static Signal Bubbling(Take take, float seconds, float level)
        {
            Curve energy = Curve.Hump(seconds * 0.3f, seconds);
            return Liquid.Bubbles(take, seconds, energy * (240f + 900f * level), energy, 0.0012f, 0.0075f, 2f, 0.35f);
        }

        // Acid on something: a fine, dense hissing with spits in it, thinner than a flame and higher.
        private static Signal Sizzle(Take take, float seconds)
        {
            Signal fizz = take.White(seconds).Bandpass(6400f, 0.8f);
            Curve wobble = take.Drift(seconds, 30f);
            fizz.Shape(new Curve(t => 0.3f + wobble.At(t) * 0.9f));
            fizz.Add(Grains.Stream(take, seconds, 420f, 1f, new GrainStyle { LowHz = 3000f, HighHz = 13000f, MinDecay = 0.0002f, MaxDecay = 0.0012f, Partials = 1, Noise = 0.7f, Spread = 16f }), 0f, 2.5f);
            return fizz;
        }

        // Voices with no singer: a soft stack of vowels, opening slowly.
        private static Signal Choir(Take take, float seconds, float pitch, float rise)
        {
            SoundRandom random = take.Random;
            Signal choir = take.Track(seconds);
            float[] chord = { 1f, 1.5f, 2f, 2.5f, 3f };
            foreach (float note in chord)
            {
                for (int voice = 0; voice < 3; voice++)
                {
                    float detune = 1f + random.Gaussian() * 0.004f;
                    float vibrato = random.Range(4.5f, 6f);
                    float depth = random.Range(0.003f, 0.006f);
                    float f = pitch * note * detune;
                    Signal one = take.Sine(seconds, new Curve(t => f * (1f + depth * MathF.Sin(MathF.Tau * vibrato * t))), random.Range(0f, 6f));
                    choir.Add(one, 0f, 0.18f);
                }
            }
            Signal air = take.Pink(seconds).Bandpass(pitch * 4f, 2f);
            choir.Add(air, 0f, 0.2f);
            choir.Bandpass(Curve.Glide(pitch * 1.5f, pitch * 4.5f, MathF.Max(seconds * 0.5f, 0.05f)), 0.7f);
            choir.Shape(Curve.Hump(MathF.Max(rise, 0.02f) + seconds * 0.16f, seconds));
            return choir;
        }

        // Something breathing that shouldn't be: a pipe of air, drawn in and out, with its tone shifted down from anything living.
        private static Signal Breath(Take take, float seconds, Curve energy)
        {
            Signal air = take.Pink(seconds).Bandpass(Curve.Glide(1200f, 310f, seconds * 0.7f), 3.4f).Comb(Curve.Glide(95f, 62f, seconds), 0.74f, 0.55f);
            return air.Shape(energy);
        }

        private static Signal Sub(Take take, float seconds, float pitch)
        {
            return take.Sine(seconds, Curve.Settle(pitch * 2f, pitch, seconds * 0.25f)).Drive(2.2f).Shape(Curve.Hump(seconds * 0.2f, seconds));
        }

        // Pure glassy tones, in a scale nobody uses, arriving from nowhere in particular.
        private static Signal Motes(Take take, float seconds, float pitch)
        {
            SoundRandom random = take.Random;
            Signal motes = take.Track(seconds);
            float[] scale = { 1f, 1.122f, 1.26f, 1.498f, 1.682f, 2f, 2.245f, 2.52f };
            int count = (int)(seconds * 22f);
            for (int i = 0; i < count; i++)
            {
                float at = random.Range(0f, seconds * 0.8f);
                float f = pitch * random.Pick(scale) * (random.Chance(0.5f) ? 1f : 2f);
                Signal note = take.Sine(MathF.Min(0.32f, seconds - at), f, random.Range(0f, 6f)).Percussive(0.0015f, 0.07f);
                motes.Add(note, at, random.Range(0.2f, 0.7f));
            }
            return motes;
        }

        // --- Whole sounds ---

        private static void Healing(Take take)
        {
            SoundRandom random = take.Random;
            take.Mix(Choir(take, 1.5f, random.Pick(new[] { 349.23f, 392f }), 0.3f).Normalize(0f), 0f, -3f);
            take.Mix(Motes(take, 1.3f, 1100f).Normalize(0f), 0.05f, -8f);
            take.Mix(take.Pink(1.3f).Bandpass(Curve.Glide(600f, 4200f, 0.9f), 1.4f).Shape(Curve.Hump(0.6f, 1.3f)).Normalize(0f), 0f, -13f);
        }

        private static void Strengthen(Take take)
        {
            take.Mix(take.Sine(0.9f, Curve.Glide(120f, 280f, 0.5f)).Drive(2.4f).Shape(Curve.Hump(0.4f, 0.9f)).Normalize(0f), 0f, -6f);
            take.Mix(Motes(take, 1f, 780f).Normalize(0f), 0f, -6f);
            take.Mix(take.Pink(0.9f).Bandpass(Curve.Glide(400f, 3200f, 0.5f), 1.2f).Shape(Curve.Hump(0.5f, 0.9f)).Normalize(0f), 0f, -9f);
        }

        private static void Weaken(Take take)
        {
            take.Mix(take.Sine(1f, Curve.Glide(310f, 72f, 0.8f)).Drive(3f).Shape(Curve.Hump(0.25f, 1f)).Normalize(0f), 0f, -6f);
            take.Mix(Breath(take, 1f, Curve.Hump(0.3f, 1f)).Normalize(0f), 0f, -6f);
        }

        private static void Sick(Take take)
        {
            take.Mix(Bubbling(take, 0.5f, 0.6f).Normalize(0f), 0f, -3f);
            take.Mix(take.Sine(0.45f, Curve.Glide(190f, 92f, 0.4f)).Shape(Curve.Hump(0.1f, 0.45f)).Normalize(0f), 0f, -11f);
            take.Mix(Sizzle(take, 0.45f).Percussive(0.004f, 0.08f).Normalize(0f), 0f, -12f);
        }

        private static void Scorched(Take take)
        {
            take.Mix(Flare(take, 0.4f).Percussive(0.004f, 0.11f).Normalize(0f), 0f, -3f);
            take.Mix(Spit(take, 0.4f, 200f).Normalize(0f), 0f, -7f);
        }

        private static void Iced(Take take)
        {
            take.Mix(Crackling(take, 0.5f, Curve.Settle(500f, 30f, 0.09f), 1800f, 9000f, 0.002f).Normalize(0f), 0f, -3f);
            take.Mix(Glass(take, 0.7f, 1900f).Normalize(0f), 0.02f, -8f);
        }

        private static void Dazed(Take take)
        {
            // A ringing in the ears: a thin tone wavering, which is the one thing that can only be inside a head.
            float pitch = take.Random.LogRange(2700f, 3500f);
            Curve wavering = take.Drift(1.3f, 5f);
            Curve fade = new Curve(t => MathF.Exp(-t / 0.45f) * (0.6f + 0.4f * wavering.At(t)));
            take.MixStereo(take.Sine(1.3f, pitch).Shape(fade).Normalize(0f), take.Sine(1.3f, pitch * 1.006f).Shape(fade).Normalize(0f));
            take.Mix(take.Brown(0.4f).Lowpass(300f).Percussive(0.002f, 0.09f).Normalize(0f), 0f, -6f);
        }

        private static void GuideCalled(Take take)
        {
            SoundRandom random = take.Random;

            // A long breath of wind that seems to answer: a rising sweep and, inside it, a single soft high note.
            float seconds = 2.2f;
            Curve shape = Curve.Hump(0.8f, seconds);
            Signal left = take.Pink(seconds).Bandpass(Curve.Glide(280f, 1900f, 0.95f), 1.3f).Shape(shape).Normalize(0f);
            Signal right = take.Pink(seconds).Bandpass(Curve.Glide(340f, 2200f, 1f), 1.3f).Shape(shape).Normalize(0f);
            take.MixStereo(left, right);
            take.Mix(take.Sine(seconds, Curve.Glide(660f, 880f, 0.9f)).Shape(Curve.Hump(1f, seconds)).Normalize(0f), 0f, -16f);
        }
    }
}
