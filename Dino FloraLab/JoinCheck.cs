using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Holds what the skin promises of every place a limb leaves what it grew from (FLORA.md, Skin): that its foot is on its parent or under it, never standing clear of it, which
    /// is a gap someone can see through. It's measured on the mesh as it's drawn, each vertex of a foot against the parent's own triangles, and not by anything the skin worked out
    /// on the way. It also says how many feet found a face to end on and how many were set on the parent's smooth shape instead, and draws the thickest joins of a plant close up.
    ///   joins [--plants a,b] [--count N]                          Every join of every plant with wood, at N seeds. Exit code 1 if a foot stands clear.
    ///         [--out file.png] [--show N] [--tile px] [--seed N]  And the N thickest joins of each plant named, from two sides.
    /// </summary>
    internal static class JoinCheck
    {
        private const float MostProud = 0.015f;     // Metres a foot may stand clear of its parent's faces: less than the relief of any bark.
        private const float Buried = 0.02f;         // Metres up: below this a foot is in the ground.

        public static int Run(Options options)
        {
            int seeds = options.Int("count", 6);
            int failures = 0;
            long joins = 0, feet = 0;
            int kindCount = Enum.GetValues<FootKind>().Length;
            var kinds = new long[kindCount];
            var proudest = new float[kindCount];
            Array.Fill(proudest, float.MinValue);
            var gaps = new List<(FloraMesh mesh, FloraJoin join)>();
            float worstOfAll = 0f;
            string worstName = "";
            FloraJoins.Reset();
            Console.WriteLine($"{"",-26}{"joins",8}{"collars",9}{"proudest mm",13}");

            foreach (PlantSpecies species in Program.Chosen(options))
            {
                int count = 0, collars = 0;
                float worst = float.MinValue;
                for (int i = 0; i < seeds; i++)
                {
                    ulong seed = (ulong)(i * 2654435761L + 17);
                    FloraMesh mesh = Grow(species, seed, out List<FloraJoin> made);
                    foreach (FloraJoin join in made)
                    {
                        count++;
                        if (join.Lands) collars++;
                        if (join.Faces.Length == 0) continue;
                        bool failed = false;
                        for (int v = 0; v < join.Vertices; v++)
                        {
                            Vector3 at = mesh.Vertices[join.FirstVertex + v].Position;
                            // What's under the ground is under the ground: a root's foot below the end of the trunk's skin is nobody's gap.
                            if (at.Y < Buried) continue;
                            feet++;
                            FootKind kind = join.Kinds[v];
                            float proud = Proud(mesh, join, at);
                            kinds[(int)kind]++;
                            proudest[(int)kind] = MathF.Max(proudest[(int)kind], proud);
                            worst = MathF.Max(worst, proud);
                            if (proud <= MostProud || failed) continue;
                            Console.WriteLine($"  {species.Id} at seed {seed}: a foot of a limb {join.Radius * 100f:0.0} cm thick on wood {join.HostRadius * 100f:0.0} cm thick stands {proud * 1000f:0} mm clear of it, {at.Y:0.0} m up ({kind})");
                            if (options.Has("why")) Console.WriteLine($"    foot {at.X:0.000},{at.Y:0.000},{at.Z:0.000}; the limb comes out at {join.At.X:0.000},{join.At.Y:0.000},{join.At.Z:0.000} going {join.Out.X:0.00},{join.Out.Y:0.00},{join.Out.Z:0.00}; {join.Faces.Sum(f => f.Count) / 3} faces looked at; kinds {string.Join(",", join.Kinds)}");
                            failures++;
                            failed = true;
                            if (gaps.Count < 8) gaps.Add((mesh, join));
                        }
                    }
                }
                if (count == 0) continue;
                joins += count;
                Console.WriteLine($"{species.Id,-26}{count,8}{collars,9}{worst * 1000f,13:0.0}");
                if (worst > worstOfAll)
                {
                    worstOfAll = worst;
                    worstName = species.Id;
                }
            }

            Console.WriteLine($"{joins} joins, {feet} vertices of feet above the ground:");
            foreach (FootKind kind in Enum.GetValues<FootKind>())
            {
                if (kinds[(int)kind] > 0) Console.WriteLine($"  {kind,-8}{kinds[(int)kind],8} ({100f * kinds[(int)kind] / MathF.Max(feet, 1),5:0.0}%), the proudest {proudest[(int)kind] * 1000f,6:0.0} mm clear");
            }
            Console.WriteLine($"The proudest foot stands {worstOfAll * 1000f:0.0} mm clear, on {worstName}; {MostProud * 1000f:0} mm is allowed.");
            Console.WriteLine(failures == 0 ? "Every limb meets what it grew from." : $"{failures} join(s) with a gap.");

            if (options.Has("out")) Draw(options, gaps);
            return failures == 0 ? 0 : 1;
        }

        private static FloraMesh Grow(PlantSpecies species, ulong seed, out List<FloraJoin> joins)
        {
            joins = new List<FloraJoin>();
            FloraJoins.Watch = joins;
            try
            {
                return FloraForge.Grow(species, seed);
            }
            finally
            {
                FloraJoins.Watch = null;
            }
        }

        // How far a point stands outside the nearest of the parent's faces, in metres: negative under them. A point outside the nearest face can still be under another, as a foot
        // set against a limb is under the swell of that limb's own collar, which is what's drawn there: so one that seems to stand clear is looked at from outside, along the way
        // out of the face it's nearest, and if a face lies over it, it's under that.
        private static float Proud(FloraMesh mesh, FloraJoin join, Vector3 point)
        {
            float nearest = float.MaxValue, signed = 0f;
            Vector3 outward = Vector3.UnitY;
            foreach ((int first, int count) in join.Faces)
            {
                for (int i = first; i < first + count; i += 3)
                {
                    Vector3 a = mesh.Vertices[mesh.Indices[i]].Position, b = mesh.Vertices[mesh.Indices[i + 1]].Position, c = mesh.Vertices[mesh.Indices[i + 2]].Position;
                    Vector3 on = Closest(point, a, b, c);
                    float distance = (point - on).LengthSquared;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    signed = Vector3.Dot(point - on, normal) >= 0f ? MathF.Sqrt(distance) : -MathF.Sqrt(distance);
                    if (normal.LengthSquared > 1e-12f) outward = normal.Normalized();
                }
            }
            if (nearest == float.MaxValue) return 0f;
            if (signed > 0f && Covered(mesh, join, point, outward)) return -signed;
            return signed;
        }

        // Whether any of the parent's faces lies over a point, looking at it from the way given.
        private static bool Covered(FloraMesh mesh, FloraJoin join, Vector3 point, Vector3 from)
        {
            foreach ((int first, int count) in join.Faces)
            {
                for (int i = first; i < first + count; i += 3)
                {
                    Vector3 a = mesh.Vertices[mesh.Indices[i]].Position;
                    Vector3 ab = mesh.Vertices[mesh.Indices[i + 1]].Position - a, ac = mesh.Vertices[mesh.Indices[i + 2]].Position - a;
                    Vector3 p = Vector3.Cross(from, ac);
                    float det = Vector3.Dot(ab, p);
                    if (MathF.Abs(det) < 1e-12f) continue;
                    float inverse = 1f / det;
                    Vector3 to = point - a;
                    float u = Vector3.Dot(to, p) * inverse;
                    if (u < 0f || u > 1f) continue;
                    Vector3 q = Vector3.Cross(to, ab);
                    float w = Vector3.Dot(from, q) * inverse;
                    if (w < 0f || u + w > 1f) continue;
                    if (Vector3.Dot(ac, q) * inverse > 1e-4f) return true;
                }
            }
            return false;
        }

        // The point of a triangle nearest a point (Ericson, Real-Time Collision Detection, 5.1.5).
        private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denominator = 1f / (va + vb + vc);
            return a + ab * (vb * denominator) + ac * (vc * denominator);
        }

        // The thickest joins of each plant named, each from two sides, a row to a join.
        private static void Draw(Options options, List<(FloraMesh mesh, FloraJoin join)> gaps)
        {
            BakedCards cards = CardLibrary.Bake();
            List<PlantSpecies> species = Program.Chosen(options);
            int show = options.Int("show", 6);
            int tile = options.Int("tile", 520);
            int seed = options.Int("seed", 1);
            const int Shrink = 2, Views = 2;
            float pitch = MathHelper.DegreesToRadians(options.Float("angle", 12f));
            bool marks = options.Text("marks", gaps.Count > 0 ? "true" : "false") == "true";

            // Those with a gap, if there are any, as they're what's to be looked at; otherwise the thickest of each plant named.
            var jobs = new List<(FloraMesh mesh, FloraJoin join)>(gaps);
            foreach (PlantSpecies plant in gaps.Count > 0 ? new List<PlantSpecies>() : species)
            {
                FloraMesh mesh = Grow(plant, (ulong)seed, out List<FloraJoin> made);
                foreach (FloraJoin join in made.Where(j => j.Lands).OrderByDescending(j => j.Radius).Take(show)) jobs.Add((mesh, join));
            }
            if (jobs.Count == 0)
            {
                Console.WriteLine("Nothing named has a limb thick enough for a collar.");
                return;
            }

            var sheet = new byte[Views * tile * jobs.Count * tile * 4];
            Parallel.For(0, jobs.Count, i =>
            {
                (FloraMesh mesh, FloraJoin join) = jobs[i];
                var preview = new Preview(tile * Shrink, tile * Shrink, cards) { GrainFloor = 0.62f, GrainDepth = 0.45f, WoodOnly = true };
                // From either side of the limb, so that its upper and lower sides are both in outline.
                float facing = MathF.Atan2(join.Out.X, join.Out.Z);
                float span = MathF.Max(MathF.Max(join.Radius * 9f, join.HostRadius * 4.5f), 0.7f);
                for (int view = 0; view < Views; view++)
                {
                    preview.Draw(mesh, pitch, facing + (view == 0 ? 1.25f : -1.9f), span, join.At);
                    if (marks)
                    {
                        // Its foot, a dot to a vertex: red where no face was found for it, yellow where it was brought in or already under, and blue where it's on one.
                        for (int v = 0; v < join.Vertices; v++)
                        {
                            FootKind kind = join.Kinds[v];
                            Vector3 colour = kind == FootKind.Set ? new Vector3(1f, 0.1f, 0.1f) : kind is FootKind.Brought or FootKind.Sunk ? new Vector3(1f, 0.9f, 0.1f) : new Vector3(0.2f, 0.5f, 1f);
                            preview.Mark(mesh.Vertices[join.FirstVertex + v].Position, colour);
                        }
                        preview.Mark(join.At, new Vector3(1f, 1f, 1f), 2);
                    }
                    preview.Blit(sheet, Views * tile, view * tile, i * tile, Shrink);
                }
            });

            string path = options.Text("out", "joins.png");
            PngWriter.Write(path, sheet, Views * tile, jobs.Count * tile);
            Console.WriteLine($"Wrote {path}");
        }
    }
}
