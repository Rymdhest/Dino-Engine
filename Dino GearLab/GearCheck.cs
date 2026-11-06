using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>Whether gear holds up: every kind, across everything a spec can say, for a great many seeds. Exit code 1 if anything is wrong.</summary>
    internal static class GearCheck
    {
        private const int MostTriangles = 9000;
        private const float BiggestPiece = 8f;

        /// <summary>A spec from anywhere in the space of them, as what a game sends can be any mix of these.</summary>
        public static GearSpec Sample(GearKind kind, ulong seed, int index)
        {
            var random = new WorldRandom(seed).Fork("sample").Fork(index);
            float a = random.Float(), b = random.Float() * (1f - a);
            var form = new GearForm(random.Float(), random.Float(), random.Float(), random.Float(), random.Float(), random.Float());
            return new GearSpec(seed, kind, a, b, 1f - a - b, form, random.Int(5), (GearTheme)random.Int(6));
        }

        public static int Run(Options options)
        {
            int count = options.Int("count", 3000);
            int failures = 0;
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                if (options.Has("kind") && !string.Equals(options.Text("kind", ""), kind.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!GearForge.CanMake(kind)) continue;
                failures += CheckKind(kind, count);
            }

            Console.WriteLine(failures == 0 ? "All passed" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int CheckKind(GearKind kind, int count)
        {
            int failed = 0;
            int largest = 0;
            long triangles = 0;
            float biggest = 0f;
            string? first = null;

            void Fail(GearSpec spec, string why)
            {
                failed++;
                first ??= $"seed {spec.Seed}: {why}";
            }

            for (int i = 0; i < count; i++)
            {
                GearSpec spec = Sample(kind, (ulong)i * 2654435761UL + 1, i);
                GearMesh made = GearForge.Make(spec);
                Mesh mesh = made.Build(false);

                largest = Math.Max(largest, made.TriangleCount);
                triangles += made.TriangleCount;
                if (made.TriangleCount == 0)
                {
                    Fail(spec, "nothing was made");
                    continue;
                }
                if (made.TriangleCount > MostTriangles) Fail(spec, $"{made.TriangleCount} triangles");

                if (!Same(GearForge.Make(spec), made)) Fail(spec, "the same spec made something else");

                Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                foreach (MeshVertex vertex in mesh.meshVertices)
                {
                    if (!Finite(vertex.position) || !Finite(vertex.normal)) Fail(spec, "a number that isn't one");
                    if (Finite(vertex.normal) && MathF.Abs(vertex.normal.LengthSquared - 1f) > 0.01f) Fail(spec, $"a normal of length {vertex.normal.Length}");
                    if (vertex.colour.Red < 0f || vertex.colour.Green < 0f || vertex.colour.Blue < 0f) Fail(spec, "a colour below nothing");
                    min = Vector3.ComponentMin(min, vertex.position);
                    max = Vector3.ComponentMax(max, vertex.position);
                }

                float size = (max - min).Length;
                biggest = MathF.Max(biggest, size);
                if (size > BiggestPiece) Fail(spec, $"{size} m across");

                foreach (Face face in mesh.faces)
                {
                    Vector3 cross = Vector3.Cross(face.B.position - face.A.position, face.C.position - face.A.position);
                    if (cross.LengthSquared < 1e-14f) Fail(spec, "a triangle with no area");
                }
            }

            Console.WriteLine($"  {(failed == 0 ? "ok  " : "FAIL")}  {kind,-10} {count} made, {triangles / count} triangles on average, {largest} at most, biggest {biggest:0.0} m{(first != null ? $". {failed} faults, first: {first}" : "")}");
            return failed == 0 ? 0 : 1;
        }

        private static bool Same(GearMesh a, GearMesh b)
        {
            if (a.VertexCount != b.VertexCount || a.TriangleCount != b.TriangleCount) return false;
            for (int i = 0; i < a.VertexCount; i++)
            {
                Vertex x = a.Vertices[i], y = b.Vertices[i];
                if (x.position != y.position || x.materialTextureIndex != y.materialTextureIndex || x.colour.ToVector3() != y.colour.ToVector3()) return false;
            }
            return true;
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    }
}
