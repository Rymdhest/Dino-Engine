using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
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
        private readonly List<PlantJoint> _joints = new();

        /// <summary>
        /// Texture repeats per metre, for whatever wants every material at one size. Left at NaN, as it should almost always be, each material is mapped at the size
        /// it was made for (<see cref="MaterialSpec.TileMetres"/>), which is the size its relief's depth means: a stone wall's stones are the size of stones
        /// whatever they're built into.
        /// </summary>
        public float TilesPerMetre = float.NaN;
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

        private float TilesFor(VertexMaterial material) => float.IsNaN(TilesPerMetre) ? MaterialCatalog.TilesPerMetre(material.materialTextureIndex) : TilesPerMetre;

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
            // Offset by where the face is, so that the same detail repeated doesn't show the same patch of texture.
            Quad(a, b, c, d, material, new Vector2(a.X + a.Z, a.Y));
        }

        /// <summary>A face whose texture starts at <paramref name="start"/>, in metres, at <paramref name="a"/>: u runs from a towards b, and v up the face.</summary>
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, VertexMaterial material, Vector2 startMetres)
        {
            float tiles = TilesFor(material);
            Vector3 along = b - a;
            float width = along.Length;
            Vector3 u = width > 1e-5f ? along / width : Vector3.UnitX;
            Vector3 side = d - a;
            Vector3 v = (side - u * Vector3.Dot(side, u));
            float height = v.Length;
            v = height > 1e-5f ? v / height : Vector3.UnitY;

            Vector2 start = startMetres * tiles;
            Vector3 ta = Transform(a), tb = Transform(b), tc = Transform(c), td = Transform(d);
            Vector3 n = FaceNormal(ta, tb, tc, td);
            int first = _vertices.Count;
            AddPlaced(ta, material, start, EdgesOf(ta, n, ta, tb, tc, td));
            AddPlaced(tb, material, start + new Vector2(Vector3.Dot(b - a, u), Vector3.Dot(b - a, v)) * tiles, EdgesOf(tb, n, ta, tb, tc, td));
            AddPlaced(tc, material, start + new Vector2(Vector3.Dot(c - a, u), Vector3.Dot(c - a, v)) * tiles, EdgesOf(tc, n, ta, tb, tc, td));
            AddPlaced(td, material, start + new Vector2(Vector3.Dot(d - a, u), Vector3.Dot(d - a, v)) * tiles, EdgesOf(td, n, ta, tb, tc, td));
            AddFace(first, first + 1, first + 2);
            AddFace(first, first + 2, first + 3);
        }

        // The face's normal by its winding, from both diagonals so that a quad that isn't quite flat still has a fair one.
        private static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(c - a, d - b);
            return n.LengthSquared > 1e-20f ? n.Normalized() : Vector3.UnitY;
        }

        // How far a point of a face is from the line through `from` and `to`, within the face: positive inside whichever way the face is wound, as inside is on
        // the left of every edge seen from the side the winding faces.
        private static float FromEdge(Vector3 p, Vector3 n, Vector3 from, Vector3 to)
        {
            Vector3 inward = Vector3.Cross(n, to - from);
            float length = inward.Length;
            return length > 1e-8f ? Vector3.Dot(p - from, inward / length) : 1e6f;
        }

        private static Vector4 EdgesOf(Vector3 p, Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            return new Vector4(FromEdge(p, n, a, b), FromEdge(p, n, b, c), FromEdge(p, n, c, d), FromEdge(p, n, d, a));
        }

        private void AddPlaced(Vector3 placed, VertexMaterial material, Vector2 uv, Vector4 edges)
        {
            _vertices.Add(new Vertex(placed, material, uv) { Edges = edges });
        }

        /// <summary>
        /// A flat rectangle that's one piece of a larger face, such as a wall broken by windows: its corners are <paramref name="origin"/> + u·<paramref name="uAxis"/> + v·<paramref name="vAxis"/>
        /// for u from <paramref name="u0"/> to <paramref name="u1"/> and v from <paramref name="v0"/> to <paramref name="v1"/>, seen from the side cross(uAxis, vAxis) faces. Its texture runs on from the
        /// face's origin, so the pieces of one face meet with no seam, and its relief comes up to nothing only at the face's own edges, <paramref name="bounds"/> (least u, most u, least v, most v),
        /// not at the piece's. The axes are unit and square to each other, and v runs up a wall.
        /// </summary>
        public void Panel(Vector3 origin, Vector3 uAxis, Vector3 vAxis, float u0, float u1, float v0, float v1, VertexMaterial material, Vector4 bounds)
        {
            if (u1 - u0 < 1e-4f || v1 - v0 < 1e-4f) return;
            PlaneQuad(origin + uAxis * u0 + vAxis * v0, origin + uAxis * u1 + vAxis * v0, origin + uAxis * u1 + vAxis * v1, origin + uAxis * u0 + vAxis * v1, material, origin, uAxis, vAxis, bounds);
        }

        /// <summary>
        /// Any four corners lying in a face that's laid out by <paramref name="origin"/>, <paramref name="uAxis"/> and <paramref name="vAxis"/>, as <see cref="Panel"/> is, wound from
        /// <paramref name="a"/> round to <paramref name="d"/> counter-clockwise seen from the front: a slanted piece of wall under an arch, or a gable. <paramref name="tint"/>, if it's
        /// given, colours each corner, so that a roof can weather in soft patches rather than all of a piece.
        /// </summary>
        public void PlaneQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, VertexMaterial material, Vector3 origin, Vector3 uAxis, Vector3 vAxis, Vector4 bounds, Func<Vector3, Colour>? tint = null)
        {
            float tiles = TilesFor(material);
            float scale = MathF.Cbrt(MathF.Abs(Stretch.X * Stretch.Y * Stretch.Z));
            int first = _vertices.Count;
            void Corner(Vector3 p)
            {
                float u = Vector3.Dot(p - origin, uAxis);
                float v = Vector3.Dot(p - origin, vAxis);
                Vector4 edges = new Vector4(u - bounds.X, bounds.Y - u, v - bounds.Z, bounds.W - v) * scale;
                VertexMaterial painted = tint == null ? material : new VertexMaterial(material.materialTextureIndex, tint(p));
                AddPlaced(Transform(p), painted, new Vector2(u, v) * tiles, edges);
            }
            Corner(a);
            Corner(b);
            Corner(c);
            Corner(d);
            AddFace(first, first + 1, first + 2);
            AddFace(first, first + 2, first + 3);
        }

        /// <summary>A triangle in a face laid out as <see cref="PlaneQuad"/>'s is: the point of a gable.</summary>
        public void PlaneTriangle(Vector3 a, Vector3 b, Vector3 c, VertexMaterial material, Vector3 origin, Vector3 uAxis, Vector3 vAxis, Vector4 bounds)
        {
            float tiles = TilesFor(material);
            float scale = MathF.Cbrt(MathF.Abs(Stretch.X * Stretch.Y * Stretch.Z));
            int first = _vertices.Count;
            void Corner(Vector3 p)
            {
                float u = Vector3.Dot(p - origin, uAxis);
                float v = Vector3.Dot(p - origin, vAxis);
                AddPlaced(Transform(p), material, new Vector2(u, v) * tiles, new Vector4(u - bounds.X, bounds.Y - u, v - bounds.Z, bounds.W - v) * scale);
            }
            Corner(a);
            Corner(b);
            Corner(c);
            AddFace(first, first + 1, first + 2);
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

            Vector3 ta = Transform(a), tb = Transform(b), tc = Transform(c);
            Vector3 n = Vector3.Cross(tb - ta, tc - ta);
            n = n.LengthSquared > 1e-20f ? n.Normalized() : Vector3.UnitY;
            Vector4 Edges(Vector3 p) => new Vector4(FromEdge(p, n, ta, tb), FromEdge(p, n, tb, tc), FromEdge(p, n, tc, ta), 1e6f);
            int first = _vertices.Count;
            AddPlaced(ta, material, Vector2.Zero, Edges(ta));
            float tiles = TilesFor(material);
            AddPlaced(tb, material, new Vector2(Vector3.Dot(b - a, u), Vector3.Dot(b - a, v)) * tiles, Edges(tb));
            AddPlaced(tc, material, new Vector2(Vector3.Dot(c - a, u), Vector3.Dot(c - a, v)) * tiles, Edges(tc));
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

            // The sides' texture runs on round the box, so that courses of stone and brick carry round every corner but the one it starts from; offset by where
            // the box is, so that boxes side by side don't show the same patch.
            float lengthX = max.X - min.X, lengthZ = max.Z - min.Z;
            float round = centre.X + centre.Z;
            Quad(p001, p101, p111, p011, material, new Vector2(round, min.Y));                                  // +z
            Quad(p101, p100, p110, p111, material, new Vector2(round + lengthX, min.Y));                        // +x
            Quad(p100, p000, p010, p110, material, new Vector2(round + lengthX + lengthZ, min.Y));              // -z
            Quad(p000, p001, p011, p010, material, new Vector2(round + 2f * lengthX + lengthZ, min.Y));         // -x
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
            float tiles = TilesFor(material);
            // A whole number of tiles round it, so that its texture closes on itself with no seam.
            float around = MathF.Max(1f, MathF.Round(MathF.Tau * MathF.Max(radiusFrom, radiusTo) * tiles));
            // Its ends are its only edges: round it, it goes on for ever.
            float placedLength = (Transform(to) - Transform(from)).Length;
            Vector4 atFrom = new Vector4(0f, placedLength, 1e6f, 1e6f);
            Vector4 atTo = new Vector4(placedLength, 0f, 1e6f, 1e6f);
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * (MathF.Tau / sides);
                Vector3 direction = right * MathF.Cos(angle) + up * MathF.Sin(angle);
                float u = (float)i / sides * around;
                AddPlaced(Transform(from + direction * radiusFrom), material, new Vector2(u, 0f), atFrom);
                AddPlaced(Transform(to + direction * radiusTo), material, new Vector2(u, length * tiles), atTo);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = first + i * 2;
                AddFace(a, a + 2, a + 1);
                AddFace(a + 2, a + 3, a + 1);
            }

            if (!capped) return;
            // The cap's edge is its rim: its middle is as far from it as the radius.
            int centreTop = _vertices.Count;
            float placedRadius = (Transform(to + right * radiusTo) - Transform(to)).Length;
            AddPlaced(Transform(to), material, Vector2.Zero, new Vector4(placedRadius, 1e6f, 1e6f, 1e6f));
            int ring = _vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (MathF.Tau / sides);
                Vector3 direction = right * MathF.Cos(angle) + up * MathF.Sin(angle);
                AddPlaced(Transform(to + direction * radiusTo), material, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radiusTo * tiles, new Vector4(0f, 1e6f, 1e6f, 1e6f));
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
            float tiles = TilesFor(material);

            // Texture follows distance along the surface, so that it doesn't stretch up a roof.
            for (int j = 0; j <= stepsV; j++)
            {
                for (int i = 0; i <= stepsU; i++)
                {
                    float u = (float)i / stepsU;
                    float v = (float)j / stepsV;
                    Vector3 p = point(u, v);
                    float lengthU = (point(1f, v) - point(0f, v)).Length;
                    float lengthV = (point(u, 1f) - point(u, 0f)).Length;
                    float acrossU = lengthU * u;
                    float acrossV = lengthV * v;
                    // Its edges are the grid's boundary, never the lines between its quads.
                    float scale = MathF.Cbrt(MathF.Abs(Stretch.X * Stretch.Y * Stretch.Z));
                    Vector4 edges = new Vector4(acrossU, lengthU - acrossU, acrossV, lengthV - acrossV) * scale;
                    AddPlaced(Transform(p), material, new Vector2(acrossU, acrossV) * tiles, edges);
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

        /// <summary>A joint for a plant. Nothing's tied to it until <see cref="BindFrom"/>.</summary>
        public int AddJoint(PlantJoint joint)
        {
            joint.Base = Transform(joint.Base);
            joint.Tip = Transform(joint.Tip);
            _joints.Add(joint);
            return _joints.Count - 1;
        }

        /// <summary>Ties every vertex from <paramref name="firstVertex"/> on that nothing holds yet to a joint.</summary>
        public void BindFrom(int firstVertex, int joint)
        {
            for (int i = firstVertex; i < _vertices.Count; i++)
            {
                if (_vertices[i].joint < 0) _vertices[i].joint = joint;
            }
        }

        /// <summary>A chain of joints through <paramref name="points"/>, with every vertex from <paramref name="firstVertex"/> on tied to the link it's nearest. See <see cref="Mesh.BindChain"/>.</summary>
        public int BindChain(int firstVertex, PlantJoint like, int parent, params Vector3[] points)
        {
            int first = _joints.Count;
            foreach (PlantJoint link in like.Chain(points))
            {
                PlantJoint joint = link;
                joint.Parent = _joints.Count > first ? _joints.Count - 1 : parent;
                parent = AddJoint(joint);
            }
            for (int i = firstVertex; i < _vertices.Count; i++)
            {
                if (_vertices[i].joint < 0) _vertices[i].joint = PlantRig.Nearest(_joints, first, parent, _vertices[i].position);
            }
            return parent;
        }

        /// <param name="parent">The joint of this builder that the mesh's own hang from, if it has any.</param>
        public void Add(Mesh mesh, Vector3 offset, Vector3 scale, float yaw = 0f, int parent = -1)
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
            int firstJoint = _joints.Count;
            foreach (Vertex vertex in vertices)
            {
                int joint = vertex.joint >= 0 ? vertex.joint + firstJoint : -1;
                Vector4 edges = vertex.Edges == Vertex.NoEdges ? Vertex.NoEdges : vertex.Edges * MathF.Cbrt(MathF.Abs(Stretch.X * Stretch.Y * Stretch.Z));
                _vertices.Add(new Vertex(Transform(vertex.position), vertex.materialTextureIndex, vertex.colour, vertex.boneIndices, vertex.BoneWeights, vertex.UVs) { joint = joint, Edges = edges });
            }
            foreach (vIndex index in indices) _indices.Add(first + index.index);
            if (mesh.joints != null)
            {
                foreach (PlantJoint joint in mesh.joints)
                {
                    PlantJoint added = joint;
                    added.Parent = joint.Parent >= 0 ? joint.Parent + firstJoint : parent;
                    AddJoint(added);
                }
            }

            Origin = savedOrigin;
            Yaw = savedYaw;
            Stretch = savedStretch;
        }

        public Mesh Build()
        {
            return new Mesh(_vertices, _indices) { joints = _joints.Count > 0 ? _joints : null };
        }
    }
}
