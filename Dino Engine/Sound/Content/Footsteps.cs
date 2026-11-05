using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;
using Dino_Engine.World.Content;

namespace Dino_Engine.Sound.Content
{
    /// <summary>What a foot can land on, as far as the ear can tell.</summary>
    public enum Ground
    {
        Dirt,
        Grass,
        Gravel,
        Sand,
        WetSand,
        Stone,
        Snow,
        Ice,
        Mud,
        Wood,
        Leaves,
        Shallows,
        Water,
    }

    /// <summary>
    /// A footstep is a heel coming down, the weight rolling forward and the toe slapping after it. The force of that is the double hump of a ground reaction force, and it is what's used
    /// here to drive every layer (Farnell's "Marching onwards", Cook's PhOLIES, Turchet's aggregate surfaces): the same walk on any ground, with only the ground changing. That keeps each
    /// surface to a line in the table at the bottom. What a step is heard as is its middle: a thud from 150 to 600 Hz says how hard the ground is, and what happens above that says what
    /// it's made of. Below that there's only weight, which few speakers play.
    /// </summary>
    [SoundCollection]
    public static class Footsteps
    {
        /// <summary>How a ground answers. Levels are decibels against a full-scale layer: a line here is all a new ground needs.</summary>
        private sealed class Surface
        {
            public float BodyHz = 260f;             // The thud: how hard the ground is, and the part of a step that carries.
            public float BodyQ = 1f;
            public float BodyDecay = 0.02f;
            public float BodyLevel = -4f;
            public float ThumpHz = 110f;            // The weight of a body arriving, felt more than heard.
            public float ThumpDecay = 0.018f;
            public float ThumpLevel = -15f;
            public float ClickHz = 900f;            // The sole meeting it.
            public float ClickDecay = 0.004f;
            public float ClickLevel = -100f;
            public GrainStyle Crunch;               // What gives way underfoot, in pieces big enough to hear one by one. In snow these reach down to 100 Hz.
            public float CrunchRate;                // Per second, at full force.
            public float CrunchLevel = -100f;
            public GrainStyle Grain;                // And the fine stuff among them.
            public float GrainRate;
            public float GrainLevel = -100f;
            public float SwishLow = 1400f;          // What the foot goes through on its way down, or pushes aside: blades, loose sand.
            public float SwishHigh = 5000f;
            public float SwishTime = 0.13f;
            public float SwishLevel = -100f;
            public float ScuffHz = 3000f;           // The sole sliding as it leaves.
            public float ScuffLevel = -100f;
            public Material? Ring;                  // Boards and slabs ring, however little.
            public float RingHz = 300f;
            public float RingLevel = -100f;
            public float WetLevel = -100f;          // Water in it.
            public float BubbleLow = 0.0007f;       // Metres. Thick stuff lets go of bigger bubbles than water does, which is what makes mud gloop.
            public float BubbleHigh = 0.008f;
            public float WetHz = 3200f;
            public float DragLevel = -100f;         // A leg pushed through water.
            public float SuckLevel = -100f;         // A sole pulled free of what's holding it.
        }

        private static readonly SoundDefinition[,] Steps = new SoundDefinition[Enum.GetValues<Ground>().Length, 2];

        /// <summary>A step on this ground. Running comes down harder than walking, and rolls through quicker.</summary>
        public static SoundDefinition On(Ground ground, bool running = true) => Steps[(int)ground, running ? 1 : 0];

        /// <summary>How a painted ground material sounds underfoot.</summary>
        public static Ground GroundOf(SurfaceMaterial material) => material switch
        {
            SurfaceMaterial.Grass or SurfaceMaterial.GrassySoil or SurfaceMaterial.Moss => Ground.Grass,
            SurfaceMaterial.Rock or SurfaceMaterial.Cobble => Ground.Stone,
            SurfaceMaterial.Sand or SurfaceMaterial.Dunes or SurfaceMaterial.Ash => Ground.Sand,
            SurfaceMaterial.Gravel or SurfaceMaterial.CrackedEarth => Ground.Gravel,
            SurfaceMaterial.Snow => Ground.Snow,
            SurfaceMaterial.Ice => Ground.Ice,
            SurfaceMaterial.Mud => Ground.Mud,
            _ => Ground.Dirt,
        };

        static Footsteps()
        {
            foreach (Ground ground in Enum.GetValues<Ground>())
            {
                Surface surface = SurfaceOf(ground);
                for (int gait = 0; gait < 2; gait++)
                {
                    bool running = gait == 1;
                    Steps[(int)ground, gait] = SoundCatalog.Add(new SoundDefinition($"step.{ground.ToString().ToLowerInvariant()}.{(running ? "run" : "walk")}", take => Step(take, surface, running))
                    {
                        Bus = SoundBus.Foley,
                        Takes = 10,
                        PlaysPerTake = 2,
                        PitchSpread = 0.6f,
                        GainSpread = 1.6f,
                        Mastering = Mastering.Default with { Target = -25f },
                        NearDistance = 7f,
                        FarDistance = 42f,
                        MostAtOnce = 8,
                        Spacing = 0.05f,
                        Priority = 30,
                        ReverbSend = 0.1f,
                    });
                }
            }
        }

        private static void Step(Take take, Surface ground, bool running)
        {
            SoundRandom random = take.Random;

            // One foot is never quite like the last: how it lands and how long it stays down. Most of a step is over in a tenth of a second, and a long one only blurs into the next.
            float stance = (running ? 0.13f : 0.24f) * random.Range(0.85f, 1.2f);
            float heel = random.Range(0.72f, 1f) * (running ? 1f : 0.8f);
            float toe = random.Range(0.4f, 0.75f) * (running ? 0.8f : 0.62f);
            float seconds = stance + 0.16f;
            float toeAt = stance * random.Range(0.42f, 0.6f);
            float heelLevel = AudioFormat.GainToDecibels(heel);
            float toeLevel = AudioFormat.GainToDecibels(toe);

            // The two humps of a ground reaction force: the heel coming down, then the push from the toe.
            Curve heelForce = Curve.Hump(stance * 0.1f, stance * 0.55f, heel, 0.8f);
            Curve toeForce = Curve.Hump(stance * 0.18f, stance * 0.42f, toe);
            Curve force = new Curve(t => heelForce.At(t) + toeForce.At(t - toeAt));

            // The thud, from the heel and again from the toe, higher as there's less of the foot in it. Its pitch drops as the ground gives.
            float bodyHz = ground.BodyHz * random.Range(0.86f, 1.18f);
            float bodyDecay = ground.BodyDecay * random.Range(0.85f, 1.2f);
            Signal body = take.Pink(0.14f).Bandpass(Curve.Settle(bodyHz * 1.7f, bodyHz, 0.008f), ground.BodyQ).Percussive(0.0012f, bodyDecay).Normalize(0f);
            take.Mix(body, 0f, ground.BodyLevel + heelLevel);
            Signal toeBody = take.Pink(0.1f).Bandpass(bodyHz * 1.35f, ground.BodyQ).Percussive(0.001f, bodyDecay * 0.7f).Normalize(0f);
            take.Mix(toeBody, toeAt, ground.BodyLevel - 3f + toeLevel);

            float thumpHz = ground.ThumpHz * random.Range(0.86f, 1.18f);
            Signal thump = Impacts.Thump(take, thumpHz * 1.5f, thumpHz, 0.01f, ground.ThumpDecay * random.Range(0.85f, 1.2f), 0.12f).Normalize(0f);
            take.Mix(thump, 0f, ground.ThumpLevel + heelLevel);

            // Sole on ground. Short and dull: it's rubber and leather, not a hammer.
            if (ground.ClickLevel > -90f)
            {
                Signal click = take.White(0.04f)
                    .Bandpass(Curve.Settle(ground.ClickHz * 1.8f, ground.ClickHz, 0.004f), 0.9f)
                    .Percussive(0.0003f, ground.ClickDecay * random.Range(0.8f, 1.3f)).Normalize(0f);
                take.Mix(click, 0.0004f, ground.ClickLevel + heelLevel);
                take.Mix(take.White(0.03f).Bandpass(ground.ClickHz * 1.25f, 0.9f).Percussive(0.0005f, ground.ClickDecay * 0.7f).Normalize(0f), toeAt, ground.ClickLevel - 4f + toeLevel);
            }

            if (ground.SwishLevel > -90f)
            {
                float time = ground.SwishTime * random.Range(0.8f, 1.25f) * (running ? 0.8f : 1f);
                Signal swish = take.Pink(time + 0.02f).Bandpass(Curve.Glide(ground.SwishHigh, ground.SwishLow, time), 0.7f).Shape(Curve.Hump(time * 0.3f, time)).Normalize(0f);
                take.Mix(swish, 0f, ground.SwishLevel + heelLevel * 0.5f);
            }

            // Particles give way in proportion to the force on them, so the same curve drives how many and how hard.
            if (ground.CrunchLevel > -90f)
            {
                take.Mix(Grains.Stream(take, seconds, force * ground.CrunchRate, force, ground.Crunch).Normalize(0f), 0f, ground.CrunchLevel + heelLevel * 0.5f);
            }
            if (ground.GrainLevel > -90f)
            {
                take.Mix(Grains.Stream(take, seconds, force * ground.GrainRate, force, ground.Grain).Normalize(0f), 0f, ground.GrainLevel + heelLevel * 0.5f);
            }

            if (ground.Ring != null)
            {
                float hz = ground.RingHz * random.Range(0.8f, 1.25f);
                ModalObject ring = ModalObject.Draw(random, ground.Ring, ModalShape.Plate, hz, modes: 16, tilt: -3f);
                take.Mix(ring.Ring(Contact.Press(take, 0.0028f, 1f), 0.16f).Normalize(0f), 0f, ground.RingLevel + heelLevel);
            }

            if (ground.ScuffLevel > -90f)
            {
                // Pushing off, the sole drags a little before it's clear.
                float scuffTime = stance * random.Range(0.3f, 0.55f);
                Curve speed = Curve.Hump(scuffTime * 0.5f, scuffTime, random.Range(0.4f, 0.8f));
                Signal scuff = Contact.Scrape(take, scuffTime, speed, 1f, 0.85f, ground.ScuffHz).Highpass(ground.ScuffHz * 0.4f).Normalize(0f);
                take.Mix(scuff, toeAt, ground.ScuffLevel - 6f + toeLevel);
            }

            if (ground.WetLevel > -90f)
            {
                Curve energy = Curve.Settle(1f, 0f, 0.05f);
                Signal slosh = Liquid.Bubbles(take, 0.26f, energy * 2000f, energy, ground.BubbleLow, ground.BubbleHigh, 2.2f, 0.12f).Normalize(0f);
                take.Mix(slosh, 0.004f, ground.WetLevel);
                take.Mix(take.Pink(0.2f).Bandpass(Curve.Glide(ground.WetHz, 700f, 0.1f), 0.7f).Percussive(0.002f, 0.04f).Normalize(0f), 0.002f, ground.WetLevel - 2f);
            }

            if (ground.DragLevel > -90f)
            {
                // Water pushed aside by the shin, and what it throws up coming back down.
                float drag = stance + 0.14f;
                take.Mix(take.Pink(drag).Bandpass(Curve.Glide(700f, 260f, drag), 0.8f).Shape(Curve.Hump(drag * 0.4f, drag)).Normalize(0f), 0.01f, ground.DragLevel);
                Curve spray = Curve.Hump(0.1f, 0.34f);
                take.Mix(Liquid.Bubbles(take, 0.36f, spray * 220f, spray, 0.0003f, 0.0014f, 2.4f, 0.2f).Normalize(0f), 0.05f, ground.DragLevel);
            }

            if (ground.SuckLevel > -90f)
            {
                // The seal round the boot breaking as it lifts: a short rising gulp.
                float lift = stance * random.Range(0.8f, 1.1f);
                take.Mix(take.Sine(0.07f, Curve.Glide(random.Range(170f, 240f), random.Range(620f, 900f), 0.06f)).Percussive(0.012f, 0.02f).Normalize(0f), lift, ground.SuckLevel);
            }
        }

        private static Surface SurfaceOf(Ground ground) => ground switch
        {
            Ground.Dirt => new Surface
            {
                BodyHz = 240f, BodyQ = 1f, BodyDecay = 0.024f, BodyLevel = -2f,
                ThumpHz = 104f, ThumpDecay = 0.02f, ThumpLevel = -13f,
                ClickHz = 800f, ClickDecay = 0.005f, ClickLevel = -15f,
                Crunch = new GrainStyle { LowHz = 280f, HighHz = 2200f, MinDecay = 0.0016f, MaxDecay = 0.007f, Partials = 1, Noise = 0.9f, Spread = 20f },
                CrunchRate = 60f, CrunchLevel = -12f,
                Grain = new GrainStyle { LowHz = 1200f, HighHz = 5000f, MinDecay = 0.0006f, MaxDecay = 0.003f, Partials = 1, Noise = 0.75f, Spread = 22f },
                GrainRate = 300f, GrainLevel = -19f,
                ScuffHz = 1600f, ScuffLevel = -16f,
            },
            Ground.Grass => new Surface
            {
                BodyHz = 190f, BodyQ = 0.9f, BodyDecay = 0.03f, BodyLevel = -3f,
                ThumpHz = 92f, ThumpDecay = 0.022f, ThumpLevel = -14f,
                // Blades pushed down and springing back, which has no attack to speak of.
                SwishLow = 1800f, SwishHigh = 5200f, SwishTime = 0.14f, SwishLevel = -6f,
            },
            Ground.Gravel => new Surface
            {
                BodyHz = 320f, BodyQ = 0.8f, BodyDecay = 0.02f, BodyLevel = -9f,
                ThumpHz = 120f, ThumpDecay = 0.016f, ThumpLevel = -17f,
                Crunch = new GrainStyle { LowHz = 700f, HighHz = 5200f, MinDecay = 0.0022f, MaxDecay = 0.0095f, Partials = 3, Noise = 0.25f, Spread = 18f },
                CrunchRate = 110f, CrunchLevel = -1f,
                Grain = new GrainStyle { LowHz = 2200f, HighHz = 11000f, MinDecay = 0.0006f, MaxDecay = 0.003f, Partials = 2, Noise = 0.3f, Spread = 24f },
                GrainRate = 1100f, GrainLevel = -8f,
                ScuffHz = 2600f, ScuffLevel = -17f,
            },
            Ground.Sand => new Surface
            {
                BodyHz = 140f, BodyQ = 0.7f, BodyDecay = 0.04f, BodyLevel = -7f,
                ThumpHz = 84f, ThumpDecay = 0.026f, ThumpLevel = -13f,
                // Dry and fine: a hush high up that slides away, with nothing to click on.
                SwishLow = 4200f, SwishHigh = 10000f, SwishTime = 0.1f, SwishLevel = -5f,
                Grain = new GrainStyle { LowHz = 3000f, HighHz = 14000f, MinDecay = 0.0004f, MaxDecay = 0.0018f, Partials = 1, Noise = 1f, Spread = 10f },
                GrainRate = 2200f, GrainLevel = -9f,
                ScuffHz = 5600f, ScuffLevel = -14f,
            },
            Ground.WetSand => new Surface
            {
                // Packed firm by the water: a flat, tight slap, with a little water pressed out of it.
                BodyHz = 380f, BodyQ = 1.6f, BodyDecay = 0.016f, BodyLevel = -2f,
                ThumpHz = 110f, ThumpDecay = 0.016f, ThumpLevel = -13f,
                ClickHz = 1500f, ClickDecay = 0.005f, ClickLevel = -11f,
                WetLevel = -14f,
            },
            Ground.Stone => new Surface
            {
                BodyHz = 420f, BodyQ = 1.3f, BodyDecay = 0.012f, BodyLevel = -6f,
                ThumpHz = 150f, ThumpDecay = 0.012f, ThumpLevel = -19f,
                ClickHz = 2500f, ClickDecay = 0.0028f, ClickLevel = -3f,
                Ring = Material.Stone, RingHz = 640f, RingLevel = -13f,
                ScuffHz = 3600f, ScuffLevel = -20f,
                Grain = new GrainStyle { LowHz = 2600f, HighHz = 9000f, MinDecay = 0.0004f, MaxDecay = 0.0016f, Partials = 2, Noise = 0.45f, Spread = 28f },
                GrainRate = 120f, GrainLevel = -21f,
            },
            Ground.Snow => new Surface
            {
                BodyHz = 230f, BodyQ = 0.8f, BodyDecay = 0.03f, BodyLevel = -10f,
                ThumpHz = 90f, ThumpDecay = 0.024f, ThumpLevel = -17f,
                // Snow is a crust of ice bonds breaking. Real ones show a handful of separate crunches in a step, wide and reaching well down.
                Crunch = new GrainStyle { LowHz = 170f, HighHz = 2400f, MinDecay = 0.004f, MaxDecay = 0.016f, Partials = 1, Noise = 1f, Spread = 14f },
                CrunchRate = 70f, CrunchLevel = -2f,
                Grain = new GrainStyle { LowHz = 600f, HighHz = 2800f, MinDecay = 0.0009f, MaxDecay = 0.0034f, Partials = 2, Noise = 0.55f, Spread = 18f },
                GrainRate = 900f, GrainLevel = -12f,
            },
            Ground.Ice => new Surface
            {
                BodyHz = 520f, BodyQ = 1.6f, BodyDecay = 0.012f, BodyLevel = -9f,
                ThumpHz = 170f, ThumpDecay = 0.012f, ThumpLevel = -19f,
                ClickHz = 4300f, ClickDecay = 0.002f, ClickLevel = -5f,
                Ring = Material.Ice, RingHz = 1700f, RingLevel = -4f,
                ScuffHz = 6000f, ScuffLevel = -18f,
            },
            Ground.Mud => new Surface
            {
                BodyHz = 250f, BodyQ = 1.2f, BodyDecay = 0.036f, BodyLevel = -4f,
                ThumpHz = 92f, ThumpDecay = 0.03f, ThumpLevel = -12f,
                WetLevel = -3f, SuckLevel = -5f, BubbleLow = 0.0028f, BubbleHigh = 0.018f, WetHz = 1000f,
                Crunch = new GrainStyle { LowHz = 280f, HighHz = 1500f, MinDecay = 0.003f, MaxDecay = 0.014f, Partials = 1, Noise = 0.85f, Spread = 12f },
                CrunchRate = 60f, CrunchLevel = -14f,
            },
            Ground.Wood => new Surface
            {
                BodyHz = 300f, BodyQ = 1.2f, BodyDecay = 0.02f, BodyLevel = -7f,
                ThumpHz = 122f, ThumpDecay = 0.02f, ThumpLevel = -12f,
                ClickHz = 1300f, ClickDecay = 0.004f, ClickLevel = -9f,
                Ring = Material.Plank, RingHz = 190f, RingLevel = -3f,
                ScuffHz = 2400f, ScuffLevel = -22f,
            },
            Ground.Leaves => new Surface
            {
                BodyHz = 220f, BodyQ = 0.9f, BodyDecay = 0.024f, BodyLevel = -11f,
                ThumpHz = 96f, ThumpDecay = 0.02f, ThumpLevel = -18f,
                // Dry leaves crackle: fewer, sharper and further apart than grass, and in bursts.
                Crunch = new GrainStyle { LowHz = 800f, HighHz = 7000f, MinDecay = 0.0012f, MaxDecay = 0.007f, Partials = 2, Noise = 0.75f, Spread = 18f },
                CrunchRate = 170f, CrunchLevel = -2f,
                Grain = new GrainStyle { LowHz = 2400f, HighHz = 14000f, MinDecay = 0.0005f, MaxDecay = 0.0035f, Partials = 1, Noise = 0.8f, Spread = 22f },
                GrainRate = 800f, GrainLevel = -7f,
                SwishLow = 1800f, SwishHigh = 6000f, SwishTime = 0.12f, SwishLevel = -13f,
            },
            Ground.Shallows => new Surface
            {
                BodyHz = 420f, BodyQ = 0.8f, BodyDecay = 0.03f, BodyLevel = -10f,
                ThumpHz = 118f, ThumpDecay = 0.016f, ThumpLevel = -20f,
                WetLevel = -2f,
            },
            _ => new Surface     // Water, up the shin.
            {
                BodyHz = 300f, BodyQ = 0.7f, BodyDecay = 0.05f, BodyLevel = -8f,
                ThumpHz = 96f, ThumpDecay = 0.03f, ThumpLevel = -15f,
                WetLevel = -3f, DragLevel = -3f,
            },
        };
    }

    /// <summary>
    /// Things going into water. Whatever goes in, it's the same few events at different sizes: a slap on the surface, a cavity of air shut in behind it, which rings as one big bubble and
    /// breaks into a froth of small ones (Minnaert again), and what was thrown up pattering back down a moment later.
    /// </summary>
    [SoundCollection]
    public static class Splashes
    {
        /// <summary>Someone stepping or running in from the bank.</summary>
        public static readonly SoundDefinition Wade = Add("splash.wade", take => Crown(take, 0.35f), -24f, 42f);

        /// <summary>A body going down in the water.</summary>
        public static readonly SoundDefinition Body = Add("splash.body", take => Crown(take, 1f), -19f, 60f);

        private static SoundDefinition Add(string id, SoundRecipe recipe, float target, float reach)
        {
            return SoundCatalog.Add(new SoundDefinition(id, recipe)
            {
                Bus = SoundBus.Foley,
                Takes = 5,
                PlaysPerTake = 2,
                PitchSpread = 1f,
                GainSpread = 1.5f,
                Mastering = Mastering.Default with { Target = target },
                NearDistance = 8f,
                FarDistance = reach,
                Spacing = 0.08f,
                ReverbSend = 0.16f,
            });
        }

        private static void Crown(Take take, float size)
        {
            SoundRandom random = take.Random;
            float slapDecay = 0.025f + 0.05f * size;

            // The surface hit flat.
            take.Mix(take.Pink(0.2f + 0.2f * size).Bandpass(Curve.Glide(2600f, 500f, 0.05f + 0.06f * size), 0.7f).Percussive(0.0012f, slapDecay).Normalize(0f), 0f, -3f);

            // The cavity closing: the larger the thing, the bigger the air shut in and the lower it rings.
            Signal cavity = take.Track(0.3f + 0.3f * size);
            Liquid.Bubble(cavity, 0.02f, random.LogRange(0.005f, 0.008f) * (1f + size * 1.5f), 1f, 0.25f);
            take.Mix(cavity, 0.03f + 0.05f * size, -8f);

            // Froth, thinning quickly.
            float froth = 0.3f + 0.35f * size;
            Curve energy = Curve.Settle(1f, 0f, 0.08f + 0.08f * size);
            take.Mix(Liquid.Bubbles(take, froth, energy * (1400f + 1000f * size), energy, 0.0006f, 0.008f + 0.006f * size, 2.3f, 0.14f).Normalize(0f), 0.015f, -5f);

            // What was thrown up, coming back down all over.
            float fall = 0.35f + 0.5f * size;
            Curve pattering = Curve.Hump(fall * 0.35f, fall);
            take.Mix(Liquid.Bubbles(take, fall, pattering * (120f + 160f * size), pattering, 0.0003f, 0.0016f, 2.4f, 0.25f).Normalize(0f), 0.1f + 0.1f * size, -12f);
        }
    }
}
