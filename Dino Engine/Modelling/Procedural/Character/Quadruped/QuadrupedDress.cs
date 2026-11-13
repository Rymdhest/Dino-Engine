using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// What the skin of an animal on four legs needs to know of its body, in the body's own frame (facing +z, +x on its left, feet at y = 0): the line of it from the back of the rump to the end of
    /// the snout, the places along that line where the spine and the chest take over from the hips, where the neck turns and the head, which way the head points, and the joints of its left legs.
    /// A boar and a wolf each give one, and the skin is hung on it the same way.
    /// </summary>
    public sealed class QuadrupedFrame
    {
        public float Withers;               // To the top of the shoulders, which every reach here is a share of.
        public float Breadth;               // How wide the shoulders are either side.
        public Vector3[] Line = Array.Empty<Vector3>();
        public Vector3 SpineFrom, ChestFrom, NeckRoot, Atlas;
        public Vector3 HeadForward, HeadUp;
        public float HeadLength;
        public Vector3 Shoulder, Carpus, ForeCoronet;   // The left foreleg: where it hangs from, the knee (the wrist), and the top of the foot.
        public Vector3 Hip, Hock, HindCoronet;          // The left hind leg: the hip joint, the hock and the top of the foot.

        /// <summary>The same point on the right side.</summary>
        public static Vector3 Right(Vector3 left) => new Vector3(-left.X, left.Y, left.Z);
    }

    /// <summary>
    /// How the skin of an animal on four legs hangs on its bones and how its texture lies, which is the same whatever the animal: along the body a stretch of skin belongs to whichever bone that
    /// stretch is and blends into the next across each joint; a leg's skin is its own bones', blending at the knee or hock and at the foot. Where a leg meets the body the two share, half each right
    /// at the join, over a wide band round the shoulder or the hip and a narrow one further down, where the legs lie close against the belly: shared there, the inside of a forearm stayed with the
    /// belly as the leg swung, and the belly between the legs went with whichever leg was nearest, which changed from one side of it to the other, and walking tore it into flaps. What colour it is
    /// and where its coat grows are each animal's own.
    /// </summary>
    public abstract class QuadrupedDress : ISkinDresser
    {
        public const int Round = 0;         // Round the line of the body.
        public const int Side = 1;          // From the side, for the head.
        public const int LimbStart = 2;     // Round each leg, from here on.

        protected readonly QuadrupedFrame Frame;
        private readonly float _tile;
        private readonly Vector3[] _line;
        private readonly float[] _lineAt;
        private readonly (float At, BoneType Bone)[] _chain;

        protected QuadrupedDress(QuadrupedFrame frame)
        {
            Frame = frame;
            _tile = frame.Withers * 0.24f;
            _line = frame.Line;
            _lineAt = new float[_line.Length];
            for (int i = 1; i < _line.Length; i++) _lineAt[i] = _lineAt[i - 1] + (_line[i] - _line[i - 1]).Length;

            _chain = new[]
            {
                (0f, BoneType.Pelvis),
                (AlongOf(frame.SpineFrom), BoneType.Spine),
                (AlongOf(frame.ChestFrom), BoneType.Chest),
                (AlongOf(frame.NeckRoot), BoneType.Neck),
                (AlongOf(frame.Atlas), BoneType.Head),
            };
        }

        /// <summary>A share of the withers.</summary>
        protected float W(float share) => Frame.Withers * share;

        protected static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        protected static float Segment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a;
            t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared, 1e-9f), 0f, 1f);
            return (a + ab * t - p).Length;
        }

        protected static bool IsLeg(SaurianPart part) => part is SaurianPart.LegL or SaurianPart.LegR or SaurianPart.ForeL or SaurianPart.ForeR;

        /// <summary>A point where the body is, in the head's own frame: across, up and forward from the joint it turns on.</summary>
        protected Vector3 ToHead(Vector3 p)
        {
            Vector3 d = p - Frame.Atlas;
            return new Vector3(d.X, Vector3.Dot(d, Frame.HeadUp), Vector3.Dot(d, Frame.HeadForward));
        }

        // How far along the line of the body the nearest point of it is.
        protected float AlongOf(Vector3 p) => Nearest(p, out _);

        private float Nearest(Vector3 p, out int segment)
        {
            float best = float.MaxValue, along = 0f;
            segment = 0;
            for (int i = 0; i + 1 < _line.Length; i++)
            {
                float d = Segment(p, _line[i], _line[i + 1], out float t);
                if (d >= best) continue;
                best = d;
                along = _lineAt[i] + (_lineAt[i + 1] - _lineAt[i]) * t;
                segment = i;
            }
            return along;
        }

        private Vector3 LineAt(float along, out Vector3 way)
        {
            int i = 0;
            while (i + 2 < _line.Length && along > _lineAt[i + 1]) i++;
            float t = Math.Clamp((along - _lineAt[i]) / MathF.Max(_lineAt[i + 1] - _lineAt[i], 1e-6f), 0f, 1f);
            way = (_line[i + 1] - _line[i]).Normalized();
            return Vector3.Lerp(_line[i], _line[i + 1], t);
        }

        // ---- Bones.

        /// <summary>The bones a point on the body's own skin hangs from, for what's laid on it.</summary>
        public Hang BodyHang(Vector3 p) => Along(AlongOf(p));

        /// <summary>The bones a stretch of the body's line hangs from, each joint blending over a third of the stretch either side of it.</summary>
        private Hang Along(float along)
        {
            int k = 0;
            while (k + 1 < _chain.Length && along >= _chain[k + 1].At) k++;
            float start = _chain[k].At;
            float end = k + 1 < _chain.Length ? _chain[k + 1].At : float.MaxValue;
            float own = end == float.MaxValue ? Frame.HeadLength : end - start;
            if (k > 0)
            {
                float width = 0.35f * MathF.Min(own, start - _chain[k - 1].At);
                if (along < start + width) return new Hang(_chain[k].Bone, _chain[k - 1].Bone, Smooth(start - width, start + width, along));
            }
            if (end != float.MaxValue)
            {
                float after = k + 2 < _chain.Length ? _chain[k + 2].At - end : Frame.HeadLength;
                float width = 0.35f * MathF.Min(own, after);
                if (along > end - width) return new Hang(_chain[k].Bone, _chain[k + 1].Bone, 1f - Smooth(end - width, end + width, along));
            }
            return new Hang(_chain[k].Bone);
        }

        // Two bones at most: the strongest of what the leg and the body each bring, the leg with this much of the say.
        private static Hang Mix(Hang limb, Hang body, float limbShare)
        {
            float limbFirst = limb.Share * limbShare;
            float bodyFirst = body.Share * (1f - limbShare);
            return limbFirst >= bodyFirst ? new Hang(limb.First, body.First, limbFirst / (limbFirst + bodyFirst)) : new Hang(body.First, limb.First, bodyFirst / (limbFirst + bodyFirst));
        }

        public Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts)
        {
            Hang body = Along(AlongOf(p));
            if (part == SaurianPart.Head) return body;

            // Which leg a point is on, or nearest, of the two on its own side.
            bool onLeg = IsLeg(part);
            bool left = p.X >= 0f;
            SaurianPart fore = left ? SaurianPart.ForeL : SaurianPart.ForeR, hind = left ? SaurianPart.LegL : SaurianPart.LegR;
            SaurianPart leg = onLeg ? part : parts[(int)fore] <= parts[(int)hind] ? fore : hind;
            LegJoints(leg, out Vector3 top, out _, out _, out BoneType upper, out _, out _);

            // How far it is to the body's surface less how far to the leg's, which is 0 at the join: the two share across a band that's wide round the shoulder or the hip and narrow
            // further down, and none at all along the middle of the belly.
            float gap = parts[(int)SaurianPart.Body] - parts[(int)leg];
            bool forelegs = leg is SaurianPart.ForeL or SaurianPart.ForeR;
            float near = 1f - Smooth(W(0.08f), W(forelegs ? 0.3f : 0.34f), (p - top).Length);
            float band = MathHelper.Lerp(W(0.015f), W(0.18f), near) * Smooth(Frame.Breadth * 0.15f, Frame.Breadth * 0.45f, MathF.Abs(p.X));
            float share = band > 1e-6f ? Smooth(-1f, 1f, gap / band) : gap >= 0f ? 1f : 0f;

            Hang limb = onLeg ? LegHang(leg, p) : new Hang(upper);
            if (share >= 0.999f) return limb;
            if (share <= 0.001f) return body;
            return Mix(limb, body, share);
        }

        // A leg's three bones: from the shoulder or hip down to the knee or hock, from there to the top of the foot, and the foot.
        protected void LegJoints(SaurianPart part, out Vector3 top, out Vector3 middle, out Vector3 bottom, out BoneType upper, out BoneType lower, out BoneType end)
        {
            bool fore = part is SaurianPart.ForeL or SaurianPart.ForeR;
            bool left = part is SaurianPart.ForeL or SaurianPart.LegL;
            top = fore ? Frame.Shoulder : Frame.Hip;
            middle = fore ? Frame.Carpus : Frame.Hock;
            bottom = fore ? Frame.ForeCoronet : Frame.HindCoronet;
            if (!left)
            {
                top = QuadrupedFrame.Right(top);
                middle = QuadrupedFrame.Right(middle);
                bottom = QuadrupedFrame.Right(bottom);
            }
            upper = fore ? (left ? BoneType.UpperArmL : BoneType.UpperArmR) : (left ? BoneType.UpperLegL : BoneType.UpperLegR);
            lower = fore ? (left ? BoneType.LowerArmL : BoneType.LowerArmR) : (left ? BoneType.LowerLegL : BoneType.LowerLegR);
            end = fore ? (left ? BoneType.HandL : BoneType.HandR) : (left ? BoneType.FootL : BoneType.FootR);
        }

        private Hang LegHang(SaurianPart part, Vector3 p)
        {
            LegJoints(part, out Vector3 top, out Vector3 middle, out Vector3 bottom, out BoneType upper, out BoneType lower, out BoneType end);

            // The foot is all the foot's, and just above it shares with what's above.
            if (p.Y < bottom.Y) return new Hang(end, lower, 1f - 0.5f * Smooth(bottom.Y - W(0.008f), bottom.Y, p.Y));
            float a = Segment(p, top, middle, out float ta);
            float b = Segment(p, middle, bottom, out float tb);
            if (a <= b) return ta > 0.8f ? new Hang(upper, lower, 1f - 0.5f * Smooth(0.8f, 1f, ta)) : new Hang(upper);
            if (tb < 0.2f) return new Hang(lower, upper, 0.5f + 0.5f * Smooth(0f, 0.2f, tb));
            if (tb > 0.88f) return new Hang(lower, end, 1f - 0.5f * Smooth(0.88f, 1f, tb));
            return new Hang(lower);
        }

        // ---- How a texture lies.

        public int Mapping(Vector3 p, SaurianPart part) => part switch
        {
            SaurianPart.Head => Side,
            SaurianPart.Body => Round,
            _ => LimbStart + (int)part,
        };

        public Vector2 Uv(Vector3 p, int mapping, out float period)
        {
            period = 0f;
            if (mapping == Side)
            {
                Vector3 local = ToHead(p);
                return new Vector2(local.Z, local.Y) / _tile;
            }
            if (mapping == Round)
            {
                float along = AlongOf(p);
                Vector3 centre = LineAt(along, out Vector3 way);
                Vector3 up = Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way);
                up = up.LengthSquared > 1e-6f ? up.Normalized() : Vector3.UnitZ;
                Vector3 across = Vector3.Cross(up, way);
                Vector3 off = p - centre;

                // The seam runs along the belly, where nothing sees it.
                float angle = MathF.Atan2(Vector3.Dot(off, across), Vector3.Dot(off, up));
                float radius = MathF.Max(off.Length, W(0.1f));
                period = MathF.Tau * radius / _tile;
                return new Vector2(along / _tile, angle * radius / _tile);
            }

            SaurianPart part = (SaurianPart)(mapping - LimbStart);
            (Vector3 a, Vector3 b, float start) = LimbSpan(part, p);
            Vector3 axis = (b - a).Normalized();
            Segment(p, a, b, out float t);
            Vector3 point = a + (b - a) * t;
            Vector3 side = Vector3.Cross(axis, Vector3.UnitZ);
            if (side.LengthSquared < 1e-4f) side = Vector3.Cross(axis, Vector3.UnitX);
            side.Normalize();
            Vector3 front = Vector3.Cross(side, axis);
            float r = MathF.Max((p - point).Length, W(0.02f));
            float turn = MathF.Atan2(Vector3.Dot(p - point, front), Vector3.Dot(p - point, side));
            period = MathF.Tau * r / _tile;
            return new Vector2((start + (b - a).Length * t) / _tile, turn * r / _tile);
        }

        private (Vector3 a, Vector3 b, float start) LimbSpan(SaurianPart part, Vector3 p)
        {
            LegJoints(part, out Vector3 top, out Vector3 middle, out Vector3 bottom, out _, out _, out _);
            float upper = Segment(p, top, middle, out _);
            float lower = Segment(p, middle, bottom, out _);
            return upper <= lower ? (top, middle, 0f) : (middle, bottom, (middle - top).Length);
        }

        public Vector3 Tangent(Vector3 p, Vector3 normal, int mapping)
        {
            if (mapping == Side) return Frame.HeadForward;
            if (mapping == Round)
            {
                LineAt(AlongOf(p), out Vector3 way);
                return way;
            }
            (Vector3 a, Vector3 b, _) = LimbSpan((SaurianPart)(mapping - LimbStart), p);
            return (b - a).Normalized();
        }

        // ---- What's each animal's own.

        public abstract Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow);

        public abstract float Shell(Vector3 p, Vector3 normal, SaurianPart part);
    }
}
