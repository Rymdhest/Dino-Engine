using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>One slice across something lofted: where it is, which way it faces, and how big it is.</summary>
    public struct Ring
    {
        public Vector3 Centre;
        public Vector3 Side;        // Which way a section's x goes.
        public Vector3 Front;       // Which way its y goes.
        public float HalfWidth;
        public float HalfDepth;
        public bool Hard;           // A crease right round, rather than a smooth run into the next.
        public Vector2[]? Shape;    // Instead of the section's own outline, for what changes along its length. Always the same number of points.

        /// <summary>A ring at this height, lying flat.</summary>
        public static Ring Flat(float y, float halfWidth, float halfDepth, bool hard = false) => new Ring
        {
            Centre = new Vector3(0f, y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth, HalfDepth = halfDepth, Hard = hard,
        };
    }

    /// <summary>
    /// Builds what a piece of gear is made of. Like <see cref="Architecture.MeshBuilder"/> it keeps plain lists and makes a mesh once at the end, but it lofts and sweeps, keeps edges
    /// hard where they should be, knows which bones it's on, and can be merged and posed, as the same piece goes on a body, on the ground, and in front of a camera.
    /// </summary>
    public sealed class GearMesh
    {
        private const float Thinnest = 0.0015f;     // Nothing goes down to nothing: a face with no area has no normal.

        private readonly List<Vertex> _vertices = new();
        private readonly List<int> _indices = new();
        private readonly List<(int a, int b)> _welds = new();
        private Matrix4 _place = Matrix4.Identity;
        private bool _flipped;

        /// <summary>Texture repeats per metre.</summary>
        public float TilesPerMetre = 0.6f;

        /// <summary>The bone what's built next goes on, until something says otherwise.</summary>
        public int Bone;

        /// <summary>What this hides of a body wearing it.</summary>
        public BodyRegions Covers;

        public int VertexCount => _vertices.Count;
        public int TriangleCount => _indices.Count / 3;
        public IReadOnlyList<Vertex> Vertices => _vertices;

        /// <summary>Everything built inside <paramref name="build"/> goes through this, on top of wherever things are already going.</summary>
        public void At(Matrix4 place, Action build)
        {
            Matrix4 saved = _place;
            bool flipped = _flipped;
            _place = place * _place;
            _flipped ^= place.Determinant < 0f;
            build();
            _place = saved;
            _flipped = flipped;
        }

        /// <summary>Builds something twice, once as it is and once reflected across x. A thing whose two sides are the same is most of what makes it look made.</summary>
        public void BothSides(Action build)
        {
            build();
            At(Matrix4.CreateScale(-1f, 1f, 1f), build);
        }

        private int AddVertex(Vector3 position, in VertexMaterial material, Vector2 uv)
        {
            _vertices.Add(new Vertex(Vector3.TransformPosition(position, _place), material.materialTextureIndex, material.colour, new Vector2(Bone, 0f), new Vector2(1f, 0f), uv));
            return _vertices.Count - 1;
        }

        private void AddFace(int a, int b, int c)
        {
            _indices.Add(a);
            if (_flipped)
            {
                _indices.Add(c);
                _indices.Add(b);
            }
            else
            {
                _indices.Add(b);
                _indices.Add(c);
            }
        }

        /// <summary>
        /// A skin through a run of rings. Which way it faces follows from which way the rings run, so nothing has to care. <paramref name="material"/> is asked for each point of each ring,
        /// so colour can change along and around. A section's hard points and a ring's <see cref="Ring.Hard"/> are kept as creases by giving each side a vertex of its own.
        /// </summary>
        public void Loft(Section section, IReadOnlyList<Ring> rings, Func<int, int, VertexMaterial> material, bool capStart = true, bool capEnd = true)
        {
            int points = section.Count;

            // Where each column of vertices falls round the outline. A hard point has two, one for the face on either side of it, and the first point comes round again at the end.
            var columns = new List<(int point, float around)>(points * 2);
            float distance = 0f;
            Vector2[] first = rings[0].Shape ?? section.Points;
            for (int i = 0; i <= points; i++)
            {
                int point = i % points;
                columns.Add((point, distance));
                if (i > 0 && i < points && section.Hard[point]) columns.Add((point, distance));
                if (i < points) distance += (first[(i + 1) % points] - first[point]).Length;
            }

            // From the first two, as a loop ends where it started.
            Vector3 axis = rings[1].Centre - rings[0].Centre;
            bool forwards = Vector3.Dot(Vector3.Cross(rings[0].Side, rings[0].Front), axis) < 0f;

            float along = 0f;
            int[]? previous = null;
            for (int r = 0; r < rings.Count; r++)
            {
                if (r > 0) along += (rings[r].Centre - rings[r - 1].Centre).Length;

                int[] row = AddRow(section, rings[r], columns, r, along, material);
                if (previous != null) Stitch(previous, row, columns, forwards);
                previous = rings[r].Hard && r > 0 && r < rings.Count - 1 ? AddRow(section, rings[r], columns, r, along, material) : row;

                Weld(row, columns, section);
                if (!ReferenceEquals(previous, row)) Weld(previous, columns, section);
            }

            if (capStart) Cap(section, rings[0], 0, material, !forwards);
            if (capEnd) Cap(section, rings[^1], rings.Count - 1, material, forwards);
        }

        public void Loft(Section section, IReadOnlyList<Ring> rings, VertexMaterial material, bool capStart = true, bool capEnd = true)
        {
            Loft(section, rings, (_, _) => material, capStart, capEnd);
        }

        private int[] AddRow(Section section, in Ring ring, List<(int point, float around)> columns, int index, float along, Func<int, int, VertexMaterial> material)
        {
            var row = new int[columns.Count];
            for (int c = 0; c < columns.Count; c++)
            {
                (int point, float around) = columns[c];
                row[c] = AddVertex(PointOn(section, ring, point), material(index, point), new Vector2(around * MathF.Max(ring.HalfWidth, ring.HalfDepth), along) * TilesPerMetre);
            }
            return row;
        }

        private static Vector3 PointOn(Section section, in Ring ring, int point)
        {
            Vector2 p = (ring.Shape ?? section.Points)[point];
            return ring.Centre + ring.Side * (p.X * MathF.Max(ring.HalfWidth, Thinnest)) + ring.Front * (p.Y * MathF.Max(ring.HalfDepth, Thinnest));
        }

        private void Stitch(int[] from, int[] to, List<(int point, float around)> columns, bool forwards)
        {
            for (int c = 0; c + 1 < columns.Count; c++)
            {
                // A hard point has two columns in one place, and there's no face between those.
                if (columns[c].point == columns[c + 1].point) continue;

                int a = from[c], b = from[c + 1], d = to[c], e = to[c + 1];
                if (forwards)
                {
                    AddFace(a, d, e);
                    AddFace(a, e, b);
                }
                else
                {
                    AddFace(a, e, d);
                    AddFace(a, b, e);
                }
            }
        }

        // The first and last columns are the same place on a closed outline, and unless that's meant to be a crease they have to shade as one.
        private void Weld(int[] row, List<(int point, float around)> columns, Section section)
        {
            if (!section.Hard[0]) _welds.Add((row[0], row[columns.Count - 1]));
        }

        private void Cap(Section section, in Ring ring, int index, Func<int, int, VertexMaterial> material, bool forwards)
        {
            int centre = section.ClosesAcross ? -1 : AddVertex(ring.Centre, material(index, 0), Vector2.Zero);
            int first = _vertices.Count;
            for (int i = 0; i < section.Count; i++)
            {
                Vector2 p = (ring.Shape ?? section.Points)[i];
                AddVertex(PointOn(section, ring, i), material(index, i), new Vector2(p.X * ring.HalfWidth, p.Y * ring.HalfDepth) * TilesPerMetre);
            }
            if (section.ClosesAcross)
            {
                for (int i = 0; i + 1 < section.Count / 2; i++)
                {
                    int a = first + i, b = first + i + 1, c = first + section.Count - 2 - i, d = first + section.Count - 1 - i;
                    if (forwards)
                    {
                        AddFace(a, b, c);
                        AddFace(a, c, d);
                    }
                    else
                    {
                        AddFace(a, c, b);
                        AddFace(a, d, c);
                    }
                }
                return;
            }

            for (int i = 0; i < section.Count; i++)
            {
                int a = first + i, b = first + (i + 1) % section.Count;
                if (forwards) AddFace(centre, a, b);
                else AddFace(centre, b, a);
            }
        }

        /// <summary>Something turned on a lathe, up y from where it's put, with its outline given by <paramref name="radius"/> from 0 to 1 of the way up.</summary>
        public void Lathe(Section section, float height, int steps, Func<float, float> radius, VertexMaterial material, bool capStart = true, bool capEnd = true, float squash = 1f)
        {
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float r = radius(t);
                rings[i] = Ring.Flat(t * height, r, r * squash);
            }
            Loft(section, rings, material, capStart, capEnd);
        }

        /// <summary>A section carried along a line. Its frame turns as little as it can on the way, so that nothing twists that shouldn't.</summary>
        /// <param name="size">How big it is, from 0 to 1 of the way along, as its half width and half depth.</param>
        /// <param name="up">Roughly which way the section's y should start out.</param>
        public void Sweep(Section section, IReadOnlyList<Vector3> spine, Func<float, Vector2> size, VertexMaterial material, Vector3 up, bool capStart = true, bool capEnd = true)
        {
            var rings = new Ring[spine.Count];
            Vector3 tangent = (spine[1] - spine[0]).Normalized();
            Vector3 front = (up - tangent * Vector3.Dot(up, tangent)).Normalized();
            for (int i = 0; i < spine.Count; i++)
            {
                Vector3 next = (spine[Math.Min(i + 1, spine.Count - 1)] - spine[Math.Max(i - 1, 0)]).Normalized();
                front = (front - next * Vector3.Dot(front, next)).Normalized();
                tangent = next;

                Vector2 s = size((float)i / (spine.Count - 1));
                rings[i] = new Ring { Centre = spine[i], Side = Vector3.Cross(front, tangent), Front = front, HalfWidth = s.X, HalfDepth = s.Y };
            }
            Loft(section, rings, material, capStart, capEnd);
        }

        /// <summary>A loop of something, flat around y, for rings and bands.</summary>
        public void Hoop(Section section, float radius, float halfWidth, float halfDepth, int steps, VertexMaterial material)
        {
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = (float)i / steps * MathF.Tau;
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                rings[i] = new Ring { Centre = outwards * radius, Side = outwards, Front = Vector3.UnitY, HalfWidth = halfWidth, HalfDepth = halfDepth };
            }
            Loft(section, rings, material, false, false);
        }

        /// <summary>The brim of a hat: a flat ring from one radius to another, drooping a little at its edge.</summary>
        public void Hoop2(Vector3 at, float outer, float inner, float droop, VertexMaterial material)
        {
            Ring[] rings =
            {
                new Ring { Centre = at + Vector3.UnitY * 0.012f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = inner, HalfDepth = inner },
                new Ring { Centre = at + Vector3.UnitY * (0.012f - droop * 0.4f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = (inner + outer) * 0.5f, HalfDepth = (inner + outer) * 0.5f },
                new Ring { Centre = at - Vector3.UnitY * droop, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = outer, HalfDepth = outer },
            };
            Loft(Section.Round(16), rings, material, capStart: false, capEnd: false);
        }

        /// <summary>A cut stone. Its facets stay facets.</summary>
        public void Gem(int sides, float radius, float crown, float pavilion, float table, VertexMaterial material)
        {
            Ring[] rings =
            {
                Ring.Flat(-pavilion, radius * 0.02f, radius * 0.02f, true),
                Ring.Flat(0f, radius, radius, true),
                Ring.Flat(crown, radius * table, radius * table, true),
            };
            Loft(Section.Polygon(sides), rings, material);
        }

        /// <summary>Takes in everything from another, as it stands, through wherever things are going here.</summary>
        public void Add(GearMesh other)
        {
            int first = _vertices.Count;
            foreach (Vertex vertex in other._vertices)
            {
                _vertices.Add(new Vertex(Vector3.TransformPosition(vertex.position, _place), vertex.materialTextureIndex, vertex.colour, vertex.boneIndices, vertex.BoneWeights, vertex.UVs));
            }
            for (int i = 0; i < other._indices.Count; i += 3) AddFace(first + other._indices[i], first + other._indices[i + 1], first + other._indices[i + 2]);
            foreach ((int a, int b) in other._welds) _welds.Add((first + a, first + b));
        }

        /// <summary>Takes in a mesh that was made some other way, with the bones it has.</summary>
        public void Add(Mesh mesh)
        {
            int first = _vertices.Count;
            Vertex[] vertices = mesh.getAllVerticesArray();
            foreach (Vertex vertex in vertices)
            {
                _vertices.Add(new Vertex(Vector3.TransformPosition(vertex.position, _place), vertex.materialTextureIndex, vertex.colour, vertex.boneIndices, vertex.BoneWeights, vertex.UVs));
            }
            vIndex[] indices = mesh.getAllIndicesArray();
            for (int i = 0; i + 2 < indices.Length; i += 3) AddFace(first + indices[i].index, first + indices[i + 1].index, first + indices[i + 2].index);
        }

        /// <summary>Puts everything on one bone.</summary>
        public void SetBone(int bone)
        {
            Bone = bone;
            foreach (Vertex vertex in _vertices)
            {
                vertex.boneIndices = new Vector2(bone, 0f);
                vertex.BoneWeights = new Vector2(1f, 0f);
            }
        }

        /// <summary>Sets what every vertex built so far is skinned to, from where it is.</summary>
        public void SkinBy(Func<Vector3, (Vector2 bones, Vector2 weights)> skin, int from = 0)
        {
            for (int i = from; i < _vertices.Count; i++)
            {
                (Vector2 bones, Vector2 weights) = skin(_vertices[i].position);
                _vertices[i].boneIndices = bones;
                _vertices[i].BoneWeights = weights;
            }
        }

        /// <summary>Moves every vertex by what its bone says, for a posed copy of something worn. Weights are left as they are, as a posed copy doesn't use them.</summary>
        public void Pose(Func<int, Matrix4> matrixOfBone)
        {
            foreach (Vertex vertex in _vertices)
            {
                vertex.position = Vector3.TransformPosition(vertex.position, matrixOfBone((int)vertex.boneIndices.X));
            }
        }

        public void Transform(Matrix4 matrix)
        {
            foreach (Vertex vertex in _vertices) vertex.position = Vector3.TransformPosition(vertex.position, matrix);
            if (matrix.Determinant >= 0f) return;
            for (int i = 0; i < _indices.Count; i += 3) (_indices[i + 1], _indices[i + 2]) = (_indices[i + 2], _indices[i + 1]);
        }

        public GearMesh Copy()
        {
            var copy = new GearMesh();
            copy.Add(this);
            return copy;
        }

        public Mesh Build(bool bones)
        {
            // Vertices are shared with whatever this was built from, and a mesh keeps what it's given.
            var mesh = new Mesh(_vertices.ConvertAll(v => new Vertex(v)), _indices) { isUsingBones = bones };
            mesh.calculateAllNormals();

            foreach ((int a, int b) in _welds)
            {
                MeshVertex first = mesh.meshVertices[a];
                MeshVertex second = mesh.meshVertices[b];
                Vector3 normal = first.normal + second.normal;
                if (normal.LengthSquared < 1e-8f) continue;
                normal.Normalize();
                first.normal = normal;
                second.normal = normal;
            }
            return mesh;
        }
    }
}
