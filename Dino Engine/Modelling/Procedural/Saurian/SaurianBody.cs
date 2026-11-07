using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// The line a body is built around, from the tip of the tail to the back of the skull, in the plane down the middle of the body. It's sampled finely, and everything along a body is said by how far
    /// along this it is: the thickness of the flesh, which bone a stretch of skin hangs from, and the way a hide's stripes run.
    /// </summary>
    public sealed class SaurianAxis
    {
        public readonly Vector3[] Points;
        public readonly float[] Along;      // Metres from the tail tip.
        public readonly float[] Deep;       // Half the depth of the flesh around each point.
        public readonly float[] Wide;       // Half its width.

        public float Length => Along[^1];

        public SaurianAxis(Vector3[] points, Func<float, float> deep, Func<float, float> wide)
        {
            Points = points;
            Along = new float[points.Length];
            for (int i = 1; i < points.Length; i++) Along[i] = Along[i - 1] + (points[i] - points[i - 1]).Length;
            Deep = new float[points.Length];
            Wide = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Deep[i] = deep(Along[i]);
                Wide[i] = wide(Along[i]);
            }
        }

        /// <summary>Where along it something is, in metres from the tail tip.</summary>
        public Vector3 At(float along)
        {
            int i = Span(along, out float t);
            return Vector3.Lerp(Points[i], Points[Math.Min(i + 1, Points.Length - 1)], t);
        }

        public float DeepAt(float along)
        {
            int i = Span(along, out float t);
            return MathHelper.Lerp(Deep[i], Deep[Math.Min(i + 1, Deep.Length - 1)], t);
        }

        public float WideAt(float along)
        {
            int i = Span(along, out float t);
            return MathHelper.Lerp(Wide[i], Wide[Math.Min(i + 1, Wide.Length - 1)], t);
        }

        /// <summary>Which way it runs there, towards the head.</summary>
        public Vector3 Way(float along)
        {
            int i = Index(along);
            return (Points[Math.Min(i + 1, Points.Length - 1)] - Points[Math.Max(i, 0)]).Normalized();
        }

        /// <summary>The nearest point on it to <paramref name="p"/>, as how far along it is and how far off it is, across and up, in its own frame there.</summary>
        public float Nearest(Vector3 p, out float distance)
        {
            float best = float.MaxValue;
            float bestAlong = 0f;
            for (int i = 0; i + 1 < Points.Length; i++)
            {
                Vector3 a = Points[i], b = Points[i + 1];
                Vector3 ab = b - a;
                float t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared, 1e-9f), 0f, 1f);
                float d = (a + ab * t - p).LengthSquared;
                if (d >= best) continue;
                best = d;
                bestAlong = Along[i] + (Along[i + 1] - Along[i]) * t;
            }
            distance = MathF.Sqrt(best);
            return bestAlong;
        }

        private int Index(float along)
        {
            int low = 0, high = Along.Length - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (Along[middle] <= along) low = middle;
                else high = middle;
            }
            return low;
        }

        private int Span(float along, out float t)
        {
            along = Math.Clamp(along, 0f, Length);
            int i = Index(along);
            int j = Math.Min(i + 1, Along.Length - 1);
            float span = Along[j] - Along[i];
            t = span > 1e-6f ? (along - Along[i]) / span : 0f;
            return i;
        }
    }

    /// <summary>One leg as it stands, in the body's own space: hip, knee, ankle and the ball of the foot, which is where the toes turn. Built folded, as it stands, which is where a skin bends best from.</summary>
    public sealed class SaurianLeg
    {
        public float Side;
        public Vector3 Hip, Knee, Ankle, Ball;
        public float HipRadius, KneeRadius, AnkleRadius, BallRadius;
        public float ToeLength;
        public bool Front;      // A foreleg, on what stands on four.
    }

    /// <summary>One arm as it's carried: shoulder, elbow, wrist and the end of the longest finger.</summary>
    public sealed class SaurianArm
    {
        public float Side;
        public Vector3 Collar, Shoulder, Elbow, Wrist, Hand;
        public float Radius;
        public int Fingers;
        public float Claw;
    }

    /// <summary>The head, in a frame of its own: x across to the left, y up, z forward along the skull from the back of it, which is where the head turns on the neck.</summary>
    public sealed class SaurianHead
    {
        public Vector3 Origin;          // The back of the skull, in the body's space.
        public Vector3 Forward, Up;     // How it's held, in the body's space.
        public float Length, BackWidth, BackDepth, TipWidth, TipDepth;
        public float Profile, Brow, BrowHorns, Crest, EyeSize, Jowls, Throat;
        public float LipDrop;           // How far the line of the mouth falls from back to front, as a share of the length.
        public float Hinge;             // How far forward the jaw's hinge is.
        public int Teeth;
        public float Tooth, Lips;
        public float Beak, NoseHorn, Frill, FrillSpikes;
        public int FrillPoints;
        public float EyeAt;             // How far along it the eyes are.

        public Vector3 ToBody(Vector3 local) => Origin + Forward * local.Z + Up * local.Y + Vector3.Cross(Up, Forward) * local.X;

        public Vector3 ToHead(Vector3 body)
        {
            Vector3 d = body - Origin;
            return new Vector3(Vector3.Dot(d, Vector3.Cross(Up, Forward)), Vector3.Dot(d, Up), Vector3.Dot(d, Forward));
        }

        /// <summary>
        /// The line of the mouth, in the head's own frame, at a share of the way to the snout: under the middle of the skull, falling a little, and rising again at the very front where a theropod's
        /// first teeth sit. A beak hooks down over it instead.
        /// </summary>
        public float Lip(float along)
        {
            float t = along / Length;
            float kink = Beak > 0f ? 0f : MathF.Exp(-MathF.Pow((t - 0.93f) / 0.06f, 2f)) * 0.025f;
            float hook = Beak * Gait01(0.74f, 1f, t);
            return -BackDepth * 0.2f - LipDrop * Length * t + MathF.Sin(t * MathF.PI * 1.6f) * 0.012f * Length + kink * Length - hook;
        }

        public float WidthAt(float along)
        {
            float t = Math.Clamp(along / Length, 0f, 1f);
            float taper = MathF.Pow(Gait01(0.12f, 1f, t), 0.75f);
            return MathHelper.Lerp(BackWidth, TipWidth, taper) * 0.5f;
        }

        public float DepthAt(float along)
        {
            float t = Math.Clamp(along / Length, 0f, 1f);
            return MathHelper.Lerp(BackDepth, TipDepth, Gait01(0.1f, 1f, t)) * 0.5f;
        }

        internal static float Gait01(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>The colours of one animal's hide, drawn from its species and its seed.</summary>
    public sealed class SaurianCoat
    {
        public Vector3 Back, Flank, Belly, Pattern, Display, Mouth, Claw, Horn, Eye;
        public HidePattern Patterns;
        public float StripeEvery;       // Metres between stripes, along the body.
        public float StripePhase;
        public float SpotSize;
        public uint Seed;
    }

    /// <summary>
    /// One dinosaur's body: every measurement of it, drawn from its species and a seed. The rig and the skin are both made from this, so the bones are always where the flesh is. Nothing here touches a GPU.
    /// </summary>
    public sealed class SaurianBody
    {
        public readonly SaurianSpecies Species;
        public readonly Genes Genes;
        public readonly float Length;
        public SaurianAxis Axis = null!;

        // Along the axis, in metres from the tail tip.
        public float TipAt, Tail3At, Tail2At, Tail1At, TailRootAt, HipAt, SpineAt, ChestAt, NeckAt, NeckTopAt, HeadAt;

        public float ChestDepth;        // Half the depth of the chest.
        public float ChestWidth;
        public float HipJoint;          // Height of the hip joint.
        public float BellyHang;         // How far the belly hangs below the line of the body.

        public readonly SaurianLeg[] Legs;
        public readonly SaurianArm[] Arms;
        public SaurianHead Head = null!;
        public SaurianCoat Coat = null!;

        public float Quills;            // How long the mane is, or 0 for none.
        public float Scutes;
        public bool Feathered => Quills > 0f;

        public bool OnFour => Species.Form == SaurianForm.Horned;

        public SaurianBody(SaurianSpecies species, ulong seed)
        {
            Species = species;
            Genes = new Genes(seed);
            Genes shape = Genes.Part("shape");
            Length = shape.Range("length", species.Length);
            Legs = new SaurianLeg[OnFour ? 4 : 2];
            Arms = OnFour ? Array.Empty<SaurianArm>() : new SaurianArm[2];

            BuildAxis(shape);
            BuildLegs(shape);
            if (!OnFour) BuildArms(shape);
            BuildHead(shape);
            BuildCoat(Genes.Part("coat"));

            Genes hide = Genes.Part("hide");
            Quills = hide.Chance("quills", species.QuillChance) ? hide.Range("quill length", species.Quills) * Length : 0f;
            Scutes = hide.Chance("scutes", species.ScuteChance) ? hide.Range("scute size", species.Scutes) * Length : 0f;
        }

        // ---- The line of the body.

        private void BuildAxis(Genes g)
        {
            SaurianSpecies s = Species;
            float hipShare = g.Range("hip", s.Hip);
            float skull = g.Range("skull", s.Skull) * Length;
            float neck = g.Range("neck", s.Neck) * Length;
            float trunk = g.Range("trunk", s.Trunk) * Length;
            float tail = MathF.Max(Length - skull - neck - trunk, Length * 0.15f);

            ChestDepth = g.Range("girth", s.Girth) * Length;
            ChestWidth = ChestDepth * g.Range("breadth", s.Breadth);
            BellyHang = ChestDepth * g.Range("belly", s.Belly);
            HipJoint = hipShare * Length;

            float hipDepth = ChestDepth * 0.72f;
            Vector3 hip = new Vector3(0f, HipJoint + hipDepth * 0.12f, 0f);

            // The back rises a little to the shoulders. A tail is carried level or nearly, and a neck leaves the shoulders climbing and curves forward into the head.
            float slope = g.Range("back slope", s.BackSlope);
            Vector3 shoulder = hip + new Vector3(0f, MathF.Sin(slope), MathF.Cos(slope)) * trunk;

            float rise = g.Range("neck rise", s.NeckRise);
            Vector3 neckWay = new Vector3(0f, MathF.Sin(rise), MathF.Cos(rise));
            float headPitch = g.Range("head pitch", OnFour ? 0.25f : 0.08f, OnFour ? 0.4f : 0.2f);
            Vector3 headWay = new Vector3(0f, -MathF.Sin(headPitch), MathF.Cos(headPitch));
            // The neck climbs steeply off the shoulders and bends over into the head, which is held level and forward: the S of every theropod.
            Vector3 neckRoot = shoulder + new Vector3(0f, ChestDepth * 0.2f, ChestDepth * 0.25f);
            float chord = neck * (OnFour ? 0.97f : 0.84f);
            float bend = rise * (OnFour ? 0.5f : 0.74f);
            Vector3 occiput = neckRoot + new Vector3(0f, MathF.Sin(bend), MathF.Cos(bend)) * chord;

            float droop = g.Range("tail droop", s.TailDroop);
            Vector3 tailWay = new Vector3(0f, -MathF.Sin(droop), -MathF.Cos(droop));
            Vector3 tip = hip + tailWay * tail + new Vector3(0f, g.Range("tail lift", -0.02f, 0.03f) * tail, 0f);

            var points = new List<Vector3>();
            void Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int count, bool first)
            {
                for (int i = first ? 0 : 1; i <= count; i++)
                {
                    float t = i / (float)count, u = 1f - t;
                    points.Add(a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t));
                }
            }

            // From the tail tip to the hips, sagging a little in the middle as a tail does, then to the shoulders, up the neck and along the skull.
            Curve(tip, tip + (hip - tip) * 0.34f + new Vector3(0f, droop * tail * 0.12f, 0f), hip + tailWay * tail * 0.3f, hip, 60, true);
            Curve(hip, hip + (shoulder - hip) * 0.33f, shoulder - (shoulder - hip) * 0.33f, shoulder, 24, false);
            Curve(shoulder, shoulder + (neckRoot - shoulder) * 0.5f, neckRoot - neckWay * ChestDepth * 0.1f, neckRoot, 4, false);
            Curve(neckRoot, neckRoot + neckWay * neck * 0.5f, occiput - headWay * neck * 0.42f, occiput, 22, false);
            Curve(occiput, occiput + headWay * skull * 0.33f, occiput + headWay * skull * 0.66f, occiput + headWay * skull, 14, false);

            float neckGirth = ChestDepth * g.Range("neck girth", s.NeckGirth);
            float tailRoot = ChestDepth * g.Range("tail root", s.TailRoot);
            float rod = s.TailRod;

            // Where each part begins, by how far along the line it is.
            var lengths = new float[points.Count];
            for (int i = 1; i < points.Count; i++) lengths[i] = lengths[i - 1] + (points[i] - points[i - 1]).Length;
            HipAt = lengths[60];
            float shoulderAt = lengths[60 + 24];
            NeckAt = lengths[60 + 24 + 4];
            HeadAt = lengths[60 + 24 + 4 + 22];
            float headEnd = lengths[^1];
            float tailRun = HipAt;
            TailRootAt = HipAt - tailRun * 0.07f;
            Tail1At = HipAt - tailRun * 0.24f;
            Tail2At = HipAt - tailRun * 0.42f;
            Tail3At = HipAt - tailRun * 0.6f;
            TipAt = HipAt - tailRun * 0.78f;
            SpineAt = HipAt + (shoulderAt - HipAt) * 0.45f;
            ChestAt = HipAt + (shoulderAt - HipAt) * 0.92f;
            NeckTopAt = NeckAt + (HeadAt - NeckAt) * 0.52f;
            float chestPeak = HipAt + (shoulderAt - HipAt) * 0.62f;
            float wideRatio = ChestWidth / ChestDepth;

            float Deep(float along)
            {
                if (along <= HipAt)
                {
                    // The tail, from its root to a point. A rod tail is stiffened by tendons and thins fast, and runs on thin.
                    float t = 1f - along / MathF.Max(HipAt, 1e-3f);
                    float even = MathF.Pow(1f - t, 1.12f);
                    float fast = MathF.Pow(1f - t, 2.6f) * 0.55f + (1f - t) * 0.45f;
                    float taper = MathHelper.Lerp(even, fast, rod);
                    float root = MathHelper.Lerp(hipDepth, tailRoot, Smooth(0f, 0.18f, t));
                    return MathF.Max(root * taper, Length * 0.0045f);
                }
                if (along <= chestPeak) return MathHelper.Lerp(hipDepth, ChestDepth, Smooth(HipAt, chestPeak, along));
                if (along <= NeckAt) return MathHelper.Lerp(ChestDepth, neckGirth, Smooth(chestPeak, NeckAt + (HeadAt - NeckAt) * 0.1f, along));
                if (along <= HeadAt) return MathHelper.Lerp(neckGirth, neckGirth * 0.68f, Smooth(NeckAt, HeadAt, along));
                return neckGirth * 0.68f * (1f - 0.5f * Smooth(HeadAt, headEnd, along));
            }

            // The root of a tail is wide with the muscle that pulls the thighs back, and a tail's tip is round. The trunk is narrower than it's deep, and a neck rounds out towards the head.
            float Wide(float along)
            {
                float d = Deep(along);
                if (along <= HipAt) return d * MathHelper.Lerp(wideRatio + 0.1f, 1f, 1f - along / MathF.Max(HipAt, 1e-3f));
                if (along <= NeckAt) return d * MathHelper.Lerp(wideRatio + 0.1f, wideRatio, Smooth(HipAt, chestPeak, along));
                return d * MathHelper.Lerp(wideRatio, 0.88f, Smooth(NeckAt, HeadAt, along));
            }

            Axis = new SaurianAxis(points.ToArray(), Deep, Wide);
        }

        private static float Smooth(float from, float to, float value) => SaurianHead.Gait01(from, to, value);

        // ---- Legs, built folded the way each stands.

        private void BuildLegs(Genes g)
        {
            SaurianSpecies s = Species;
            float crouch = g.Range("crouch", s.Crouch);
            Vector3 bones = s.LegBones * (Vector3.One + new Vector3(g.Around("femur", 0f, 0.03f), g.Around("tibia", 0f, 0.03f), g.Around("metatarsus", 0f, 0.03f)));

            // Femur forward, tibia back and metatarsus forward again: a dinosaur's leg is a Z on its side, and the more folded the faster the animal. The ankle stands under the hip.
            float femurAngle = 0.15f + crouch * 0.55f;
            float metaAngle = 0.32f + crouch * 0.28f;
            float toeShare = g.Range("toe", s.Toe);

            // Scale the bones until the hip is where it was said to be.
            float Height(float unit, out float tibiaAngle)
            {
                float f = bones.X * unit, t = bones.Y * unit, m = bones.Z * unit;
                tibiaAngle = MathF.Asin(Math.Clamp(f * MathF.Sin(femurAngle) / t, -0.95f, 0.95f));
                return f * MathF.Cos(femurAngle) + t * MathF.Cos(tibiaAngle) + m * MathF.Cos(metaAngle) + m * 0.08f;
            }

            float unit = HipJoint;
            for (int i = 0; i < 6; i++) unit *= HipJoint / Height(unit, out _);
            Height(unit, out float tibia);
            float femur = bones.X * unit, shin = bones.Y * unit, meta = bones.Z * unit;
            float sole = meta * 0.08f;

            float hipX = ChestWidth * (OnFour ? 0.62f : 0.55f);
            float thigh = ChestDepth * g.Range("thigh", s.Thigh);
            float shinRadius = ChestDepth * g.Range("shin", s.Shin);

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1f : -1f;
                var leg = new SaurianLeg { Side = x, Front = false };
                leg.Hip = new Vector3(x * hipX, HipJoint, 0f);
                leg.Knee = leg.Hip + new Vector3(x * 0.05f, -MathF.Cos(femurAngle), MathF.Sin(femurAngle)).Normalized() * femur;
                leg.Ankle = leg.Knee + new Vector3(-x * 0.03f, -MathF.Cos(tibia), -MathF.Sin(tibia)).Normalized() * shin;
                leg.Ball = leg.Ankle + new Vector3(0f, -MathF.Cos(metaAngle), MathF.Sin(metaAngle)) * meta;
                leg.HipRadius = thigh;
                leg.KneeRadius = shinRadius;
                leg.AnkleRadius = shinRadius * 0.42f;
                leg.BallRadius = shinRadius * 0.38f;
                leg.ToeLength = meta * toeShare;
                Legs[side] = leg;
            }

            if (!OnFour) return;

            // Forelegs stand under the shoulders as an elephant's do, the shoulder joint low on the ribs and the elbow back, with the wrist as high off the ground as the hind ankle, so that every foot
            // rolls the same. They're as long as it takes to reach the ground from there, a little folded.
            Vector3 chest = Axis.At(ChestAt);
            float ankle = Legs[0].Ankle.Y;
            float armGirth = ChestDepth * g.Range("arm girth", s.ArmGirth);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1f : -1f;
                var leg = new SaurianLeg { Side = x, Front = true };
                leg.Hip = new Vector3(x * ChestWidth * 0.68f, chest.Y - ChestDepth * 0.58f, chest.Z);
                leg.Ankle = new Vector3(leg.Hip.X + x * ChestWidth * 0.06f, ankle, leg.Hip.Z + ChestDepth * 0.08f);
                float reach = (leg.Ankle - leg.Hip).Length / 0.94f;
                leg.Knee = Fold(leg.Hip, leg.Ankle, reach * 0.53f, reach * 0.47f, new Vector3(x * 0.15f, 0f, -1f));
                leg.Ball = leg.Ankle + new Vector3(0f, sole - ankle, Legs[0].Ball.Z - Legs[0].Ankle.Z);
                leg.HipRadius = armGirth;
                leg.KneeRadius = armGirth * 0.62f;
                leg.AnkleRadius = armGirth * 0.45f;
                leg.BallRadius = armGirth * 0.42f;
                leg.ToeLength = Legs[0].ToeLength * 0.5f;
                Legs[2 + side] = leg;
            }
        }

        // Where the middle joint of a limb goes, given its ends and which way it folds.
        private static Vector3 Fold(Vector3 root, Vector3 end, float upper, float lower, Vector3 bend)
        {
            Vector3 line = end - root;
            float reach = line.Length;
            Vector3 way = line / reach;
            float along = (upper * upper - lower * lower + reach * reach) / (2f * reach);
            float away = MathF.Sqrt(MathF.Max(upper * upper - along * along, 0f));
            Vector3 across = bend - way * Vector3.Dot(bend, way);
            return root + way * along + across.Normalized() * away;
        }

        // ---- Arms, carried folded, with the palms facing each other.

        private void BuildArms(Genes g)
        {
            SaurianSpecies s = Species;
            float femur = (Legs[0].Knee - Legs[0].Hip).Length;
            float arm = femur * g.Range("arm", s.Arm);
            float upper = arm * 0.55f, lower = arm * 0.45f;
            float radius = ChestDepth * g.Range("arm girth", s.ArmGirth);
            Vector3 chest = Axis.At(ChestAt);
            float claw = arm * g.Range("claw", s.Claw);

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1f : -1f;
                var a = new SaurianArm { Side = x, Radius = radius, Fingers = s.Fingers, Claw = claw };
                // Long arms fold back along the body as a bird's wings do, and short ones are held forward. Either way the palms face each other.
                float fold = Math.Clamp((arm / femur - 0.5f) / 0.7f, 0f, 1f);
                a.Collar = chest + new Vector3(x * ChestWidth * 0.3f, -ChestDepth * 0.15f, ChestDepth * 0.1f);
                a.Shoulder = chest + new Vector3(x * ChestWidth * 0.78f, -ChestDepth * 0.45f, ChestDepth * 0.2f);
                a.Elbow = a.Shoulder + Vector3.Lerp(new Vector3(x * 0.08f, -0.86f, -0.5f), new Vector3(x * 0.12f, -0.55f, -0.83f), fold).Normalized() * upper;
                a.Wrist = a.Elbow + Vector3.Lerp(new Vector3(-x * 0.06f, -0.38f, 0.92f), new Vector3(-x * 0.05f, -0.12f, 0.99f), fold).Normalized() * lower;
                a.Hand = a.Wrist + Vector3.Lerp(new Vector3(-x * 0.04f, -0.5f, 0.86f), new Vector3(-x * 0.03f, -0.62f, 0.78f), fold).Normalized() * (arm * 0.42f);
                Arms[side] = a;
            }
        }

        // ---- The head.

        private void BuildHead(Genes g)
        {
            SaurianSpecies s = Species;
            Vector3 occiput = Axis.At(HeadAt);
            Vector3 forward = (Axis.At(Axis.Length) - occiput).Normalized();
            Vector3 up = Vector3.Cross(forward, Vector3.UnitX).Normalized();
            if (up.Y < 0f) up = -up;
            float length = Axis.Length - HeadAt;

            Head = new SaurianHead
            {
                Origin = occiput,
                Forward = forward,
                Up = up,
                Length = length,
                BackDepth = length * g.Range("skull depth", s.SkullDepth),
                BackWidth = length * g.Range("skull width", s.SkullWidth),
                Profile = g.Range("profile", s.Profile),
                Brow = g.Range("brow", s.Brow) * length,
                BrowHorns = g.Range("brow horns", s.BrowHorns) * length,
                Crest = g.Range("crest", s.Crest) * length,
                EyeSize = g.Range("eye", s.Eye) * length,
                Jowls = g.Range("jowls", s.Jowls) * length,
                Throat = g.Range("throat", s.Throat) * length,
                LipDrop = g.Range("lip drop", 0.02f, 0.06f),
                Hinge = length * 0.1f,
                Teeth = g.Count("teeth", s.Teeth.Min, s.Teeth.Max),
                Tooth = g.Range("tooth", s.Tooth) * length,
                Lips = s.Lips,
                Beak = g.Range("beak", s.Beak) * length,
                NoseHorn = g.Range("nose horn", s.NoseHorn) * length,
                Frill = g.Range("frill", s.Frill) * length,
                FrillSpikes = g.Range("frill spikes", s.FrillSpikes) * length,
                FrillPoints = g.Count("frill points", s.FrillPoints.Min, s.FrillPoints.Max),
                EyeAt = OnFour ? 0.38f : g.Range("eye at", 0.24f, 0.3f),
            };
            Head.TipWidth = Head.BackWidth * g.Range("snout", s.Snout);
            Head.TipDepth = Head.BackDepth * g.Range("snout depth", s.SnoutDepth);
        }

        // ---- Colours.

        private void BuildCoat(Genes g)
        {
            Hide hide = Species.Hides[g.Pick("hide", Species.Hides.Select(_ => 1f).ToArray())];
            float drift = hide.Drift;
            Vector3 Drift(string name, Vector3 colour)
            {
                float value = 1f + g.Around(name + " value", 0f, drift);
                float warm = g.Around(name + " warmth", 0f, drift * 0.35f);
                return Vector3.ComponentMax(colour * value + new Vector3(warm, warm * 0.2f, -warm) * colour.Length, new Vector3(0.004f));
            }

            Vector3 back = Drift("back", hide.Back);
            Vector3 belly = Drift("belly", hide.Belly);
            Coat = new SaurianCoat
            {
                Back = back,
                Belly = belly,
                Flank = Vector3.Lerp(back, belly, 0.38f),
                Pattern = Drift("pattern", hide.Pattern),
                Display = Drift("display", hide.Display),
                Mouth = new Vector3(0.24f, 0.05f, 0.05f),
                Claw = new Vector3(0.075f, 0.065f, 0.055f),
                Horn = Drift("horn", new Vector3(0.22f, 0.19f, 0.15f)),
                Eye = g.Pick("eye", 1f, 1f, 1f) switch { 0 => new Vector3(0.42f, 0.24f, 0.02f), 1 => new Vector3(0.3f, 0.3f, 0.04f), _ => new Vector3(0.36f, 0.12f, 0.02f) },
                Patterns = hide.Patterns,
                StripeEvery = Length * g.Range("stripes", 0.035f, 0.06f),
                StripePhase = g.Range("stripe phase", 0f, 10f),
                SpotSize = Length * g.Range("spots", 0.012f, 0.025f),
                Seed = (uint)g.Stream("pattern seed").NextULong(),
            };
        }

        // ---- The rig, from the same numbers.

        /// <summary>Where this body's joints are, and what it adds to a person's rig.</summary>
        public Rig BuildRig()
        {
            var pivots = new Vector3[Rig.BoneCount];
            void Set(BoneType bone, Vector3 at) => pivots[(int)bone] = at;

            Set(BoneType.Pelvis, Axis.At(HipAt));
            Set(BoneType.Spine, Axis.At(SpineAt));
            Set(BoneType.Chest, Axis.At(ChestAt));
            Set(BoneType.Neck, Axis.At(NeckAt));
            Set(BoneType.NeckTop, Axis.At(NeckTopAt));
            Set(BoneType.Head, Head.Origin);
            Set(BoneType.Jaw, Head.ToBody(new Vector3(0f, Head.Lip(Head.Hinge) - Head.Length * 0.01f, Head.Hinge)));
            Set(BoneType.TailBase, Axis.At(TailRootAt));
            Set(BoneType.Tail1, Axis.At(Tail1At));
            Set(BoneType.Tail2, Axis.At(Tail2At));
            Set(BoneType.Tail3, Axis.At(Tail3At));
            Set(BoneType.TailTip, Axis.At(TipAt));

            BoneType[] upper = { BoneType.UpperLegL, BoneType.UpperLegR, BoneType.UpperArmL, BoneType.UpperArmR };
            BoneType[] lower = { BoneType.LowerLegL, BoneType.LowerLegR, BoneType.LowerArmL, BoneType.LowerArmR };
            BoneType[] end = { BoneType.FootL, BoneType.FootR, BoneType.HandL, BoneType.HandR };
            for (int i = 0; i < Legs.Length; i++)
            {
                SaurianLeg leg = Legs[i];
                Set(upper[i], leg.Hip);
                Set(lower[i], leg.Knee);
                Set(end[i], leg.Ankle);
                if (!leg.Front) Set(i == 0 ? BoneType.ToeL : BoneType.ToeR, leg.Ball);
            }

            if (OnFour)
            {
                // Collarbones go nowhere on four legs, and sit inside the chest.
                Set(BoneType.ClavicleL, Axis.At(ChestAt));
                Set(BoneType.ClavicleR, Axis.At(ChestAt));
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    SaurianArm arm = Arms[i];
                    bool left = i == 0;
                    Set(left ? BoneType.ClavicleL : BoneType.ClavicleR, arm.Collar);
                    Set(left ? BoneType.UpperArmL : BoneType.UpperArmR, arm.Shoulder);
                    Set(left ? BoneType.LowerArmL : BoneType.LowerArmR, arm.Elbow);
                    Set(left ? BoneType.HandL : BoneType.HandR, arm.Wrist);
                }
            }

            var frame = new SaurianFrame
            {
                HeadTip = Axis.At(Axis.Length),
                TailEnd = Axis.At(0f),
                ToeLength = Legs[0].ToeLength,
            };
            float[] thick = frame.Thickness;
            thick[(int)BoneType.Pelvis] = Axis.DeepAt(HipAt) * 0.85f;
            thick[(int)BoneType.Spine] = Axis.DeepAt(SpineAt) * 0.85f;
            thick[(int)BoneType.Chest] = Axis.DeepAt(ChestAt) * 0.85f;
            thick[(int)BoneType.Neck] = Axis.DeepAt(NeckAt) * 0.8f;
            thick[(int)BoneType.NeckTop] = Axis.DeepAt(NeckTopAt) * 0.8f;
            thick[(int)BoneType.Head] = Head.BackDepth * 0.42f;
            thick[(int)BoneType.TailBase] = Axis.DeepAt(TailRootAt) * 0.8f;
            thick[(int)BoneType.Tail1] = Axis.DeepAt(Tail1At) * 0.8f;
            thick[(int)BoneType.Tail2] = Axis.DeepAt(Tail2At) * 0.8f;
            thick[(int)BoneType.Tail3] = Axis.DeepAt(Tail3At) * 0.8f;
            thick[(int)BoneType.TailTip] = Axis.DeepAt(TipAt) * 0.8f;
            for (int i = 0; i < Legs.Length; i++)
            {
                thick[(int)upper[i]] = Legs[i].HipRadius * 0.6f;
                thick[(int)lower[i]] = Legs[i].KneeRadius * 0.8f;
                thick[(int)end[i]] = Legs[i].AnkleRadius * 1.2f;
            }
            if (!OnFour)
            {
                for (int i = 0; i < 2; i++)
                {
                    bool left = i == 0;
                    thick[(int)(left ? BoneType.ClavicleL : BoneType.ClavicleR)] = Arms[i].Radius;
                    thick[(int)(left ? BoneType.UpperArmL : BoneType.UpperArmR)] = Arms[i].Radius;
                    thick[(int)(left ? BoneType.LowerArmL : BoneType.LowerArmR)] = Arms[i].Radius * 0.7f;
                    thick[(int)(left ? BoneType.HandL : BoneType.HandR)] = Arms[i].Radius * 0.6f;
                }
            }

            SaurianLeg hind = Legs[0];
            float ankle = hind.Ankle.Y;
            float forward = MathF.Max(hind.Ball.Z - hind.Ankle.Z, ankle * 0.1f);
            return new Rig(OnFour ? BodyPlan.Quadruped : BodyPlan.Biped, pivots, ankle, forward, forward * 0.3f, frame);
        }
    }
}
