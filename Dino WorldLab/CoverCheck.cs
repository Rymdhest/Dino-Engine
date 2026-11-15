using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows a world's ground cover as the game streams it in, a chunk at a time, and holds every card that lies on the ground (a GroundForm: leaf litter, moss and the cushions)
    /// to lying on the ground as it's drawn and being seen there. It's measured on the model as TerrainChunkSystem spawns it, against the ground as Terrain.vert draws it: the
    /// cubic height map with TerrainRenderer.SurfaceOffset over it, which the relief only ever takes deeper. Laid on the height map, as every plant was, 97% of the cards' area was
    /// under the drawn ground, and on a slope a card stood out of the hillside as a level shelf, by as much as 1.8 m: this fails both. Then says where cards lie thickest, for --at.
    /// </summary>
    internal static class CoverCheck
    {
        private const float Chunk = 8f;                 // What the game streams ground cover in, a terrain chunk.
        private const int Steps = 8;                    // Each triangle is read at Steps² points, which share its area.
        private const float MostStandOff = 0.15f;      // Metres a card's underside may stand over the drawn ground anywhere: GroundCards lays none over ground that strays 12 cm from its plane.
        private const float LeastShown = 0.05f;        // The least share of any one card's area that's over the drawn ground: below it, it's buried.
        private const float LeastShownLand = 0.4f;     // ...and of all of a land's cards together, which was 3% when they were laid on the height map.

        private sealed class Kind
        {
            public int Cards;
            public double Area, Shown;
        }

        public static int Run(int seed, int count, string? archetype)
        {
            int failures = 0;
            for (int i = 0; i < count; i++) failures += One(seed + i, archetype);
            Console.WriteLine(failures == 0 ? "Every card lies on the ground and is seen." : $"{failures} ground card problem(s).");
            return failures == 0 ? 0 : 1;
        }

        private static int One(int seed, string? archetype)
        {
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            var cover = new WorldGroundCover(world);
            var plants = new List<GroundPlant>();
            var meshes = new Dictionary<string, FloraMesh?>();
            var kinds = new SortedDictionary<string, Kind>(StringComparer.Ordinal);
            int n = (int)(world.Size / Chunk);
            var map = new int[n, n];
            int failures = 0;
            float worstStandOff = float.MinValue, deepest = float.MaxValue, leastShown = float.MaxValue;
            double area = 0, shown = 0;

            void Fail(string message)
            {
                if (failures < 10) Console.WriteLine($"  seed {seed}: {message}");
                failures++;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    cover.Generate(new Vector2(i * Chunk, j * Chunk), Chunk, plants);
                    foreach (GroundPlant plant in plants)
                    {
                        if (!meshes.TryGetValue(plant.Model, out FloraMesh? mesh))
                        {
                            string id = NatureCatalog.SpeciesOf(plant.Model, out ulong modelSeed);
                            mesh = FloraCatalog.TryGet(id, out PlantSpecies species) && species.Form is GroundForm ? FloraForge.Grow(species, modelSeed) : null;
                            meshes[plant.Model] = mesh;
                        }
                        if (mesh == null) continue;

                        string name = NatureCatalog.SpeciesOf(plant.Model, out _);
                        if (!kinds.TryGetValue(name, out Kind? kind)) kinds[name] = kind = new Kind();
                        (double cardArea, double cardShown, float standOff, float sunk) = Measure(world, mesh, plant);
                        kind.Cards++;
                        kind.Area += cardArea;
                        kind.Shown += cardShown;
                        area += cardArea;
                        shown += cardShown;
                        map[i, j]++;
                        worstStandOff = MathF.Max(worstStandOff, standOff);
                        deepest = MathF.Min(deepest, sunk);
                        float share = (float)(cardShown / Math.Max(cardArea, 1e-9));
                        leastShown = MathF.Min(leastShown, share);

                        var at = new Vector2(plant.Position.X, plant.Position.Z);
                        if (standOff > MostStandOff) Fail($"{plant.Model} at {at.X:0},{at.Y:0} stands {standOff:0.00} m off the ground as it's drawn");
                        if (share < LeastShown) Fail($"{plant.Model} at {at.X:0},{at.Y:0} is buried: {share * 100f:0}% of it is over the ground as it's drawn");
                    }
                }
            }

            int cards = kinds.Values.Sum(k => k.Cards);
            if (cards == 0)
            {
                Console.WriteLine($"seed {seed} ({world.Plan.Archetype.Id}): no cards lie on its ground.");
                return failures;
            }
            double landShown = shown / area;
            if (landShown < LeastShownLand) Fail($"only {landShown * 100.0:0}% of its cards' area is over the ground as it's drawn");
            Console.WriteLine($"seed {seed} ({world.Plan.Archetype.Id}): {cards} cards, {landShown * 100.0:0}% of their area over the drawn ground (the least of any one {leastShown * 100f:0}%), "
                + $"undersides at most {worstStandOff * 100f:0} cm over it and {-deepest * 100f:0} cm under it; "
                + string.Join(", ", kinds.OrderByDescending(k => k.Value.Cards).Select(k => $"{k.Key} {k.Value.Cards} ({k.Value.Shown / k.Value.Area * 100.0:0}%)")));
            if (Busiest(map, n) is (int most, Vector2 view) && most > 0) Console.WriteLine($"  most cards: {most} in a view at --at {view.X:0},{view.Y:0}");
            return failures;
        }

        // A card's area seen from above, how much of that is over the drawn ground, and how far its underside (the plane of its rim) stands over the drawn ground at most and
        // goes under it at most. Read at Steps² points a triangle, both of the card and of its underside below them, through the matrix the game draws it with.
        private static (double area, double shown, float standOff, float sunk) Measure(WorldData world, FloraMesh mesh, GroundPlant plant)
        {
            Matrix4 matrix = MyMath.createTransformationMatrix(plant.Position, GroundCards.Rotation(plant.Yaw, plant.Up), new Vector3(plant.Scale));
            float lowest = float.MaxValue;
            foreach (FloraVertex vertex in mesh.Vertices) lowest = MathF.Min(lowest, vertex.Position.Y);

            double area = 0, shown = 0;
            float standOff = float.MinValue, sunk = float.MaxValue;
            for (int t = 0; t + 2 < mesh.Indices.Count; t += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Indices[t]].Position, b = mesh.Vertices[mesh.Indices[t + 1]].Position, c = mesh.Vertices[mesh.Indices[t + 2]].Position;
                Vector3 wa = Vector3.TransformPosition(a, matrix), wb = Vector3.TransformPosition(b, matrix), wc = Vector3.TransformPosition(c, matrix);
                double each = 0.5 * Math.Abs((wb.X - wa.X) * (wc.Z - wa.Z) - (wc.X - wa.X) * (wb.Z - wa.Z)) / (Steps * Steps);
                for (int u = 0; u < Steps; u++)
                {
                    for (int w = 0; w < Steps - u; w++)
                    {
                        Read(u + 1f / 3f, w + 1f / 3f);
                        if (u + w < Steps - 1) Read(u + 2f / 3f, w + 2f / 3f);
                    }
                }

                void Read(float bu, float bw)
                {
                    Vector3 local = a + (b - a) * (bu / Steps) + (c - a) * (bw / Steps);
                    Vector3 p = Vector3.TransformPosition(local, matrix);
                    Vector3 under = Vector3.TransformPosition(new Vector3(local.X, lowest, local.Z), matrix);
                    area += each;
                    if (p.Y > Drawn(world, p.X, p.Z)) shown += each;
                    float off = under.Y - Drawn(world, under.X, under.Z);
                    standOff = MathF.Max(standOff, off);
                    sunk = MathF.Min(sunk, off);
                }
            }
            return (area, shown, standOff, sunk);
        }

        // The ground as Terrain.vert draws it: the cubic height map at every vertex of its mesh (TerrainGenerator.getHeightAt), SurfaceOffset over it.
        private static float Drawn(WorldData world, float x, float z) => world.Height.SampleCubic(x, z) + TerrainRenderer.SurfaceOffset;

        // The busiest view, 48 by 24 m as the game's camera sees, in whole chunks.
        private static (int, Vector2)? Busiest(int[,] map, int n)
        {
            int best = -1;
            Vector2 at = default;
            for (int i = 0; i + 6 <= n; i++)
            {
                for (int j = 0; j + 3 <= n; j++)
                {
                    int c = 0;
                    for (int a = 0; a < 6; a++) for (int b = 0; b < 3; b++) c += map[i + a, j + b];
                    if (c <= best) continue;
                    best = c;
                    at = new Vector2((i + 3) * Chunk, (j + 1.5f) * Chunk);
                }
            }
            return best < 0 ? null : (best, at);
        }
    }
}
