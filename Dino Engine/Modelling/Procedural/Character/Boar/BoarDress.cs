using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// How a boar's skin hangs on its bones, how its texture lies, what colour it is and where its bristles grow. Along the body a stretch of skin belongs to whichever bone that stretch is and
    /// blends into the next across each joint; a leg's skin is its own bones', blending at the knee or hock and at the hoof, and the body and a leg share where they meet, half each right at the
    /// join, so neither side of it pulls away from the other.
    /// </summary>
    public sealed class BoarDress : ISkinDresser
    {
        public const int Round = 0;         // Round the line of the body.
        public const int Side = 1;          // From the side, for the head.
        public const int LimbStart = 2;     // Round each leg, from here on.

        private readonly BoarBody _body;
        private readonly float _tile;
        private readonly Vector3[] _line;
        private readonly float[] _lineAt;
        private readonly (float At, BoneType Bone)[] _chain;
        private readonly uint _seed;

        public BoarDress(BoarBody body)
        {
            _body = body;
            _seed = (uint)(body.Seed ^ (body.Seed >> 32));
            _tile = body.W(0.24f);

            // The line of the body, from the back of the rump through the middle of the trunk and up the neck to the head, and down the head to the end of the snout.
            var line = new List<Vector3> { new Vector3(0f, body.Trunk[0].Middle, body.RumpBack) };
            foreach (BoarStation station in body.Trunk) line.Add(new Vector3(0f, station.Middle, station.Z));
            line.Add(body.Atlas);
            line.Add(body.SnoutTip);
            _line = line.ToArray();
            _lineAt = new float[_line.Length];
            for (int i = 1; i < _line.Length; i++) _lineAt[i] = _lineAt[i - 1] + (_line[i] - _line[i - 1]).Length;

            float spacing = body.ForeZ - body.HindZ;
            _chain = new[]
            {
                (0f, BoneType.Pelvis),
                (AlongOf(new Vector3(0f, body.Hip.Y, body.HindZ + spacing * 0.3f)), BoneType.Spine),
                (AlongOf(new Vector3(0f, body.Shoulder.Y, body.ForeZ - spacing * 0.25f)), BoneType.Chest),
                (AlongOf(body.NeckRoot), BoneType.Neck),
                (AlongOf(body.Atlas), BoneType.Head),
            };
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float Segment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            Vector3 ab = b - a;
            t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared, 1e-9f), 0f, 1f);
            return (a + ab * t - p).Length;
        }

        // How far along the line of the body the nearest point of it is.
        private float AlongOf(Vector3 p) => Nearest(p, out _);

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

        /// <summary>The bones a stretch of the body's line hangs from, each joint blending over a third of the stretch either side of it.</summary>
        private Hang Along(float along)
        {
            int k = 0;
            while (k + 1 < _chain.Length && along >= _chain[k + 1].At) k++;
            float start = _chain[k].At;
            float end = k + 1 < _chain.Length ? _chain[k + 1].At : float.MaxValue;
            float own = end == float.MaxValue ? _body.HeadLength : end - start;
            if (k > 0)
            {
                float width = 0.35f * MathF.Min(own, start - _chain[k - 1].At);
                if (along < start + width) return new Hang(_chain[k].Bone, _chain[k - 1].Bone, Smooth(start - width, start + width, along));
            }
            if (end != float.MaxValue)
            {
                float after = k + 2 < _chain.Length ? _chain[k + 2].At - end : _body.HeadLength;
                float width = 0.35f * MathF.Min(own, after);
                if (along > end - width) return new Hang(_chain[k].Bone, _chain[k + 1].Bone, 1f - Smooth(end - width, end + width, along));
            }
            return new Hang(_chain[k].Bone);
        }

        private float JoinReach => _body.W(0.18f);

        // Two bones at most: the strongest of what the leg and the body each bring, the leg with this much of the say.
        private static Hang Mix(Hang limb, Hang body, float limbShare)
        {
            float limbFirst = limb.Share * limbShare;
            float bodyFirst = body.Share * (1f - limbShare);
            return limbFirst >= bodyFirst ? new Hang(limb.First, body.First, limbFirst / (limbFirst + bodyFirst)) : new Hang(body.First, limb.First, bodyFirst / (limbFirst + bodyFirst));
        }

        public Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts)
        {
            if (part == SaurianPart.Head) return Along(AlongOf(p));
            if (IsLeg(part))
            {
                float gap = parts[(int)SaurianPart.Body] - parts[(int)part];
                Hang leg = LegHang(part, p);
                if (gap >= JoinReach) return leg;
                return Mix(leg, Along(AlongOf(p)), 0.5f + 0.5f * Smooth(0f, JoinReach, gap));
            }

            // The body's skin near a leg shares with it as the leg's does with the body, so the two meet halfway.
            Hang body = Along(AlongOf(p));
            SaurianPart nearest = SaurianPart.LegL;
            for (SaurianPart leg = SaurianPart.LegL; leg <= SaurianPart.ForeR; leg++)
            {
                if (parts[(int)leg] < parts[(int)nearest]) nearest = leg;
            }
            float off = parts[(int)nearest] - parts[(int)SaurianPart.Body];
            if (off >= JoinReach) return body;
            return Mix(LegHang(nearest, p), body, 0.5f * (1f - Smooth(0f, JoinReach, off)));
        }

        private static bool IsLeg(SaurianPart part) => part is SaurianPart.LegL or SaurianPart.LegR or SaurianPart.ForeL or SaurianPart.ForeR;

        // A leg's three bones: from the shoulder or hip down to the knee or hock, from there to the top of the hoof, and the hoof.
        private void LegJoints(SaurianPart part, out Vector3 top, out Vector3 middle, out Vector3 bottom, out BoneType upper, out BoneType lower, out BoneType end)
        {
            bool fore = part is SaurianPart.ForeL or SaurianPart.ForeR;
            bool left = part is SaurianPart.ForeL or SaurianPart.LegL;
            top = fore ? _body.Shoulder : _body.Hip;
            middle = fore ? _body.Carpus : _body.Hock;
            bottom = fore ? _body.ForeCoronet : _body.HindCoronet;
            if (!left)
            {
                top = BoarBody.Right(top);
                middle = BoarBody.Right(middle);
                bottom = BoarBody.Right(bottom);
            }
            upper = fore ? (left ? BoneType.UpperArmL : BoneType.UpperArmR) : (left ? BoneType.UpperLegL : BoneType.UpperLegR);
            lower = fore ? (left ? BoneType.LowerArmL : BoneType.LowerArmR) : (left ? BoneType.LowerLegL : BoneType.LowerLegR);
            end = fore ? (left ? BoneType.HandL : BoneType.HandR) : (left ? BoneType.FootL : BoneType.FootR);
        }

        private Hang LegHang(SaurianPart part, Vector3 p)
        {
            LegJoints(part, out Vector3 top, out Vector3 middle, out Vector3 bottom, out BoneType upper, out BoneType lower, out BoneType end);

            // The hoof is all the foot's, and the coronet above it shares with the pastern.
            if (p.Y < bottom.Y) return new Hang(end, lower, 1f - 0.5f * Smooth(bottom.Y - _body.W(0.008f), bottom.Y, p.Y));
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
                Vector3 local = _body.ToHead(p);
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
                float radius = MathF.Max(off.Length, _body.W(0.1f));
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
            float r = MathF.Max((p - point).Length, _body.W(0.02f));
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
            if (mapping == Side) return _body.HeadForward;
            if (mapping == Round)
            {
                LineAt(AlongOf(p), out Vector3 way);
                return way;
            }
            (Vector3 a, Vector3 b, _) = LimbSpan((SaurianPart)(mapping - LimbStart), p);
            return (b - a).Normalized();
        }

        // ---- Colour.

        private float Noise(Vector3 p, float size, uint salt) => FloraNoise.Value(p / size, _seed + salt);

        // The trunk: dark along the top and the mane, the flanks a little lighter, the belly no paler than a wild boar's is, all of it grizzled in patches.
        private Vector3 Coat(Vector3 p)
        {
            BoarHide hide = _body.Hide;
            BoarStation station = _body.Station(MathF.Min(p.Z, _body.Trunk[^1].Z));
            float height = (p.Y - station.Middle) / MathF.Max(station.Deep, 1e-3f);
            Vector3 colour = Vector3.Lerp(hide.Belly, hide.Flank, Smooth(-0.75f, -0.1f, height));
            colour = Vector3.Lerp(colour, hide.Back, Smooth(0.3f, 0.9f, height));

            // The mane's longer bristles are darker, from behind the ears to the middle of the back.
            float maneFrom = _body.HindZ + (_body.ForeZ - _body.HindZ) * 0.3f;
            float mane = Smooth(0.82f, 1.05f, height) * Smooth(maneFrom, maneFrom + _body.W(0.2f), p.Z);
            colour = Vector3.Lerp(colour, hide.Back * 0.7f, mane);

            // Darker over the front of the shoulders and the throat, as most wild boars are.
            float front = Smooth(_body.ForeZ, _body.ChestFront + _body.W(0.1f), p.Z) * (1f - Smooth(0.2f, 0.8f, height));
            colour = Vector3.Lerp(colour, hide.Face, front * 0.55f);

            float grizzle = 0.88f + 0.24f * Noise(p, _body.W(0.2f), 11u) + 0.1f * (Noise(p, _body.W(0.05f), 12u) - 0.5f);
            return colour * grizzle;
        }

        public Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
        {
            glow = 1f;
            material = GearMaterials.Leather;
            BoarHide hide = _body.Hide;
            if (IsLeg(part))
            {
                if (p.Y < _body.AnkleHeight + _body.W(0.003f)) return hide.Hoof * (0.9f + 0.2f * Noise(p, _body.W(0.03f), 21u));
                float dark = 1f - Smooth(_body.W(0.22f), _body.Brisket + _body.W(0.06f), p.Y);
                return Vector3.Lerp(Coat(p), hide.Legs, dark);
            }
            if (part == SaurianPart.Head) return Face(p, normal);
            return Coat(p);
        }

        // The head: the top of it the colour of the back, the face and snout darker, a pale grizzled band along the cheek under the eye, darker lips, and a bare pink-grey disc with its nostrils.
        private Vector3 Face(Vector3 p, Vector3 normal)
        {
            BoarHide hide = _body.Hide;
            Vector3 local = _body.ToHead(p) / _body.HeadLength;
            float forward = local.Z, up = local.Y, across = MathF.Abs(local.X);

            if (forward > 0.93f && Vector3.Dot(normal, _body.HeadForward) > 0.45f)
            {
                float nostril = MathF.Min(new Vector2(across - 0.035f, up + 0.015f).Length, 1f);
                return Vector3.Lerp(hide.Disc * 0.25f, hide.Disc, Smooth(0.012f, 0.026f, nostril));
            }

            Vector3 colour = Vector3.Lerp(hide.Back, hide.Face, Smooth(0.18f, 0.5f, forward));
            float cheek = Smooth(0.06f, 0.18f, forward) * (1f - Smooth(0.48f, 0.66f, forward)) * Smooth(0.08f, -0.06f, up) * Smooth(0.05f, 0.15f, across);
            colour = Vector3.Lerp(colour, hide.Cheek, cheek * 0.85f);

            // The line of the mouth, back from the disc to the corner of the lips under the eye.
            float lip = MathHelper.Lerp(-0.075f, -0.06f, Smooth(0.45f, 0.95f, forward));
            float mouth = (1f - Smooth(0.008f, 0.02f, MathF.Abs(up - lip))) * Smooth(0.42f, 0.5f, forward) * Smooth(0.03f, 0.07f, across);
            colour = Vector3.Lerp(colour, hide.Face * 0.45f, mouth);

            float grizzle = 0.9f + 0.2f * Noise(p, _body.W(0.12f), 31u);
            return colour * grizzle;
        }

        // ---- Bristles.

        /// <summary>
        /// Where its coat grows (SHELLS.md): over all of the body, thinner down the legs to the fetlocks and none on the hooves, thinner down the snout and none on its disc or the lips.
        /// </summary>
        public float Shell(Vector3 p, Vector3 normal, SaurianPart part)
        {
            if (IsLeg(part))
            {
                if (p.Y < _body.AnkleHeight + _body.W(0.006f)) return 0f;
                return MathHelper.Lerp(0.6f, 1f, Smooth(_body.W(0.12f), _body.Brisket, p.Y));
            }
            if (part != SaurianPart.Head) return 1f;

            Vector3 local = _body.ToHead(p) / _body.HeadLength;
            if (local.Z > 0.92f && Vector3.Dot(normal, _body.HeadForward) > 0.35f) return 0f;
            float lips = Smooth(-0.04f, -0.1f, local.Y) * Smooth(0.5f, 0.7f, local.Z);
            return (1f - 0.45f * Smooth(0.45f, 0.85f, local.Z)) * (1f - 0.6f * lips);
        }
    }
}
