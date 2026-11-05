using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>Things sent through the air: the bow that looses them, their flight, and where they end.</summary>
    [SoundCollection]
    public static class Archery
    {
        public static readonly SoundDefinition Draw = Add("bow.draw", DrawBow, -24f);
        public static readonly SoundDefinition Loose = Add("bow.loose", LooseArrow, -18f);
        public static readonly SoundDefinition Flight = Add("arrow.flight", Whizz, -19f, doppler: true);
        public static readonly SoundDefinition InFlesh = Add("arrow.flesh", take => Sticks(take, Material.Flesh, 110f, 0.25f), -16f);
        public static readonly SoundDefinition InWood = Add("arrow.wood", take => Sticks(take, Material.Wood, 340f, 1f), -16f);
        public static readonly SoundDefinition InGround = Add("arrow.ground", take => Sticks(take, Material.Earth, 150f, 0.7f), -19f);
        public static readonly SoundDefinition OnStone = Add("arrow.stone", take => Glance(take), -17f);
        public static readonly SoundDefinition OnWater = Add("arrow.water", Plop, -20f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, bool doppler = false)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Combat,
                Doppler = doppler,
                Takes = 8,
                Spacing = 0.04f,
                PitchSpread = 0.8f,
                Priority = 55,
                NearDistance = 10f,
                FarDistance = 55f,
                Mastering = Mastering.Default with { Target = target },
            });
        }

        private static void DrawBow(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.32f, 0.5f);

            // Wood bending and a string taking up: creaks, close together, slowing as it fills.
            take.Mix(Treasure.Hinge(take, seconds, random.LogRange(70f, 120f)).Normalize(0f), 0f, -5f);
            Curve shape = Curve.Hump(seconds * 0.7f, seconds);
            take.Mix(Grains.Stream(take, seconds, shape * 500f, shape, new GrainStyle { LowHz = 1300f, HighHz = 5000f, MinDecay = 0.001f, MaxDecay = 0.005f, Partials = 1, Noise = 0.95f, Spread = 13f }).Normalize(0f), 0f, -14f);
        }

        private static void LooseArrow(Take take)
        {
            SoundRandom random = take.Random;

            // The string snaps back: a low plucked note, gone at once, as the limbs take the energy back.
            float string1 = random.Range(120f, 200f);
            take.Mix(take.Sine(0.2f, Curve.Settle(string1 * 2.4f, string1, 0.012f)).Percussive(0.0003f, 0.038f).Add(take.Sine(0.2f, Curve.Settle(string1 * 4.8f, string1 * 2f, 0.01f)).Percussive(0.0003f, 0.02f), 0f, 0.45f).Normalize(0f));

            // The slap on the bracer.
            take.Mix(Impacts.Crack(take, 0.0012f, 9000f).Normalize(0f), 0f, -8f);
            take.Mix(take.White(0.05f).Bandpass(1500f, 0.9f).Percussive(0.0003f, 0.009f).Normalize(0f), 0.0008f, -9f);

            // And the shaft leaving.
            Signal away = take.Pink(0.14f).Bandpass(Curve.Glide(1900f, 3800f, 0.09f), 1.2f).Shape(Curve.Hump(0.02f, 0.14f)).Normalize(0f);
            take.Mix(away, 0.004f, -11f);
        }

        private static void Whizz(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.32f, 0.5f);

            // A thin shaft and its fletching in a fast stream of air: a narrow Aeolian note in a rush, swelling as it passes.
            float pitch = Aero.ToneFrequency(random.Range(38f, 52f), 0.008f);
            Curve pass = Curve.Hump(seconds * 0.5f, seconds, 1f, 1.5f);
            Curve bend = Curve.Line(pitch * 1.18f, pitch * 0.85f, seconds);
            Signal tone = take.Pink(seconds).Bandpass(bend, 7f).Shape(pass).Normalize(0f);
            Signal rush = take.White(seconds).Bandpass(Curve.Line(4600f, 2800f, seconds), 1.1f).Shape(pass).Normalize(0f);
            take.Mix(tone, 0f, -3f);
            take.Mix(rush, 0f, -9f);
        }

        private static void Sticks(Take take, Material material, float pitch, float quiver)
        {
            SoundRandom random = take.Random;

            // It goes in with a short hard knock.
            var blow = new Blow { Material = material, Shape = ModalShape.Bar, Fundamental = pitch * random.Range(0.85f, 1.2f), Modes = 14, Tilt = -2.5f, Hardness = 0.0005f, Rattle = 0.5f, Noise = -3f, NoiseLow = 90f };
            take.Mix(Impact.Of(take, blow, 0.26f).Normalize(0f));
            take.Mix(Impacts.Crack(take, 0.001f, 12000f).Normalize(0f), 0f, -7f);

            if (quiver <= 0f) return;

            // Then the shaft shivers from the blow: the classic twang, a note that beats itself out against whatever holds it.
            float shake = random.Range(58f, 92f);
            Signal shaft = take.Sine(0.5f, random.LogRange(310f, 520f)).Add(take.Sine(0.5f, random.LogRange(860f, 1300f)), 0f, 0.3f);
            shaft.Shape(new Curve(t => MathF.Abs(MathF.Sin(MathF.Tau * shake * t * 0.5f))));
            shaft.Percussive(0.002f, 0.08f).Normalize(0f);
            take.Mix(shaft, 0.004f, -14f + 5f * quiver);
        }

        private static void Glance(Take take)
        {
            SoundRandom random = take.Random;
            var blow = new Blow { Material = Material.Stone, Shape = ModalShape.Solid, Fundamental = random.LogRange(1100f, 2100f), Modes = 16, Tilt = -1.2f, Rattle = 1f, Noise = -4f, NoiseLow = 400f, Bounces = 2 };
            take.Mix(Impact.Of(take, blow, 0.12f).Normalize(0f));
            var head = new Blow { Material = Material.Steel, Shape = ModalShape.Bar, Fundamental = random.LogRange(2400f, 3800f), Modes = 12, Tilt = -1f, Ringing = 0.25f, Rattle = 0.8f, Noise = -8f, NoiseLow = 1500f };
            take.Mix(Impact.Of(take, head, 0.3f).Normalize(0f), 0.0005f, -5f);

            // And it skitters away.
            take.Mix(Grains.Settle(take, 0.32f, 8f, 0.08f, new GrainStyle { LowHz = 900f, HighHz = 3400f, MinDecay = 0.002f, MaxDecay = 0.009f, Partials = 2, Noise = 0.2f, Spread = 8f }).Normalize(0f), 0.05f, -12f);
        }

        private static void Plop(Take take)
        {
            SoundRandom random = take.Random;

            // Slipping into water: a small crown, and one good bubble behind it.
            Curve energy = Curve.Settle(1f, 0f, 0.05f);
            take.Mix(Liquid.Bubbles(take, 0.26f, energy * 1200f, energy, 0.0006f, 0.0042f, 2.2f, 0.2f).Normalize(0f), 0.002f, -6f);
            Signal big = take.Track(0.3f);
            Liquid.Bubble(big, 0.008f, random.Range(0.0042f, 0.0088f), 1f, 0.7f);
            take.Mix(big.Normalize(0f), 0f, -3f);
            take.Mix(take.White(0.03f).Highpass(1400f).Percussive(0.0001f, 0.004f).Normalize(0f), 0f, -10f);
        }
    }
}
