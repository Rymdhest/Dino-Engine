using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>
    /// What's the same about every body built one way when it stops being held up: which points it's made of, what joins them, and how far each joint can go. One of these serves everyone on a rig.
    /// It's all read off the rig, so anything with bones can fall over, and nobody has to say how.
    /// </summary>
    internal sealed class RagdollPlan
    {
        public readonly struct Stick
        {
            public readonly int A;
            public readonly int B;
            public readonly float Length;
            public readonly float ShareA;   // How much of a correction goes to each end, by how heavy they are.
            public readonly float ShareB;

            public Stick(int a, int b, float length, float inverseA, float inverseB)
            {
                A = a;
                B = b;
                Length = length;
                float total = MathF.Max(inverseA + inverseB, 1e-5f);
                ShareA = inverseA / total;
                ShareB = inverseB / total;
            }
        }

        /// <summary>A joint that only bends one way. Going by the frame of what it hangs from, <see cref="Sign"/> says which way is forward.</summary>
        public readonly record struct Hinge(int Root, int Middle, int End, float Sign, bool OnHips);

        public const int HeadTop = Rig.BoneCount;
        public const int PointCount = Rig.BoneCount + 1;

        public readonly Rig Rig;
        public readonly bool HasTail;
        public readonly float Span;         // The furthest any part of it is from its hips.
        public readonly bool[] Used = new bool[PointCount];
        public readonly float[] InverseMass = new float[PointCount];
        public readonly float[] Radius = new float[PointCount];
        public readonly Vector3[] Rest = new Vector3[PointCount];
        public readonly Stick[] Sticks;
        public readonly Stick[] Spacers;    // Only ever push apart: these are what keep a body from folding through itself.
        public readonly Hinge[] Hinges;

        // What each bone of the body takes its turn from: where it points and what's across it, as they were built.
        public readonly (int from, int to, int left, int right, Quaternion restInverse)[] Trunk;

        private static readonly BoneType[] Spine = { BoneType.Pelvis, BoneType.Spine, BoneType.Chest, BoneType.Neck, BoneType.Head };

        public RagdollPlan(Rig rig)
        {
            Rig = rig;
            bool biped = rig.Plan == BodyPlan.Biped;
            Array.Copy(rig.Pivots, Rest, Rig.BoneCount);
            Vector3 headTop = rig.Pivots[(int)BoneType.Head] + (biped ? Vector3.UnitY * 0.72f : Vector3.UnitZ * 0.7f);
            Rest[HeadTop] = headTop;

            void Use(BoneType bone, float mass, float radius)
            {
                Used[(int)bone] = true;
                InverseMass[(int)bone] = 1f / mass;
                Radius[(int)bone] = radius;
            }

            Use(BoneType.Pelvis, 8f, 0.52f);
            Use(BoneType.Spine, 8f, 0.5f);
            Use(BoneType.Chest, 9f, 0.56f);
            Use(BoneType.Neck, 2f, 0.2f);
            Use(BoneType.Head, 2.5f, 0.32f);
            Used[HeadTop] = true;
            InverseMass[HeadTop] = 1f / 2.5f;
            Radius[HeadTop] = 0.34f;

            foreach (Limb limb in rig.Legs)
            {
                Use(limb.Upper, 3.5f, 0.26f);
                Use(limb.Lower, 2.2f, 0.2f);
                Use(limb.End, 1.2f, biped ? 0.18f : 0.1f);
            }
            foreach (Limb limb in rig.Arms)
            {
                Use(limb.Parent, 1.5f, 0.14f);
                Use(limb.Upper, 2f, 0.16f);
                Use(limb.Lower, 1.3f, 0.12f);
                Use(limb.End, 1f, 0.1f);
            }
            HasTail = !biped && (Rest[(int)BoneType.TailBase] - Rest[(int)BoneType.TailTip]).LengthSquared > 1e-4f;
            if (HasTail)
            {
                Use(BoneType.TailBase, 0.6f, 0.08f);
                Use(BoneType.TailTip, 0.4f, 0.06f);
            }

            for (int i = 0; i < PointCount; i++)
            {
                if (Used[i]) Span = MathF.Max(Span, (Rest[i] - Rest[(int)BoneType.Pelvis]).Length);
            }

            var sticks = new List<Stick>();
            void Join(int a, int b) => sticks.Add(new Stick(a, b, (Rest[a] - Rest[b]).Length, InverseMass[a], InverseMass[b]));
            void Bones(BoneType a, BoneType b) => Join((int)a, (int)b);

            for (int bone = 0; bone < Rig.BoneCount; bone++)
            {
                int parent = rig.Parents[bone];
                if (parent >= 0 && Used[bone] && Used[parent]) Join(bone, parent);
            }
            Join((int)BoneType.Head, HeadTop);

            // Forelegs hang from the chest, where a person's arms hang from a collarbone.
            if (!biped)
            {
                Bones(BoneType.UpperArmL, BoneType.Chest);
                Bones(BoneType.UpperArmR, BoneType.Chest);
            }

            // Hips and shoulders are each a solid piece, and the back is what bends between them.
            BoneType hipL = BoneType.UpperLegL, hipR = BoneType.UpperLegR, shoulderL = BoneType.UpperArmL, shoulderR = BoneType.UpperArmR;
            Bones(hipL, hipR);
            Bones(hipL, BoneType.Spine);
            Bones(hipR, BoneType.Spine);
            Bones(shoulderL, shoulderR);
            Bones(shoulderL, BoneType.Neck);
            Bones(shoulderR, BoneType.Neck);
            Bones(shoulderL, BoneType.Chest);
            Bones(shoulderR, BoneType.Chest);
            Sticks = sticks.ToArray();

            var spacers = new List<Stick>();
            void Apart(int a, int b, float share) => spacers.Add(new Stick(a, b, (Rest[a] - Rest[b]).Length * share, InverseMass[a], InverseMass[b]));

            // A back can curl, but not fold shut, and a head can't go down into a chest.
            Apart((int)BoneType.Pelvis, (int)BoneType.Chest, 0.72f);
            Apart((int)BoneType.Spine, (int)BoneType.Neck, 0.72f);
            Apart((int)BoneType.Chest, (int)BoneType.Head, 0.82f);
            Apart((int)BoneType.Pelvis, HeadTop, 0.72f);

            foreach (Limb limb in rig.Legs)
            {
                Apart((int)limb.Upper, (int)limb.End, 0.42f);
                if (biped) Apart((int)limb.Lower, (int)BoneType.Chest, 0.62f);
            }
            foreach (Limb limb in rig.Arms) Apart((int)limb.Upper, (int)limb.End, 0.32f);
            Spacers = spacers.ToArray();

            var hinges = new List<Hinge>();
            foreach (Limb limb in rig.Legs) hinges.Add(new Hinge((int)limb.Upper, (int)limb.Lower, (int)limb.End, limb.Bends.Z, limb.Parent != BoneType.Chest));
            foreach (Limb limb in rig.Arms) hinges.Add(new Hinge((int)limb.Upper, (int)limb.Lower, (int)limb.End, limb.Bends.Z, false));
            Hinges = hinges.ToArray();

            var trunk = new List<(int, int, int, int, Quaternion)>();
            void Bone(BoneType bone, int to, BoneType left, BoneType right)
            {
                Vector3 along = (Rest[to] - Rest[(int)bone]).Normalized();
                Vector3 across = Frames.Across(Rest[(int)left] - Rest[(int)right], along, Vector3.UnitX);
                trunk.Add(((int)bone, to, (int)left, (int)right, Quaternion.Invert(Frames.Of(along, across))));
            }
            Bone(BoneType.Pelvis, (int)BoneType.Spine, hipL, hipR);
            Bone(BoneType.Spine, (int)BoneType.Chest, hipL, hipR);
            Bone(BoneType.Chest, (int)BoneType.Neck, shoulderL, shoulderR);
            Bone(BoneType.Neck, (int)BoneType.Head, shoulderL, shoulderR);
            Bone(BoneType.Head, HeadTop, shoulderL, shoulderR);
            Trunk = trunk.ToArray();
        }
    }

    /// <summary>
    /// A body with nobody in it. It's a stick figure of points and rods, as Hitman's corpses were: each point remembers where it was instead of how fast it's going, and every rod is put right by moving its ends.
    /// That can lose energy but can't gain any, so nothing here can ever blow up, however hard something is thrown, and thirty at once from one explosion cost next to nothing.
    /// Limbs get their turn from where their joints have ended up, and what they can't get that way, the twist along a bone, is carried on from how they were when the body fell.
    /// </summary>
    public sealed class Ragdoll
    {
        private const float StepTime = 1f / 60f;
        private const int MostSteps = 4;            // In a frame. A long frame just takes a body longer to land.
        private const int Passes = 5;
        private const float Air = 0.992f;           // What's left of a speed after a step through it.
        private const float Friction = 0.42f;
        private const float StillSpeed = 0.012f;    // Metres in a step.
        private const float StillFor = 0.5f;        // Seconds before it's left alone.
        private const float FastestPoint = 2.4f;    // Metres in a step, or about 140 m/s. It's only here so that nothing can leave the world.
        private const float Thud = 0.2f;            // How much of what took it into something solid it comes back with.
        private const float Scrape = 0.85f;         // What's left of its speed along what it hit.
        private const int MostLooks = 8;

        private static readonly Dictionary<Rig, RagdollPlan> Plans = new();

        private readonly RagdollPlan _plan;
        private readonly float _scale;
        private readonly Vector3[] _at = new Vector3[RagdollPlan.PointCount];
        private readonly Vector3[] _was = new Vector3[RagdollPlan.PointCount];
        private readonly Vector3[] _drawnFrom = new Vector3[RagdollPlan.PointCount];
        private readonly Quaternion[] _rotations = new Quaternion[Rig.BoneCount];
        private readonly Quaternion[] _handsOnArms = new Quaternion[Rig.BoneCount];
        private readonly Quaternion[] _limbTwist = new Quaternion[Rig.BoneCount];
        private float _time;
        private float _still;

        /// <summary>Nothing's moved for a while, and nothing will. Such a body costs nothing.</summary>
        public bool Asleep { get; private set; }

        public Vector3 Centre => _at[(int)BoneType.Pelvis];

        /// <param name="joints">Where each joint is now, in the world.</param>
        /// <param name="last">Where each joint was a frame ago, which is how it's already moving.</param>
        /// <param name="rotations">How each bone is turned, in the world.</param>
        /// <param name="push">Metres a second, on top of that, from whatever did this.</param>
        public Ragdoll(Rig rig, float scale, Vector3[] joints, Vector3[] last, Quaternion[] rotations, float frameTime, Vector3 push)
        {
            lock (Plans)
            {
                if (!Plans.TryGetValue(rig, out RagdollPlan? plan))
                {
                    plan = new RagdollPlan(rig);
                    Plans.Add(rig, plan);
                }
                _plan = plan;
            }
            _scale = scale;

            // It's what was already going on, a frame at a time, as a step here is another length.
            float carried = StepTime / MathF.Max(frameTime, 1e-3f);
            Vector3 head = Vector3.Transform(_plan.Rest[RagdollPlan.HeadTop] - _plan.Rest[(int)BoneType.Head], rotations[(int)BoneType.Head]) * scale;
            for (int i = 0; i < RagdollPlan.PointCount; i++)
            {
                if (!_plan.Used[i]) continue;
                bool top = i == RagdollPlan.HeadTop;
                Vector3 at = top ? joints[(int)BoneType.Head] + head : joints[i];
                Vector3 before = top ? last[(int)BoneType.Head] + head : last[i];
                Vector3 moved = (at - before) * carried;
                if (moved.LengthSquared > 1f) moved = moved.Normalized();

                // A blow goes in high up and takes the feet out from under it: the top of a body goes further, which is what makes it turn over.
                float height = Math.Clamp((_plan.Rest[i].Y - _plan.Rest[(int)BoneType.Pelvis].Y) / 2f, -1f, 1f);
                Vector3 thrown = push * (1f + 0.3f * height);
                _at[i] = at;
                _was[i] = at - moved - thrown * StepTime;
                _drawnFrom[i] = at;
            }

            for (int i = 0; i < Rig.BoneCount; i++) _rotations[i] = rotations[i];
            ReadTwists(rotations);
        }

        // How the ends of limbs lie on what they're on, and how each limb is twisted along itself, to go on with.
        private void ReadTwists(Quaternion[] rotations)
        {
            Rig rig = _plan.Rig;
            foreach (Limb limb in rig.Legs)
            {
                _handsOnArms[(int)limb.End] = Quaternion.Invert(rotations[(int)limb.Lower]) * rotations[(int)limb.End];
            }
            foreach (Limb limb in rig.Arms)
            {
                _handsOnArms[(int)limb.End] = Quaternion.Invert(rotations[(int)limb.Lower]) * rotations[(int)limb.End];
            }
        }

        public void Step(float deltaTime, IGround ground)
        {
            if (Asleep) return;

            _time += MathF.Min(deltaTime, StepTime * MostSteps);
            int steps = 0;
            while (_time >= StepTime && steps < MostSteps)
            {
                _time -= StepTime;
                steps++;
                for (int i = 0; i < _at.Length; i++) _drawnFrom[i] = _at[i];
                Integrate();

                // Once for the whole body, as there's nearly always nothing there.
                bool solids = ground.LookAround(Centre.Xz, _plan.Span * _scale + FastestPoint);
                for (int pass = 0; pass < Passes; pass++)
                {
                    Rods();
                    Hinges();
                    Collide(ground, solids);
                }
                CheckStill();
            }
        }

        private void Integrate()
        {
            float fall = Rig.Gravity * StepTime * StepTime;
            for (int i = 0; i < _at.Length; i++)
            {
                if (!_plan.Used[i]) continue;
                Vector3 moved = (_at[i] - _was[i]) * Air;
                float length = moved.Length;
                if (length > FastestPoint) moved *= FastestPoint / length;
                _was[i] = _at[i];
                _at[i] += moved - Vector3.UnitY * fall;
            }
        }

        private void Rods()
        {
            foreach (ref readonly RagdollPlan.Stick stick in _plan.Sticks.AsSpan())
            {
                Vector3 between = _at[stick.B] - _at[stick.A];
                float length = between.Length;
                if (length < 1e-5f) continue;
                Vector3 correction = between * ((length - stick.Length * _scale) / length);
                _at[stick.A] += correction * stick.ShareA;
                _at[stick.B] -= correction * stick.ShareB;
            }

            foreach (ref readonly RagdollPlan.Stick stick in _plan.Spacers.AsSpan())
            {
                Vector3 between = _at[stick.B] - _at[stick.A];
                float length = between.Length;
                float least = stick.Length * _scale;
                if (length >= least || length < 1e-5f) continue;
                Vector3 correction = between * ((length - least) / length);
                _at[stick.A] += correction * stick.ShareA;
                _at[stick.B] -= correction * stick.ShareB;
            }
        }

        // Knees and elbows only go one way. Nothing gives a ragdoll away like a leg folded backwards, so a joint going past straight is pushed back the way it should go.
        private void Hinges()
        {
            Vector3 hipsLeft = (_at[(int)BoneType.UpperLegL] - _at[(int)BoneType.UpperLegR]);
            Vector3 hipsUp = _at[(int)BoneType.Spine] - _at[(int)BoneType.Pelvis];
            Vector3 shouldersLeft = _at[(int)BoneType.UpperArmL] - _at[(int)BoneType.UpperArmR];
            Vector3 shouldersUp = _at[(int)BoneType.Neck] - _at[(int)BoneType.Chest];
            if (_plan.Rig.Plan == BodyPlan.Quadruped)
            {
                hipsUp = Vector3.Cross(_at[(int)BoneType.Spine] - _at[(int)BoneType.Pelvis], hipsLeft);
                shouldersUp = hipsUp;
            }

            foreach (ref readonly RagdollPlan.Hinge hinge in _plan.Hinges.AsSpan())
            {
                Vector3 left = hinge.OnHips ? hipsLeft : shouldersLeft;
                Vector3 forward = Vector3.Cross(left, hinge.OnHips ? hipsUp : shouldersUp);
                float length = forward.Length;
                if (length < 1e-5f) continue;
                forward *= hinge.Sign / length;

                Vector3 root = _at[hinge.Root];
                Vector3 end = _at[hinge.End];
                Vector3 line = end - root;
                float span = line.LengthSquared;
                if (span < 1e-5f) continue;

                // How far the joint stands off the line from one end of the limb to the other, the way it's meant to go.
                Vector3 toMiddle = _at[hinge.Middle] - root;
                Vector3 off = toMiddle - line * (Vector3.Dot(toMiddle, line) / span);
                float out0 = Vector3.Dot(off, forward);
                if (out0 >= 0f) continue;
                _at[hinge.Middle] -= forward * (out0 * 0.6f);
            }
        }

        private void Collide(IGround ground, bool solids)
        {
            for (int i = 0; i < _at.Length; i++)
            {
                if (!_plan.Used[i]) continue;
                if (solids) Stop(ground, i);
                ref Vector3 at = ref _at[i];
                float floor = ground.HeightAt(at.X, at.Z) + _plan.Radius[i] * _scale;
                if (at.Y >= floor) continue;

                float depth = floor - at.Y;
                at.Y = floor;

                // Rubbing along the ground takes away from how far it moved, and more the harder it's pressed. Going by what it was doing, so that it can't start it going backwards.
                ref Vector3 was = ref _was[i];
                Vector2 slid = at.Xz - was.Xz;
                float slide = slid.Length;
                if (slide < 1e-6f) continue;
                float taken = MathF.Min(Friction * (depth + 0.01f) + slide * 0.04f, slide);
                Vector2 kept = slid * (1f - taken / slide);
                was.X = at.X - kept.X;
                was.Z = at.Z - kept.Y;
            }
        }

        // What's standing stops what runs into it. A body thrown hard goes further in a step than a fence is thick, so it's the way it came that's looked along, and not only where it's got to.
        private void Stop(IGround ground, int point)
        {
            ref Vector3 at = ref _at[point];
            Vector3 from = _drawnFrom[point];
            Vector3 moved = at - from;
            float radius = _plan.Radius[point] * _scale;

            int looks = Math.Clamp((int)(moved.Xz.Length / MathF.Max(radius, 0.05f)) + 1, 1, MostLooks);
            for (int look = 1; look <= looks; look++)
            {
                Vector3 reached = from + moved * ((float)look / looks);
                if (!ground.TryPushOut(reached, radius, out Vector2 push)) continue;

                // Going by what it was doing, as with the ground: what took it in is gone, bar a little that comes back, and rubbing along it takes some of the rest.
                ref Vector3 was = ref _was[point];
                Vector2 speed = at.Xz - was.Xz;
                float depth = push.Length;
                if (depth > 1e-6f)
                {
                    Vector2 outward = push / depth;
                    float into = Vector2.Dot(speed, outward);
                    if (into < 0f) speed -= outward * (into * (1f + Thud));
                }
                speed *= Scrape;

                at = reached + new Vector3(push.X, 0f, push.Y);
                was.X = at.X - speed.X;
                was.Z = at.Z - speed.Y;
                return;
            }
        }

        private void CheckStill()
        {
            float fastest = 0f;
            for (int i = 0; i < _at.Length; i++)
            {
                if (!_plan.Used[i]) continue;
                fastest = MathF.Max(fastest, (_at[i] - _was[i]).LengthSquared);
            }

            _still = fastest < StillSpeed * StillSpeed ? _still + StepTime : 0f;
            if (_still >= StillFor) Asleep = true;
        }

        /// <summary>Writes where the bones are, for a body whose entity stands at <paramref name="position"/> turned by <paramref name="yaw"/>. It goes under the ground by <paramref name="sunk"/> as it's cleared away.</summary>
        public void Pose(Vector3 position, float yaw, float scale, float sunk, Vector3[] joints, Quaternion[] rotations)
        {
            Quaternion toBody = Frames.Yaw(-yaw);
            float inverse = 1f / scale;
            float alpha = Asleep ? 1f : Math.Clamp(_time / StepTime, 0f, 1f);

            ReadBones();

            for (int i = 0; i < Rig.BoneCount; i++)
            {
                if (!_plan.Used[i]) continue;
                Vector3 at = Vector3.Lerp(_drawnFrom[i], _at[i], alpha) - Vector3.UnitY * sunk;
                joints[i] = Vector3.Transform(at - position, toBody) * inverse;
                rotations[i] = toBody * _rotations[i];
            }
        }

        private void ReadBones()
        {
            Rig rig = _plan.Rig;

            foreach ((int from, int to, int left, int right, Quaternion restInverse) in _plan.Trunk)
            {
                Vector3 along = _at[to] - _at[from];
                if (along.LengthSquared < 1e-8f) continue;
                along.Normalize();
                Vector3 across = Frames.Across(_at[left] - _at[right], along, Vector3.UnitX);
                _rotations[from] = Frames.Of(along, across) * restInverse;
            }

            foreach (Limb limb in rig.Legs) ReadLimb(limb, _rotations[limb.Parent == BoneType.Chest ? (int)BoneType.Chest : (int)BoneType.Pelvis]);
            if (_plan.HasTail)
            {
                Vector3 restTail = (rig.Pivots[(int)BoneType.TailTip] - rig.Pivots[(int)BoneType.TailBase]).Normalized();
                Vector3 tail = _at[(int)BoneType.TailTip] - _at[(int)BoneType.TailBase];
                if (tail.LengthSquared > 1e-8f) _rotations[(int)BoneType.TailBase] = Frames.Between(restTail, tail.Normalized());
                _rotations[(int)BoneType.TailTip] = _rotations[(int)BoneType.TailBase];
            }
            foreach (Limb limb in rig.Arms)
            {
                _rotations[(int)limb.Parent] = _rotations[(int)BoneType.Chest];
                ReadLimb(limb, _rotations[(int)BoneType.Chest]);
            }
        }

        private void ReadLimb(Limb limb, Quaternion parent)
        {
            Vector3 root = _at[(int)limb.Upper];
            Vector3 middle = _at[(int)limb.Lower];
            Vector3 end = _at[(int)limb.End];
            Vector3 upperWay = middle - root;
            Vector3 lowerWay = end - middle;
            if (upperWay.LengthSquared < 1e-8f || lowerWay.LengthSquared < 1e-8f) return;
            upperWay.Normalize();
            lowerWay.Normalize();

            // The way it's folded says which way its hinge lies. Dead straight there's no telling, and it goes by what it's hanging from.
            Vector3 hinge = Vector3.Cross(upperWay, lowerWay);
            if (hinge.LengthSquared < 1e-4f) hinge = Vector3.Cross(upperWay, Vector3.Transform(limb.Bends, parent));
            if (hinge.LengthSquared < 1e-8f) return;
            hinge.Normalize();

            // Each bone's own frame is a direction along it and the way its middle joint goes.
            _rotations[(int)limb.Upper] = Frames.Of(upperWay, Vector3.Cross(hinge, upperWay)) * limb.RestFrameInverse;
            _rotations[(int)limb.Lower] = Frames.Of(lowerWay, Vector3.Cross(hinge, lowerWay)) * limb.RestFrameInverse;
            _rotations[(int)limb.End] = _rotations[(int)limb.Lower] * _handsOnArms[(int)limb.End];
        }
    }
}
