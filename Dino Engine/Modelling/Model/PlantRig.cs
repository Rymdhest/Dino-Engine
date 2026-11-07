using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    /// <summary>
    /// How a plant's joints bend: the CPU's copy of Shaders/Plant.glsl, which is what's drawn, and what's checked. Change them together.
    ///
    /// A joint doesn't turn about a point, which would swing the middle of a stem out of line with its ends. It curves into an arc: the line from base to tip
    /// keeps its length, whatever's beside that line turns with it, and a joint's children are carried whole, by where they sit on it. Joints are taken from
    /// a vertex's own up to the root, each in its own rest pose, so nothing builds on the frame before.
    /// </summary>
    public static class PlantRig
    {
        public const int FloatsPerJoint = 16;
        public const int MostLevels = 6;            // PLANT_MOST_LEVELS
        public const float Floor = 0.03f;           // PLANT_FLOOR: metres above its own ground that a plant can be pressed to.
        private const float LagPerLevel = 0.045f;   // Seconds each joint runs behind the one it hangs from.

        /// <summary>How many joints there are from this one to the root, this one included.</summary>
        public static int Depth(List<PlantJoint> joints, int joint)
        {
            int depth = 0;
            for (; joint >= 0 && depth <= joints.Count; joint = joints[joint].Parent) depth++;
            return depth;
        }

        /// <summary>How far along a joint a point sits, 0 at its base and 1 at its tip. Beyond either end it goes with that end.</summary>
        public static float Along(in PlantJoint joint, Vector3 point)
        {
            Vector3 line = joint.Tip - joint.Base;
            float lengthSquared = line.LengthSquared;
            if (lengthSquared < 1e-10f) return 0f;
            return Math.Clamp(Vector3.Dot(point - joint.Base, line) / lengthSquared, 0f, 1f);
        }

        /// <summary>Which of the joints <paramref name="first"/> to <paramref name="last"/> a point is nearest.</summary>
        public static int Nearest(List<PlantJoint> joints, int first, int last, Vector3 point)
        {
            int nearest = first;
            float best = float.MaxValue;
            for (int i = first; i <= last; i++)
            {
                PlantJoint joint = joints[i];
                Vector3 line = joint.Base + (joint.Tip - joint.Base) * Along(joint, point);
                float distance = (point - line).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                nearest = i;
            }
            return nearest;
        }

        /// <summary>The bend at a joint's tip for a lean, as an axis as long as the angle: about whatever would bring the joint towards the lean, and no further than it can go.</summary>
        public static Vector3 Turn(in PlantJoint joint, Vector3 lean)
        {
            float length = joint.Length;
            if (length < 1e-5f) return Vector3.Zero;
            Vector3 turn = Vector3.Cross((joint.Tip - joint.Base) / length, lean);
            float amount = turn.Length;
            if (amount < 1e-5f) return Vector3.Zero;
            return turn * (joint.MostBend * (1f - MathF.Exp(-amount / joint.MostBend)) / amount);
        }

        /// <summary>Bends a point held by <paramref name="joint"/>, with <paramref name="leanOf"/> saying how each joint is being pushed, in the model's space.</summary>
        public static Vector3 Pose(List<PlantJoint> joints, int joint, Vector3 point, Func<int, Vector3> leanOf)
        {
            if (joint < 0) return point;
            Vector3 rest = point;
            float along = Along(joints[joint], point);
            for (int level = 0; level < MostLevels && joint >= 0; level++)
            {
                PlantJoint at = joints[joint];
                point = Bend(at, point, along, Turn(at, leanOf(joint)));
                along = at.Parent >= 0 ? Along(joints[at.Parent], at.Base) : 0f;
                joint = at.Parent;
            }
            point.Y = MathF.Max(point.Y, MathF.Min(rest.Y, Floor));
            return point;
        }

        private static Vector3 Bend(in PlantJoint joint, Vector3 point, float along, Vector3 turn)
        {
            float bend = turn.Length;
            if (bend < 1e-5f) return point;

            float length = joint.Length;
            Vector3 axis = (joint.Tip - joint.Base) / length;
            Vector3 about = turn / bend;
            float angle = bend * along;
            float sine = MathF.Sin(angle);
            float cosine = MathF.Cos(angle);

            Vector3 side = point - joint.Base - axis * (along * length);
            Vector3 turned = side * cosine + Vector3.Cross(about, side) * sine + about * (Vector3.Dot(about, side) * (1f - cosine));
            Vector3 arc = (axis * sine + Vector3.Cross(about, axis) * (1f - cosine)) * (length / bend);
            return joint.Base + arc + turned;
        }

        /// <summary>The furthest any vertex can go from where it rests, with every joint at its limit at once. Culling rests on this.</summary>
        public static float Reach(in Mesh mesh)
        {
            List<PlantJoint>? joints = mesh.joints;
            if (joints == null) return 0f;

            float furthest = 0f;
            foreach (MeshVertex vertex in mesh.meshVertices)
            {
                float reach = 0f;
                float along = -1f;
                for (int joint = vertex.joint, level = 0; joint >= 0 && level < MostLevels; level++)
                {
                    PlantJoint at = joints[joint];
                    if (along < 0f) along = Along(at, vertex.position);
                    Vector3 line = at.Base + (at.Tip - at.Base) * along;
                    // A point on an arc moves about half what it would if the joint turned whole, and what's beside the line swings as far as it stands off it.
                    reach += at.MostBend * along * (0.6f * along * at.Length + (vertex.position - line).Length);
                    along = at.Parent >= 0 ? Along(joints[at.Parent], at.Base) : 0f;
                    joint = at.Parent;
                }
                furthest = MathF.Max(furthest, reach);
            }
            return furthest;
        }

        /// <summary>What Plant.glsl reads, four vec4s to a joint.</summary>
        public static float[] Pack(List<PlantJoint> joints)
        {
            float[] data = new float[joints.Count * FloatsPerJoint];
            for (int i = 0; i < joints.Count; i++)
            {
                PlantJoint joint = joints[i];
                float length = MathF.Max(joint.Length, 1e-4f);
                Vector3 axis = (joint.Tip - joint.Base) / length;
                float attach = joint.Parent >= 0 ? Along(joints[joint.Parent], joint.Base) : 0f;
                int at = i * FloatsPerJoint;

                data[at + 0] = joint.Base.X;
                data[at + 1] = joint.Base.Y;
                data[at + 2] = joint.Base.Z;
                data[at + 3] = length;
                data[at + 4] = axis.X;
                data[at + 5] = axis.Y;
                data[at + 6] = axis.Z;
                data[at + 7] = joint.Give;
                data[at + 8] = joint.Parent;
                data[at + 9] = attach;
                data[at + 10] = joint.Touch;
                data[at + 11] = joint.MostBend;
                data[at + 12] = Seed(i);
                data[at + 13] = joint.Flutter;
                data[at + 14] = joint.Heavy ? 1f : 0f;
                data[at + 15] = (Depth(joints, i) - 1) * LagPerLevel;
            }
            return data;
        }

        // From its number and nothing else: drawing on the generators' random numbers here would grow every plant differently.
        private static float Seed(int joint)
        {
            uint h = (uint)joint * 0x9e3779b9u;
            h ^= h >> 16;
            h *= 0x7feb352du;
            h ^= h >> 15;
            return (h & 0xffffu) / 65536f;
        }
    }
}
