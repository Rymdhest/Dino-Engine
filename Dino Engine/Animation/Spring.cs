using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>
    /// How anything that moves in an animation gets from where it is to where it's wanted. Each of these is the exact solution of a spring over a step, and not a step of one, so it behaves the
    /// same at any frame rate and can't blow up however long a frame is. A half-life is how long it takes to get half way, which is a number a person can think about. After Daniel Holden's "Spring-It-On".
    /// </summary>
    public static class Spring
    {
        private const float Ln2 = 0.6931472f;
        private const float ShortestHalfLife = 1e-4f;

        /// <summary>How much of what's left is still left after a step: the whole of what a damper is.</summary>
        public static float Remaining(float halfLife, float deltaTime) => MathF.Exp(-Ln2 * deltaTime / MathF.Max(halfLife, ShortestHalfLife));

        /// <summary>How much of the way a damper goes in a step.</summary>
        public static float Share(float halfLife, float deltaTime) => 1f - Remaining(halfLife, deltaTime);

        public static float Damp(float value, float goal, float halfLife, float deltaTime) => goal + (value - goal) * Remaining(halfLife, deltaTime);

        public static Vector3 Damp(Vector3 value, Vector3 goal, float halfLife, float deltaTime) => goal + (value - goal) * Remaining(halfLife, deltaTime);

        /// <summary>The same for an angle, the short way round.</summary>
        public static float DampAngle(float value, float goal, float halfLife, float deltaTime)
        {
            return value + Shortest(goal - value) * Share(halfLife, deltaTime);
        }

        /// <summary>An angle as the shortest turn it could be.</summary>
        public static float Shortest(float angle) => MathF.IEEERemainder(angle, MathF.Tau);

        /// <summary>A critically damped spring, which gets there as fast as it can without going past.</summary>
        public static void Step(ref float value, ref float velocity, float goal, float halfLife, float deltaTime)
        {
            float rate = 2f * Ln2 / MathF.Max(halfLife, ShortestHalfLife);
            float offset = value - goal;
            float carried = velocity + offset * rate;
            float fade = MathF.Exp(-rate * deltaTime);
            value = goal + fade * (offset + carried * deltaTime);
            velocity = fade * (velocity - carried * rate * deltaTime);
        }

        public static void Step(ref Vector3 value, ref Vector3 velocity, Vector3 goal, float halfLife, float deltaTime)
        {
            float rate = 2f * Ln2 / MathF.Max(halfLife, ShortestHalfLife);
            Vector3 offset = value - goal;
            Vector3 carried = velocity + offset * rate;
            float fade = MathF.Exp(-rate * deltaTime);
            value = goal + fade * (offset + carried * deltaTime);
            velocity = fade * (velocity - carried * rate * deltaTime);
        }

        /// <summary>A spring that goes past and comes back, for what a blow does to a body. <paramref name="frequency"/> is in cycles a second, and <paramref name="damping"/> is under 1.</summary>
        public static void Ring(ref Vector3 value, ref Vector3 velocity, float frequency, float damping, float deltaTime)
        {
            float natural = MathF.Tau * frequency;
            float decay = damping * natural;
            float ringing = natural * MathF.Sqrt(MathF.Max(1f - damping * damping, 1e-4f));
            float fade = MathF.Exp(-decay * deltaTime);
            float cos = MathF.Cos(ringing * deltaTime);
            float sin = MathF.Sin(ringing * deltaTime);

            Vector3 along = (velocity + value * decay) / ringing;
            Vector3 next = fade * (value * cos + along * sin);
            velocity = fade * ((along * ringing - value * decay) * cos - (value * ringing + along * decay) * sin);
            value = next;
        }

        /// <summary>A rotation as a vector along its axis, as long as its angle, so that rotations can be scaled and added. Always the short way round.</summary>
        public static Vector3 ToVector(Quaternion rotation)
        {
            if (rotation.W < 0f) rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
            var axis = new Vector3(rotation.X, rotation.Y, rotation.Z);
            float sine = axis.Length;
            if (sine < 1e-6f) return axis * 2f;
            return axis * (2f * MathF.Atan2(sine, rotation.W) / sine);
        }

        public static Quaternion ToRotation(Vector3 vector)
        {
            float angle = vector.Length;
            if (angle < 1e-6f) return new Quaternion(vector.X * 0.5f, vector.Y * 0.5f, vector.Z * 0.5f, 1f).Normalized();
            float half = angle * 0.5f;
            Vector3 axis = vector * (MathF.Sin(half) / angle);
            return new Quaternion(axis.X, axis.Y, axis.Z, MathF.Cos(half));
        }

        /// <summary>What's left of a jump, dying away. See <see cref="Continuity"/>.</summary>
        public static void Fade(ref Vector3 offset, ref Vector3 velocity, float halfLife, float deltaTime)
        {
            float rate = 2f * Ln2 / MathF.Max(halfLife, ShortestHalfLife);
            Vector3 carried = velocity + offset * rate;
            float fade = MathF.Exp(-rate * deltaTime);
            offset = fade * (offset + carried * deltaTime);
            velocity = fade * (velocity - carried * rate * deltaTime);
        }
    }

    /// <summary>
    /// What lets anything start at once and still never pop. Whatever a body was asked to do, the new goal takes over this instant, and the gap between it and where the body really was is kept as an
    /// offset that dies away: so a dodge begins on the frame it was pressed, and the eye sees a very quick move rather than a jump. This is inertialization, as Gears of War brought it to games.
    ///
    /// It also takes a speed limit, and anything arriving faster than that goes into the offset instead. That's what makes it a safety net as well as a transition: a value can't jump here,
    /// whatever anything upstream of it does, and nothing downstream of it has to care.
    /// </summary>
    public struct Continuity
    {
        private Vector3 _offset;
        private Vector3 _offsetVelocity;
        private Vector3 _last;
        private Vector3 _lastVelocity;
        private bool _started;

        public readonly Vector3 Velocity => _lastVelocity;

        /// <summary>The goal has changed its mind: what's been seen stays where it is and goes on as it was, and comes round to the new goal.</summary>
        public Vector3 Follow(Vector3 goal, float halfLife, float fastest, float deltaTime, bool transition = false)
        {
            if (!_started || deltaTime <= 0f)
            {
                _started = true;
                _last = goal;
                _lastVelocity = Vector3.Zero;
                return goal + _offset;
            }

            // What the goal itself is doing, as it hasn't got any of the offset in it.
            Vector3 moved = goal - _last;
            float most = fastest * deltaTime;
            if (transition)
            {
                _offset -= moved;
                _offsetVelocity += _lastVelocity;
                moved = Vector3.Zero;
            }
            else if (moved.LengthSquared > most * most)
            {
                // All of it that's over the limit. The rest is ordinary motion.
                Vector3 allowed = moved * (most / moved.Length);
                _offset -= moved - allowed;
                moved = allowed;
            }

            Spring.Fade(ref _offset, ref _offsetVelocity, halfLife, deltaTime);
            _lastVelocity = moved / deltaTime;
            _last = goal;
            return goal + _offset;
        }

        public void Reset()
        {
            _started = false;
            _offset = Vector3.Zero;
            _offsetVelocity = Vector3.Zero;
            _lastVelocity = Vector3.Zero;
        }
    }

    /// <summary>The same for a rotation. The offset is kept as a vector, so that it can die away the same way.</summary>
    public struct TurnContinuity
    {
        private Vector3 _offset;
        private Vector3 _offsetVelocity;
        private Quaternion _last;
        private Vector3 _lastVelocity;
        private bool _started;

        public Quaternion Follow(Quaternion goal, float halfLife, float fastest, float deltaTime, bool transition = false)
        {
            if (!_started || deltaTime <= 0f)
            {
                _started = true;
                _last = goal;
                _lastVelocity = Vector3.Zero;
                return Combine(goal);
            }

            Vector3 moved = Spring.ToVector(goal * Quaternion.Invert(_last));
            float most = fastest * deltaTime;
            if (transition)
            {
                _offset -= moved;
                _offsetVelocity += _lastVelocity;
                moved = Vector3.Zero;
            }
            else if (moved.LengthSquared > most * most)
            {
                Vector3 allowed = moved * (most / moved.Length);
                _offset -= moved - allowed;
                moved = allowed;
            }

            Spring.Fade(ref _offset, ref _offsetVelocity, halfLife, deltaTime);
            _lastVelocity = moved / deltaTime;
            _last = goal;
            return Combine(goal);
        }

        private readonly Quaternion Combine(Quaternion goal) => _offset.LengthSquared < 1e-9f ? goal : (Spring.ToRotation(_offset) * goal).Normalized();

        public void Reset()
        {
            _started = false;
            _offset = Vector3.Zero;
            _offsetVelocity = Vector3.Zero;
            _lastVelocity = Vector3.Zero;
        }
    }
}
