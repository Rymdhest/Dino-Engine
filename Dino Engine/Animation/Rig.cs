using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>The ways of being built that move differently. Everything else about a body is measurements.</summary>
    public enum BodyPlan
    {
        Biped,
        Quadruped,
    }

    /// <summary>
    /// A limb, from where it leaves the body to the joint at its end. Nothing that moves a limb knows what it's a limb of: it says where the end should be and which way the middle should go,
    /// and <see cref="Reach"/> does the rest, for an arm, a leg or the foreleg of a boar alike.
    /// </summary>
    public sealed class Limb
    {
        public readonly BoneType Upper;
        public readonly BoneType Lower;
        public readonly BoneType End;

        /// <summary>The bone this hangs from.</summary>
        public readonly BoneType Parent;

        public readonly float UpperLength;
        public readonly float LowerLength;
        public readonly float Length;

        /// <summary>1 on the left, -1 on the right.</summary>
        public readonly float Side;

        /// <summary>Which way the middle joint goes when it bends, in the body's own frame, once the limb is hanging as it hangs.</summary>
        public readonly Vector3 Bends;

        /// <summary>The whole limb's frame as it was built, from root to end.</summary>
        internal readonly Quaternion RestFrameInverse;

        /// <summary>Each bone's own frame as it was built. A person's limbs are built straight, and these are the same as the whole limb's. A dinosaur's legs are built folded, as they stand, which
        /// is where a skin bends best from, so each bone keeps the way it really pointed.</summary>
        internal readonly Quaternion RestUpperInverse;
        internal readonly Quaternion RestLowerInverse;

        public Limb(Vector3[] pivots, BoneType parent, BoneType upper, BoneType lower, BoneType end, float side, Vector3 restBend, Vector3 bends)
        {
            Parent = parent;
            Upper = upper;
            Lower = lower;
            End = end;
            Side = side;
            Bends = bends;

            Vector3 root = pivots[(int)upper];
            Vector3 middle = pivots[(int)lower];
            Vector3 tip = pivots[(int)end];
            UpperLength = (middle - root).Length;
            LowerLength = (tip - middle).Length;
            Length = UpperLength + LowerLength;

            Vector3 direction = (tip - root).Normalized();
            Vector3 across = Frames.Across(restBend, direction, Vector3.UnitZ);
            RestFrameInverse = Quaternion.Invert(Frames.Of(direction, across));

            // Folded, the way the middle joint stands off the line is the way it bends, and the hinge is square to both. That's how Reach.Solve frames each bone, so the pose it was built in comes back unturned.
            Vector3 toMiddle = middle - root;
            Vector3 off = toMiddle - direction * Vector3.Dot(toMiddle, direction);
            if (off.Length > Length * 0.01f) across = off.Normalized();
            Vector3 hinge = Vector3.Cross(direction, across);
            Vector3 upperWay = toMiddle / MathF.Max(UpperLength, 1e-6f);
            Vector3 lowerWay = (tip - middle) / MathF.Max(LowerLength, 1e-6f);
            RestUpperInverse = Quaternion.Invert(Frames.Of(upperWay, Vector3.Cross(hinge, upperWay)));
            RestLowerInverse = Quaternion.Invert(Frames.Of(lowerWay, Vector3.Cross(hinge, lowerWay)));
        }

        /// <summary>Where the limb is folded in the pose it was built in, as a share of being straight: 0 for a person's.</summary>
        public float RestFold(Vector3[] pivots) => 1f - (pivots[(int)End] - pivots[(int)Upper]).Length / Length;
    }

    /// <summary>
    /// What a dinosaur's rig has that a person's doesn't. Its neck, jaw, tail and toes are real bones with skin on them, its snout is a long way from where its head turns, and it's far thicker in
    /// places than anyone the fall of a body was first worked out for, so a body falls as what it's made of.
    /// </summary>
    public sealed class SaurianFrame
    {
        /// <summary>Where the snout ends, in the pose it was built in.</summary>
        public Vector3 HeadTip;

        /// <summary>How far out from each bone its body goes, by bone, for falling. Nothing there means it isn't part of how it falls.</summary>
        public float[] Thickness = new float[Rig.BoneCount];

        /// <summary>The tail's bones that have any length, from the hips out.</summary>
        public BoneType[] Tail = { BoneType.TailBase, BoneType.Tail1, BoneType.Tail2, BoneType.Tail3, BoneType.TailTip };

        /// <summary>Where the tail ends, past its last joint.</summary>
        public Vector3 TailEnd;

        /// <summary>How long its toes are, from the ball of the foot, which the toes turn about.</summary>
        public float ToeLength;
    }

    /// <summary>
    /// Where a body's joints are and how they're joined, in the pose it was built in. Everything is in the body's own space, with its feet at y = 0 and facing +z, and +x on its left. All bones are at rest
    /// unrotated, so a bone's rotation is simply how it's turned from how it was built. One of these is shared by everyone built the same way.
    /// </summary>
    public sealed class Rig
    {
        public const int BoneCount = (int)BoneType.Count;

        /// <summary>The fall of something dropped, in metres a second squared, for a world where a person stands 4.7 m tall. It's what every stride and every body in the air is scaled by.</summary>
        public const float Gravity = 26f;

        public readonly BodyPlan Plan;
        public readonly Vector3[] Pivots;
        public readonly int[] Parents;
        public readonly Matrix4[] InverseBind;

        /// <summary>All of them, hind pair first on what has four.</summary>
        public readonly Limb[] Legs;

        /// <summary>What can hold something. Left first, and nothing on an animal.</summary>
        public readonly Limb[] Arms;

        /// <summary>How high the hips are, standing.</summary>
        public readonly float HipHeight;

        /// <summary>From a hip to the sole of its foot.</summary>
        public readonly float LegLength;

        /// <summary>How high an ankle is off the ground.</summary>
        public readonly float AnkleHeight;

        /// <summary>From the ankle joint to the ball of the foot, and back to the heel.</summary>
        public readonly float FootForward;
        public readonly float FootBack;

        public float ArmLength => Arms.Length > 0 ? Arms[0].Length : 0f;

        /// <summary>A dinosaur's, with a jaw, a neck in two, a long tail and toes, which are all moved. Anyone else's are where they'd be and stay out of it.</summary>
        public bool Saurian => Frame != null;

        public readonly SaurianFrame? Frame;

        public Rig(BodyPlan plan, Vector3[] pivots, float ankleHeight, float footForward, float footBack, SaurianFrame? frame = null)
        {
            Plan = plan;
            Pivots = pivots;
            AnkleHeight = ankleHeight;
            FootForward = footForward;
            FootBack = footBack;
            Frame = frame;

            // Bones a body hasn't got sit on the joint they'd come from, so that everything hung from them is where it always was.
            if (frame == null)
            {
                pivots[(int)BoneType.NeckTop] = pivots[(int)BoneType.Head];
                pivots[(int)BoneType.Jaw] = pivots[(int)BoneType.Head];
                pivots[(int)BoneType.Tail1] = pivots[(int)BoneType.TailTip];
                pivots[(int)BoneType.Tail2] = pivots[(int)BoneType.TailTip];
                pivots[(int)BoneType.Tail3] = pivots[(int)BoneType.TailTip];
                pivots[(int)BoneType.ToeL] = pivots[(int)BoneType.FootL];
                pivots[(int)BoneType.ToeR] = pivots[(int)BoneType.FootR];
            }

            Parents = new int[BoneCount];
            Array.Fill(Parents, -1);
            void Join(BoneType child, BoneType parent) => Parents[(int)child] = (int)parent;

            Join(BoneType.Spine, BoneType.Pelvis);
            Join(BoneType.Chest, BoneType.Spine);
            Join(BoneType.Neck, BoneType.Chest);
            Join(BoneType.NeckTop, BoneType.Neck);
            Join(BoneType.Head, BoneType.NeckTop);
            Join(BoneType.Jaw, BoneType.Head);
            Join(BoneType.ClavicleL, BoneType.Chest);
            Join(BoneType.ClavicleR, BoneType.Chest);
            Join(BoneType.UpperArmL, BoneType.ClavicleL);
            Join(BoneType.LowerArmL, BoneType.UpperArmL);
            Join(BoneType.HandL, BoneType.LowerArmL);
            Join(BoneType.UpperArmR, BoneType.ClavicleR);
            Join(BoneType.LowerArmR, BoneType.UpperArmR);
            Join(BoneType.HandR, BoneType.LowerArmR);
            Join(BoneType.UpperLegL, BoneType.Pelvis);
            Join(BoneType.LowerLegL, BoneType.UpperLegL);
            Join(BoneType.FootL, BoneType.LowerLegL);
            Join(BoneType.UpperLegR, BoneType.Pelvis);
            Join(BoneType.LowerLegR, BoneType.UpperLegR);
            Join(BoneType.FootR, BoneType.LowerLegR);
            Join(BoneType.TailBase, BoneType.Pelvis);
            Join(BoneType.Tail1, BoneType.TailBase);
            Join(BoneType.Tail2, BoneType.Tail1);
            Join(BoneType.Tail3, BoneType.Tail2);
            Join(BoneType.TailTip, BoneType.Tail3);
            Join(BoneType.ToeL, BoneType.FootL);
            Join(BoneType.ToeR, BoneType.FootR);

            InverseBind = new Matrix4[BoneCount];
            for (int i = 0; i < BoneCount; i++) InverseBind[i] = Matrix4.CreateTranslation(-pivots[i]);

            // A dinosaur's knees go forward whether it stands on two legs or four, as a person's do. A furred animal's hind legs bend back at the hock.
            Vector3 knees = plan == BodyPlan.Biped || frame != null ? Vector3.UnitZ : -Vector3.UnitZ;
            Limb hindLeft = new Limb(pivots, BoneType.Pelvis, BoneType.UpperLegL, BoneType.LowerLegL, BoneType.FootL, 1f, Vector3.UnitZ, knees);
            Limb hindRight = new Limb(pivots, BoneType.Pelvis, BoneType.UpperLegR, BoneType.LowerLegR, BoneType.FootR, -1f, Vector3.UnitZ, knees);

            if (plan == BodyPlan.Biped)
            {
                Legs = new[] { hindLeft, hindRight };

                // An arm is built out sideways, and folds as if it were hanging: the elbow goes back. A dinosaur's are built folded, and go the same way.
                Arms = new[]
                {
                    new Limb(pivots, BoneType.ClavicleL, BoneType.UpperArmL, BoneType.LowerArmL, BoneType.HandL, 1f, -Vector3.UnitZ, -Vector3.UnitZ),
                    new Limb(pivots, BoneType.ClavicleR, BoneType.UpperArmR, BoneType.LowerArmR, BoneType.HandR, -1f, -Vector3.UnitZ, -Vector3.UnitZ),
                };
            }
            else
            {
                // On an animal the same bones are forelegs. They're built out to the side all the same, but they carry weight, and the middle joint goes forward. A dinosaur stands on its
                // forelegs as an elephant does, built hanging under the shoulder with the elbow back.
                Vector3 elbows = frame != null ? -Vector3.UnitZ : Vector3.UnitZ;
                Legs = new[]
                {
                    hindLeft,
                    hindRight,
                    new Limb(pivots, BoneType.Chest, BoneType.UpperArmL, BoneType.LowerArmL, BoneType.HandL, 1f, elbows, elbows),
                    new Limb(pivots, BoneType.Chest, BoneType.UpperArmR, BoneType.LowerArmR, BoneType.HandR, -1f, elbows, elbows),
                };
                Arms = Array.Empty<Limb>();
            }

            HipHeight = pivots[(int)BoneType.Pelvis].Y;
            LegLength = HipHeight;
        }

        /// <summary>The toe bone on the end of a leg, if it has one of its own.</summary>
        public static BoneType ToeOf(BoneType foot) => foot switch
        {
            BoneType.FootL => BoneType.ToeL,
            BoneType.FootR => BoneType.ToeR,
            _ => BoneType.Count,
        };

        /// <summary>Where a person's joints are. The meshes in <see cref="Modelling.Procedural.Character.HumanoidGenerator"/> and the armour around <see cref="Modelling.Procedural.Gear.BodyForm"/> are built around these.</summary>
        public static readonly Rig Humanoid = BuildHumanoid();

        private static Rig BuildHumanoid()
        {
            const float shoulderX = 0.45f;
            const float shoulderY = 3.85f;
            const float arm = 1.7f;
            const float hipX = 0.25f;
            const float ankle = 0.24f;

            var pivots = new Vector3[BoneCount];
            void Set(BoneType bone, float x, float y, float z) => pivots[(int)bone] = new Vector3(x, y, z);

            Set(BoneType.Pelvis, 0f, 2f, 0f);
            Set(BoneType.Spine, 0f, 2.62f, 0f);
            Set(BoneType.Chest, 0f, 3.22f, 0f);
            Set(BoneType.Neck, 0f, 3.9f, -0.03f);
            Set(BoneType.Head, 0f, 4.14f, -0.03f);

            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side > 0;
                Set(left ? BoneType.ClavicleL : BoneType.ClavicleR, side * 0.1f, 3.74f, 0f);
                Set(left ? BoneType.UpperArmL : BoneType.UpperArmR, side * shoulderX, shoulderY, 0f);
                Set(left ? BoneType.LowerArmL : BoneType.LowerArmR, side * (shoulderX + arm * 0.5f), shoulderY, 0f);
                Set(left ? BoneType.HandL : BoneType.HandR, side * (shoulderX + arm), shoulderY, 0f);
                Set(left ? BoneType.UpperLegL : BoneType.UpperLegR, side * hipX, 2f, 0f);
                Set(left ? BoneType.LowerLegL : BoneType.LowerLegR, side * hipX, 1f, 0f);
                Set(left ? BoneType.FootL : BoneType.FootR, side * hipX, ankle, 0f);
            }

            Set(BoneType.TailBase, 0f, 2f, -0.3f);
            Set(BoneType.TailTip, 0f, 2f, -0.6f);
            return new Rig(BodyPlan.Biped, pivots, ankle, 0.34f, 0.2f);
        }
    }

    /// <summary>Building rotations out of directions, which is how nearly everything here thinks.</summary>
    public static class Frames
    {
        /// <summary>The rotation that takes x to <paramref name="x"/> and y to <paramref name="y"/>, which have to be unit length and square to each other.</summary>
        public static Quaternion Of(Vector3 x, Vector3 y)
        {
            Vector3 z = Vector3.Cross(x, y);
            float trace = x.X + y.Y + z.Z;
            float scale;
            if (trace > 0f)
            {
                scale = MathF.Sqrt(trace + 1f) * 2f;
                return new Quaternion((y.Z - z.Y) / scale, (z.X - x.Z) / scale, (x.Y - y.X) / scale, scale * 0.25f);
            }
            if (x.X > y.Y && x.X > z.Z)
            {
                scale = MathF.Sqrt(1f + x.X - y.Y - z.Z) * 2f;
                return new Quaternion(scale * 0.25f, (y.X + x.Y) / scale, (z.X + x.Z) / scale, (y.Z - z.Y) / scale);
            }
            if (y.Y > z.Z)
            {
                scale = MathF.Sqrt(1f + y.Y - x.X - z.Z) * 2f;
                return new Quaternion((y.X + x.Y) / scale, scale * 0.25f, (z.Y + y.Z) / scale, (z.X - x.Z) / scale);
            }
            scale = MathF.Sqrt(1f + z.Z - x.X - y.Y) * 2f;
            return new Quaternion((z.X + x.Z) / scale, (z.Y + y.Z) / scale, scale * 0.25f, (x.Y - y.X) / scale);
        }

        /// <summary>What's left of <paramref name="wanted"/> once it's square to <paramref name="axis"/>, as a unit vector, or <paramref name="otherwise"/> if nothing is.</summary>
        public static Vector3 Across(Vector3 wanted, Vector3 axis, Vector3 otherwise)
        {
            Vector3 across = wanted - axis * Vector3.Dot(wanted, axis);
            if (across.LengthSquared > 1e-6f) return across.Normalized();
            across = otherwise - axis * Vector3.Dot(otherwise, axis);
            if (across.LengthSquared > 1e-6f) return across.Normalized();
            return MathF.Abs(axis.X) < 0.9f ? Vector3.Cross(axis, Vector3.UnitX).Normalized() : Vector3.Cross(axis, Vector3.UnitY).Normalized();
        }

        /// <summary>The shortest turn from one direction to another, which have to be unit length.</summary>
        public static Quaternion Between(Vector3 from, Vector3 to)
        {
            float dot = Vector3.Dot(from, to);
            if (dot > 0.99999f) return Quaternion.Identity;
            if (dot < -0.99999f)
            {
                Vector3 across = Across(Vector3.UnitY, from, Vector3.UnitX);
                return new Quaternion(across.X, across.Y, across.Z, 0f);
            }
            Vector3 axis = Vector3.Cross(from, to);
            return new Quaternion(axis.X, axis.Y, axis.Z, 1f + dot).Normalized();
        }

        public static Quaternion Yaw(float angle) => new Quaternion(0f, MathF.Sin(angle * 0.5f), 0f, MathF.Cos(angle * 0.5f));

        public static Quaternion Pitch(float angle) => new Quaternion(MathF.Sin(angle * 0.5f), 0f, 0f, MathF.Cos(angle * 0.5f));

        public static Quaternion Roll(float angle) => new Quaternion(0f, 0f, MathF.Sin(angle * 0.5f), MathF.Cos(angle * 0.5f));

        /// <summary>How something facing +z has turned about y, as the game measures it.</summary>
        public static float YawOf(Quaternion rotation)
        {
            Vector3 forward = Vector3.Transform(Vector3.UnitZ, rotation);
            return MathF.Atan2(forward.X, forward.Z);
        }

        /// <summary>Which way +z ends up after a yaw.</summary>
        public static Vector3 Forward(float yaw) => new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));

        /// <summary>The way to a body's left after a yaw.</summary>
        public static Vector3 Left(float yaw) => new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
    }

    /// <summary>
    /// Solves a limb. It's exact, takes one pass and remembers nothing, so a pose depends only on what's asked for now: a solver that carries anything from frame to frame can end up tied in a knot, as Spore's
    /// authors found. What's out of reach is reached for: the limb eases towards straight and never quite gets there, so a knee can't snap at full stretch.
    /// </summary>
    public static class Reach
    {
        private const float Softness = 0.05f;   // The last share of a limb's length is approached and not reached.
        private const float LeastFolded = 0.06f;

        /// <param name="root">Where the limb leaves the body.</param>
        /// <param name="target">Where its end is wanted.</param>
        /// <param name="bend">Which way the middle should go. Only what's square to the limb counts.</param>
        public static void Solve(Limb limb, Vector3 root, Vector3 target, Vector3 bend, out Vector3 middle, out Vector3 end, out Quaternion upper, out Quaternion lower)
        {
            float a = limb.UpperLength;
            float b = limb.LowerLength;
            float longest = a + b;

            Vector3 toTarget = target - root;
            float distance = toTarget.Length;
            Vector3 direction = distance > 1e-5f ? toTarget / distance : Vector3.Transform(Vector3.UnitX, Quaternion.Invert(limb.RestFrameInverse));

            float soft = longest * Softness;
            float start = longest - soft;
            float reach = distance <= start ? distance : start + soft * (1f - MathF.Exp((start - distance) / soft));
            reach = MathF.Max(reach, MathF.Max(MathF.Abs(a - b), longest * LeastFolded));

            Vector3 across = Frames.Across(bend, direction, Vector3.UnitZ);
            float along = (a * a - b * b + reach * reach) / (2f * reach);
            float away = MathF.Sqrt(MathF.Max(a * a - along * along, 0f));

            middle = root + direction * along + across * away;
            end = root + direction * reach;

            Vector3 upperDirection = (middle - root) / a;
            Vector3 lowerDirection = (end - middle) / b;
            Vector3 hinge = Vector3.Cross(direction, across);
            upper = Frames.Of(upperDirection, Vector3.Cross(hinge, upperDirection)) * limb.RestUpperInverse;
            lower = Frames.Of(lowerDirection, Vector3.Cross(hinge, lowerDirection)) * limb.RestLowerInverse;
        }
    }
}
