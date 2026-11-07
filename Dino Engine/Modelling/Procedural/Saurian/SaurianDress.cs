using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// How a dinosaur's skin hangs on its bones, and how its texture lies. Along the body a stretch of skin belongs to whichever bone that stretch of the line is, and blends into the next across each
    /// joint. A limb's skin is its own bones', blending at the knee and ankle and softly into the body where it joins. Two bones a vertex at most, which is all a shader reads.
    /// </summary>
    public class SaurianDress : ISkinDresser
    {
        public const int Round = 0;         // Round the line of the body.
        public const int Side = 1;          // From the side, for a head.
        public const int LimbStart = 2;     // Round each limb, from here on.

        protected readonly SaurianBody Body;
        private readonly float _tile;
        private readonly (float At, BoneType Bone)[] _chain;
        private readonly SaurianPaint _paint;

        public SaurianDress(SaurianBody body, SaurianPaint paint)
        {
            Body = body;
            _paint = paint;
            _tile = body.Length / 7f;
            _chain = new[]
            {
                (0f, BoneType.TailTip),
                (body.TipAt, BoneType.Tail3),
                (body.Tail3At, BoneType.Tail2),
                (body.Tail2At, BoneType.Tail1),
                (body.Tail1At, BoneType.TailBase),
                (body.TailRootAt, BoneType.Pelvis),
                (body.SpineAt, BoneType.Spine),
                (body.ChestAt, BoneType.Chest),
                (body.NeckAt, BoneType.Neck),
                (body.NeckTopAt, BoneType.NeckTop),
                (body.HeadAt, BoneType.Head),
            };
        }

        // ---- Bones.

        /// <summary>The bones a stretch of the body's line hangs from, by how far along it is.</summary>
        public Hang Along(float along)
        {
            int k = 0;
            while (k + 1 < _chain.Length && along >= _chain[k + 1].At) k++;

            // Each joint blends over a little of the stretch either side of it.
            float start = _chain[k].At;
            float end = k + 1 < _chain.Length ? _chain[k + 1].At : float.MaxValue;
            float before = k > 0 ? start - _chain[k - 1].At : float.MaxValue;
            float after = k + 2 < _chain.Length ? _chain[k + 2].At - end : end - start;
            float own = end == float.MaxValue ? Body.Head.Length : end - start;

            if (k > 0)
            {
                float width = 0.35f * MathF.Min(own, before);
                if (along < start + width)
                {
                    float share = Smooth(start - width, start + width, along);
                    return new Hang(_chain[k].Bone, _chain[k - 1].Bone, share);
                }
            }
            if (end != float.MaxValue)
            {
                float width = 0.35f * MathF.Min(own, after);
                if (along > end - width)
                {
                    float share = Smooth(end - width, end + width, along);
                    return new Hang(_chain[k].Bone, _chain[k + 1].Bone, 1f - share);
                }
            }
            return new Hang(_chain[k].Bone);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public virtual Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts)
        {
            switch (part)
            {
                case SaurianPart.Head:
                    return Along(Body.Axis.Nearest(p, out _));
                case SaurianPart.LegL:
                case SaurianPart.LegR:
                case SaurianPart.ForeL:
                case SaurianPart.ForeR:
                    return Joined(p, LegHang(LegOf(part), p), parts[(int)part], parts[(int)SaurianPart.Body]);
                case SaurianPart.ArmL:
                case SaurianPart.ArmR:
                    return Joined(p, ArmHang(Body.Arms[part == SaurianPart.ArmL ? 0 : 1], p), parts[(int)part], parts[(int)SaurianPart.Body]);
                default:
                    return Along(Body.Axis.Nearest(p, out _));
            }
        }

        private SaurianLeg LegOf(SaurianPart part) => part switch
        {
            SaurianPart.LegL => Body.Legs[0],
            SaurianPart.LegR => Body.Legs[1],
            SaurianPart.ForeL => Body.Legs[2],
            _ => Body.Legs[3],
        };

        // Where a limb meets the body, its skin is shared with whatever bone the body is there, by how close the body's surface is.
        private Hang Joined(Vector3 p, Hang limb, float limbDistance, float bodyDistance)
        {
            float gap = bodyDistance - limbDistance;
            float reach = Body.ChestDepth * 0.35f;
            if (gap >= reach) return limb;
            float shared = 0.5f * (1f - Smooth(0f, reach, gap));
            Hang body = Along(Body.Axis.Nearest(p, out _));

            // Two bones at most: the strongest of what the limb and the body each bring.
            float limbFirst = limb.Share * (1f - shared);
            float bodyFirst = body.Share * shared;
            return limbFirst >= bodyFirst ? new Hang(limb.First, body.First, limbFirst / (limbFirst + bodyFirst)) : new Hang(body.First, limb.First, bodyFirst / (limbFirst + bodyFirst));
        }

        private static float Segment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a;
            t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared, 1e-9f), 0f, 1f);
            return (a + ab * t - p).Length;
        }

        private Hang LegHang(SaurianLeg leg, Vector3 p)
        {
            bool left = leg.Side > 0f;
            BoneType upper = leg.Front ? (left ? BoneType.UpperArmL : BoneType.UpperArmR) : (left ? BoneType.UpperLegL : BoneType.UpperLegR);
            BoneType lower = leg.Front ? (left ? BoneType.LowerArmL : BoneType.LowerArmR) : (left ? BoneType.LowerLegL : BoneType.LowerLegR);
            BoneType foot = leg.Front ? (left ? BoneType.HandL : BoneType.HandR) : (left ? BoneType.FootL : BoneType.FootR);
            BoneType toe = leg.Front ? foot : (left ? BoneType.ToeL : BoneType.ToeR);

            float thigh = Segment(p, leg.Hip, leg.Knee, out float tThigh);
            float shin = Segment(p, leg.Knee, leg.Ankle, out float tShin);
            float meta = Segment(p, leg.Ankle, leg.Ball, out float tMeta);

            // Knee and ankle each blend over the last and first part of what meets there.
            if (thigh <= shin && thigh <= meta) return tThigh > 0.8f ? new Hang(upper, lower, 1f - 0.5f * Smooth(0.8f, 1f, tThigh)) : new Hang(upper);
            if (shin <= meta)
            {
                if (tShin < 0.2f) return new Hang(lower, upper, 0.5f + 0.5f * Smooth(0f, 0.2f, tShin));
                if (tShin > 0.85f) return new Hang(lower, foot, 1f - 0.5f * Smooth(0.85f, 1f, tShin));
                return new Hang(lower);
            }
            if (tMeta < 0.15f) return new Hang(foot, lower, 0.5f + 0.5f * Smooth(0f, 0.15f, tMeta));

            // The pad under the ball of the foot goes half with the toes, as they stay flat when the foot rolls over them.
            if (tMeta > 0.92f && Vector3.Dot(p - leg.Ball, leg.Ball - leg.Ankle) > 0f) return new Hang(foot, toe, 0.5f);
            return new Hang(foot);
        }

        private static Hang ArmHang(SaurianArm arm, Vector3 p)
        {
            bool left = arm.Side > 0f;
            BoneType upper = left ? BoneType.UpperArmL : BoneType.UpperArmR;
            BoneType lower = left ? BoneType.LowerArmL : BoneType.LowerArmR;
            BoneType hand = left ? BoneType.HandL : BoneType.HandR;
            float a = Segment(p, arm.Shoulder, arm.Elbow, out float ta);
            float b = Segment(p, arm.Elbow, arm.Wrist, out float tb);
            float c = Segment(p, arm.Wrist, arm.Hand, out _);
            if (a <= b && a <= c) return ta > 0.8f ? new Hang(upper, lower, 1f - 0.5f * Smooth(0.8f, 1f, ta)) : new Hang(upper);
            if (b <= c)
            {
                if (tb < 0.2f) return new Hang(lower, upper, 0.5f + 0.5f * Smooth(0f, 0.2f, tb));
                return tb > 0.85f ? new Hang(lower, hand, 1f - 0.5f * Smooth(0.85f, 1f, tb)) : new Hang(lower);
            }
            return new Hang(hand);
        }

        // ---- How a texture lies.

        public virtual int Mapping(Vector3 p, SaurianPart part) => part switch
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
                Vector3 local = Body.Head.ToHead(p);
                return new Vector2(local.Z, local.Y) / _tile;
            }
            if (mapping == Round)
            {
                float along = Body.Axis.Nearest(p, out _);
                Vector3 centre = Body.Axis.At(along);
                Vector3 way = Body.Axis.Way(along);
                Vector3 up = Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way);
                up = up.LengthSquared > 1e-6f ? up.Normalized() : Vector3.UnitZ;
                Vector3 across = Vector3.Cross(up, way);
                Vector3 off = p - centre;

                // The seam runs along the belly, where nothing sees it.
                float angle = MathF.Atan2(Vector3.Dot(off, across), Vector3.Dot(off, up));
                float radius = (Body.Axis.DeepAt(along) + Body.Axis.WideAt(along)) * 0.5f;
                period = MathF.Tau * radius / _tile;
                return new Vector2(along / _tile, angle * radius / _tile);
            }

            // Round a limb, along whichever of its bones is nearest.
            SaurianPart part = (SaurianPart)(mapping - LimbStart);
            (Vector3 a, Vector3 b, float r, float start) = LimbSpan(part, p);
            Vector3 axis = (b - a).Normalized();
            Segment(p, a, b, out float t);
            Vector3 point = a + (b - a) * t;
            Vector3 side = Vector3.Cross(axis, Vector3.UnitZ);
            if (side.LengthSquared < 1e-4f) side = Vector3.Cross(axis, Vector3.UnitX);
            side.Normalize();
            Vector3 front = Vector3.Cross(side, axis);
            float turn = MathF.Atan2(Vector3.Dot(p - point, front), Vector3.Dot(p - point, side));
            period = MathF.Tau * r / _tile;
            return new Vector2((start + (b - a).Length * t) / _tile, turn * r / _tile);
        }

        private (Vector3 a, Vector3 b, float radius, float start) LimbSpan(SaurianPart part, Vector3 p)
        {
            if (part is SaurianPart.ArmL or SaurianPart.ArmR)
            {
                SaurianArm arm = Body.Arms[part == SaurianPart.ArmL ? 0 : 1];
                float a = Segment(p, arm.Shoulder, arm.Elbow, out _);
                float b = Segment(p, arm.Elbow, arm.Wrist, out _);
                float upperLength = (arm.Elbow - arm.Shoulder).Length;
                if (a <= b) return (arm.Shoulder, arm.Elbow, arm.Radius, 0f);
                return (arm.Elbow, arm.Hand, arm.Radius * 0.7f, upperLength);
            }

            SaurianLeg leg = LegOf(part);
            float thigh = Segment(p, leg.Hip, leg.Knee, out _);
            float shin = Segment(p, leg.Knee, leg.Ankle, out _);
            float meta = Segment(p, leg.Ankle, leg.Ball, out _);
            float femur = (leg.Knee - leg.Hip).Length;
            float tibia = (leg.Ankle - leg.Knee).Length;
            if (thigh <= shin && thigh <= meta) return (leg.Hip, leg.Knee, leg.HipRadius * 0.8f, 0f);
            if (shin <= meta) return (leg.Knee, leg.Ankle, leg.KneeRadius * 0.8f, femur);
            return (leg.Ankle, leg.Ball, leg.AnkleRadius, femur + tibia);
        }

        public Vector3 Tangent(Vector3 p, Vector3 normal, int mapping)
        {
            if (mapping == Side) return Body.Head.Forward;
            if (mapping == Round) return Body.Axis.Way(Body.Axis.Nearest(p, out _));
            (Vector3 a, Vector3 b, _, _) = LimbSpan((SaurianPart)(mapping - LimbStart), p);
            return (b - a).Normalized();
        }

        public virtual Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
        {
            glow = 1f;
            return _paint.Hide(p, normal, part, out material);
        }
    }
}
