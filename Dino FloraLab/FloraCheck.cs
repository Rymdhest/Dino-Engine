using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_FloraLab
{
    /// <summary>
    /// What holds for every plant of every species at any seed, as a world grows hundreds and nobody looks at them all: that the same seed is the same plant, that a mesh is sound, that it
    /// keeps to what it can spend, that it fits where the world says it does. Every biome's lists are checked too, as a plant nothing grows is a plant that's been forgotten.
    /// </summary>
    internal static class FloraCheck
    {
        // What a plant may spend on triangles. A forest shows a hundred trees and a meadow a few thousand flowers, so these are what the frame rate rests on.
        private static readonly Dictionary<WorldAssetKind, int> MostTriangles = new()
        {
            { WorldAssetKind.Tree, 13000 },
            { WorldAssetKind.Shrub, 2600 },
            { WorldAssetKind.GroundCover, 1400 },
            { WorldAssetKind.Deadwood, 2000 },
            { WorldAssetKind.Crop, 1300 },
        };

        private const float MostMs = 400f;      // A plant that takes this long to grow holds up a world.
        private const float TallestTree = 30f;  // The camera is some 33 m up, and a crown that reaches it is a wall.

        private static int _failures;

        private static void Fail(string message)
        {
            Console.WriteLine($"  {message}");
            _failures++;
        }

        public static int Run(Options options)
        {
            _failures = 0;
            int seeds = options.Int("count", 12);
            var clock = Stopwatch.StartNew();
            long plants = 0, triangles = 0, slowest = 0;
            string slowestName = "";

            foreach (PlantSpecies species in Program.Chosen(options))
            {
                if (species.Asset.Footprint <= 0f) Fail($"{species.Id} has no footprint");
                if (species.Kind == WorldAssetKind.Tree && species.SolidRadius <= 0f) Fail($"{species.Id} is a tree that can be walked through");
                if (species.Variants < 1) Fail($"{species.Id} has no variants");

                for (int i = 0; i < seeds; i++)
                {
                    ulong seed = (ulong)(i * 2654435761L + 17);
                    var timer = Stopwatch.StartNew();
                    FloraMesh mesh = FloraForge.Grow(species, seed);
                    long ms = timer.ElapsedMilliseconds;
                    if (ms > slowest)
                    {
                        slowest = ms;
                        slowestName = species.Id;
                    }
                    if (ms > MostMs) Fail($"{species.Id} took {ms} ms at seed {seed}");
                    Sound(species, mesh, seed);
                    plants++;
                    triangles += mesh.TriangleCount;
                }

                // The same seed is the same plant, on any thread, however many have grown since.
                FloraMesh first = FloraForge.Grow(species, 99);
                FloraMesh second = Task.Run(() => FloraForge.Grow(species, 99)).Result;
                if (first.VertexCount != second.VertexCount || first.Indices.Count != second.Indices.Count) Fail($"{species.Id} isn't the same plant twice");
                else
                {
                    for (int i = 0; i < first.VertexCount; i++)
                    {
                        if (first.Vertices[i].Position == second.Vertices[i].Position && first.Vertices[i].Colour == second.Vertices[i].Colour) continue;
                        Fail($"{species.Id} isn't the same plant twice");
                        break;
                    }
                }
            }

            CheckBiomes();

            Console.WriteLine($"{plants} plants of {FloraCatalog.Species.Count} species and {FloraCatalog.Cards.Count} cards, {triangles / Math.Max(plants, 1)} triangles on average, slowest {slowestName} at {slowest} ms, in {clock.Elapsed.TotalSeconds:0.0} s");
            Console.WriteLine(_failures == 0 ? "Every plant is sound." : $"{_failures} plant problem(s).");
            return _failures == 0 ? 0 : 1;
        }

        private static void Sound(PlantSpecies species, FloraMesh mesh, ulong seed)
        {
            string name = $"{species.Id} at seed {seed}";
            if (mesh.VertexCount == 0 || mesh.TriangleCount == 0)
            {
                Fail($"{name} grew nothing");
                return;
            }
            if (MostTriangles.TryGetValue(species.Kind, out int most) && mesh.TriangleCount > most) Fail($"{name} has {mesh.TriangleCount} triangles, and {species.Kind} may have {most}");

            foreach (int index in mesh.Indices)
            {
                if (index < 0 || index >= mesh.VertexCount)
                {
                    Fail($"{name} has an index off the end");
                    return;
                }
            }

            int cardLayers = FloraCatalog.Cards.Count;
            foreach (FloraVertex vertex in mesh.Vertices)
            {
                if (!Finite(vertex.Position) || !Finite(vertex.Normal) || !Finite(vertex.Tangent) || !Finite(vertex.Colour))
                {
                    Fail($"{name} has a vertex that's not a number");
                    return;
                }
                if (MathF.Abs(vertex.Normal.LengthSquared - 1f) > 0.02f)
                {
                    Fail($"{name} has a normal {vertex.Normal.Length:0.00} long");
                    return;
                }
                if (vertex.Joint >= mesh.Joints.Count)
                {
                    Fail($"{name} has a vertex held by no joint");
                    return;
                }
                if (vertex.Material >= Preview.FirstCard + cardLayers)
                {
                    Fail($"{name} has a card that doesn't exist");
                    return;
                }
                if (vertex.Colour.X > 1.5f || vertex.Colour.Y > 1.5f || vertex.Colour.Z > 1.5f)
                {
                    Fail($"{name} is brighter than any albedo");
                    return;
                }
            }

            for (int i = 0; i < mesh.Joints.Count; i++)
            {
                if (PlantRig.Depth(mesh.Joints, i) > PlantRig.MostLevels) Fail($"{name} has a joint {PlantRig.Depth(mesh.Joints, i)} deep");
                if (mesh.Joints[i].Parent >= i) Fail($"{name} has a joint hanging from one after it");
            }

            mesh.Bounds(out Vector3 min, out Vector3 max);
            if (species.Kind == WorldAssetKind.Tree && max.Y * species.Scale.Max > TallestTree) Fail($"{name} reaches {max.Y * species.Scale.Max:0.0} m, into the camera");
            if (min.Y < -3f) Fail($"{name} goes {-min.Y:0.0} m underground");
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        private static void CheckBiomes()
        {
            var used = new HashSet<string>();
            foreach (BiomeDefinition biome in BiomeCatalog.All)
            {
                foreach (Species[] list in new[] { biome.Canopy, biome.Edge, biome.Shrubs, biome.Deadwood, biome.ForestFloor, biome.OpenGround, biome.Waterside })
                {
                    foreach (Species species in list)
                    {
                        used.Add(species.Asset.Id);
                        if (species.Weight <= 0f) Fail($"{biome.Id} grows {species.Asset.Id} at a weight of {species.Weight}");
                    }
                }
                if (biome.ForestCover > 0.05f && biome.Canopy.Length == 0) Fail($"{biome.Id} has forest and no trees");
            }

            // Crops are placed by the settlements, and a dead pine by the sites.
            foreach (PlantSpecies species in FloraCatalog.Species)
            {
                if (species.Kind == WorldAssetKind.Crop || species.Id == "pine.dead") continue;
                if (!used.Contains(species.Id)) Fail($"{species.Id} grows in no biome");
            }
        }
    }

    /// <summary>
    /// Are plants really different? Variety is measured here, not claimed: two plants of a species are drawn from above and from the side as silhouettes, and how much of them doesn't
    /// overlap is how different they are. The test is that nothing's a clone, and that a species is more like itself than like its neighbours is left to the eye.
    /// </summary>
    internal static class FloraVariety
    {
        private const int Size = 96;
        private const float LeastDifference = 0.12f;    // Of the area two plants cover between them, how much only one covers.

        public static int Run(Options options)
        {
            int seeds = options.Int("count", 6);
            int failures = 0;
            Console.WriteLine($"{"",-26}{"closest",9}{"typical",9}");

            foreach (PlantSpecies species in Program.Chosen(options))
            {
                if (species.Kind is not (WorldAssetKind.Tree or WorldAssetKind.Shrub)) continue;

                var masks = new List<bool[]>();
                for (int i = 0; i < seeds; i++) masks.Add(Silhouette(FloraForge.Grow(species, (ulong)(i * 7919 + 5))));

                float closest = 1f, total = 0f;
                int pairs = 0;
                for (int a = 0; a < masks.Count; a++)
                {
                    for (int b = a + 1; b < masks.Count; b++)
                    {
                        float difference = Difference(masks[a], masks[b]);
                        closest = MathF.Min(closest, difference);
                        total += difference;
                        pairs++;
                    }
                }
                Console.WriteLine($"{species.Id,-26}{closest,9:0%}{total / MathF.Max(pairs, 1),9:0%}");
                if (closest < LeastDifference)
                {
                    Console.WriteLine($"  two {species.Id} are only {closest:0%} apart");
                    failures++;
                }
            }
            Console.WriteLine(failures == 0 ? "No two plants are alike." : $"{failures} species repeat themselves.");
            return failures == 0 ? 0 : 1;
        }

        // Side on and from above, side by side, to one scale: what it fills and not what it's drawn with.
        private static bool[] Silhouette(FloraMesh mesh)
        {
            mesh.Bounds(out Vector3 min, out Vector3 max);
            float reach = MathF.Max(max.Y, MathF.Max(MathF.Abs(min.X), MathF.Max(MathF.Abs(max.X), MathF.Max(MathF.Abs(min.Z), MathF.Abs(max.Z))))) * 1.05f;
            var mask = new bool[Size * Size * 2];
            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Indices[i]].Position, b = mesh.Vertices[mesh.Indices[i + 1]].Position, c = mesh.Vertices[mesh.Indices[i + 2]].Position;
                Fill(mask, 0, new Vector2(a.X, a.Y) / reach, new Vector2(b.X, b.Y) / reach, new Vector2(c.X, c.Y) / reach);
                Fill(mask, Size * Size, new Vector2(a.X, a.Z) / reach, new Vector2(b.X, b.Z) / reach, new Vector2(c.X, c.Z) / reach);
            }
            return mask;
        }

        private static void Fill(bool[] mask, int offset, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 Pixel(Vector2 p) => (p * 0.5f + new Vector2(0.5f)) * Size;
            a = Pixel(a);
            b = Pixel(b);
            c = Pixel(c);
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (MathF.Abs(area) < 1e-5f) return;
            int left = Math.Max((int)MathF.Min(a.X, MathF.Min(b.X, c.X)), 0), right = Math.Min((int)MathF.Max(a.X, MathF.Max(b.X, c.X)) + 1, Size - 1);
            int top = Math.Max((int)MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), 0), bottom = Math.Min((int)MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) + 1, Size - 1);
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    if (w0 >= 0f && w1 >= 0f && w0 + w1 <= 1f) mask[offset + y * Size + x] = true;
                }
            }
        }

        private static float Difference(bool[] a, bool[] b)
        {
            int either = 0, one = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] || b[i]) either++;
                if (a[i] != b[i]) one++;
            }
            return either > 0 ? one / (float)either : 0f;
        }
    }
}
