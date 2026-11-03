using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    /// <summary>
    /// Collects flat-shaded shapes into one mesh. Adding meshes together with + rebuilds everything each time, which is far too
    /// slow for a building's hundreds of beams; this keeps plain lists and makes the mesh once at the end.
    /// Faces are wound counter-clockwise seen from outside. A shape's corners are moved by <see cref="Origin"/>, <see cref="Yaw"/>
    /// and <see cref="Stretch"/>, so a detail can be built once in its own space and placed anywhere.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vertex> _vertices = new();
        private readonly List<int> _indices = new();

        /// <summary>Texture repeats per metre.</summary>
        public float TilesPerMetre = 0.2f;
        public Vector3 Origin = Vector3.Zero;
        public float Yaw = 0f;
        public Vector3 Stretch = Vector3.One;

        public int VertexCount => _vertices.Count;

        public void ResetPlacement()
        {
            Origin = Vector3.Zero;
            Yaw = 0f;
            Stretch = Vector3.One;
        }

        public void Place(Vector3 origin, float yaw = 0f)
        {
            Origin = origin;
            Yaw = yaw;
        }

        private Vector3 Transform(Vector3 p)
        {
            p *= Stretch;
            float cos = MathF.Cos(Yaw);
            float sin = MathF.Sin(Yaw);
            return Origin + new Vector3(p.X * cos + p.Z * sin, p.Y, -p.X * sin + p.Z * cos);
        }

        /// <summary>A flat four-cornered face, from <paramref name="a"/> around to <paramref name="d"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, VertexMaterial material)
        {
            Vector3 along = b - a;
            float width = along.Length;
            Vector3 u = width > 1e-5f ? along / width : Vector3.UnitX;
            Vector3 side = d - a;
            Vector3 v = (side - u * Vector3.Dot(side, u));
            float height = v.Length;
            v = height > 1e-5f ? v / height : Vector3.UnitY;

            // Offset by where the face is, so that the same detail repeated doesn't show the same patch of texture.
            Vector2 start = new Vector2(a.X + a.Z, a.Y) * TilesPerMetre;
            int first = _vertices.Count;
            AddVertex(a, material, start);
            AddVertex(b, material, start + new Vector2(Vector3.Dot(b - a, u), Vector3.Dot(b - a, v)) * TilesPerMetre);
            AddVertex(c, material, start + new Vector2(Vector3.Dot(c - a, u), Vector3.Dot(c - a, v)) * TilesPerMetre);
            AddVertex(d, material, start + new Vector2(Vector3.Dot(d - a, u), Vector3.Dot(d - a, v)) * TilesPerMetre);
            AddFace(first, first + 1, first + 2);
            AddFace(first, first + 2, first + 3);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, VertexMaterial material)
        {
            Vector3 along = b - a;
            float width = along.Length;
            Vector3 u = width > 1e-5f ? along / width : Vector3.UnitX;
            Vector3 side = c - a;
            Vector3 v = side - u * Vector3.Dot(side, u);
            float height = v.Length;
            v = height > 1e-5f ? v / height : Vector3.UnitY;

            int first = _vertices.Count;
            AddVertex(a, material, Vector2.Zero);
            AddVertex(b, material, new Vector2(Vector3.Dot(b - a, u), Vector3.Dot(b - a, v)) * TilesPerMetre);
            AddVertex(c, material, new Vector2(Vector3.Dot(c - a, u), Vector3.Dot(c - a, v)) * TilesPerMetre);
            AddFace(first, first + 1, first + 2);
        }

        private void AddVertex(Vector3 position, VertexMaterial material, Vector2 uv)
        {
            _vertices.Add(new Vertex(Transform(position), material, uv));
        }

        private void AddFace(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        public void Box(Vector3 centre, Vector3 half, VertexMaterial material, bool closeBottom = false)
        {
            Vector3 min = centre - half;
            Vector3 max = centre + half;
            Vector3 p000 = new Vector3(min.X, min.Y, min.Z);
            Vector3 p100 = new Vector3(max.X, min.Y, min.Z);
            Vector3 p010 = new Vector3(min.X, max.Y, min.Z);
            Vector3 p110 = new Vector3(max.X, max.Y, min.Z);
            Vector3 p001 = new Vector3(min.X, min.Y, max.Z);
            Vector3 p101 = new Vector3(max.X, min.Y, max.Z);
            Vector3 p011 = new Vector3(min.X, max.Y, max.Z);
            Vector3 p111 = new Vector3(max.X, max.Y, max.Z);

            Quad(p001, p101, p111, p011, material);     // +z
            Quad(p100, p000, p010, p110, material);     // -z
            Quad(p101, p100, p110, p111, material);     // +x
            Quad(p000, p001, p011, p010, material);     // -x
            Quad(p011, p111, p110, p010, material);     // +y
            if (closeBottom) Quad(p000, p100, p101, p001, material);
        }

        /// <summary>A square timber from one point to another.</summary>
        public void Beam(Vector3 from, Vector3 to, float halfWidth, VertexMaterial material)
        {
            Vector3 axis = to - from;
            float length = axis.Length;
            if (length < 1e-4f) return;
            axis /= length;

            Vector3 reference = MathF.Abs(axis.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Cross(axis, reference).Normalized() * halfWidth;
            Vector3 up = Vector3.Cross(right, axis).Normalized() * halfWidth;

            Vector3 a0 = from - right - up, a1 = from + right - up, a2 = from + right + up, a3 = from - right + up;
            Vector3 b0 = to - right - up, b1 = to + right - up, b2 = to + right + up, b3 = to - right + up;

            Quad(a0, a1, b1, b0, material);
            Quad(a1, a2, b2, b1, material);
            Quad(a2, a3, b3, b2, material);
            Quad(a3, a0, b0, b3, material);
            Quad(b0, b1, b2, b3, material);
            Quad(a1, a0, a3, a2, material);
        }

        /// <summary>A round log or pole, tapering from one radius to the other, with shared vertices so it shades round.</summary>
        public void Tube(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int sides, VertexMaterial material, bool capped = true)
        {
            Vector3 axis = to - from;
            float length = axis.Length;
            if (length < 1e-4f) return;
            axis /= length;

            Vector3 reference = MathF.Abs(axis.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Cross(axis, reference).Normalized();
            Vector3 up = Vector3.Cross(right, axis).Normalized();

            int first = _vertices.Count;
            float around = MathF.Tau * MathF.Max(radiusFrom, radiusTo) * TilesPerMetre;
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * (MathF.Tau / sides);
                Vector3 direction = right * MathF.Cos(angle) + up * MathF.Sin(angle);
                float u = (float)i / sides * around;
                AddVertex(from + direction * radiusFrom, material, new Vector2(u, 0f));
                AddVertex(to + direction * radiusTo, material, new Vector2(u, length * TilesPerMetre));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = first + i * 2;
                AddFace(a, a + 2, a + 1);
                AddFace(a + 2, a + 3, a + 1);
            }

            if (!capped) return;
            int centreTop = _vertices.Count;
            AddVertex(to, material, Vector2.Zero);
            int ring = _vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (MathF.Tau / sides);
                Vector3 direction = right * MathF.Cos(angle) + up * MathF.Sin(angle);
                AddVertex(to + direction * radiusTo, material, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radiusTo * TilesPerMetre);
            }
            for (int i = 0; i < sides; i++)
            {
                AddFace(centreTop, ring + (i + 1) % sides, ring + i);
            }
        }

        /// <summary>
        /// A smooth surface over a grid of points, from a function of u and v (each 0..1). Corners run counter-clockwise seen from
        /// outside as u and then v increase.
        /// </summary>
        public void Surface(int stepsU, int stepsV, Func<float, float, Vector3> point, VertexMaterial material)
        {
            int first = _vertices.Count;
            int row = stepsU + 1;

            // Texture follows distance along the surface, so that it doesn't stretch up a roof.
            for (int j = 0; j <= stepsV; j++)
            {
                for (int i = 0; i <= stepsU; i++)
                {
                    float u = (float)i / stepsU;
                    float v = (float)j / stepsV;
                    Vector3 p = point(u, v);
                    float acrossU = (point(1f, v) - point(0f, v)).Length * u;
                    float acrossV = (point(u, 1f) - point(u, 0f)).Length * v;
                    AddVertex(p, material, new Vector2(acrossU, acrossV) * TilesPerMetre);
                }
            }

            for (int j = 0; j < stepsV; j++)
            {
                for (int i = 0; i < stepsU; i++)
                {
                    int a = first + j * row + i;
                    AddFace(a, a + 1, a + row + 1);
                    AddFace(a, a + row + 1, a + row);
                }
            }
        }

        public void Add(Mesh mesh, Vector3 offset, Vector3 scale, float yaw = 0f)
        {
            Vector3 savedOrigin = Origin;
            float savedYaw = Yaw;
            Vector3 savedStretch = Stretch;
            Origin = Transform(offset);
            Yaw += yaw;
            Stretch = savedStretch * scale;

            Vertex[] vertices = mesh.getAllVerticesArray();
            vIndex[] indices = mesh.getAllIndicesArray();
            int first = _vertices.Count;
            foreach (Vertex vertex in vertices)
            {
                _vertices.Add(new Vertex(Transform(vertex.position), vertex.materialTextureIndex, vertex.colour, vertex.boneIndices, vertex.BoneWeights, vertex.UVs));
            }
            foreach (vIndex index in indices) _indices.Add(first + index.index);

            Origin = savedOrigin;
            Yaw = savedYaw;
            Stretch = savedStretch;
        }

        public Mesh Build()
        {
            return new Mesh(_vertices, _indices);
        }
    }
}
