using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Wildlife
{
    /// <summary>One ring of a body, square to the way it's going. <paramref name="Bone"/> holds it, and <paramref name="Share"/> of it goes to <paramref name="Next"/>, for skin that bends.</summary>
    public readonly record struct Ring(Vector3 Centre, float Wide, float High, int Bone, int Next = -1, float Share = 0f);

    /// <summary>
    /// Collects a small animal into one skinned mesh. Everything alive here is smooth, so it's lofted from rings that share their vertices, and coloured in the vertices, as what's a few dozen pixels across needs no
    /// texture and its colour is most of what's seen of it. Headless, so it can be looked at in a lab. A body is on the plain surface everything coloured by its vertices is on.
    /// </summary>
    public sealed class BodyMesh
    {
        private readonly List<Vertex> _vertices = new();
        private readonly List<int> _indices = new();

        public int VertexCount => _vertices.Count;
        public int TriangleCount => _indices.Count / 3;

        /// <summary>Adds a corner, and says which one it is, for <see cref="Triangle"/>.</summary>
        public int Corner(Vector3 position, Vector3 colour, int bone, int next = -1, float share = 0f)
        {
            Vector2 bones = new Vector2(bone, next < 0 ? bone : next);
            Vector2 weights = next < 0 ? new Vector2(1f, 0f) : new Vector2(1f - share, share);
            _vertices.Add(new Vertex(position, FloraMaterials.Flesh, new Colour(colour), bones, weights, new Vector2(position.X + position.Y, position.Z)));
            return _vertices.Count - 1;
        }

        public void Triangle(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        /// <summary>
        /// A skin over rings, closed at both ends. <paramref name="colour"/> is given how far along it is, 0 to 1, and how far up the side, -1 under it to 1 on top.
        /// </summary>
        public void Loft(ReadOnlySpan<Ring> rings, int sides, Func<float, float, Vector3> colour)
        {
            int count = rings.Length;
            int first = _vertices.Count;
            int[] tips = new int[2];

            for (int r = 0; r < count; r++)
            {
                Ring ring = rings[r];
                Vector3 along = Along(rings, r);
                Vector3 side = Vector3.Cross(Vector3.UnitY, along);
                if (side.LengthSquared < 1e-6f) side = Vector3.UnitX;
                side.Normalize();
                Vector3 up = Vector3.Cross(along, side).Normalized();
                float share = count > 1 ? (float)r / (count - 1) : 0f;

                for (int i = 0; i < sides; i++)
                {
                    float angle = i * (MathF.Tau / sides);
                    float height = MathF.Sin(angle);
                    Vector3 place = ring.Centre + side * (MathF.Cos(angle) * ring.Wide) + up * (height * ring.High);
                    Corner(place, colour(share, height), ring.Bone, ring.Next, ring.Share);
                }
            }

            for (int r = 0; r + 1 < count; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    int a = first + r * sides + i;
                    int b = first + r * sides + next;
                    int c = first + (r + 1) * sides + next;
                    int d = first + (r + 1) * sides + i;
                    Quad(a, d, c, b);
                }
            }

            Ring start = rings[0];
            Ring end = rings[count - 1];
            tips[0] = Corner(start.Centre - Along(rings, 0) * MathF.Min(start.Wide, start.High) * 0.5f, colour(0f, 0f), start.Bone, start.Next, start.Share);
            tips[1] = Corner(end.Centre + Along(rings, count - 1) * MathF.Min(end.Wide, end.High) * 0.5f, colour(1f, 0f), end.Bone, end.Next, end.Share);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Triangle(tips[0], first + i, first + next);
                Triangle(tips[1], first + (count - 1) * sides + next, first + (count - 1) * sides + i);
            }
        }

        private static Vector3 Along(ReadOnlySpan<Ring> rings, int index)
        {
            int from = Math.Max(index - 1, 0);
            int to = Math.Min(index + 1, rings.Length - 1);
            Vector3 direction = rings[to].Centre - rings[from].Centre;
            return direction.LengthSquared > 1e-10f ? direction.Normalized() : Vector3.UnitZ;
        }

        /// <summary>An egg, along the z axis, turned by <paramref name="pitch"/> about x.</summary>
        public void Egg(Vector3 centre, Vector3 radii, Vector3 colour, int bone, int sides = 7, int rings = 4, float pitch = 0f)
        {
            Span<Ring> list = stackalloc Ring[rings + 2];
            Vector3 axis = new Vector3(0f, -MathF.Sin(pitch), MathF.Cos(pitch));
            for (int i = 0; i < list.Length; i++)
            {
                float t = (i + 0.5f) / list.Length;
                float along = MathF.Cos(MathF.PI * (1f - t));
                float across = MathF.Sqrt(MathF.Max(1f - along * along, 0.02f));
                list[i] = new Ring(centre + axis * (along * radii.Z), radii.X * across, radii.Y * across, bone);
            }
            Loft(list, sides, (_, _) => colour);
        }

        /// <summary>A round limb from one point to the next, thinning as it goes.</summary>
        public void Limb(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, Vector3 colour, int bone, int sides = 5)
        {
            Span<Ring> list = stackalloc Ring[2];
            list[0] = new Ring(from, radiusFrom, radiusFrom, bone);
            list[1] = new Ring(to, radiusTo, radiusTo, bone);
            Loft(list, sides, (_, _) => colour);
        }

        /// <summary>The mesh, tied to its bones.</summary>
        public Mesh Build()
        {
            var mesh = new Mesh(_vertices, _indices) { isUsingBones = true };
            mesh.calculateAllNormals();
            return mesh;
        }
    }
}
