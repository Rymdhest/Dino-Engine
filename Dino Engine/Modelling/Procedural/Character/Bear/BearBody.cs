using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Which bear to grow: a number, which is all its model's name says (<c>bear2</c>), and a seed worked out from it, so any thread on any machine grows the same animal from the name.
    /// </summary>
    public readonly record struct BearSpec(int Variant, ulong Seed)
    {
        public const string Prefix = "bear";

        /// <summary>The bear a name means, or nothing if it doesn't name one. "bear" alone is the first of them.</summary>
        public static BearSpec? Parse(string key)
        {
            if (!key.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            int variant = 0;
            if (key.Length > Prefix.Length && !int.TryParse(key.AsSpan(Prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out variant)) return null;
            ulong seed = 0xBEA2_CA7EUL;
            seed = (seed ^ (ulong)variant) * 0x100000001B3UL;
            seed = (seed ^ (seed >> 29)) * 0xBF58476D1CE4E5B9UL;
            return new BearSpec(variant, seed);
        }

        /// <summary>The name its model is kept by, which every way of writing it comes to.</summary>
        public string Key => $"{Prefix}{Variant}";
    }

    /// <summary>
    /// A bear's colours, as albedos under a sun of about 13: a shaggy coat paler over the hump and the back where the tips of the hair catch the light, darker down the legs, a paler muzzle, a black
    /// nose, lips and pads, and pale horn claws.
    /// </summary>
    public readonly record struct BearHide(Vector3 Back, Vector3 Flank, Vector3 Belly, Vector3 Legs, Vector3 Face, Vector3 Muzzle, Vector3 Ears, Vector3 Nose, Vector3 Eye, Vector3 Claw);

    /// <summary>
    /// What one cave bear is built like, drawn from its seed, in the units the game's world is in, facing +z with +x on its left and its feet at y = 0. A bear is massive and long in the body, a
    /// hump of muscle over the shoulders standing above a back that falls away to a broad rump, on short, thick legs as long behind as in front but set lower; it walks on the whole sole of its foot,
    /// heel and all, with long claws on its toes, and carries a broad, domed head low on a short neck: a steep brow, a short thick muzzle, small round ears set wide. It hardly has a tail. Its rig comes
    /// from the same numbers, so bones and flesh always agree.
    /// </summary>
    public sealed class BearBody
    {
        public readonly ulong Seed;

        /// <summary>How heavy it's built, from 0 for a lean young bear to 1 for an old male: the hump, the head and the girth all grow with it.</summary>
        public readonly float Build;

        /// <summary>To the top of the hump, under the coat. Everything else is measured from it.</summary>
        public readonly float Withers;

        /// <summary>Along the body: where the forelegs and the hind legs stand, the front of the chest and the back of the rump.</summary>
        public readonly float ForeZ, HindZ, ChestFront, RumpBack;

        /// <summary>How high the top of the rump is and the underside of the chest, and how wide the body is either side.</summary>
        public readonly float RumpTop, Brisket, Breadth;

        /// <summary>The trunk from the side and from above, from the back of the rump to the throat.</summary>
        public readonly BoarStation[] Trunk;

        /// <summary>
        /// The left legs. A bear stands on its soles, so its legs bend where every animal's here do, forward at the wrist and back at the ankle, with the elbow and the knee held in the bones above
        /// them: the wrist is just above the paw, and the foot's bone is the toes, turning at the ball of the foot, with the sole behind it going with the ankle.
        /// </summary>
        public readonly Vector3 Shoulder, Elbow, Carpus, ForePaw;
        public readonly Vector3 Hip, Knee, Ankle, HindPaw;

        /// <summary>How high the ball of a foot is, where its toes turn, and how far a paw reaches in front of that and behind.</summary>
        public readonly float AnkleHeight, PawFront, PawBack;

        /// <summary>Where the head turns on the neck, which way it points, and how long it is from there to the end of the nose.</summary>
        public readonly Vector3 Atlas, HeadForward, HeadUp;
        public readonly float HeadLength;

        /// <summary>Where the neck turns, deep in the shoulders under the hump.</summary>
        public readonly Vector3 NeckRoot;

        /// <summary>How long the ears are, how far the hump stands, and how long the claws of the fore and hind paws are.</summary>
        public readonly float Ears, Hump, ForeClaws, HindClaws;

        /// <summary>The stub of a tail: where it leaves the rump, the joint in it and its end.</summary>
        public readonly Vector3 TailRoot, TailMiddle, TailEnd;

        public readonly BearHide Hide;

        /// <summary>Which of the coats it has: 0 brown, 1 nearly black, 2 grizzled, 3 tawny.</summary>
        public readonly int Coat;

        /// <summary>From the end of the nose to the back of the rump.</summary>
        public float Length => NoseTip.Z - RumpBack;

        /// <summary>The middle of the end of the nose.</summary>
        public Vector3 NoseTip => Atlas + HeadForward * HeadLength;

        /// <summary>A share of the withers, which is what every measurement here is.</summary>
        public float W(float share) => Withers * share;

        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        private static readonly BearHide[] Hides =
        {
            new BearHide(Back: V(0.086f, 0.056f, 0.034f), Flank: V(0.079f, 0.051f, 0.032f), Belly: V(0.059f, 0.04f, 0.025f), Legs: V(0.036f, 0.024f, 0.017f), Face: V(0.079f, 0.053f, 0.033f),
                Muzzle: V(0.112f, 0.079f, 0.053f), Ears: V(0.05f, 0.033f, 0.021f), Nose: V(0.02f, 0.018f, 0.017f), Eye: V(0.03f, 0.018f, 0.01f), Claw: V(0.32f, 0.28f, 0.22f)),
            new BearHide(Back: V(0.04f, 0.029f, 0.021f), Flank: V(0.034f, 0.025f, 0.018f), Belly: V(0.028f, 0.02f, 0.016f), Legs: V(0.02f, 0.015f, 0.012f), Face: V(0.038f, 0.028f, 0.02f),
                Muzzle: V(0.063f, 0.048f, 0.034f), Ears: V(0.024f, 0.018f, 0.013f), Nose: V(0.018f, 0.016f, 0.015f), Eye: V(0.03f, 0.018f, 0.01f), Claw: V(0.3f, 0.27f, 0.22f)),
            new BearHide(Back: V(0.125f, 0.099f, 0.066f), Flank: V(0.086f, 0.059f, 0.036f), Belly: V(0.053f, 0.036f, 0.023f), Legs: V(0.028f, 0.02f, 0.015f), Face: V(0.112f, 0.086f, 0.056f),
                Muzzle: V(0.125f, 0.092f, 0.059f), Ears: V(0.046f, 0.033f, 0.023f), Nose: V(0.02f, 0.018f, 0.017f), Eye: V(0.03f, 0.018f, 0.01f), Claw: V(0.34f, 0.3f, 0.24f)),
            new BearHide(Back: V(0.139f, 0.099f, 0.059f), Flank: V(0.125f, 0.086f, 0.05f), Belly: V(0.099f, 0.066f, 0.04f), Legs: V(0.053f, 0.036f, 0.023f), Face: V(0.132f, 0.092f, 0.056f),
                Muzzle: V(0.152f, 0.112f, 0.066f), Ears: V(0.079f, 0.056f, 0.033f), Nose: V(0.02f, 0.018f, 0.017f), Eye: V(0.03f, 0.018f, 0.01f), Claw: V(0.34f, 0.3f, 0.24f)),
        };

        // Which coat each of the first few has, so that whichever leads a pack it's most often brown, with a grizzled, a black and a tawny one among them.
        private static readonly int[] Coats = { 0, 2, 1, 3 };

        public BearBody(BearSpec spec)
        {
            Seed = spec.Seed;
            var genes = new Genes(spec.Seed);
            Build = genes.Unit("build");
            float size = genes.Range("size", 0.92f, 1.08f);
            Withers = 3.2f * size * (0.94f + 0.12f * Build);

            // A long trunk on legs that stand most of it apart, the middle of it nearer the forelegs, which carry the weight of the head and the hump.
            float trunk = W(genes.Range("trunk", 1.12f, 1.22f));
            float spacing = trunk * 0.72f;
            ForeZ = spacing * 0.44f;
            HindZ = -spacing * 0.56f;
            ChestFront = ForeZ + trunk * 0.14f;
            RumpBack = HindZ - trunk * 0.13f;

            // A deep chest on short legs, the hips lower than the shoulders, the hump the highest thing about it and the back falling from it to the rump.
            Brisket = W(genes.Range("legs", 0.4f, 0.44f));
            float shoulderHeight = Brisket + W(0.17f);
            float hipHeight = shoulderHeight - W(0.05f);
            RumpTop = hipHeight + W(genes.Range("rump", 0.26f, 0.3f));
            Breadth = W(genes.Range("breadth", 0.23f, 0.26f) * (1f + 0.08f * Build));
            Hump = W(genes.Range("hump", 0.12f, 0.16f) * (0.85f + 0.3f * Build));

            float loin = HindZ + spacing * 0.3f, barrel = HindZ + spacing * 0.65f;
            Trunk = new[]
            {
                new BoarStation(RumpBack + W(0.08f), RumpTop - W(0.08f), Brisket + W(0.15f), Breadth * 0.74f),
                new BoarStation(HindZ, RumpTop, Brisket + W(0.06f), Breadth * 0.9f),
                new BoarStation(loin, MathHelper.Lerp(RumpTop, W(0.93f), 0.3f), Brisket + W(0.02f), Breadth * 0.95f),
                new BoarStation(barrel, MathHelper.Lerp(RumpTop, W(0.95f), 0.7f), Brisket - W(0.01f), Breadth),
                new BoarStation(ForeZ - W(0.04f), W(0.95f), Brisket, Breadth * 0.98f),
                new BoarStation(ChestFront - W(0.05f), W(0.84f), Brisket + W(0.08f), Breadth * 0.86f),
            };

            // The forelegs: the point of the shoulder, the elbow held in at the bottom of the chest, the wrist just above the paw (built a little forward of the line, which is the way it bends),
            // and the palm, where the paw turns.
            float foreX = Breadth * 0.6f;
            Shoulder = new Vector3(foreX, shoulderHeight, ForeZ);
            Elbow = new Vector3(foreX * 1.08f, Brisket + W(0.02f), ForeZ - W(0.07f));
            Carpus = new Vector3(foreX * 0.98f, W(0.11f), ForeZ + W(0.07f));
            AnkleHeight = W(0.035f);
            ForePaw = new Vector3(foreX * 0.98f, AnkleHeight, ForeZ + W(0.02f));

            // The hind legs: the hip, the knee forward at the bottom of the flank, the ankle just above the heel (well behind the line, which is the way it bends), and the ball of the foot.
            float hindX = Breadth * 0.6f;
            Hip = new Vector3(hindX, hipHeight, HindZ);
            Knee = new Vector3(hindX * 1.05f, Brisket + W(0.04f), HindZ + W(0.1f));
            Ankle = new Vector3(hindX * 0.98f, W(0.09f), HindZ - W(0.08f));
            HindPaw = new Vector3(hindX * 0.98f, W(0.03f), HindZ + W(0.07f));
            // The foot's bone is its toes, so a step rolls over no more than their length in front of the ball, and nothing behind it, as the sole behind the ball goes with the ankle: given the
            // whole paw, every step rolled the ankle a third of a metre up, and the hips went with it.
            PawFront = W(0.06f);
            PawBack = W(0.015f);

            // A broad, domed head with a steep brow and a short muzzle, carried low on a short, thick neck.
            HeadLength = W(genes.Range("head", 0.36f, 0.4f) * (1f + 0.05f * Build));
            float carriage = genes.Range("carriage", 0.3f, 0.45f);
            HeadForward = new Vector3(0f, -MathF.Sin(carriage), MathF.Cos(carriage));
            HeadUp = new Vector3(0f, MathF.Cos(carriage), MathF.Sin(carriage));
            Atlas = new Vector3(0f, W(0.72f), ChestFront + W(0.12f));
            NeckRoot = new Vector3(0f, W(0.78f), ChestFront - W(0.08f));

            Ears = HeadLength * genes.Range("ears", 0.17f, 0.21f);
            ForeClaws = W(genes.Range("claws", 0.05f, 0.065f));
            HindClaws = ForeClaws * 0.75f;

            // A stub of a tail, hidden in the coat at the top of the rump.
            TailRoot = new Vector3(0f, RumpTop - W(0.08f), RumpBack + W(0.05f));
            TailMiddle = TailRoot + new Vector3(0f, -0.5f, -0.86f) * W(0.04f);
            TailEnd = TailMiddle + new Vector3(0f, -0.8f, -0.6f) * W(0.04f);

            Coat = spec.Variant >= 0 && spec.Variant < Coats.Length ? Coats[spec.Variant] : genes.Pick("hide", 0.45f, 0.2f, 0.2f, 0.15f);
            BearHide hide = Hides[Coat];
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
                Shoulder = Shoulder, Carpus = Carpus, ForeCoronet = ForePaw, Hip = Hip, Hock = Ankle, HindCoronet = HindPaw,
            };
        }

        /// <summary>
        /// Where its joints are, on the same bones as people and every other animal. The forelegs are the arms, bending forward at the wrist, and the hind legs bend back at the ankle; the elbow and
        /// the knee are held in the bones above. Built standing, which is where a skin bends best from.
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
            Set(BoneType.LowerLegL, Ankle);
            Set(BoneType.FootL, HindPaw);
            Set(BoneType.UpperLegR, Right(Hip));
            Set(BoneType.LowerLegR, Right(Ankle));
            Set(BoneType.FootR, Right(HindPaw));

            return new Rig(BodyPlan.Quadruped, bones, AnkleHeight, PawFront, PawBack);
        }
    }
}
