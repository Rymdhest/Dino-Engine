using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>The biggest things that happen: explosions, thunder and what falls down after. They're allowed to take over, and the mix makes way.</summary>
    [SoundCollection]
    public static class Cataclysm
    {
        public static readonly SoundDefinition ExplosionSmall = Add("blast.small", take => Explosion(take, 0.35f), -13f, 6f, 90f);
        public static readonly SoundDefinition ExplosionMedium = Add("blast.medium", take => Explosion(take, 0.65f), -11f, 9f, 130f);
        public static readonly SoundDefinition ExplosionBig = Add("blast.big", take => Explosion(take, 1f), -9f, 12f, 200f);
        public static readonly SoundDefinition LightningNear = Add("sky.lightning", LightningStrike, -9f, 12f, 500f);
        public static readonly SoundDefinition ThunderNear = Add("sky.thunder.near", take => Roll(take, 500f), -14f, 8f, 3000f);
        public static readonly SoundDefinition ThunderFar = Add("sky.thunder.far", take => Roll(take, 3500f), -22f, 3f, 3000f);
        public static readonly SoundDefinition SplashBig = Add("water.splash.big", take => Splash(take, 1f), -16f, 1f, 80f);
        public static readonly SoundDefinition SplashSmall = Add("water.splash", take => Splash(take, 0.35f), -20f, 0f, 50f);
        public static readonly SoundDefinition TimberFall = Add("world.timber", Timber, -14f, 3f, 110f);
        public static readonly SoundDefinition GlassBreak = Add("world.glass", Shatter, -15f, 1f, 70f);
        public static readonly SoundDefinition RockFall = Add("world.rockfall", Rocks, -14f, 3f, 120f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float presence, float far)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Combat,
                Stereo = true,
                Takes = 3,
                PlaysPerTake = 1,
                Presence = presence,
                Priority = 85,
                MostAtOnce = 3,
                Spacing = 0.08f,
                NearDistance = 22f,
                FarDistance = far,
                ReverbSend = 0.34f,
                PitchSpread = 1f,
                Mastering = Mastering.Default with { Target = target, Stage = -9f, TailFloor = -58f },
            });
        }

        private static void Explosion(Take take, float size)
        {
            SoundRandom random = take.Random;
            float seconds = 1.1f + 2.4f * size;

            // The shock arrives first: an instant rise, then the long suction behind it. At the rise nothing else has started, and it's
            // what tells the ear this is air being hit, not noise being played.
            float positive = 0.008f + 0.05f * size;
            Signal shock = Blast.Friedlander(take, positive, 0.9f + size).Lowpass(1400f + 7000f * (1f - size * 0.5f)).Normalize(0f);
            take.Mix(shock, 0f, 0f);

            // Right at the front, the crack, as wide as the spectrum goes.
            take.Mix(take.White(0.03f).Highpass(240f).Percussive(0.00002f, 0.004f + 0.003f * size).Drive(3f).Normalize(0f), 0f, -3f);

            // Then the fireball: broadband, losing its top first, as recordings show on a falling diagonal.
            Curve fall = Curve.Glide(19000f, 200f, seconds * 0.22f);
            Signal left = take.Pink(seconds).Lowpass(fall).Bell(420f, 12f, 0.55f).Percussive(0.0008f, 0.24f + 0.6f * size).Drive(2.2f).Normalize(0f);
            Signal right = take.Pink(seconds).Lowpass(fall).Bell(420f, 12f, 0.55f).Percussive(0.0008f, 0.24f + 0.6f * size).Drive(2.2f).Normalize(0f);
            take.MixStereo(left, right, 0.0015f, -1f);

            // The earth moved: a heavy low swell with its own slow decay, which is the part that's felt.
            Signal body = take.Brown(seconds).Bandpass(150f, 0.6f).Percussive(0.01f, 0.5f + 0.9f * size).Normalize(0f);
            take.Mix(body, 0.004f, -6f);
            take.Mix(take.Sine(seconds, Curve.Settle(110f - 22f * size, 44f, 0.1f)).Percussive(0.004f, 0.2f + 0.26f * size).Drive(1.8f).Normalize(0f), 0.002f, -11f);

            // Bits coming down, spreading out across the stereo field after the first second has got through.
            float debrisAt = 0.25f + 0.4f * size;
            float debrisSeconds = seconds - debrisAt;
            var stones = new GrainStyle { LowHz = 480f, HighHz = 5200f, MinDecay = 0.003f, MaxDecay = 0.02f, Partials = 3, Noise = 0.25f, Spread = 22f };
            Curve energy = Curve.Hump(0.14f, debrisSeconds, 1f, 1.2f);
            Signal debrisLeft = Grains.Stream(take, debrisSeconds, energy * (60f + 240f * size), energy, stones).Normalize(0f);
            Signal debrisRight = Grains.Stream(take, debrisSeconds, energy * (60f + 240f * size), energy, stones).Normalize(0f);
            take.MixStereo(debrisLeft, debrisRight, debrisAt, -13f);

            // Echoes off whatever's around, late and dull.
            Signal echo = take.Brown(0.9f).Lowpass(320f).Percussive(0.01f, 0.2f).Normalize(0f);
            take.Mix(echo, 0.42f + 0.3f * random.Float(), -13f, random.Range(-0.6f, 0.6f));
        }

        private static void LightningStrike(Take take)
        {
            SoundRandom random = take.Random;

            // Close enough to see it hit: the leader first, a crackling tearing rising for a fraction of a second, then the channel itself.
            float tear = random.Range(0.1f, 0.2f);
            Curve rise = new Curve(t => MathF.Pow(Math.Clamp(t / tear, 0f, 1f), 2.4f));
            Signal leader = Grains.Stream(take, tear, rise * 2600f, rise, new GrainStyle { LowHz = 1600f, HighHz = 13000f, MinDecay = 0.00015f, MaxDecay = 0.0009f, Partials = 1, Noise = 0.8f, Spread = 20f }).Normalize(0f);
            take.Mix(leader, 0f, -9f);

            // The main crack is an N-wave from air heated to several times the sun's surface in a microsecond. It's loud enough to distort the air itself.
            Signal crack = take.Track(0.1f);
            Blast.NWave(crack.Samples, crack.Length, 0.0003f, 0.0028f, 1f);
            crack.Add(take.White(0.1f).Highpass(320f).Percussive(0.00002f, 0.012f), 0f, 0.8f);
            take.Mix(crack.Drive(5f).Normalize(0f), tear, 0f);

            // A few returning strokes, each of a sound a few tens of milliseconds behind the last.
            int strokes = 1 + random.Int(3);
            float at = tear;
            for (int i = 0; i < strokes; i++)
            {
                at += random.Range(0.03f, 0.09f);
                take.Mix(take.White(0.06f).Highpass(240f).Percussive(0.00003f, 0.008f).Drive(3f).Normalize(0f), at, -4f - i * 3f, random.Range(-0.4f, 0.4f));
            }

            // Then the sky falling in, near enough to be nearly all at once.
            take.MixStereo(Rumble(take, 5.5f, 260f), Rumble(take, 5.5f, 260f), tear + 0.02f, -2f);
        }

        private static void Roll(Take take, float distance)
        {
            SoundRandom random = take.Random;
            float seconds = 8f + distance * 0.0016f;

            // Made twice, as the same storm reaches each ear differently at this range.
            take.MixStereo(Rumble(take, seconds, distance), Rumble(take, seconds, distance));
        }

        // Each channel's claps, and a body of low air that swells as they do: the part of the channel too far along to make out one wave from
        // another. Measured thunder sits between 100 and 400 Hz, a dense roll, with the claps riding on it.
        private static Signal Rumble(Take take, float seconds, float distance)
        {
            SoundRandom random = take.Random;
            Signal claps = Blast.Thunder(take, seconds, distance, random.Range(2800f, 4200f)).ThroughAir(distance);
            Signal swell = claps.Follower(0.09f).Normalize(0f);
            Signal roll = take.Brown(seconds).Add(take.Pink(seconds), 0f, 0.4f).Bandpass(190f, 0.55f).Multiply(swell).Normalize(0f);
            return roll.Add(claps.Normalize(0f), 0f, 0.55f).Normalize(0f);
        }

        private static void Splash(Take take, float size)
        {
            SoundRandom random = take.Random;
            float seconds = 0.4f + 0.9f * size;

            // A cavity is punched into the water and slams shut: a low thud of air, then all the bubbles that trapped.
            take.Mix(take.Sine(0.3f, Curve.Glide(240f - 120f * size, 90f - 30f * size, 0.08f)).Percussive(0.002f, 0.05f + 0.05f * size).Normalize(0f), 0f, -6f);
            take.Mix(take.White(0.1f).Highpass(900f).Percussive(0.0002f, 0.012f + 0.02f * size).Normalize(0f), 0f, -9f);
            Curve energy = Curve.Settle(1f, 0f, seconds * 0.3f);
            take.Mix(Liquid.Bubbles(take, seconds, energy * (1200f + 3000f * size), energy, 0.0006f, 0.004f + 0.012f * size, 2.1f, 0.2f).Normalize(0f), 0.006f);
            take.Mix(take.Pink(seconds).Bandpass(Curve.Glide(4000f, 900f, seconds * 0.5f), 0.7f).Percussive(0.01f, seconds * 0.22f).Normalize(0f), 0.004f, -5f);

            // Then what went up comes back down.
            float rain = seconds * 0.8f;
            Curve falling = Curve.Hump(rain * 0.3f, rain);
            take.Mix(Grains.Stream(take, rain, falling * (200f + 900f * size), falling, new GrainStyle { LowHz = 1200f, HighHz = 9000f, MinDecay = 0.0007f, MaxDecay = 0.004f, Partials = 1, Noise = 0.4f, Spread = 16f }).Normalize(0f), 0.14f, -9f);
        }

        private static void Timber(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(1.3f, 1.9f);

            // Fibres letting go, a few and then all of them.
            Curve going = new Curve(t => MathF.Pow(Math.Clamp(t / (seconds * 0.6f), 0f, 1f), 3f));
            take.Mix(Grains.Stream(take, seconds * 0.62f, going * 420f + 9f, going + 0.1f, new GrainStyle { LowHz = 420f, HighHz = 4200f, MinDecay = 0.0015f, MaxDecay = 0.012f, Partials = 3, Noise = 0.25f, Spread = 18f }).Normalize(0f), 0f, -3f);
            take.Mix(Treasure.Hinge(take, seconds * 0.62f, random.LogRange(60f, 110f)).Normalize(0f), 0f, -6f);

            // And the fall.
            float landed = seconds * 0.62f;
            var trunk = new Blow { Material = Material.Plank, Shape = ModalShape.Bar, Fundamental = random.LogRange(80f, 140f), Modes = 14, Tilt = -3f, Hardness = 0.002f, Rattle = 0.5f, Noise = -3f, NoiseLow = 45f, Bounces = 2 };
            take.Mix(Impact.Of(take, trunk, 0.9f).Normalize(0f), landed);
            take.Mix(take.Brown(1.2f).Lowpass(150f).Percussive(0.005f, 0.24f).Normalize(0f), landed, -2f);
        }

        private static void Shatter(Take take)
        {
            SoundRandom random = take.Random;

            // One hard moment, and then a spray of pieces that each ring on their own and land one by one.
            var pane = new Blow { Material = Material.Glass, Shape = ModalShape.Plate, Fundamental = random.LogRange(1100f, 2000f), Modes = 42, Tilt = -0.6f, Rattle = 1f, Noise = -3f, NoiseLow = 700f };
            take.Mix(Impact.Of(take, pane, 0.5f).Normalize(0f), 0f, -2f);
            take.Mix(Impacts.Crack(take, 0.0016f).Normalize(0f), 0f, -2f);
            var shards = new GrainStyle { LowHz = 2600f, HighHz = 14000f, MinDecay = 0.005f, MaxDecay = 0.05f, Partials = 3, Noise = 0f, Spread = 18f };
            Signal left = Grains.Settle(take, 1.1f, 60f, 0.22f, shards).Normalize(0f);
            Signal right = Grains.Settle(take, 1.1f, 60f, 0.22f, shards).Normalize(0f);
            take.MixStereo(left, right, 0.012f, -3f);
        }

        private static void Rocks(Take take)
        {
            SoundRandom random = take.Random;
            float seconds = random.Range(1.6f, 2.5f);
            Curve energy = Curve.Hump(0.26f, seconds, 1f, 1.1f);
            var pieces = new GrainStyle { LowHz = 160f, HighHz = 2600f, MinDecay = 0.008f, MaxDecay = 0.05f, Partials = 3, Noise = 0.25f, Spread = 16f };
            Signal left = Grains.Stream(take, seconds, energy * 90f, energy, pieces).Normalize(0f);
            Signal right = Grains.Stream(take, seconds, energy * 90f, energy, pieces).Normalize(0f);
            take.MixStereo(left, right);
            take.Mix(take.Brown(seconds).Lowpass(170f).Shape(energy).Normalize(0f), 0f, -4f);
            take.Mix(Grains.Stream(take, seconds, energy * 700f, energy, new GrainStyle { LowHz = 1500f, HighHz = 8200f, MinDecay = 0.0012f, MaxDecay = 0.006f, Partials = 1, Noise = 0.6f, Spread = 14f }).Normalize(0f), 0f, -11f);
        }
    }
}
