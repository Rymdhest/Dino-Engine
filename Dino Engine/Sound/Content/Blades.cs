using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>Weapons in the air and against each other.</summary>
    [SoundCollection]
    public static class Blades
    {
        public static readonly SoundDefinition Swing = Add("blade.swing", take => Stroke(take, new Aero.Blade { Length = 1.05f, Thickness = 0.0075f, Sources = 7, Turbulence = 0.25f }, 26f, 0.26f, 0.5f), stereo: true, target: -21f);
        public static readonly SoundDefinition HeavySwing = Add("blade.swing.heavy", take => Stroke(take, new Aero.Blade { Length = 1.35f, Thickness = 0.026f, Sources = 8, Turbulence = 0.65f }, 15f, 0.42f, 0.2f), stereo: true, target: -21f);
        public static readonly SoundDefinition Whirl = Add("blade.whirl", WhirlStroke, stereo: true, target: -18f, presence: 2f);
        public static readonly SoundDefinition Dash = Add("body.dash", DashRush, stereo: true, target: -21f);
        public static readonly SoundDefinition Clash = Add("blade.clash", SteelOnSteel, target: -15f, presence: 2f);
        public static readonly SoundDefinition Draw = Add("blade.draw", DrawnSteel, target: -20f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, bool stereo = false, float presence = 0f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Combat,
                Stereo = stereo,
                Takes = 8,
                PlaysPerTake = 2,
                Presence = presence,
                Mastering = Mastering.Default with { Target = target },
                Priority = 60,
                Spacing = 0.05f,
                NearDistance = 10f,
                FarDistance = 60f,
            });
        }

        private static void Stroke(Take take, Aero.Blade blade, float peakSpeed, float seconds, float ring)
        {
            SoundRandom random = take.Random;
            float length = seconds * random.Range(0.85f, 1.2f);
            float peak = length * random.Range(0.42f, 0.58f);

            // Speed through the arc: the arm accelerates, then the wrist snaps, then it's caught.
            Curve speed = Curve.Hump(peak, length, peakSpeed * random.Range(0.85f, 1.15f), 1.15f);
            float side = random.Chance(0.5f) ? 1f : -1f;
            Curve arc = Curve.Line(-0.8f * side, 0.8f * side, length);

            (Signal left, Signal right) = Aero.Swing(take, length + 0.04f, speed, blade, arc);

            // The bright edge of the air: the top of a real swish, which vortices off a thin edge reach and a broad body doesn't.
            float air = 1f - blade.Turbulence * 0.8f;
            Curve lean = Curve.Hump(peak, length, 2400f) + 700f;
            Curve swell = Curve.Hump(peak, length, 1f, 2.2f);
            Signal edgeLeft = take.White(length + 0.04f).Highpass(lean).Lowpass(9500f).Shape(swell);
            Signal edgeRight = take.White(length + 0.04f).Highpass(lean).Lowpass(9500f).Shape(swell);
            left.Normalize(0f);
            right.Normalize(0f);
            take.MixStereo(left, right);
            take.MixStereo(edgeLeft.Normalize(0f), edgeRight.Normalize(0f), 0f, -20f + 7f * air);

            if (ring > 0f)
            {
                // Steel in the hand sings faintly as it's whipped about.
                ModalObject steel = ModalObject.Draw(random, Material.Steel, ModalShape.Bar, random.LogRange(1800f, 2800f), modes: 9, tilt: -3f, ringing: 1.4f);
                Signal sing = steel.Ring(Contact.Press(take, 0.002f, 1f), length + 0.2f).Shape(Curve.Hump(peak * 1.1f, length + 0.2f, 1f, 1.5f));
                take.Mix(sing, 0f, -26f + 8f * ring);
            }

            // Sleeves and straps moving with it, a beat behind the arm.
            Signal cloth = take.Pink(length).Bandpass(Curve.Hump(peak, length, 900f) + 700f, 0.8f).Shape(Curve.Hump(peak * 0.8f, length, 1f, 1.6f));
            take.Mix(cloth, 0.008f, -20f);
        }

        private static void WhirlStroke(Take take)
        {
            SoundRandom random = take.Random;
            var blade = new Aero.Blade { Length = 1.1f, Thickness = 0.0095f, Sources = 8, Turbulence = 0.4f };
            float seconds = random.Range(0.55f, 0.68f);

            // A full turn and a bit: speed peaks twice, as the blade passes nearest and furthest.
            Curve speed = new Curve(t =>
            {
                float x = Math.Clamp(t / seconds, 0f, 1f);
                float sweep = 0.68f + 0.32f * MathF.Sin(x * MathF.Tau * 2.1f - 1.2f);
                return 30f * sweep * MathF.Sin(MathF.PI * x);
            });
            Curve arc = new Curve(t => MathF.Sin(t / seconds * MathF.Tau * 1.25f));
            (Signal left, Signal right) = Aero.Swing(take, seconds + 0.05f, speed, blade, arc);
            take.MixStereo(left, right);

            // The air it drags round with it.
            Signal body = take.Pink(seconds + 0.2f).Lowpass(Curve.Hump(seconds * 0.5f, seconds, 1100f) + 180f, 1.2f).Shape(Curve.Hump(seconds * 0.45f, seconds + 0.18f, 1f, 1.2f));
            take.Mix(body, 0f, -7f);
        }

        private static void DashRush(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.22f, 0.3f);
            Curve shape = Curve.Hump(seconds * 0.3f, seconds, 1f, 1.1f);

            // A whole body through the air: low and wide next to a blade.
            Signal left = take.Pink(seconds).Bandpass(Curve.Hump(seconds * 0.35f, seconds, 1500f) + 420f, 0.9f).Shape(shape);
            Signal right = take.Pink(seconds).Bandpass(Curve.Hump(seconds * 0.35f, seconds, 1700f) + 380f, 0.9f).Shape(shape);
            take.MixStereo(left, right);

            Signal rustle = Grains.Stream(take, seconds, Curve.Hump(seconds * 0.3f, seconds, 1400f), Curve.Hump(seconds * 0.3f, seconds), new GrainStyle { LowHz = 1600f, HighHz = 7000f, MinDecay = 0.0009f, MaxDecay = 0.005f, Partials = 1, Noise = 0.95f, Spread = 16f });
            take.Mix(rustle, 0f, -11f);
        }

        private static void SteelOnSteel(Take take)
        {
            SoundRandom random = take.Random;

            // Two blades, each with its own modes, both set ringing by the same blow. Their lowest modes hardly reach the air, a blade being
            // too narrow to push much of it, so most of what's heard starts in the kilohertz, as measured clashes do.
            var first = new Blow { Material = Material.Steel, Shape = ModalShape.Plate, Fundamental = random.LogRange(2600f, 3800f), Modes = 60, Tilt = 0.4f, Ringing = 0.8f, Rattle = 0.9f, Noise = -12f, NoiseLow = 140f, Bounces = 1 };
            var second = new Blow { Material = Material.Steel, Shape = ModalShape.Plate, Fundamental = random.LogRange(3600f, 5400f), Modes = 48, Tilt = 0.1f, Ringing = 0.7f, Rattle = 0.9f, Noise = -12f, NoiseLow = 300f, Bounces = 2 };
            take.Mix(Impact.Of(take, first, 1.15f).Normalize(0f));
            take.Mix(Impact.Of(take, second, 1.15f).Normalize(0f), random.Range(0.0005f, 0.004f), -2f);

            // The crack of the contact itself, before anything has had time to ring.
            take.Mix(Impacts.Crack(take, 0.0014f).Normalize(0f), 0f, -1f);

            // Then edge drags along edge for a moment, and the blades sing with it.
            float slide = random.Range(0.1f, 0.24f);
            ModalObject singing = ModalObject.Draw(random, Material.Steel, ModalShape.Bar, random.LogRange(2200f, 3400f), modes: 12, tilt: -1f, ringing: 0.6f);
            Signal scrape = Contact.Scrape(take, slide, Curve.Settle(0.9f, 0.2f, slide * 0.5f), Curve.Settle(1f, 0f, slide * 0.45f), 0.75f, random.LogRange(2400f, 3800f)).Highpass(2200f);
            take.Mix(singing.Ring(scrape, slide + 0.4f).Normalize(0f), 0.002f, -7f);
            take.Mix(scrape.Normalize(0f), 0.001f, -11f);

            // The hands take the shock: a low knock under it, which is what gives a clash its weight.
            take.Mix(Impacts.Thump(take, 260f, 120f, 0.02f, 0.03f, 0.14f).Normalize(0f), 0f, -13f);
        }

        private static void DrawnSteel(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.3f, 0.42f);
            ModalObject steel = ModalObject.Draw(random, Material.Steel, ModalShape.Plate, random.LogRange(1500f, 2300f), modes: 30, tilt: -0.8f, ringing: 0.7f);
            Curve speed = Curve.Hump(seconds * 0.6f, seconds, 1f);
            Signal scrape = Contact.Scrape(take, seconds, speed, 1f, 0.8f, 3400f).Highpass(1400f).Normalize(0f);
            take.Mix(scrape, 0f, -3f);
            take.Mix(steel.Ring(scrape, seconds + 0.5f).Normalize(0f), 0f, -3f);

            // Clear of the scabbard, the tip rings.
            take.Mix(Residual.Of(take, Material.Steel, 0.5f, 1200f, 18000f, -1f, 0.25f).Normalize(0f), seconds * 0.85f, -10f);
        }
    }
}
