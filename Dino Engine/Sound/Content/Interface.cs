using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// The sound of the interface. A game's menus usually sound like a computer; these are small real things, wood and leather and paper, as the
    /// world has no computers in it. They're short and quiet, and mustn't tire an ear that hears them hundreds of times an hour.
    /// </summary>
    [SoundCollection]
    public static class Interface
    {
        public static readonly SoundDefinition Hover = Add("ui.hover", take => Tap(take, 2600f, 0.0009f, 0.3f), -35f, spacing: 0.05f);
        public static readonly SoundDefinition Click = Add("ui.click", take => Tap(take, 1500f, 0.0016f, 1f), -24f);
        public static readonly SoundDefinition Toggle = Add("ui.toggle", Latch, -25f);
        public static readonly SoundDefinition Tick = Add("ui.tick", take => Tap(take, 3200f, 0.0007f, 0.5f), -31f, spacing: 0.02f);
        public static readonly SoundDefinition Open = Add("ui.open", take => Page(take, rising: true), -24f);
        public static readonly SoundDefinition Close = Add("ui.close", take => Page(take, rising: false), -25f);
        public static readonly SoundDefinition Confirm = Add("ui.confirm", Agreed, -22f);
        public static readonly SoundDefinition Refuse = Add("ui.refuse", Refused, -22f);
        public static readonly SoundDefinition Equip = Add("ui.equip", Buckle, -21f);
        public static readonly SoundDefinition Trade = Add("ui.trade", take => Purse(take), -21f);
        public static readonly SoundDefinition Type = Add("ui.type", take => Tap(take, 2000f, 0.0008f, 0.7f), -32f, spacing: 0.015f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float spacing = 0.03f)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Interface,
                Positional = false,
                Stereo = true,
                Takes = 5,
                PlaysPerTake = 6,
                PitchSpread = 0.4f,
                GainSpread = 0.8f,
                Spacing = spacing,
                Priority = 90,
                ReverbSend = 0f,
                Mastering = Mastering.Default with { Target = target, Stage = -19f },
            });
        }

        // A fingertip's worth of wood: a tiny modal knock and a short hollow behind it.
        private static void Tap(Take take, float pitch, float hardness, float body)
        {
            SoundRandom random = take.Random;
            var blow = new Blow { Material = Material.Wood, Shape = ModalShape.Bar, Fundamental = pitch * random.Range(0.85f, 1.15f), Modes = 8, Tilt = -2f, Hardness = hardness * 0.5f, Rattle = 0.25f, Noise = -7f, NoiseLow = 700f };
            take.Mix(Impact.Of(take, blow, 0.08f).Normalize(0f));
            take.Mix(take.Sine(0.04f, Curve.Settle(pitch * 0.32f, pitch * 0.2f, 0.006f)).Percussive(0.0005f, 0.007f).Normalize(0f), 0f, -13f + 6f * body);
        }

        private static void Latch(Take take)
        {
            SoundRandom random = take.Random;
            Tap(take, 2100f, 0.0009f, 0.6f);
            var pin = new Blow { Material = Material.Iron, Shape = ModalShape.Bar, Fundamental = random.LogRange(2400f, 3400f), Modes = 8, Tilt = -1.5f, Ringing = 0.25f, Rattle = 0.5f, Noise = -9f, NoiseLow = 1200f };
            take.Mix(Impact.Of(take, pin, 0.05f).Normalize(0f), 0.018f, -9f);
        }

        private static void Page(Take take, bool rising)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(0.13f, 0.19f);

            // A leaf of heavy paper turning over: it rushes through the air, ticks at its edge, and lays down.
            Curve shape = Curve.Hump(seconds * (rising ? 0.6f : 0.35f), seconds);
            Curve sweep = rising ? Curve.Glide(800f, 3200f, seconds) : Curve.Glide(3000f, 800f, seconds);
            take.Mix(take.Pink(seconds).Bandpass(sweep, 0.9f).Shape(shape).Normalize(0f), 0f, -6f);
            take.Mix(Grains.Stream(take, seconds, shape * 900f, shape, new GrainStyle { LowHz = 2400f, HighHz = 10000f, MinDecay = 0.0005f, MaxDecay = 0.0026f, Partials = 1, Noise = 0.9f, Spread = 14f }).Normalize(0f), 0f, -7f);
            float landing = rising ? seconds * 0.9f : seconds * 0.5f;
            take.Mix(take.Sine(0.05f, Curve.Settle(240f, 150f, 0.008f)).Percussive(0.001f, 0.012f).Normalize(0f), landing, -13f);
        }

        private static void Agreed(Take take)
        {
            Tap(take, 1500f, 0.0014f, 1f);
            take.Mix(Bell(take, 880f).Normalize(0f), 0.012f, -8f, -0.2f);
            take.Mix(Bell(take, 1318.5f).Normalize(0f), 0.07f, -10f, 0.2f);
        }

        private static void Refused(Take take)
        {
            SoundRandom random = take.Random;

            // Wood on wood, twice and dull. A buzz would say the computer disapproves; this is a door that doesn't open.
            for (int i = 0; i < 2; i++)
            {
                var knock = new Blow { Material = Material.Plank, Shape = ModalShape.Bar, Fundamental = random.LogRange(170f, 240f), Modes = 10, Tilt = -3f, Hardness = 0.0014f, Rattle = 0.2f, Noise = -7f, NoiseLow = 90f };
                take.Mix(Impact.Of(take, knock, 0.13f).Normalize(0f), i * 0.1f, -i * 3f);
            }
        }

        private static void Buckle(Take take)
        {
            SoundRandom random = take.Random;
            Curve shape = Curve.Hump(0.03f, 0.13f);
            take.Mix(Grains.Stream(take, 0.13f, shape * 1100f, shape, new GrainStyle { LowHz = 1300f, HighHz = 6000f, MinDecay = 0.001f, MaxDecay = 0.006f, Partials = 1, Noise = 0.9f, Spread = 13f }).Normalize(0f), 0f, -7f);
            var buckle = new Blow { Material = Material.Iron, Shape = ModalShape.Plate, Fundamental = random.LogRange(2400f, 3900f), Modes = 24, Tilt = 0.3f, Ringing = 0.3f, Rattle = 1f, Noise = -6f, NoiseLow = 1400f, Bounces = 2 };
            take.Mix(Impact.Of(take, buckle, 0.26f).Normalize(0f), 0.04f, -2f);
            take.Mix(take.Sine(0.06f, Curve.Settle(300f, 170f, 0.01f)).Percussive(0.001f, 0.02f).Normalize(0f), 0.04f, -20f);
        }

        private static void Purse(Take take)
        {
            SoundRandom random = take.Random;
            float time = 0f;
            int count = 4 + random.Int(4);
            for (int i = 0; i < count; i++)
            {
                var coin = new Blow { Material = Material.Gold, Shape = ModalShape.Shell, Fundamental = random.LogRange(5200f, 9000f), Modes = 16, Tilt = 0.5f, Ringing = random.Range(0.35f, 0.6f), Rattle = 0.9f, Noise = -7f, NoiseLow = 2400f, Bounces = 1 };
                take.Mix(Impact.Of(take, coin, 0.24f).Normalize(0f), time, -random.Range(0f, 8f), random.Range(-0.4f, 0.4f));
                time += random.Exponential(0.018f);
            }
            take.Mix(take.Sine(0.08f, Curve.Settle(200f, 120f, 0.01f)).Percussive(0.001f, 0.03f).Normalize(0f), 0.01f, -12f);
        }

        private static Signal Bell(Take take, float pitch)
        {
            Signal bell = take.Track(0.5f);
            float[] partials = { 1f, 2.76f, 5.4f };
            for (int p = 0; p < partials.Length; p++)
            {
                bell.Add(take.Sine(0.5f, pitch * partials[p], take.Random.Range(0f, 6f)).Percussive(0.0006f, 0.11f / (p + 1)), 0f, 1f / (p * 2.5f + 1f));
            }
            return bell;
        }
    }
}
