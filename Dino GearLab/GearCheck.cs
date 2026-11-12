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
        private const float BiggestPiece = 9f;

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
            bool costs = options.Text("costs", "false") == "true";
            int failures = 0;
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                if (options.Has("kind") && !string.Equals(options.Text("kind", ""), kind.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                failures += CheckKind(kind, count, costs);
            }
            failures += CheckOpening(count);

            Console.WriteLine(failures == 0 ? "All passed" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int CheckKind(GearKind kind, int count, bool showCosts)
        {
            int failed = 0;
            int largest = 0;
            long triangles = 0;
            float biggest = 0f;
            string? first = null;
            var costs = new Dictionary<string, int>();
            var made_ = new Dictionary<string, int>();
            GearSpec? dearest = null;

            void Fail(GearSpec spec, string why)
            {
                failed++;
                first ??= $"seed {spec.Seed}: {why}";
            }

            for (int i = 0; i < count; i++)
            {
                GearSpec spec = Sample(kind, (ulong)i * 2654435761UL + 1, i);
                var parts = new Dictionary<string, int>();
                GearMesh made = GearForge.Make(spec, parts);
                Mesh mesh = made.Build(false);
                foreach ((string part, int cost) in parts)
                {
                    costs[part] = costs.GetValueOrDefault(part) + cost;
                    made_[part] = made_.GetValueOrDefault(part) + 1;
                }

                if (made.TriangleCount > largest) dearest = spec;
                largest = Math.Max(largest, made.TriangleCount);
                triangles += made.TriangleCount;
                if (made.TriangleCount == 0)
                {
                    Fail(spec, "nothing was made");
                    continue;
                }
                if (made.TriangleCount > MostTriangles) Fail(spec, $"{made.TriangleCount} triangles");

                for (int v = 0; v < made.VertexCount; v++)
                {
                    if (!Finite(made.Vertices[v].position)) Fail(spec, $"a position that isn't one, in {GearFitCheck.PartOf(made, v)}");
                }
                if (!Same(GearForge.Make(spec), made)) Fail(spec, "the same spec made something else");

                Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                foreach (MeshVertex vertex in mesh.meshVertices)
                {
                    if (!Finite(vertex.position)) Fail(spec, $"a position that isn't one, at vertex {vertex.index.index} of {mesh.meshVertices.Count}");
                    if (!Finite(vertex.normal)) Fail(spec, $"a normal that isn't one, at vertex {vertex.index.index} of {mesh.meshVertices.Count}, at {vertex.position}, with {vertex.faces.Count} faces");
                    if (Finite(vertex.normal) && MathF.Abs(vertex.normal.LengthSquared - 1f) > 0.01f) Fail(spec, $"a normal of length {vertex.normal.Length}");
                    if (!Finite(vertex.tangent) || !Finite(vertex.bitangent)) Fail(spec, $"a tangent that isn't one, at vertex {vertex.index.index} of {mesh.meshVertices.Count}, at {vertex.position}");
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
                    if (cross.LengthSquared < 1e-14f)
                    {
                        int at = -1;
                        for (int v = 0; v < made.VertexCount && at < 0; v++) if (made.Vertices[v].position == face.A.position) at = v;
                        Fail(spec, $"a triangle with no area, in {(at >= 0 ? GearFitCheck.PartOf(made, at) : "something")}, at {face.A.position}, {face.B.position}, {face.C.position}");
                    }
                }
            }

            Console.WriteLine($"  {(failed == 0 ? "ok  " : "FAIL")}  {kind,-10} {count} made, {triangles / count} triangles on average, {largest} at most, biggest {biggest:0.0} m{(first != null ? $". {failed} faults, first: {first}" : "")}");
            // What each named part costs, when it's over its budget or asked, as finding what to make cheaper is the whole of fixing it.
            if (costs.Count > 0 && (showCosts || largest > MostTriangles))
            {
                Console.WriteLine("          what its parts cost, on average where there's one: " + string.Join(", ", costs.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value / made_[c.Key]} ({made_[c.Key] * 100 / count}%)")));
                if (dearest is GearSpec worst)
                {
                    var parts = new Dictionary<string, int>();
                    GearForge.Make(worst, parts);
                    Console.WriteLine($"          the dearest, seed {worst.Seed}: " + string.Join(", ", parts.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value}")));
                }
            }
            return failed == 0 ? 0 : 1;
        }

        // A book being read has to be that book and not a stranger's, which comes down to its boards being the same size. Each of an open book's is as wide as a shut one, give or take what's on the spine and the clasp.
        private static int CheckOpening(int count)
        {
            const float Allowed = 0.35f;
            int strangers = 0;
            for (int i = 0; i < count; i++)
            {
                GearSpec shut = Sample(GearKind.Book, (ulong)i * 2654435761UL + 1, i);
                if (MathF.Abs(Across(GearForge.Make(shut with { Kind = GearKind.OpenBook })) - Across(GearForge.Make(shut)) * 2f) > Allowed) strangers++;
            }
            Console.WriteLine($"  {(strangers == 0 ? "ok  " : "FAIL")}  Opened     {count} opened, {strangers} came out a different book");
            return strangers == 0 ? 0 : 1;
        }

        private static float Across(GearMesh mesh)
        {
            float least = float.MaxValue, most = float.MinValue;
            foreach (Vertex vertex in mesh.Vertices)
            {
                least = MathF.Min(least, vertex.position.X);
                most = MathF.Max(most, vertex.position.X);
            }
            return most - least;
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
