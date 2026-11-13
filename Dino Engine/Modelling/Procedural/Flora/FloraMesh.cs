using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>One point of a plant, just as it goes to the GPU.</summary>
    public struct FloraVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Vector3 Colour;
        public Vector2 UV;
        public int Material;
        public int Joint;

        /// <summary>How much of the plant's growth stands here, 0 to 1: moss on bark (<see cref="FloraMesh.ShellStyle"/>, SHELLS.md).</summary>
        public float Shell;

        /// <summary>
        /// Which way what grows here is pushed out, as long as it has to be to keep the same depth over every face it's a corner of, or zero for <see cref="Normal"/>.
        /// A mesh with its normals split at hard edges needs it, or every crease opens a crack through its moss.
        /// </summary>
        public Vector3 ShellNormal;
    }

    /// <summary>
    /// A plant as it comes out of the forge: plain lists and nothing else. A world wants hundreds of these, grown on worker threads, and a <see cref="Mesh"/> is an object
    /// for every vertex and every face, with its normals worked out from those faces. A plant knows its normals already, and a card's are nothing to do with its faces.
    /// </summary>
    public sealed class FloraMesh
    {
        public readonly List<FloraVertex> Vertices = new();
        public readonly List<int> Indices = new();
        public readonly List<PlantJoint> Joints = new();

        /// <summary>What grows on it as shells (<see cref="Rendering.Shells.ShellStyles"/>), 0 for nothing; each vertex's <see cref="FloraVertex.Shell"/> says how much.</summary>
        public byte ShellStyle;

        /// <summary>
        /// For moss that grows by Rock.glsl's rule, on what faces the sky (<see cref="Rendering.Shells.ShellMask.Rock"/>): how far up, in its own frame, a corner
        /// must face for its triangle to be drawn as shells at all. The rock's skin stands in the world as it was made and grows nothing under 0.25; a stone may be
        /// tipped where it's put, so it keeps a margin.
        /// </summary>
        public float ShellFacing = -1f;

        public int VertexCount => Vertices.Count;
        public int TriangleCount => Indices.Count / 3;

        public int Add(in FloraVertex vertex)
        {
            Vertices.Add(vertex);
            return Vertices.Count - 1;
        }

        /// <summary>Faces go counter-clockwise seen from outside.</summary>
        public void Triangle(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        public int AddJoint(PlantJoint joint)
        {
            Joints.Add(joint);
            return Joints.Count - 1;
        }

        /// <summary>Stills a plant, for what's built like one but doesn't move.</summary>
        public void Still()
        {
            Joints.Clear();
            for (int i = 0; i < Vertices.Count; i++)
            {
                FloraVertex vertex = Vertices[i];
                vertex.Joint = -1;
                Vertices[i] = vertex;
            }
        }

        /// <summary>Everything from <paramref name="firstVertex"/> on, moved. Joints are moved by whoever made them, as a part doesn't know which are its own.</summary>
        public void Move(int firstVertex, Matrix4 transform)
        {
            Matrix3 turn = new Matrix3(transform);
            for (int i = firstVertex; i < Vertices.Count; i++)
            {
                FloraVertex vertex = Vertices[i];
                vertex.Position = Vector3.TransformPosition(vertex.Position, transform);
                vertex.Normal = (vertex.Normal * turn).Normalized();
                vertex.Tangent = (vertex.Tangent * turn).Normalized();
                Vertices[i] = vertex;
            }
        }

        public void Bounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            foreach (FloraVertex vertex in Vertices)
            {
                min = Vector3.ComponentMin(min, vertex.Position);
                max = Vector3.ComponentMax(max, vertex.Position);
            }
        }

        /// <summary>The same plant as a <see cref="Mesh"/>, for whatever measures one. What's drawn never goes through this.</summary>
        public Mesh ToMesh()
        {
            var vertices = new List<Vertex>(Vertices.Count);
            foreach (FloraVertex vertex in Vertices)
            {
                vertices.Add(new Vertex(vertex.Position, new VertexMaterial(vertex.Material, new Colour(vertex.Colour)), vertex.UV) { joint = vertex.Joint, Shell = vertex.Shell });
            }

            var mesh = new Mesh(vertices, Indices) { joints = Joints.Count > 0 ? new List<PlantJoint>(Joints) : null, ShellStyle = ShellStyle };
            for (int i = 0; i < Vertices.Count; i++)
            {
                mesh.meshVertices[i].normal = Vertices[i].Normal;
                mesh.meshVertices[i].tangent = Vertices[i].Tangent;
                mesh.meshVertices[i].bitangent = Vector3.Cross(Vertices[i].Tangent, Vertices[i].Normal);
            }
            mesh.finishedNormals = true;
            return mesh;
        }
    }
}
