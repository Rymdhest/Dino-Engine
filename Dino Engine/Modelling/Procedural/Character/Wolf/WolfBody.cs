using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Which wolf to grow: a number, and whether it's a dire wolf, which is all its model's name says (<c>wolf3</c>, <c>direwolf1</c>), and a seed worked out from them, so any thread on any machine
    /// grows the same animal from the name.
    /// </summary>
    public readonly record struct WolfSpec(int Variant, ulong Seed, bool Dire)
    {
        public const string Prefix = "wolf";
        public const string DirePrefix = "direwolf";

        /// <summary>The wolf a name means, or nothing if it doesn't name one. "wolf" or "direwolf" alone is the first of them.</summary>
        public static WolfSpec? Parse(string key)
        {
            bool dire = key.StartsWith(DirePrefix, StringComparison.Ordinal);
            if (!dire && !key.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            int length = dire ? DirePrefix.Length : Prefix.Length;
            int variant = 0;
            if (key.Length > length && !int.TryParse(key.AsSpan(length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out variant)) return null;
            ulong seed = dire ? 0xD12E_3A1FUL : 0x3A1F_C0DEUL;
            seed = (seed ^ (ulong)variant) * 0x100000001B3UL;
            seed = (seed ^ (seed >> 29)) * 0xBF58476D1CE4E5B9UL;
            return new WolfSpec(variant, seed, dire);
        }

        /// <summary>The name its model is kept by, which every way of writing it comes to.</summary>
        public string Key => $"{(Dire ? DirePrefix : Prefix)}{Variant}";
    }

    /// <summary>
    /// A wolf's colours, as albedos under a sun of about 13: a darker saddle over the back, grizzled grey or tawny flanks, pale underparts, legs and cheeks (the mask a wolf's face is known by),
    /// a darker muzzle and brow, black nose and lips, amber eyes, and a black tip to the tail.
    /// </summary>
    public readonly record struct WolfHide(Vector3 Back, Vector3 Flank, Vector3 Belly, Vector3 Legs, Vector3 Face, Vector3 Muzzle, Vector3 Ears, Vector3 Nose, Vector3 Eye, Vector3 TailTip);

    /// <summary>
    /// What one wolf is built like, drawn from its seed, in the units the game's world is in, facing +z with +x on its left and its feet at y = 0. A wolf is long in the leg and short in the body,
    /// a trunk about as long as it's tall standing on legs that are more than half its height: a deep, narrow chest, a waist drawn up sharply in front of the hips, a level back, a long neck carried
    /// up and forward, a broad skull with a long narrow muzzle, big upright ears, and a bushy tail hanging to its hocks. It stands on its toes. A dire wolf is the same, a third bigger, heavier in the
    /// shoulder and head and darker, with its hackles up. Its rig comes from the same numbers, so bones and flesh always agree.
    /// </summary>
    public sealed class WolfBody
    {
        public readonly ulong Seed;
        public readonly bool Dire;

        /// <summary>How heavy it's built, from 0 for a lean yearling to 1 for a big male: the chest, the head and the ruff all grow with it.</summary>
        public readonly float Build;

        /// <summary>To the top of the shoulders, under the coat. Everything else is measured from it.</summary>
        public readonly float Withers;

        /// <summary>Along the body: where the forelegs and the hind legs stand, the front of the chest and the back of the rump.</summary>
        public readonly float ForeZ, HindZ, ChestFront, RumpBack;

        /// <summary>How high the top of the rump is and the underside of the chest, and how wide the chest is either side.</summary>
        public readonly float RumpTop, Brisket, Breadth;

        /// <summary>The trunk from the side and from above, from the back of the rump to the throat.</summary>
        public readonly BoarStation[] Trunk;

        /// <summary>The left legs: the joints the rig bends at, and between them where the flesh is shaped from. The right are the same with x the other way.</summary>
        public readonly Vector3 Shoulder, Elbow, Carpus, ForePaw;
        public readonly Vector3 Hip, Stifle, Hock, HindPaw;

        /// <summary>How high the top of a paw is, which is where the foot's bone turns, and how far the paw reaches in front of that and behind.</summary>
        public readonly float AnkleHeight, PawFront, PawBack;

        /// <summary>Where the head turns on the neck, which way it points, and how long it is from there to the end of the nose.</summary>
        public readonly Vector3 Atlas, HeadForward, HeadUp;
        public readonly float HeadLength;

        /// <summary>Where the neck turns, at the root of it in the shoulders.</summary>
        public readonly Vector3 NeckRoot;

        /// <summary>How long the ears are, and how far each leans out and forward, in radians.</summary>
        public readonly float Ears, EarSpread, EarLean;

        /// <summary>How long the tail is and how thick at its bushiest; where it leaves the rump, the joint halfway down it and its end.</summary>
        public readonly float TailLength, TailBush;
        public readonly Vector3 TailRoot, TailMiddle, TailEnd;

        /// <summary>How far a dire wolf's hackles stand along its neck and shoulders, and nothing on a wolf.</summary>
        public readonly float Hackles;

        public readonly WolfHide Hide;

        /// <summary>Which of the coats it has: for a wolf 0 grey, 1 tawny, 2 black, 3 pale; for a dire wolf 0 iron-grey, 1 black-brown, 2 black.</summary>
        public readonly int Coat;

        /// <summary>From the end of the nose to the back of the rump.</summary>
        public float Length => NoseTip.Z - RumpBack;

        /// <summary>The middle of the end of the nose.</summary>
        public Vector3 NoseTip => Atlas + HeadForward * HeadLength;

        /// <summary>A share of the withers, which is what every measurement here is.</summary>
        public float W(float share) => Withers * share;

        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        private static readonly WolfHide[] WolfHides =
        {
            new WolfHide(Back: V(0.13f, 0.12f, 0.105f), Flank: V(0.22f, 0.2f, 0.17f), Belly: V(0.38f, 0.35f, 0.29f), Legs: V(0.32f, 0.26f, 0.19f), Face: V(0.36f, 0.33f, 0.28f),
                Muzzle: V(0.2f, 0.17f, 0.13f), Ears: V(0.17f, 0.14f, 0.1f), Nose: V(0.02f, 0.02f, 0.02f), Eye: V(0.5f, 0.32f, 0.06f), TailTip: V(0.03f, 0.03f, 0.03f)),
            new WolfHide(Back: V(0.13f, 0.105f, 0.08f), Flank: V(0.23f, 0.18f, 0.12f), Belly: V(0.37f, 0.31f, 0.23f), Legs: V(0.32f, 0.24f, 0.15f), Face: V(0.35f, 0.29f, 0.22f),
                Muzzle: V(0.2f, 0.15f, 0.09f), Ears: V(0.17f, 0.12f, 0.07f), Nose: V(0.02f, 0.02f, 0.02f), Eye: V(0.52f, 0.33f, 0.05f), TailTip: V(0.03f, 0.028f, 0.026f)),
            new WolfHide(Back: V(0.03f, 0.03f, 0.03f), Flank: V(0.048f, 0.046f, 0.044f), Belly: V(0.07f, 0.065f, 0.06f), Legs: V(0.052f, 0.049f, 0.046f), Face: V(0.07f, 0.065f, 0.06f),
                Muzzle: V(0.04f, 0.038f, 0.036f), Ears: V(0.034f, 0.034f, 0.034f), Nose: V(0.018f, 0.018f, 0.018f), Eye: V(0.55f, 0.38f, 0.08f), TailTip: V(0.022f, 0.022f, 0.022f)),
            new WolfHide(Back: V(0.26f, 0.25f, 0.23f), Flank: V(0.38f, 0.36f, 0.32f), Belly: V(0.48f, 0.46f, 0.42f), Legs: V(0.42f, 0.39f, 0.34f), Face: V(0.46f, 0.44f, 0.4f),
                Muzzle: V(0.32f, 0.29f, 0.24f), Ears: V(0.3f, 0.27f, 0.22f), Nose: V(0.02f, 0.02f, 0.02f), Eye: V(0.5f, 0.34f, 0.08f), TailTip: V(0.12f, 0.11f, 0.1f)),
        };

        private static readonly WolfHide[] DireHides =
        {
            new WolfHide(Back: V(0.07f, 0.07f, 0.072f), Flank: V(0.13f, 0.125f, 0.12f), Belly: V(0.2f, 0.19f, 0.18f), Legs: V(0.13f, 0.12f, 0.11f), Face: V(0.18f, 0.175f, 0.168f),
                Muzzle: V(0.08f, 0.077f, 0.074f), Ears: V(0.06f, 0.058f, 0.058f), Nose: V(0.015f, 0.015f, 0.015f), Eye: V(0.6f, 0.5f, 0.16f), TailTip: V(0.025f, 0.025f, 0.025f)),
            new WolfHide(Back: V(0.042f, 0.034f, 0.027f), Flank: V(0.082f, 0.063f, 0.048f), Belly: V(0.12f, 0.098f, 0.076f), Legs: V(0.07f, 0.055f, 0.043f), Face: V(0.11f, 0.09f, 0.072f),
                Muzzle: V(0.048f, 0.039f, 0.032f), Ears: V(0.04f, 0.033f, 0.028f), Nose: V(0.015f, 0.015f, 0.015f), Eye: V(0.62f, 0.46f, 0.12f), TailTip: V(0.02f, 0.018f, 0.016f)),
            new WolfHide(Back: V(0.02f, 0.02f, 0.021f), Flank: V(0.032f, 0.031f, 0.031f), Belly: V(0.05f, 0.048f, 0.047f), Legs: V(0.034f, 0.033f, 0.032f), Face: V(0.05f, 0.048f, 0.047f),
                Muzzle: V(0.028f, 0.027f, 0.027f), Ears: V(0.024f, 0.024f, 0.024f), Nose: V(0.014f, 0.014f, 0.014f), Eye: V(0.64f, 0.54f, 0.18f), TailTip: V(0.018f, 0.018f, 0.018f)),
        };

        // Which coat each of the first few has, so that however few a pack draws from, most wolves are grey, with a tawny, a pale and a black one among them, and dire wolves mostly iron-grey.
        private static readonly int[] WolfCoats = { 0, 1, 0, 3, 0, 2 };
        private static readonly int[] DireCoats = { 0, 1, 2, 0 };

        public WolfBody(WolfSpec spec)
        {
            Seed = spec.Seed;
            Dire = spec.Dire;
            var genes = new Genes(spec.Seed);
            Build = Dire ? genes.Range("build", 0.6f, 1f) : genes.Unit("build");
            float size = genes.Range("size", 0.92f, 1.07f);
            Withers = 2.05f * size * (0.95f + 0.1f * Build) * (Dire ? 1.32f : 1f);

            // The trunk is about as long as the wolf is tall, and its legs stand most of that apart, the forelegs under the withers and the hind legs under the hips. The middle of it is a
            // little nearer the forelegs, which carry more of it. The trunk's rounded ends reach a little past the places it's measured between.
            float trunk = W(genes.Range("trunk", 0.95f, 1.02f));
            float spacing = trunk * 0.78f;
            ForeZ = spacing * 0.45f;
            HindZ = -spacing * 0.55f;
            ChestFront = ForeZ + trunk * 0.13f;
            RumpBack = HindZ - trunk * 0.09f;

            // A deep chest on long legs, hips as high as the shoulders and a back that's nearly level, falling a little to the tail.
            Brisket = W(genes.Range("legs", 0.54f, 0.58f));
            float shoulderHeight = Brisket + W(0.14f);
            float hipHeight = shoulderHeight - W(0.02f);
            RumpTop = hipHeight + W(genes.Range("rump", 0.22f, 0.25f));
            Breadth = W(genes.Range("breadth", 0.13f, 0.15f) * (1f + 0.12f * Build) * (Dire ? 1.12f : 1f));

            // From the back of the rump to the throat: the croup, the hips with the waist drawn up sharply in front of them, the loin, the ribs, the shoulders under the withers, and the front of
            // the chest, where the neck begins.
            float loin = HindZ + spacing * 0.3f, ribs = HindZ + spacing * 0.62f;
            Trunk = new[]
            {
                new BoarStation(RumpBack + W(0.05f), RumpTop - W(0.06f), Brisket + W(0.16f), Breadth * 0.62f),
                new BoarStation(HindZ, RumpTop, Brisket + W(0.13f), Breadth * 0.8f),
                new BoarStation(loin, MathHelper.Lerp(RumpTop, W(1f), 0.4f), Brisket + W(0.1f), Breadth * 0.8f),
                new BoarStation(ribs, MathHelper.Lerp(RumpTop, W(1f), 0.8f), Brisket + W(0.01f), Breadth * 0.96f),
                new BoarStation(ForeZ - W(0.06f), W(1f), Brisket - W(0.005f), Breadth),
                new BoarStation(ChestFront - W(0.04f), W(0.93f), Brisket + W(0.06f), Breadth * 0.86f),
            };

            // The forelegs: the point of the shoulder, the elbow at the bottom of the chest, the knee (the wrist, which bends forward), and the top of the paw. Built a little folded at the
            // knee, so the leg has somewhere to go when the shoulders come down.
            float foreX = Breadth * 0.62f;
            Shoulder = new Vector3(foreX, shoulderHeight, ForeZ);
            Elbow = new Vector3(foreX * 1.05f, Brisket, ForeZ - W(0.05f));
            Carpus = new Vector3(foreX * 0.95f, W(0.22f), ForeZ + W(0.06f));
            AnkleHeight = W(0.065f);
            ForePaw = new Vector3(foreX * 0.95f, AnkleHeight, ForeZ + W(0.03f));

            // The hind legs: the hip joint, the stifle forward at the bottom of the flank, the hock well behind (a wolf's hind leg is long and angled), and the top of the paw.
            float hindX = Breadth * 0.58f;
            Hip = new Vector3(hindX, hipHeight, HindZ);
            Stifle = new Vector3(hindX * 1.1f, Brisket + W(0.06f), HindZ + W(0.1f));
            Hock = new Vector3(hindX * 0.95f, W(0.27f), HindZ - W(0.1f));
            HindPaw = new Vector3(hindX * 0.95f, AnkleHeight, HindZ - W(0.01f));
            PawFront = W(0.07f);
            PawBack = W(0.025f);

            // The head on a long neck carried up and forward, held nearly level: a broad skull, a long narrow muzzle. A dire wolf's is bigger for its size.
            HeadLength = W(genes.Range("head", 0.4f, 0.45f) * (1f + 0.04f * Build) * (Dire ? 1.08f : 1f));
            float carriage = genes.Range("carriage", 0.25f, 0.4f);
            HeadForward = new Vector3(0f, -MathF.Sin(carriage), MathF.Cos(carriage));
            HeadUp = new Vector3(0f, MathF.Cos(carriage), MathF.Sin(carriage));
            Atlas = new Vector3(0f, W(1.0f), ChestFront + W(0.2f));
            NeckRoot = new Vector3(0f, W(0.82f), ChestFront - W(0.06f));

            Ears = HeadLength * genes.Range("ears", 0.3f, 0.36f) * (Dire ? 0.92f : 1f);
            EarSpread = genes.Range("ear spread", 0.12f, 0.3f);
            EarLean = genes.Range("ear lean", 0.04f, 0.2f);

            // The tail leaves the top of the rump's end, hangs down and back, and is thick with hair most of the way to its black tip.
            TailLength = W(genes.Range("tail", 0.5f, 0.6f));
            TailBush = W(genes.Range("bush", 0.065f, 0.08f) * (Dire ? 1.1f : 1f));
            TailRoot = new Vector3(0f, RumpTop - W(0.05f), RumpBack + W(0.04f));
            TailMiddle = TailRoot + new Vector3(0f, -0.84f, -0.54f).Normalized() * (TailLength * 0.5f);
            TailEnd = TailMiddle + new Vector3(0f, -0.97f, -0.24f).Normalized() * (TailLength * 0.5f);

            Hackles = Dire ? W(genes.Range("hackles", 0.07f, 0.1f)) : 0f;

            WolfHide[] hides = Dire ? DireHides : WolfHides;
            int[] coats = Dire ? DireCoats : WolfCoats;
            Coat = spec.Variant >= 0 && spec.Variant < coats.Length ? coats[spec.Variant] : Dire ? genes.Pick("hide", 0.5f, 0.3f, 0.2f) : genes.Pick("hide", 0.45f, 0.2f, 0.15f, 0.2f);
            WolfHide hide = hides[Coat];
            float shade = genes.Range("shade", 0.9f, 1.1f);
            Hide = hide with { Back = hide.Back * shade, Flank = hide.Flank * shade, Belly = hide.Belly * shade, Legs = hide.Legs * shade, Face = hide.Face * shade, Muzzle = hide.Muzzle * shade };
        }

        /// <summary>The same point on the right side.</summary>
        public static Vector3 Right(Vector3 left) => QuadrupedFrame.Right(left);

        /// <summary>A point given in the head's own frame (across, up, forward from the joint it turns on), where the body is.</summary>
        public Vector3 FromHead(Vector3 local) => Atlas + Vector3.UnitX * local.X + HeadUp * local.Y + HeadForward * local.Z;

        /// <summary>A point where the body is, in the head's own frame.</summary>
        public Vector3 ToHead(Vector3 p)
        {
            Vector3 d = p - Atlas;
            return new Vector3(d.X, Vector3.Dot(d, HeadUp), Vector3.Dot(d, HeadForward));
        }

        /// <summary>The trunk at a place along it, between the stations either side.</summary>
        public BoarStation Station(float z)
        {
            if (z <= Trunk[0].Z) return Trunk[0];
            for (int i = 0; i + 1 < Trunk.Length; i++)
            {
                if (z > Trunk[i + 1].Z) continue;
                float t = (z - Trunk[i].Z) / MathF.Max(Trunk[i + 1].Z - Trunk[i].Z, 1e-4f);
                t = t * t * (3f - 2f * t);
                BoarStation a = Trunk[i], b = Trunk[i + 1];
                return new BoarStation(z, MathHelper.Lerp(a.Top, b.Top, t), MathHelper.Lerp(a.Bottom, b.Bottom, t), MathHelper.Lerp(a.Half, b.Half, t));
            }
            return Trunk[^1];
        }

        /// <summary>What its skin needs to know of it (<see cref="QuadrupedDress"/>).</summary>
        public QuadrupedFrame Frame()
        {
            var line = new List<Vector3> { new Vector3(0f, Trunk[0].Middle, RumpBack) };
            foreach (BoarStation station in Trunk) line.Add(new Vector3(0f, station.Middle, station.Z));
            line.Add(Atlas);
            line.Add(NoseTip);
            float spacing = ForeZ - HindZ;
            return new QuadrupedFrame
            {
                Withers = Withers, Breadth = Breadth, Line = line.ToArray(),
                SpineFrom = new Vector3(0f, Hip.Y, HindZ + spacing * 0.3f), ChestFrom = new Vector3(0f, Shoulder.Y, ForeZ - spacing * 0.25f), NeckRoot = NeckRoot, Atlas = Atlas,
                HeadForward = HeadForward, HeadUp = HeadUp, HeadLength = HeadLength,
                Shoulder = Shoulder, Carpus = Carpus, ForeCoronet = ForePaw, Hip = Hip, Hock = Hock, HindCoronet = HindPaw,
            };
        }

        /// <summary>
        /// Where its joints are, on the same bones as people and every other animal. The forelegs are the arms: they hang from the chest and bend forward at the knee, which is the wrist, and the
        /// hind legs bend back at the hock. Built standing, which is where a skin bends best from.
        /// </summary>
        public Rig BuildRig()
        {
            var bones = new Vector3[Rig.BoneCount];
            void Set(BoneType bone, Vector3 at) => bones[(int)bone] = at;

            Vector3 chest = new Vector3(0f, Shoulder.Y, ForeZ);
            Set(BoneType.Pelvis, new Vector3(0f, Hip.Y, HindZ));
            Set(BoneType.Spine, new Vector3(0f, (Hip.Y + Shoulder.Y) * 0.5f + W(0.04f), (HindZ + ForeZ) * 0.5f));
            Set(BoneType.Chest, chest);
            Set(BoneType.Neck, NeckRoot);
            Set(BoneType.Head, Atlas);
            Set(BoneType.ClavicleL, chest);
            Set(BoneType.ClavicleR, chest);
            Set(BoneType.TailBase, TailRoot);
            Set(BoneType.TailTip, TailMiddle);

            Set(BoneType.UpperArmL, Shoulder);
            Set(BoneType.LowerArmL, Carpus);
            Set(BoneType.HandL, ForePaw);
            Set(BoneType.UpperArmR, Right(Shoulder));
            Set(BoneType.LowerArmR, Right(Carpus));
            Set(BoneType.HandR, Right(ForePaw));

            Set(BoneType.UpperLegL, Hip);
            Set(BoneType.LowerLegL, Hock);
            Set(BoneType.FootL, HindPaw);
            Set(BoneType.UpperLegR, Right(Hip));
            Set(BoneType.LowerLegR, Right(Hock));
            Set(BoneType.FootR, Right(HindPaw));

            return new Rig(BodyPlan.Quadruped, bones, AnkleHeight, PawFront, PawBack);
        }
    }
}
