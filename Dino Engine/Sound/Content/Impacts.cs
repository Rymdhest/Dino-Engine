using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// What a blow sounds like, by what it lands on. Each is built the way a sound designer builds one: something short and bright for the
    /// instant of contact, a body for what was hit, and something low underneath for weight, with the layers a few milliseconds
    /// apart. That offset matters more than the layers do: it's what makes a blow sound like one event and not three.
    /// </summary>
    [SoundCollection]
    public static class Impacts
    {
        public static readonly SoundDefinition Flesh = Add("hit.flesh", take => Body(take, cut: false, take.Strength), -14f);
        public static readonly SoundDefinition Slash = Add("hit.slash", take => Body(take, cut: true, take.Strength), -14f);
        public static readonly SoundDefinition Heavy = Add("hit.heavy", take => Body(take, cut: false, 1f), -12f, presence: 2f);
        public static readonly SoundDefinition Critical = Add("hit.critical", CriticalCrack, -13f, presence: 3f);
        public static readonly SoundDefinition Armour = Add("hit.armour", ArmourPlate, -14f);
        public static readonly SoundDefinition Bone = Add("hit.bone", BoneBreak, -14f);
        public static readonly SoundDefinition Wood = Add("hit.wood", take => Solid(take, Material.Wood, ModalShape.Bar, 240f, 520f, 0.0007f, 0.5f), -14f);
        public static readonly SoundDefinition Stone = Add("hit.stone", take => Solid(take, Material.Stone, ModalShape.Solid, 620f, 1500f, 0.00025f, 0.3f), -14f);
        public static readonly SoundDefinition Metal = Add("hit.metal", take => Solid(take, Material.Iron, ModalShape.Plate, 380f, 760f, 0.00025f, 0.9f), -14f);
        public static readonly SoundDefinition Glass = Add("hit.glass", take => Solid(take, Material.Glass, ModalShape.Shell, 1400f, 2600f, 0.00012f, 0.6f), -15f);
        public static readonly SoundDefinition BodyFall = Add("body.fall", Fall, -17f);
        public static readonly SoundDefinition Debris = Add("world.debris", Rubble, -18f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float presence = 0f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Combat,
                Takes = 10,
                PlaysPerTake = 2,
                PitchSpread = 0.7f,
                GainSpread = 1.4f,
                Presence = presence,
                Mastering = Mastering.Default with { Target = target },
                Priority = 65,
                Spacing = 0.035f,
                NearDistance = 11f,
                FarDistance = 65f,
            });
        }

        /// <summary>
        /// The instant of contact. Measured hits all begin with a line straight up the whole spectrum, so this is a real click, as
        /// wide as the contact is hard, and mostly it's what makes a hit feel sharp.
        /// </summary>
        public static Signal Crack(Take take, float seconds, float brightness = 16000f)
        {
            return take.White(seconds * 7f).Lowpass(brightness).Highpass(180f).Percussive(0.00004f, seconds);
        }

        /// <summary>A low thump: a damped sine falling in pitch, as a struck cavity does. It's the difference between a blow and a tap.</summary>
        public static Signal Thump(Take take, float from, float to, float fall, float decay, float seconds = 0.3f)
        {
            return take.Sine(seconds, Curve.Settle(from, to, fall), 0.2f)
                .Percussive(0.0012f, decay)
                .Add(take.Brown(seconds).Lowpass(to * 3f).Percussive(0.001f, decay * 0.7f), 0f, 1.4f);
        }

        private static void Body(Take take, bool cut, float strength)
        {
            SoundRandom random = take.Random;
            float weight = 0.6f + 0.4f * strength;

            // The chest takes the blow, and a body is a soft thing: everything above a few hundred hertz is gone in moments, and
            // the bottom lingers. That is the residual of flesh, and it does most of the work.
            Signal body = Residual.Of(take, Material.Flesh, 0.45f, 50f, 17000f, -1.2f, 1.2f).Normalize(-1f);
            take.Mix(body, 0.0008f);

            // The cavity: where a punch's weight comes from.
            float pitch = random.Range(62f, 104f);
            take.Mix(Thump(take, pitch * 2.8f, pitch, 0.014f, random.Range(0.05f, 0.09f) * weight).Normalize(0f), 0.0015f, -3f);

            // A second, higher resonance, as the ribs answer. It's what separates a blow to a body from a blow to a bag.
            float chest = random.LogRange(240f, 420f);
            take.Mix(take.Sine(0.14f, Curve.Settle(chest * 1.6f, chest, 0.01f)).Percussive(0.0008f, 0.024f), 0.002f, -9f);

            take.Mix(Crack(take, 0.0012f).Normalize(0f), 0f, -4f);

            // Skin: a broad slap, its brightness gone in a few milliseconds.
            take.Mix(take.White(0.06f).Bandpass(Curve.Glide(4800f, 900f, 0.016f), 0.6f).Percussive(0.0002f, 0.0075f).Normalize(0f), 0f, -7f);

            // Wet: tissue moving, too many small sounds at once to tell apart.
            float wetness = cut ? -6f : -13f;
            Signal wet = take.Pink(0.2f).Bandpass(Curve.Glide(1900f, 420f, 0.08f), 1.1f).Percussive(0.002f, 0.035f);
            Curve energy = Curve.Settle(1f, 0f, 0.04f);
            wet.Add(Liquid.Bubbles(take, 0.2f, energy * 3400f, energy, 0.0005f, 0.0024f, 2.1f, 0.15f).Normalize(-2f));
            take.Mix(wet.Normalize(0f), 0.004f, wetness);

            if (!cut) return;

            // A blade going in: a hard, high tearing over the top, and a thin edge of steel.
            float tear = random.Range(0.05f, 0.09f);
            Signal slice = take.White(tear).Bandpass(Curve.Glide(8200f, 2800f, tear), 0.9f).Shape(Curve.Hump(tear * 0.14f, tear, 1f, 0.8f)).Normalize(0f);
            take.Mix(slice, 0f, -6f);
            ModalObject steel = ModalObject.Draw(random, Material.Steel, ModalShape.Bar, random.LogRange(2400f, 3800f), modes: 10, tilt: -1f, ringing: 0.3f);
            take.Mix(steel.Ring(Contact.Press(take, 0.0006f, 1f), 0.3f).Normalize(0f), 0.001f, -19f);
        }

        private static void CriticalCrack(Take take)
        {
            SoundRandom random = take.Random;

            // A crit sits on top of the hit it goes with: a snap of something giving way, and the air knocked out of the world for a moment.
            take.Mix(Impact.Of(take, new Blow { Material = Material.Bone, Shape = ModalShape.Solid, Fundamental = random.LogRange(900f, 1500f), Modes = 16, Tilt = -1f, Hardness = 0.00018f, Rattle = 1f, Noise = -3f, NoiseLow = 400f, Bounces = 2 }, 0.2f).Normalize(0f));
            take.Mix(Grains.Settle(take, 0.1f, 30f, 0.02f, new GrainStyle { LowHz = 1200f, HighHz = 9000f, MinDecay = 0.0004f, MaxDecay = 0.002f, Partials = 2, Noise = 0.45f, Spread = 12f }).Normalize(0f), 0.001f, -4f);
            take.Mix(Thump(take, 330f, 58f, 0.022f, 0.13f, 0.45f).Normalize(0f), 0f, -2f);
            take.Mix(Crack(take, 0.0009f).Normalize(0f), 0f, -3f);
        }

        private static void ArmourPlate(Take take)
        {
            SoundRandom random = take.Random;
            take.Mix(Impact.Of(take, new Blow { Material = Material.Iron, Shape = ModalShape.Plate, Fundamental = random.LogRange(330f, 620f), Modes = 60, Tilt = -1.6f, Ringing = 0.7f, Rattle = 0.8f, Noise = -5f, NoiseLow = 160f, Bounces = 1 }, 0.75f).Normalize(0f));
            take.Mix(Thump(take, 250f, 88f, 0.018f, 0.045f).Normalize(0f), 0.001f, -6f);
            take.Mix(Crack(take, 0.0011f).Normalize(0f), 0f, -7f);

            // Whatever isn't riveted down rattles.
            take.Mix(Grains.Settle(take, 0.28f, 14f, 0.05f, new GrainStyle { LowHz = 1700f, HighHz = 6400f, MinDecay = 0.002f, MaxDecay = 0.012f, Partials = 3, Noise = 0f, Spread = 14f }).Normalize(0f), 0.008f, -14f);
        }

        private static void BoneBreak(Take take)
        {
            SoundRandom random = take.Random;

            // Dry and hollow: a skeleton has nothing to absorb the blow, so it's all knock and no thump.
            take.Mix(Impact.Of(take, new Blow { Material = Material.Bone, Shape = ModalShape.Bar, Fundamental = random.LogRange(520f, 980f), Modes = 16, Tilt = -1.6f, Rattle = 0.8f, Noise = -4f, NoiseLow = 200f, Bounces = 2 }, 0.22f).Normalize(0f));
            take.Mix(Crack(take, 0.001f).Normalize(0f), 0f, -6f);
            take.Mix(Grains.Settle(take, 0.3f, 14f, 0.07f, new GrainStyle { LowHz = 800f, HighHz = 4200f, MinDecay = 0.0015f, MaxDecay = 0.009f, Partials = 3, Noise = 0.15f, Spread = 10f }).Normalize(0f), 0.01f, -7f);
            take.Mix(Thump(take, 220f, 130f, 0.012f, 0.02f, 0.1f).Normalize(0f), 0f, -13f);
        }

        private static void Solid(Take take, Material material, ModalShape shape, float lowest, float highest, float hardness, float rattle)
        {
            SoundRandom random = take.Random;
            take.Mix(Impact.Of(take, new Blow { Material = material, Shape = shape, Fundamental = random.LogRange(lowest, highest), Modes = shape == ModalShape.Plate ? 56 : 24, Tilt = -1.8f, Hardness = hardness, Rattle = rattle, Noise = -5f, NoiseLow = lowest * 0.4f, Bounces = random.Int(3) }, 1.1f).Normalize(0f));
            take.Mix(Crack(take, 0.0014f).Normalize(0f), 0f, -3f);
            take.Mix(Thump(take, lowest * 0.8f, lowest * 0.4f, 0.012f, 0.03f, 0.16f).Normalize(0f), 0f, -11f);
        }

        private static void Fall(Take take)
        {
            SoundRandom random = take.Random;

            // Knees first, then the rest: a body comes down in a few stages, never at once.
            float time = 0f;
            int stages = 2 + random.Int(2);
            for (int i = 0; i < stages; i++)
            {
                float pitch = random.Range(58f, 96f);
                float level = -2f - i * random.Range(0f, 3f);
                take.Mix(Thump(take, pitch * 1.9f, pitch, 0.024f, random.Range(0.05f, 0.1f)).Normalize(0f), time, level);
                take.Mix(Residual.Of(take, Material.Earth, 0.22f, 80f, 12000f, -2.5f).Normalize(0f), time, level - 5f);
                time += random.Range(0.07f, 0.17f);
            }

            // And what's worn settles after it.
            float rustle = time + 0.2f;
            Curve shape = Curve.Hump(0.05f, rustle, 1f);
            take.Mix(Grains.Stream(take, rustle, shape * 700f, shape, new GrainStyle { LowHz = 1200f, HighHz = 6000f, MinDecay = 0.001f, MaxDecay = 0.005f, Partials = 1, Noise = 0.9f, Spread = 15f }).Normalize(0f), 0.01f, -13f);
        }

        private static void Rubble(Take take)
        {
            take.Mix(Grains.Settle(take, 1.1f, 70f, 0.26f, new GrainStyle { LowHz = 520f, HighHz = 4600f, MinDecay = 0.003f, MaxDecay = 0.02f, Partials = 3, Noise = 0.2f, Spread = 22f }).Normalize(0f));
            take.Mix(take.Brown(0.9f).Lowpass(340f).Percussive(0.01f, 0.22f).Normalize(0f), 0f, -6f);
        }
    }
}
