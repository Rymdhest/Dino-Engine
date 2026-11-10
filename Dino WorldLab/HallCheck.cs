using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Halls;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Builds every hall's models with no GPU and says what they come to: triangles, lights, chimneys and how far they reach, against what a hall may cost. Exit code 1 if any is over.
    /// </summary>
    internal static class HallCheck
    {
        private const int MostShellTriangles = 120_000;
        private const int MostInsideTriangles = 260_000;
        private const int MostLights = 40;

        public static int Run()
        {
            int problems = 0;
            foreach (HallDesign design in HallDesigns.All)
            {
                foreach (bool inside in new[] { false, true })
                {
                    var chimneys = new List<Vector3>();
                    var lights = new List<AssetLight>();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Mesh mesh = HallForge.Build(design, inside, chimneys, lights);
                    watch.Stop();
                    int triangles = mesh.getAllIndicesArray().Length / 3;
                    var box = mesh.createAABB();
                    int lit = lights.Count(l => l.Intensity > 0f);
                    int flames = lights.Count(l => l.FlameWidth > 0f);
                    string part = inside ? "inside" : "shell";
                    Console.WriteLine($"{design.Name,-18} {part,-7} {triangles,7} triangles, {lit,2} lights, {flames,3} flames, {chimneys.Count} chimneys, {watch.ElapsedMilliseconds,4} ms, box {Fmt(box.Min)} to {Fmt(box.Max)}");
                    int most = inside ? MostInsideTriangles : MostShellTriangles;
                    if (triangles > most) { Console.WriteLine($"   PROBLEM: over {most} triangles"); problems++; }
                    if (lit > MostLights) { Console.WriteLine($"   PROBLEM: over {MostLights} lights"); problems++; }
                    // What flies keeps over the stated top, so it has to be true: nothing above it, and not so far short that birds climb for nothing.
                    if (box.Max.Y > design.Top) { Console.WriteLine($"   PROBLEM: rises to {box.Max.Y:0.0} m, over its Top of {design.Top:0.0}"); problems++; }
                    if (!inside && box.Max.Y < design.Top - 3f) { Console.WriteLine($"   PROBLEM: rises only to {box.Max.Y:0.0} m, well short of its Top of {design.Top:0.0}"); problems++; }
                }
            }
            return problems > 0 ? 1 : 0;
        }

        private static string Fmt(Vector3 v) => $"({v.X:0.0}, {v.Y:0.0}, {v.Z:0.0})";
    }
}
