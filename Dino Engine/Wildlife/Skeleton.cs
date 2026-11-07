using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>
    /// The joints of something small. Bones have no rest rotation, as a person's don't, so all that's stored is where each pivot is and which one it hangs from, and a pose is a turn at each joint. A parent always
    /// comes before its children, so one pass does it. Nothing here allocates: every creature in view is worked out from this each frame.
    /// </summary>
    public sealed class Skeleton
    {
        public readonly Vector3[] Pivots;
        public readonly int[] Parents;
        private readonly Quaternion[] _world;
        private readonly Vector3[] _placed;

        public int Count => Pivots.Length;

        public Skeleton(Vector3[] pivots, int[] parents)
        {
            if (pivots.Length != parents.Length) throw new ArgumentException("Every joint needs a parent, or -1.");
            for (int i = 0; i < parents.Length; i++)
            {
                if (parents[i] >= i) throw new ArgumentException($"Joint {i} comes before its parent.");
            }
            Pivots = pivots;
            Parents = parents;
            _world = new Quaternion[pivots.Length];
            _placed = new Vector3[pivots.Length];
        }

        /// <summary>Works out what each bone does to the vertices tied to it, all the way into the world: a vertex only needs its bones, and no model matrix.</summary>
        public void Solve(ReadOnlySpan<Quaternion> turns, in Matrix4 root, Span<Matrix4> skin)
        {
            for (int bone = 0; bone < Pivots.Length; bone++)
            {
                int parent = Parents[bone];
                Quaternion rotation;
                Vector3 place;
                if (parent < 0)
                {
                    rotation = turns[bone];
                    place = Pivots[bone];
                }
                else
                {
                    rotation = _world[parent] * turns[bone];
                    place = _placed[parent] + Vector3.Transform(Pivots[bone] - Pivots[parent], _world[parent]);
                }
                _world[bone] = rotation;
                _placed[bone] = place;

                // Round its pivot, then to where the pivot has got to.
                Matrix4 local = Matrix4.CreateFromQuaternion(rotation);
                Vector3 moved = place - Vector3.TransformVector(Pivots[bone], local);
                local.Row3 = new Vector4(moved, 1f);
                skin[bone] = local * root;
            }
        }

        /// <summary>Where a joint has got to, in the frame the last pose was solved in, before the root.</summary>
        public Vector3 PlaceOf(int bone) => _placed[bone];

        /// <summary>How a joint is turned, likewise.</summary>
        public Quaternion TurnOf(int bone) => _world[bone];
    }

    /// <summary>Turns about a body's axes. +Z is forward, +Y up and +X to its left, as it is for people.</summary>
    public static class Turn
    {
        /// <summary>A positive angle bows forward: the nose goes down.</summary>
        public static Quaternion Pitch(float angle) => Quaternion.FromAxisAngle(Vector3.UnitX, angle);

        /// <summary>A positive angle turns towards its left.</summary>
        public static Quaternion Yaw(float angle) => Quaternion.FromAxisAngle(Vector3.UnitY, angle);

        /// <summary>A positive angle lifts its left side.</summary>
        public static Quaternion Roll(float angle) => Quaternion.FromAxisAngle(Vector3.UnitZ, angle);

        /// <summary>A body in the world: where it is, which way it faces, how it's tipped, and how big.</summary>
        public static Matrix4 Root(Vector3 position, Quaternion facing, float size)
        {
            Matrix4 matrix = Matrix4.CreateFromQuaternion(facing);
            matrix.Row0 *= size;
            matrix.Row1 *= size;
            matrix.Row2 *= size;
            matrix.Row3 = new Vector4(position, 1f);
            return matrix;
        }

        /// <summary>A yaw of 0 faces +Z, and a positive one turns towards +X, as the wind's directions do.</summary>
        public static Vector2 Heading(float yaw) => new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));

        public static float YawOf(Vector2 direction) => MathF.Atan2(direction.X, direction.Y);
    }

    /// <summary>A curve through a few keys, in a loop or not. Smooth, so that a pose built from them never kinks, and read many times a frame, so there's nothing to it.</summary>
    public readonly struct Keys
    {
        private readonly float[] _at;
        private readonly float[] _value;

        /// <param name="pairs">When and what, in order: 0, 1.2, 0.5, -1 and so on.</param>
        public Keys(params float[] pairs)
        {
            _at = new float[pairs.Length / 2];
            _value = new float[pairs.Length / 2];
            for (int i = 0; i < _at.Length; i++)
            {
                _at[i] = pairs[i * 2];
                _value[i] = pairs[i * 2 + 1];
            }
        }

        public float At(float time)
        {
            if (time <= _at[0]) return _value[0];
            int last = _at.Length - 1;
            if (time >= _at[last]) return _value[last];
            int key = 0;
            while (time > _at[key + 1]) key++;
            float t = (time - _at[key]) / (_at[key + 1] - _at[key]);
            return _value[key] + (_value[key + 1] - _value[key]) * t * t * (3f - 2f * t);
        }
    }
}
