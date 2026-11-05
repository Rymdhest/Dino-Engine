using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// The sounds of getting something. These are the reward a player works for, so they're allowed to be nicer than life: the coins are real, the
    /// level-up is musical, and the tones are struck metal with real decay rather than a synthesizer's, which keeps them in the world.
    /// </summary>
    [SoundCollection]
    public static class Treasure
    {
        /// <summary>Seconds into <see cref="DropLegendary"/> at which it breaks. A legendary takes exactly this long to come down, so that's the moment it lands.</summary>
        public const float LegendaryStrike = 0.95f;

        public static readonly SoundDefinition Coin = Add("loot.coin", take => Coins(take, 1 + take.Random.Int(2)), -21f, positional: true);
        public static readonly SoundDefinition Handful = Add("loot.coins", take => Coins(take, 3 + take.Random.Int(3)), -19f, positional: true);
        public static readonly SoundDefinition Hoard = Add("loot.hoard", take => Coins(take, 14 + take.Random.Int(10)), -17f, positional: true);
        public static readonly SoundDefinition ItemCommon = Add("loot.item", take => Item(take, 0), -20f);
        public static readonly SoundDefinition ItemMagic = Add("loot.item.magic", take => Item(take, 1), -19f);
        public static readonly SoundDefinition ItemRare = Add("loot.item.rare", take => Item(take, 2), -18f);
        public static readonly SoundDefinition ItemLegendary = Add("loot.item.legendary", take => Item(take, 3), -17f, stereo: true);
        public static readonly SoundDefinition LandMetal = Add("loot.land.metal", LandedMetal, -24f, positional: true);
        public static readonly SoundDefinition LandJewel = Add("loot.land.jewel", LandedJewel, -25f, positional: true);
        public static readonly SoundDefinition LandCloth = Add("loot.land.cloth", LandedCloth, -25f, positional: true);
        public static readonly SoundDefinition DropRare = Drop("loot.drop.rare", RareFind, -17f, 3f, positional: true);
        public static readonly SoundDefinition DropMasterwork = Drop("loot.drop.masterwork", MasterworkFind, -14f, 6f);
        public static readonly SoundDefinition DropLegendary = Drop("loot.drop.legendary", LegendaryFind, -11f, 9f);
        public static readonly SoundDefinition ChestOpen = Add("loot.chest", Chest, -18f, positional: true);
        public static readonly SoundDefinition Flask = Add("loot.flask", DrinkFlask, -18f, positional: true);
        public static readonly SoundDefinition Experience = Add("reward.experience", Glimmer, -24f, stereo: true);
        public static readonly SoundDefinition LevelUp = Add("reward.level", Fanfare, -14f, stereo: true, presence: 4f);
        public static readonly SoundDefinition Objective = Add("reward.objective", Accomplished, -16f, stereo: true, presence: 2f);
        public static readonly SoundDefinition Discovery = Add("reward.discovery", Discovered, -18f, stereo: true, presence: 1f);
        public static readonly SoundDefinition Liberation = Add("reward.liberation", Freed, -13f, stereo: true, presence: 6f);
        public static readonly SoundDefinition Shrine = Add("reward.shrine", Kindled, -15f, stereo: true, presence: 2f);
        public static readonly SoundDefinition Rest = Add("reward.rest", Rested, -19f, stereo: true);
        public static readonly SoundDefinition Waygate = Add("world.waygate", ThroughTheGate, -14f, stereo: true, presence: 3f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, bool positional = false, bool stereo = false, float presence = 0f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Foley,
                Stereo = stereo,
                Positional = positional,

                // Long and rare: two will do, as each is replaced as soon as it's been heard.
                Takes = stereo ? 2 : 6,
                PlaysPerTake = stereo ? 1 : 3,
                Presence = presence,
                PitchSpread = 0.2f,
                Spacing = 0.04f,
                Priority = 70,
                Mastering = Mastering.Default with { Target = target, Stage = -11f },
                NearDistance = 12f,
                FarDistance = 45f,
            });
        }

        // What tells a player something good has dropped. These have to be learned like a name: the same notes every time, heard from across a fight and over everything else, so that
        // the sound alone says what's there. So they don't wander in pitch as everything else does, and each take only differs in how it rings.
        private static SoundDefinition Drop(string id, SoundRecipe recipe, float target, float presence, bool positional = false)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Foley,
                Stereo = !positional,
                Positional = positional,
                Takes = 2,
                PlaysPerTake = 1,
                Presence = presence,
                PitchSpread = 0f,
                GainSpread = 0f,
                Spacing = 0.06f,
                Priority = 90,
                MostAtOnce = 3,
                Mastering = Mastering.Default with { Target = target, Stage = -11f },
                NearDistance = 45f,
                FarDistance = 130f,
            });
        }

        // A bronze chime: a struck bar with its partials tuned to a note, so that it's music, and it still decays as metal does.
        private static Signal Chime(Take take, float pitch, float seconds, float brightness = 1f)
        {
            SoundRandom random = take.Random;
            Signal bell = take.Track(seconds);
            float[] partials = { 1f, 2f, 3f, 4.2f, 5.4f, 6.8f, 8.9f };
            float[] levels = { 1f, 0.5f, 0.32f, 0.2f, 0.12f, 0.08f, 0.05f };
            for (int p = 0; p < partials.Length; p++)
            {
                float f = pitch * partials[p] * (1f + random.Gaussian() * 0.0012f);
                if (f > 18000f) break;
                float level = levels[p] * MathF.Pow(brightness, p * 0.5f);
                float decay = Material.Bronze.DecayAt(f) * 2.1f;
                Signal partial = take.Sine(seconds, f, random.Range(0f, 6f)).Percussive(0.0012f, 1f / decay);

                // A slightly detuned twin beating against it is the life in a struck note.
                partial.Add(take.Sine(seconds, f * (1f + random.Range(0.0004f, 0.0015f)), random.Range(0f, 6f)).Percussive(0.0012f, 1f / decay), 0f, 0.5f);
                bell.Add(partial, 0f, level);
            }
            bell.Add(take.White(0.012f).Highpass(pitch * 2f).Percussive(0.00005f, 0.0015f), 0f, 0.25f * brightness);
            return bell;
        }

        private static Signal Shimmer(Take take, float seconds, float low, float high)
        {
            return Grains.Stream(take, seconds, Curve.Hump(seconds * 0.3f, seconds, 180f), Curve.Hump(seconds * 0.3f, seconds), new GrainStyle { LowHz = low, HighHz = high, MinDecay = 0.015f, MaxDecay = 0.08f, Partials = 2, Noise = 0f, Spread = 12f });
        }

        private static void Coins(Take take, int count)
        {
            SoundRandom random = take.Random;

            // Each is a small disc: its modes high and close, ringing a little while. They come down in a spill, each landing on the others.
            float time = 0f;
            for (int i = 0; i < count; i++)
            {
                var blow = new Blow { Material = Material.Gold, Shape = ModalShape.Shell, Fundamental = random.LogRange(5200f, 9000f), Modes = 18, Tilt = 0.5f, Ringing = random.Range(0.35f, 0.6f), Rattle = 0.9f, Noise = -7f, NoiseLow = 2400f, Bounces = 1 + random.Int(3) };
                take.Mix(Impact.Of(take, blow, 0.32f).Normalize(0f), time, -random.Range(0f, 7f));
                time += random.Exponential(count > 6 ? 0.014f : 0.04f);
            }
        }

        private static void Item(Take take, int rarity)
        {
            SoundRandom random = take.Random;

            // Picked up: leather and a soft knock, as it goes in the pack.
            Curve rustle = Curve.Hump(0.04f, 0.2f);
            take.Mix(Grains.Stream(take, 0.2f, rustle * 900f, rustle, new GrainStyle { LowHz = 1200f, HighHz = 6500f, MinDecay = 0.001f, MaxDecay = 0.006f, Partials = 1, Noise = 0.9f, Spread = 14f }).Normalize(0f), 0f, -6f);
            take.Mix(Impacts.Thump(take, 300f, 130f, 0.012f, 0.03f, 0.14f).Normalize(0f), 0.05f, -10f);

            if (rarity == 0) return;

            // Worth having: a note, and the better the find, the more of a chord.
            float root = random.Pick(new[] { 523.25f, 587.33f, 659.25f });
            float[] intervals = rarity switch { 1 => new[] { 1f }, 2 => new[] { 1f, 1.5f }, _ => new[] { 1f, 1.25f, 1.5f, 2f, 3f } };
            for (int i = 0; i < intervals.Length; i++)
            {
                take.Mix(Chime(take, root * intervals[i] * 2f, 1.4f + rarity * 0.5f, 0.75f + 0.1f * rarity).Normalize(0f), 0.03f + i * 0.07f, -6f - i * 1.5f, (i - 1) * 0.35f);
            }
            if (rarity >= 3) take.Mix(Shimmer(take, 1.6f, 3000f, 11000f).Normalize(0f), 0.1f, -14f);
        }

        // What comes down says what it is: blades and helms ring, rings skip and spin, and a coat just lands.
        private static void LandedMetal(Take take)
        {
            SoundRandom random = take.Random;
            var blow = new Blow { Material = Material.Iron, Shape = ModalShape.Plate, Fundamental = random.LogRange(620f, 1050f), Modes = 18, Tilt = -0.6f, Rattle = 0.7f, Noise = -7f, NoiseLow = 500f, Bounces = 2 };
            take.Mix(Impact.Of(take, blow, 0.32f).Normalize(0f));
            take.Mix(Impacts.Thump(take, 190f, 90f, 0.012f, 0.03f, 0.12f).Normalize(0f), 0f, -8f);
        }

        private static void LandedJewel(Take take)
        {
            SoundRandom random = take.Random;
            var blow = new Blow { Material = Material.Gold, Shape = ModalShape.Shell, Fundamental = random.LogRange(3600f, 5600f), Modes = 16, Tilt = 0.3f, Ringing = random.Range(0.5f, 0.8f), Rattle = 0.9f, Noise = -8f, NoiseLow = 2200f, Bounces = 3 };
            take.Mix(Impact.Of(take, blow, 0.42f).Normalize(0f));
        }

        private static void LandedCloth(Take take)
        {
            SoundRandom random = take.Random;
            var blow = new Blow { Material = Material.Leather, Shape = ModalShape.Plate, Fundamental = random.LogRange(170f, 260f), Modes = 12, Tilt = -2f, Hardness = 0.002f, Noise = -5f, NoiseLow = 200f };
            take.Mix(Impact.Of(take, blow, 0.18f).Normalize(0f));
            Curve flop = Curve.Hump(0.015f, 0.11f);
            take.Mix(Grains.Stream(take, 0.11f, flop * 700f, flop, new GrainStyle { LowHz = 900f, HighHz = 4200f, MinDecay = 0.001f, MaxDecay = 0.005f, Partials = 1, Noise = 0.9f, Spread = 12f }).Normalize(0f), 0.004f, -9f);
        }

        // A clear rising fifth, high up, where nothing else in a fight is.
        private static void RareFind(Take take)
        {
            take.Mix(Chime(take, 1318.5f, 1.3f, 1f).Normalize(0f), 0f, -2f);
            take.Mix(Chime(take, 1975.5f, 1.6f, 1f).Normalize(0f), 0.085f, -1f);
            take.Mix(Shimmer(take, 0.7f, 5000f, 12000f).Normalize(0f), 0.09f, -20f);
        }

        // A hammer on steel, and the note it leaves ringing climbs: sharp at the front, to cut through anything, then all bright metal.
        private static void MasterworkFind(Take take)
        {
            var strike = new Blow { Material = Material.Steel, Shape = ModalShape.Bar, Fundamental = 2093f, Modes = 16, Tilt = 0.5f, Ringing = 0.7f, Rattle = 0.15f, Noise = -11f, NoiseLow = 1800f };
            take.Mix(Impact.Of(take, strike, 0.8f).Normalize(0f), 0f, -2f);

            float[] notes = { 1046.5f, 1568f, 2093f, 2637f };
            for (int i = 0; i < notes.Length; i++)
            {
                take.Mix(Chime(take, notes[i], 2.1f, 1f).Normalize(0f), 0.02f + i * 0.058f, -3f - i * 0.5f, (i - 1.5f) * 0.3f);
            }
            take.Mix(Shimmer(take, 1.5f, 4200f, 13000f).Normalize(0f), 0.12f, -14f);
        }

        // Its flight is the breath in: everything drawn upwards for as long as it's in the air, then let go all at once as it lands, in the deepest bell there is and the whole chord over it.
        private static void LegendaryFind(Take take)
        {
            float rise = LegendaryStrike + 0.06f;
            take.Mix(take.Pink(rise).Bandpass(Curve.Glide(240f, 5600f, LegendaryStrike), 1.5f).Shape(Curve.Hump(LegendaryStrike * 0.97f, rise)).Normalize(0f), 0f, -11f);
            take.Mix(take.Sine(rise, Curve.Glide(62f, 148f, LegendaryStrike)).Shape(Curve.Hump(LegendaryStrike * 0.95f, rise)).Normalize(0f), 0f, -15f);
            take.Mix(Shimmer(take, LegendaryStrike, 1700f, 9000f).Normalize(0f), 0.12f, -13f);

            take.Mix(Impacts.Thump(take, 170f, 44f, 0.03f, 0.24f, 1f).Normalize(0f), LegendaryStrike, -1f);
            take.Mix(Bells.Toll(take, 146.83f, 5f).Normalize(0f), LegendaryStrike, -3f);

            float[] chord = { 293.66f, 440f, 587.33f, 739.99f, 880f, 1174.66f, 1760f };
            for (int i = 0; i < chord.Length; i++)
            {
                take.Mix(Chime(take, chord[i], 3.6f, 0.95f).Normalize(0f), LegendaryStrike + i * 0.034f, -4f - i * 0.5f, MathF.Sin(i * 1.9f) * 0.55f);
            }
            take.Mix(Shimmer(take, 3.4f, 2600f, 12500f).Normalize(0f), LegendaryStrike + 0.05f, -13f);
            take.Mix(take.Pink(3f).Bandpass(Curve.Glide(800f, 7000f, 2f), 1f).Shape(Curve.Hump(0.4f, 3f)).Normalize(0f), LegendaryStrike, -19f);
        }

        private static void Chest(Take take)
        {
            SoundRandom random = take.Random;

            // The latch.
            var latch = new Blow { Material = Material.Iron, Shape = ModalShape.Bar, Fundamental = random.LogRange(1200f, 2000f), Modes = 14, Tilt = -1f, Rattle = 1f, Noise = -6f, NoiseLow = 500f };
            take.Mix(Impact.Of(take, latch, 0.15f).Normalize(0f), 0f, -3f);

            // The hinge: stick-slip, the rapid catching and letting go that makes a creak, which is a pulse train at a wandering pitch.
            float creak = random.Range(0.45f, 0.7f);
            Signal hinge = Hinge(take, creak, random.LogRange(90f, 170f));
            take.Mix(hinge.Normalize(0f), 0.09f, -6f);

            // The lid landing back, and the hollow of the box.
            var lid = new Blow { Material = Material.Plank, Shape = ModalShape.Plate, Fundamental = random.LogRange(130f, 220f), Modes = 18, Tilt = -2.5f, Hardness = 0.001f, Rattle = 0.3f, Noise = -4f, NoiseLow = 90f, Bounces = 2 };
            take.Mix(Impact.Of(take, lid, 0.42f).Normalize(0f), 0.09f + creak, -2f);
        }

        /// <summary>Friction letting go again and again: wood or iron under load, a door, a chest, a rope.</summary>
        public static Signal Hinge(Take take, float seconds, float pitch)
        {
            SoundRandom random = take.Random;
            Signal pulses = take.Track(seconds);
            Curve drift = take.Drift(seconds, 7f);
            float time = 0f;
            while (time < seconds)
            {
                int index = (int)(time * AudioFormat.SampleRate);
                if (index >= pulses.Length) break;
                float along = time / seconds;
                float level = MathF.Sin(MathF.PI * along) * (0.5f + random.Float());
                pulses.Samples[index] = level * (random.Chance(0.5f) ? 1f : -1f);

                // Irregular: it sticks a bit longer, then lets go a bit sooner.
                float rate = pitch * (0.7f + drift.At(time) * 0.9f);
                time += (1f / rate) * random.Range(0.8f, 1.25f);
            }

            // What rattles is the wood or iron, so that's what colours it.
            Signal tone = take.Track(seconds);
            tone.Add(pulses);
            tone.Bandpass(780f, 2.2f);
            Signal bright = take.Track(seconds);
            bright.Add(pulses);
            bright.Bandpass(2100f, 3f);
            tone.Add(bright, 0f, 0.8f);
            return tone;
        }

        private static void DrinkFlask(Take take)
        {
            SoundRandom random = take.Random;

            // The cork, then swallows, each a slug of liquid moving and a bubble of air going the other way.
            take.Mix(take.White(0.02f).Bandpass(1500f, 1.2f).Percussive(0.0002f, 0.004f).Normalize(0f), 0f, -6f);
            take.Mix(take.Sine(0.05f, Curve.Glide(420f, 900f, 0.025f)).Percussive(0.001f, 0.012f).Normalize(0f), 0.002f, -8f);

            float time = 0.12f;
            int gulps = 2 + random.Int(2);
            for (int i = 0; i < gulps; i++)
            {
                float length = random.Range(0.13f, 0.2f);
                Curve energy = Curve.Hump(length * 0.3f, length);
                Signal gulp = Liquid.Bubbles(take, length + 0.05f, energy * 420f, energy, 0.0022f, 0.0075f, 2f, 0.25f).Normalize(0f);
                take.Mix(gulp, time, -3f);
                take.Mix(take.Pink(length).Bandpass(Curve.Glide(620f, 280f, length), 2f).Shape(energy).Normalize(0f), time, -9f);
                time += length + random.Range(0.04f, 0.09f);
            }

            // And a breath out, for having needed it.
            take.Mix(take.Pink(0.2f).Bandpass(Curve.Glide(1400f, 700f, 0.18f), 1.1f).Shape(Curve.Hump(0.05f, 0.2f)).Normalize(0f), time, -13f);
        }

        private static void Glimmer(Take take)
        {
            SoundRandom random = take.Random;
            float root = random.Pick(new[] { 1046.5f, 1174.7f, 1318.5f, 1568f });
            take.Mix(Chime(take, root, 0.6f, 0.7f).Normalize(0f), 0f, -2f, random.Range(-0.3f, 0.3f));
            take.Mix(Chime(take, root * 1.5f, 0.5f, 0.6f).Normalize(0f), 0.05f, -7f, random.Range(-0.5f, 0.5f));
        }

        private static void Fanfare(Take take)
        {
            SoundRandom random = take.Random;

            // Up a major triad and on to the octave, bell by bell, then all of them ringing together under a rising shimmer.
            float root = random.Pick(new[] { 261.63f, 293.66f, 329.63f });
            float[] steps = { 1f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f };
            for (int i = 0; i < steps.Length; i++)
            {
                float pan = MathF.Sin(i * 1.3f) * 0.5f;
                take.Mix(Chime(take, root * steps[i], 2.6f, 0.9f).Normalize(0f), i * 0.075f, -4f - i * 0.4f, pan);
            }
            take.Mix(Chime(take, root * 0.5f, 3f, 0.7f).Normalize(0f), 0.5f, -5f);

            // A breath of air rising with it, and a great soft low underneath for weight.
            Signal swell = take.Pink(1.8f).Bandpass(Curve.Glide(500f, 6000f, 1.1f), 1.1f).Shape(Curve.Hump(0.75f, 1.8f)).Normalize(0f);
            take.Mix(swell, 0f, -16f);
            take.Mix(Shimmer(take, 2.2f, 2800f, 12000f).Normalize(0f), 0.38f, -14f);
            take.Mix(take.Sine(1.6f, root * 0.5f).Shape(Curve.Hump(0.5f, 1.6f)).Normalize(0f), 0f, -18f);
        }

        private static void Accomplished(Take take)
        {
            SoundRandom random = take.Random;
            float root = random.Pick(new[] { 392f, 440f, 493.88f });
            take.Mix(Chime(take, root, 2f, 0.85f).Normalize(0f), 0f, -3f, -0.25f);
            take.Mix(Chime(take, root * 1.5f, 2f, 0.85f).Normalize(0f), 0.11f, -4f, 0.25f);
            take.Mix(Chime(take, root * 2f, 2.4f, 0.8f).Normalize(0f), 0.22f, -4f);
            take.Mix(Shimmer(take, 1.2f, 3200f, 10000f).Normalize(0f), 0.2f, -16f);
        }

        private static void Discovered(Take take)
        {
            SoundRandom random = take.Random;
            float root = random.Pick(new[] { 220f, 246.94f, 261.63f });

            // A place found: open fifths, without a third, so it's neither glad nor sad yet.
            take.Mix(Chime(take, root, 3f, 0.6f).Normalize(0f), 0f, -3f, -0.3f);
            take.Mix(Chime(take, root * 1.5f, 3f, 0.6f).Normalize(0f), 0.09f, -5f, 0.3f);
            take.Mix(take.Pink(2.4f).Bandpass(Curve.Glide(300f, 1800f, 1.4f), 1.8f).Shape(Curve.Hump(1f, 2.4f)).Normalize(0f), 0f, -20f);
        }

        private static void Freed(Take take)
        {
            SoundRandom random = take.Random;

            // A land set free: a low bell and the whole chord, and the air opening up over it.
            float root = 130.81f;
            float[] chord = { 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f };
            for (int i = 0; i < chord.Length; i++)
            {
                take.Mix(Chime(take, root * chord[i], 5f, 0.85f).Normalize(0f), i * 0.11f, -3f - i * 0.5f, MathF.Sin(i * 2.1f) * 0.6f);
            }
            take.Mix(Bells.Toll(take, root * 0.5f, 6f).Normalize(0f), 0f, -3f);
            Signal air = take.Pink(5f).Bandpass(Curve.Glide(300f, 7000f, 3f), 0.9f).Shape(Curve.Hump(2.5f, 5f)).Normalize(0f);
            take.Mix(air, 0f, -15f);
            take.Mix(Shimmer(take, 4.4f, 2400f, 12000f).Normalize(0f), 0.6f, -14f);
        }

        private static void Kindled(Take take)
        {
            SoundRandom random = take.Random;
            float root = random.Pick(new[] { 174.61f, 196f });

            // Fire catching, then a low bell, and the chord of the place waking.
            Curve catching = Curve.Hump(0.2f, 1.1f);
            take.Mix(take.Pink(1.1f).Bandpass(Curve.Glide(240f, 2600f, 0.5f), 0.8f).Shape(catching).Normalize(0f), 0f, -7f);
            take.Mix(Grains.Stream(take, 1.1f, catching * 110f, catching, new GrainStyle { LowHz = 1500f, HighHz = 9000f, MinDecay = 0.0003f, MaxDecay = 0.002f, Partials = 1, Noise = 0.9f, Spread = 22f }).Normalize(0f), 0.05f, -10f);
            take.Mix(Bells.Toll(take, root, 4.5f).Normalize(0f), 0.28f, -3f);
            take.Mix(Chime(take, root * 2f, 3.6f, 0.8f).Normalize(0f), 0.42f, -6f, -0.4f);
            take.Mix(Chime(take, root * 3f, 3.6f, 0.8f).Normalize(0f), 0.56f, -7f, 0.4f);
            take.Mix(Shimmer(take, 2.6f, 2400f, 9000f).Normalize(0f), 0.4f, -16f);
        }

        private static void Rested(Take take)
        {
            float root = take.Random.Pick(new[] { 349.23f, 392f });
            take.Mix(Chime(take, root, 2.4f, 0.65f).Normalize(0f), 0f, -3f, -0.2f);
            take.Mix(Chime(take, root * 1.5f, 2.4f, 0.6f).Normalize(0f), 0.14f, -6f, 0.2f);
            take.Mix(take.Pink(1.4f).Bandpass(Curve.Glide(1800f, 500f, 1.2f), 1.1f).Shape(Curve.Hump(0.3f, 1.4f)).Normalize(0f), 0f, -17f);
        }

        private static void ThroughTheGate(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = 2.6f;

            // Air drawn in to something that isn't there, and then the world going past: a rising, turning rush with a comb moving through it.
            Curve pull = Curve.Hump(1.3f, seconds);
            Signal left = take.Pink(seconds).Comb(Curve.Glide(70f, 900f, 1.5f), 0.82f, 0.7f).Bandpass(Curve.Glide(200f, 3200f, 1.4f), 0.8f).Shape(pull).Normalize(0f);
            Signal right = take.Pink(seconds).Comb(Curve.Glide(80f, 1000f, 1.5f), 0.82f, 0.7f).Bandpass(Curve.Glide(220f, 3400f, 1.4f), 0.8f).Shape(pull).Normalize(0f);
            take.MixStereo(left, right, 0f, -3f);
            take.Mix(take.Sine(seconds, Curve.Glide(48f, 112f, 1.4f)).Shape(pull).Drive(2.4f).Normalize(0f), 0f, -7f);
            take.Mix(Bells.Toll(take, 98f, 3.2f).Normalize(0f), 1.35f, -5f);
            take.Mix(Shimmer(take, 1.8f, 3000f, 12000f).Normalize(0f), 0.9f, -12f);
        }
    }

    /// <summary>Big bells: the church's, the shrine's, a world's. A real bell's partials are the hum, prime, tierce, quint and nominal, tuned by centuries of founders, not whole multiples.</summary>
    public static class Bells
    {
        private static readonly float[] Partials = { 0.5f, 1f, 1.2f, 1.5f, 2f, 2.5f, 2.67f, 3f, 4f, 5.33f };
        private static readonly float[] Levels = { 0.6f, 1f, 0.85f, 0.5f, 0.75f, 0.4f, 0.3f, 0.3f, 0.2f, 0.12f };

        public static Signal Toll(Take take, float pitch, float seconds)
        {
            SoundRandom random = take.Random;
            Signal bell = take.Track(seconds);
            for (int p = 0; p < Partials.Length; p++)
            {
                float f = pitch * Partials[p] * (1f + random.Gaussian() * 0.0015f);
                if (f > 16000f) continue;
                float decay = Material.Bronze.DecayAt(f) * 0.8f;
                Signal partial = take.Sine(seconds, f, random.Range(0f, 6f)).Percussive(0.002f, 1f / decay);

                // Every partial of a cast bell is a beating pair, as no casting is perfectly round.
                partial.Add(take.Sine(seconds, f * (1f + random.Range(0.0006f, 0.0022f)), random.Range(0f, 6f)).Percussive(0.002f, 1f / decay), 0f, 0.7f);
                bell.Add(partial, 0f, Levels[p]);
            }

            // The clapper: a dull knock at the very start, gone before the bell has opened up.
            bell.Add(take.White(0.03f).Lowpass(4200f).Percussive(0.0002f, 0.004f), 0f, 0.5f);
            return bell;
        }
    }
}
