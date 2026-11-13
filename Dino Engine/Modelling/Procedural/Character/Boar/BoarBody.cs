using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Which boar to grow: a number, which is all its model's name says (<c>boar3</c>), and a seed worked out from it, so any thread on any machine grows the same animal from the name.
    /// </summary>
    public readonly record struct BoarSpec(int Variant, ulong Seed)
    {
        public const string Prefix = "boar";

        /// <summary>The boar a name means, or nothing if it doesn't name one. "boar" alone is the first of them.</summary>
        public static BoarSpec? Parse(string key)
        {
            if (!key.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            int variant = 0;
            if (key.Length > Prefix.Length && !int.TryParse(key.AsSpan(Prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out variant)) return null;
            ulong seed = 0xB0A5_7EE1UL;
            seed = (seed ^ (ulong)variant) * 0x100000001B3UL;
            seed = (seed ^ (seed >> 29)) * 0xBF58476D1CE4E5B9UL;
            return new BoarSpec(variant, seed);
        }

        /// <summary>The name its model is kept by, which every way of writing it comes to.</summary>
        public string Key => $"{Prefix}{Variant}";
    }

    /// <summary>A boar's colours, as albedos under a sun of about 13. A wild boar's coat is dark and grizzled, darker down the legs, along the mane and over the face, paler on the cheeks.</summary>
    public readonly record struct BoarHide(Vector3 Back, Vector3 Flank, Vector3 Belly, Vector3 Legs, Vector3 Face, Vector3 Cheek, Vector3 Disc, Vector3 Hoof, Vector3 Tusk, Vector3 Eye);

    /// <summary>One place along the trunk, from the side and from above: how high its top and its underside are and how wide it is there, in a boar's own measure.</summary>
    public readonly record struct BoarStation(float Z, float Top, float Bottom, float Half)
    {
        public float Middle => (Top + Bottom) * 0.5f;
        public float Deep => (Top - Bottom) * 0.5f;
    }

    /// <summary>
    /// What one wild boar is built like, drawn from its seed. Everything is in the units the game's world is in, where a person stands about 4.6 tall, facing +z with +x on its left and its feet
    /// at y = 0. A wild boar is a wedge: deep, narrow and high at the shoulders, falling away to smaller hips, on legs a good deal longer and slimmer than a farm pig's, with a long straight-faced head carried low and
    /// a short thick neck that's barely there. Its legs stand under the body, not out at its sides, as close together along it as it is tall at the withers. Its rig comes from the same numbers, so
    /// bones and flesh always agree.
    /// </summary>
    public sealed class BoarBody
    {
        public readonly ulong Seed;

        /// <summary>How old and how male it is, from 0 for a young sow to 1 for an old tusker: the tusks, the mane, the shoulders and the head all grow with it.</summary>
        public readonly float Age;

        /// <summary>To the top of the shoulders, under the bristles. Everything else is measured from it.</summary>
        public readonly float Withers;

        /// <summary>Along the body: where the forelegs and the hind legs stand, the front of the chest and the back of the rump. The middle of the body, where most of its weight is, is 0.</summary>
        public readonly float ForeZ, HindZ, ChestFront, RumpBack;

        /// <summary>How high the top of the rump is and the underside of the chest, and how wide the shoulders are either side.</summary>
        public readonly float RumpTop, Brisket, Breadth;

        /// <summary>The trunk from the side and from above, from the back of the rump to the throat.</summary>
        public readonly BoarStation[] Trunk;

        /// <summary>The left legs. The right are the same with x the other way. Each is a joint the rig bends at, and between them where the flesh is shaped from.</summary>
        public readonly Vector3 Shoulder, Elbow, Carpus, ForeFetlock, ForeCoronet;
        public readonly Vector3 Hip, Stifle, Hock, HindFetlock, HindCoronet;

        /// <summary>How high the top of a hoof is, which is where the foot's bone turns, and how far the hoof reaches in front of it and behind.</summary>
        public readonly float AnkleHeight, HoofFront, HoofBack;

        /// <summary>Where the head turns on the neck, which way it points, and how long it is from there to the end of the snout.</summary>
        public readonly Vector3 Atlas, HeadForward, HeadUp;
        public readonly float HeadLength;

        /// <summary>Where the neck turns, deep in the shoulders.</summary>
        public readonly Vector3 NeckRoot;

        /// <summary>How far the mane stands along the top of the neck and back, and how long the lower tusks, the ears and the tail are.</summary>
        public readonly float Mane, Tusks, Ears, TailLength;

        /// <summary>How far each ear leans out and forward, in radians.</summary>
        public readonly float EarSpread, EarLean;

        /// <summary>Where the tail leaves the rump, the joint halfway down it and its end.</summary>
        public readonly Vector3 TailRoot, TailMiddle, TailEnd;

        public readonly BoarHide Hide;

        /// <summary>Which of the coats it has: 0 grizzled brown, 1 russet, 2 nearly black, 3 grey-brown.</summary>
        public readonly int Coat;

        /// <summary>From the end of the snout to the back of the rump.</summary>
        public float Length => SnoutTip.Z - RumpBack;

        /// <summary>The middle of the disc at the end of the snout.</summary>
        public Vector3 SnoutTip => Atlas + HeadForward * HeadLength;

        /// <summary>A share of the withers, which is what every measurement here is.</summary>
        public float W(float share) => Withers * share;

        // A coat's colours, by how likely each is: a grizzled brown, a russet brown, nearly black, and a grey-brown grizzle. Warm and dark, as a wild boar is: the pale tips of its bristles
        // (the coat's TipTint) are what grizzle it, and a coat any paler reads as a grey hide with nothing growing on it.
        private static readonly BoarHide[] Hides =
        {
            new BoarHide(Back: V(0.066f, 0.051f, 0.039f), Flank: V(0.112f, 0.087f, 0.064f), Belly: V(0.1f, 0.079f, 0.06f), Legs: V(0.036f, 0.03f, 0.026f), Face: V(0.048f, 0.039f, 0.032f),
                Cheek: V(0.19f, 0.165f, 0.135f), Disc: V(0.19f, 0.125f, 0.115f), Hoof: V(0.03f, 0.026f, 0.023f), Tusk: V(0.62f, 0.58f, 0.48f), Eye: V(0.03f, 0.018f, 0.01f)),
            new BoarHide(Back: V(0.085f, 0.051f, 0.031f), Flank: V(0.14f, 0.085f, 0.051f), Belly: V(0.13f, 0.085f, 0.058f), Legs: V(0.042f, 0.03f, 0.024f), Face: V(0.06f, 0.041f, 0.031f),
                Cheek: V(0.2f, 0.152f, 0.105f), Disc: V(0.2f, 0.125f, 0.105f), Hoof: V(0.032f, 0.027f, 0.023f), Tusk: V(0.64f, 0.58f, 0.46f), Eye: V(0.035f, 0.018f, 0.008f)),
            new BoarHide(Back: V(0.04f, 0.035f, 0.031f), Flank: V(0.064f, 0.056f, 0.048f), Belly: V(0.066f, 0.058f, 0.05f), Legs: V(0.028f, 0.025f, 0.023f), Face: V(0.034f, 0.03f, 0.027f),
                Cheek: V(0.14f, 0.128f, 0.11f), Disc: V(0.15f, 0.1f, 0.095f), Hoof: V(0.026f, 0.024f, 0.022f), Tusk: V(0.6f, 0.56f, 0.47f), Eye: V(0.025f, 0.015f, 0.01f)),
            new BoarHide(Back: V(0.078f, 0.069f, 0.058f), Flank: V(0.122f, 0.108f, 0.09f), Belly: V(0.112f, 0.1f, 0.084f), Legs: V(0.042f, 0.038f, 0.034f), Face: V(0.054f, 0.048f, 0.042f),
                Cheek: V(0.22f, 0.198f, 0.17f), Disc: V(0.2f, 0.135f, 0.125f), Hoof: V(0.032f, 0.029f, 0.026f), Tusk: V(0.62f, 0.58f, 0.49f), Eye: V(0.03f, 0.018f, 0.01f)),
        };

        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        // Which coat each of the first few has, so that however few a pack draws from, most are grizzled brown, with a russet, a grey-brown and a black one among them: drawn by chance,
        // six came out half black and none brown.
        private static readonly int[] Coats = { 0, 1, 0, 3, 0, 2 };

        public BoarBody(BoarSpec spec)
        {
            Seed = spec.Seed;
            var genes = new Genes(spec.Seed);
            Age = genes.Unit("age");
            float size = genes.Range("size", 0.92f, 1.06f);
            Withers = 2.3f * size * (0.93f + 0.12f * Age);

            // The trunk, from the front of the chest to the back of the rump, is a little longer than the boar is tall, and its legs stand nearly three quarters of that apart, the forelegs
            // under the withers and the hind legs under the hips. The middle of it is nearer the forelegs, as a boar carries most of its weight in front. The trunk's rounded ends reach
            // a little past the places it's measured between.
            float trunk = W(genes.Range("trunk", 0.95f, 1.02f));
            float spacing = trunk * 0.74f;
            ForeZ = spacing * 0.42f;
            HindZ = -spacing * 0.58f;
            ChestFront = ForeZ + trunk * 0.14f;
            RumpBack = HindZ - trunk * 0.12f;

            // Its hips are as high as its shoulders, as its legs are as long behind as in front: the withers stand over its hips because of what's built up over the shoulder blades.
            Brisket = W(genes.Range("legs", 0.47f, 0.52f));
            float shoulderHeight = Brisket + W(0.15f);
            float hipHeight = shoulderHeight - W(0.02f);
            RumpTop = hipHeight + W(genes.Range("rump", 0.23f, 0.27f));
            Breadth = W(genes.Range("breadth", 0.17f, 0.195f) * (1f + 0.06f * Age));

            // From the back of the rump to the throat: the rump's rounded end, the hips with the flank tucked up in front of them, the loin, the barrel, the shoulders under the withers, and
            // the front of the chest, where the neck begins.
            float loin = HindZ + spacing * 0.3f, barrel = HindZ + spacing * 0.65f;
            Trunk = new[]
            {
                new BoarStation(RumpBack + W(0.06f), RumpTop - W(0.07f), Brisket + W(0.17f), Breadth * 0.42f),
                new BoarStation(HindZ, RumpTop, Brisket + W(0.09f), Breadth * 0.6f),
                new BoarStation(loin, MathHelper.Lerp(RumpTop, W(1f), 0.3f), Brisket + W(0.04f), Breadth * 0.76f),
                new BoarStation(barrel, MathHelper.Lerp(RumpTop, W(1f), 0.75f), Brisket, Breadth * 0.95f),
                new BoarStation(ForeZ - W(0.08f), W(1f), Brisket - W(0.005f), Breadth),
                new BoarStation(ChestFront - W(0.04f), W(0.9f), Brisket + W(0.08f), Breadth * 0.84f),
            };

            // The forelegs: the point of the shoulder, the elbow at the bottom of the chest, the knee (which is the wrist, and bends forward), the fetlock, and the top of the hoof. The leg is
            // built a little folded at the knee, so that it has somewhere to go when the shoulders come down.
            float foreX = Breadth * 0.55f;
            Shoulder = new Vector3(foreX, shoulderHeight, ForeZ);
            Elbow = new Vector3(foreX * 1.05f, Brisket + W(0.02f), ForeZ - W(0.04f));
            Carpus = new Vector3(foreX * 0.98f, W(0.25f), ForeZ + W(0.05f));
            ForeFetlock = new Vector3(foreX * 0.97f, W(0.12f), ForeZ - W(0.012f));
            AnkleHeight = W(0.055f);
            ForeCoronet = new Vector3(foreX * 0.97f, AnkleHeight, ForeZ);

            // The hind legs: the hip joint in the rump, the stifle (the knee, forward at the bottom of the flank), the hock (the heel, which bends back), the fetlock and the top of the hoof.
            float hindX = Breadth * 0.5f;
            Hip = new Vector3(hindX, hipHeight, HindZ);
            Stifle = new Vector3(hindX * 1.08f, Brisket + W(0.02f), HindZ + W(0.06f));
            Hock = new Vector3(hindX * 0.95f, W(0.29f), HindZ - W(0.07f));
            HindFetlock = new Vector3(hindX * 0.95f, W(0.12f), HindZ - W(0.015f));
            HindCoronet = new Vector3(hindX * 0.95f, AnkleHeight, HindZ);
            HoofFront = W(0.045f);
            HoofBack = W(0.02f);

            // The head is big, nearly half as long as the trunk, and hangs off the front of the shoulders on a neck that's short and all muscle, pointing down and forward: a wild boar
            // carries its snout well below its back, and the line of its face runs on up its neck to the withers.
            HeadLength = W(genes.Range("head", 0.55f, 0.6f) * (1f + 0.05f * Age));
            float carriage = genes.Range("carriage", 0.5f, 0.62f);
            HeadForward = new Vector3(0f, -MathF.Sin(carriage), MathF.Cos(carriage));
            HeadUp = new Vector3(0f, MathF.Cos(carriage), MathF.Sin(carriage));
            Atlas = new Vector3(0f, W(0.7f), ChestFront + W(0.13f));
            NeckRoot = new Vector3(0f, W(0.74f), ChestFront - W(0.06f));

            Mane = W((0.035f + 0.045f * Age) * genes.Range("mane", 0.8f, 1.25f));
            Tusks = HeadLength * (0.1f + 0.2f * Age) * genes.Range("tusks", 0.85f, 1.15f);
            Ears = HeadLength * genes.Range("ears", 0.25f, 0.3f);
            EarSpread = genes.Range("ear spread", 0.3f, 0.55f);
            EarLean = genes.Range("ear lean", 0.1f, 0.4f);

            // The tail leaves the top of the rump's end and hangs down and back, with a tuft at the end.
            TailLength = W(genes.Range("tail", 0.25f, 0.31f));
            TailRoot = new Vector3(0f, RumpTop - W(0.06f), RumpBack + W(0.035f));
            Vector3 hang = new Vector3(0f, -0.86f, -0.5f);
            TailMiddle = TailRoot + hang * (TailLength * 0.5f);
            TailEnd = TailMiddle + new Vector3(0f, -0.94f, -0.34f) * (TailLength * 0.5f);

            Coat = spec.Variant >= 0 && spec.Variant < Coats.Length ? Coats[spec.Variant] : genes.Pick("hide", 0.42f, 0.22f, 0.16f, 0.2f);
            BoarHide hide = Hides[Coat];
            float shade = genes.Range("shade", 0.88f, 1.12f);
            Hide = hide with
            {
                Back = hide.Back * shade, Flank = hide.Flank * shade, Belly = hide.Belly * shade, Legs = hide.Legs * shade, Face = hide.Face * shade, Cheek = hide.Cheek * MathF.Sqrt(shade),
            };
        }

        /// <summary>The same point on the right side.</summary>
        public static Vector3 Right(Vector3 left) => new Vector3(-left.X, left.Y, left.Z);

        /// <summary>A point given in the head's own frame (across, up, forward from the joint it turns on), where the body is.</summary>
        public Vector3 FromHead(Vector3 local) => Atlas + Vector3.UnitX * local.X + HeadUp * local.Y + HeadForward * local.Z;

        /// <summary>A point where the body is, in the head's own frame.</summary>
        public Vector3 ToHead(Vector3 p)
        {
            Vector3 d = p - Atlas;
            return new Vector3(d.X, Vector3.Dot(d, HeadUp), Vector3.Dot(d, HeadForward));
        }

        /// <summary>How high the top of the trunk is at a place along it, between the stations.</summary>
        public float TopAt(float z) => Station(z).Top;

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

        /// <summary>What its skin needs to know of it (<see cref="QuadrupedDress"/>): the line of it from the rump through the trunk to the head and the snout, and where its bones take over.</summary>
        public QuadrupedFrame Frame()
        {
            var line = new List<Vector3> { new Vector3(0f, Trunk[0].Middle, RumpBack) };
            foreach (BoarStation station in Trunk) line.Add(new Vector3(0f, station.Middle, station.Z));
            line.Add(Atlas);
            line.Add(SnoutTip);
            float spacing = ForeZ - HindZ;
            return new QuadrupedFrame
            {
                Withers = Withers, Breadth = Breadth, Line = line.ToArray(),
                SpineFrom = new Vector3(0f, Hip.Y, HindZ + spacing * 0.3f), ChestFrom = new Vector3(0f, Shoulder.Y, ForeZ - spacing * 0.25f), NeckRoot = NeckRoot, Atlas = Atlas,
                HeadForward = HeadForward, HeadUp = HeadUp, HeadLength = HeadLength,
                Shoulder = Shoulder, Carpus = Carpus, ForeCoronet = ForeCoronet, Hip = Hip, Hock = Hock, HindCoronet = HindCoronet,
            };
        }

        /// <summary>
        /// Where its joints are, on the same bones as people and every other animal, so everything that moves a character moves it. The forelegs are the arms: they hang from the chest, and bend
        /// forward at the knee as a hoofed animal's do, and the hind legs bend back at the hock. Built standing, as it is, which is where a skin bends best from.
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
            Set(BoneType.HandL, ForeCoronet);
            Set(BoneType.UpperArmR, Right(Shoulder));
            Set(BoneType.LowerArmR, Right(Carpus));
            Set(BoneType.HandR, Right(ForeCoronet));

            Set(BoneType.UpperLegL, Hip);
            Set(BoneType.LowerLegL, Hock);
            Set(BoneType.FootL, HindCoronet);
            Set(BoneType.UpperLegR, Right(Hip));
            Set(BoneType.LowerLegR, Right(Hock));
            Set(BoneType.FootR, Right(HindCoronet));

            return new Rig(BodyPlan.Quadruped, bones, AnkleHeight, HoofFront, HoofBack);
        }
    }
}
