using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>How a crab is holding itself this frame. Where its feet are is its own business: see <see cref="CrabLegs"/>.</summary>
    public struct CrabPose
    {
        public float Tilt;      // Radians, its front up.
        public float Roll;      // Radians, its left side up.
        public float Raise;     // 0 claws folded in, 1 held up and wide, which is what a crab does instead of leaving.
        public float FeedLeft;  // 0 to 1, a claw on its way from the sand to its mouth.
        public float FeedRight;
    }

    /// <summary>
    /// A crab's legs. It's the rule the people here go by: <b>a foot on the ground stays exactly there until it lifts</b>, or it's sliding, and nothing looks more wrong. Legs step in two sets of four, every
    /// other leg down each side and the opposite on the other, each set going when the other is down, the alternating tetrapod a crab goes sideways on. A set steps when its feet have fallen far enough behind where
    /// they'd stand, to a place ahead of that by how fast it's going, so it strides further the faster it runs, as Raibert's rule has it. Nothing's on a clock, so it can stop dead, or turn back in one step, as a real one does.
    /// </summary>
    public sealed class CrabLegs
    {
        private const float StepTime = 0.09f;       // Seconds in the air, flat out.
        private const float SlowStepTime = 0.16f;
        private const float StepHeight = 0.11f;
        private const float ReachBefore = 0.1f;     // Metres a foot can trail before it has to go.
        private const float Lead = 0.65f;           // How much of the next stance is laid out in front.

        private readonly Vector3[] _feet = new Vector3[CrabBody.Legs];      // In the world.
        private readonly Vector3[] _from = new Vector3[CrabBody.Legs];
        private readonly Vector3[] _to = new Vector3[CrabBody.Legs];
        private readonly float[] _flight = new float[CrabBody.Legs];        // 0 down; 0 to 1 how far through a step.
        private int _lastSet;

        /// <summary>Which set of legs a leg is in: 0, 2 down its left and 1, 3 down its right, and the rest are the other.</summary>
        private static int SetOf(int leg) => (leg + leg / 4) % 2;

        public void Place(in Matrix4 root, Func<Vector2, float> ground)
        {
            for (int leg = 0; leg < CrabBody.Legs; leg++)
            {
                Vector3 foot = Vector3.TransformPosition(CrabBody.RestFoot(leg), root);
                foot.Y = ground(foot.Xz);
                _feet[leg] = foot;
                _flight[leg] = 0f;
            }
        }

        /// <param name="root">Where the body is, without what it's bobbing by.</param>
        /// <param name="velocity">How it's going, in metres a second.</param>
        public void Step(in Matrix4 root, Vector3 velocity, Func<Vector2, float> ground, float deltaTime)
        {
            float speed = velocity.Xz.Length;
            float stepTime = speed > 3f ? StepTime : SlowStepTime;
            bool airborne = false;
            for (int leg = 0; leg < CrabBody.Legs; leg++)
            {
                if (_flight[leg] <= 0f) continue;
                airborne = true;
                _flight[leg] += deltaTime / stepTime;
                if (_flight[leg] >= 1f)
                {
                    _flight[leg] = 0f;
                    _feet[leg] = _to[leg];
                    continue;
                }

                float t = _flight[leg];
                _feet[leg] = Vector3.Lerp(_from[leg], _to[leg], t * t * (3f - 2f * t)) + Vector3.UnitY * (4f * t * (1f - t) * StepHeight);
            }
            if (airborne) return;

            // All down: the set that has fallen furthest behind goes, unless both are where they should be.
            Span<float> behind = stackalloc float[2];
            for (int leg = 0; leg < CrabBody.Legs; leg++)
            {
                Vector3 rest = Vector3.TransformPosition(CrabBody.RestFoot(leg), root);
                float off = (rest.Xz - _feet[leg].Xz).Length;
                behind[SetOf(leg)] = MathF.Max(behind[SetOf(leg)], off);
            }

            int next = behind[1 - _lastSet] >= behind[_lastSet] * 0.7f ? 1 - _lastSet : _lastSet;
            if (behind[next] < ReachBefore) return;

            _lastSet = next;
            for (int leg = 0; leg < CrabBody.Legs; leg++)
            {
                if (SetOf(leg) != next) continue;
                Vector3 rest = Vector3.TransformPosition(CrabBody.RestFoot(leg), root);
                Vector3 target = rest + new Vector3(velocity.X, 0f, velocity.Z) * (stepTime + stepTime * 2f * Lead);
                target.Y = ground(target.Xz);
                _from[leg] = _feet[leg];
                _to[leg] = target;
                _flight[leg] = 1e-4f;
            }
        }

        /// <summary>Solves the crab into <paramref name="skin"/>, with its legs reaching for where its feet are. Two bones are solved exactly, so a leg can't come out wrong, and its knee stays up and out.</summary>
        public void Solve(Skeleton skeleton, in CrabPose pose, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[CrabBody.Count];
            Quaternion shell = Turn.Pitch(-pose.Tilt) * Turn.Roll(pose.Roll);
            turns[CrabBody.Shell] = shell;
            Quaternion shellBack = Quaternion.Invert(shell);

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1f : -1f;
                float feed = side == 0 ? pose.FeedLeft : pose.FeedRight;
                int arm = side == 0 ? CrabBody.ArmL : CrabBody.ArmR;
                int claw = side == 0 ? CrabBody.ClawL : CrabBody.ClawR;

                // Raised, the arm goes up and out, and the claw opens away from the middle.
                turns[arm] = Turn.Roll(pose.Raise * 1.25f * x - feed * 0.25f * x) * Turn.Yaw(pose.Raise * 0.55f * x + feed * 0.7f * x);
                turns[claw] = Turn.Pitch(-pose.Raise * 1.1f + feed * 0.9f) * Turn.Yaw(-pose.Raise * 1.2f * x - feed * 0.5f * x);
            }

            Matrix4 toLocal = Matrix4.Invert(root);
            Vector3 centre = new Vector3(0f, CrabBody.Standing, 0f);
            for (int leg = 0; leg < CrabBody.Legs; leg++)
            {
                Vector3 hip = centre + Vector3.Transform(CrabBody.Hip(leg) - centre, shell);
                Vector3 foot = Vector3.TransformPosition(_feet[leg], toLocal);
                float upper = CrabBody.UpperReach(leg);
                float lower = CrabBody.LowerReach(leg);

                Vector3 toFoot = foot - hip;
                float distance = Math.Clamp(toFoot.Length, MathF.Abs(upper - lower) + 0.01f, upper + lower - 0.002f);
                Vector3 direction = toFoot.LengthSquared > 1e-8f ? toFoot.Normalized() : -Vector3.UnitY;

                float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
                float high = MathF.Sqrt(MathF.Max(upper * upper - along * along, 0f));
                Vector3 pole = new Vector3(leg < CrabBody.Legs / 2 ? 0.5f : -0.5f, 1f, 0f);
                Vector3 bend = (pole - direction * Vector3.Dot(pole, direction)).Normalized();
                Vector3 knee = hip + direction * along + bend * high;

                Vector3 restUpper = (CrabBody.RestKnee(leg) - CrabBody.Hip(leg)).Normalized();
                Vector3 restLower = (CrabBody.RestFoot(leg) - CrabBody.RestKnee(leg)).Normalized();
                Quaternion upperTurn = Arc(restUpper, (knee - hip).Normalized());
                Quaternion lowerTurn = Arc(restLower, (hip + direction * distance - knee).Normalized());

                turns[CrabBody.Upper(leg)] = shellBack * upperTurn;
                turns[CrabBody.Lower(leg)] = Quaternion.Invert(upperTurn) * lowerTurn;
            }

            skeleton.Solve(turns, root, skin);
        }

        // The shortest turn from one direction to another.
        private static Quaternion Arc(Vector3 from, Vector3 to)
        {
            float dot = Vector3.Dot(from, to);
            if (dot < -0.9999f) return Quaternion.FromAxisAngle(Vector3.UnitZ, MathF.PI);
            Vector3 axis = Vector3.Cross(from, to);
            return new Quaternion(axis.X, axis.Y, axis.Z, 1f + dot).Normalized();
        }
    }
}
