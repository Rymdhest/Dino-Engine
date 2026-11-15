using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows a world's sea floor as the game streams it in (SEA.md) and holds it to its rules, measured on the models themselves and not on what the generator meant: nothing standing up
    /// off the bed where anyone could wade to it, nothing rigid out of the water at the trough of an ordinary wave and nothing that sways out of the still water, lilies only on still
    /// fresh water and floating on it, a giant kelp's crown lying on the surface and not standing up out of it. Then says where each kind grows thickest, for --at x,z.
    /// </summary>
    internal static class SeaCheck
    {
        private const float Chunk = 8f;               // What the game streams ground cover in, a terrain chunk.
        private const float NearBody = 1.1f;           // A body's radius: rock standing up off the bed keeps this far from anywhere anyone stands, as nothing of it is solid.
        private const float MostOverSurface = 0.25f;  // Metres a giant kelp's crown, lying out on the water, may rise over it at the most.

        private static readonly (string name, string[] prefixes)[] Families =
        {
            ("reef", new[] { "coral.", "sponge." }),
            ("kelp", new[] { "kelp." }),
            ("meadow", new[] { "seagrass.", "pondweed" }),
            ("wash", new[] { "weed." }),
            ("lilies", new[] { "lily." }),
            ("critters", new[] { "urchin.", "star." }),
            ("rock", new[] { "rock." }),
        };

        public static int Run(int seed, int count, string? archetype)
        {
            int failures = 0;
            for (int i = 0; i < count; i++) failures += One(seed + i, archetype);
            Console.WriteLine(failures == 0 ? "Every sea floor holds." : $"{failures} sea floor problem(s).");
            return failures == 0 ? 0 : 1;
        }

        private static int One(int seed, string? archetype)
        {
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            var cover = new WorldGroundCover(world);
            var plants = new List<GroundPlant>();
            var shapes = new Dictionary<string, (float top, float reach)>();
            var species = new Dictionary<string, int>();
            int n = (int)(world.Size / Chunk);
            var maps = Families.ToDictionary(f => f.name, _ => new int[n, n]);
            bool still = world.Plan.Archetype.StillWater;
            int failures = 0;
            float worstKelp = 0f;

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
                        var xz = new Vector2(plant.Position.X, plant.Position.Z);
                        string name = NatureCatalog.SpeciesOf(plant.Model, out ulong modelSeed);
                        bool isRock = NatureCatalog.TryGetRock(name, out RockKind kind, out RockForm form);
                        bool isSea = FloraCatalog.TryGet(name, out PlantSpecies kindOfPlant) && (kindOfPlant.Form is IUnderWater || kindOfPlant.Form is LilyForm);
                        float level = world.WaterLevelAt(xz);
                        float depth = level - world.Height.Sample(xz);
                        // A pebble rolled up the beach is the shore's, not the sea's; rock standing in the water is a formation.
                        if (!isSea && !(isRock && form != RockForm.Pebble && depth > 0f)) continue;

                        if (!shapes.TryGetValue(plant.Model, out var shape))
                        {
                            FloraMesh mesh = isRock ? RockForge.Forge(form, kind, modelSeed) : FloraForge.Grow(kindOfPlant, modelSeed);
                            shape = Measure(mesh);
                            shapes[plant.Model] = shape;
                        }
                        float top = plant.Position.Y + shape.top * plant.Scale;
                        species[name] = species.GetValueOrDefault(name) + 1;
                        foreach (var family in Families)
                        {
                            if (family.prefixes.Any(p => name.StartsWith(p))) maps[family.name][i, j]++;
                        }

                        if (isRock)
                        {
                            // Out of anyone's reach: deeper than anyone wades under all of it, and nobody stands within a body of it.
                            float reach = shape.reach * plant.Scale;
                            for (int k = 0; k < 16; k++)
                            {
                                float angle = k * MathF.Tau / 16f;
                                var round = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                                Vector2 edge = xz + round * reach;
                                Vector2 near = xz + round * (reach + NearBody);
                                if (world.InBounds(edge) && world.WaterDepthAt(edge) < WorldData.WadingDepth)
                                {
                                    Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} stands where it's {world.WaterDepthAt(edge):0.00} m deep, which anyone can wade");
                                    break;
                                }
                                if (world.InBounds(near) && world.Walkable.Sample(near) > 0.25f)
                                {
                                    Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} is within a body of ground someone stands on");
                                    break;
                                }
                            }
                            if (top > level - WorldSeaFloor.Trough(depth)) Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} comes {top - level + WorldSeaFloor.Trough(depth):0.00} m out of a trough");
                            continue;
                        }

                        if (kindOfPlant.Form is LilyForm)
                        {
                            bool fresh = still || level > WorldPlan.SeaLevel + 0.01f;
                            if (!fresh) Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} floats on the sea");
                            if (MathF.Abs(plant.Position.Y - level) > 0.02f) Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} is {plant.Position.Y - level:0.00} m off the water it floats on");
                            continue;
                        }

                        var under = (IUnderWater)kindOfPlant.Form;
                        if (kindOfPlant.Form is KelpForm { Floats: true })
                        {
                            // A giant kelp's crown lies out on the water: it may reach it, and rise a little over it, never stand up out of it.
                            worstKelp = MathF.Max(worstKelp, top - level);
                            if (top > level + MostOverSurface) Fail($"{plant.Model} at {xz.X:0},{xz.Y:0} stands {top - level:0.00} m up out of the water");
                        }
                        else if (under.Rigid)
                        {
                            if (top > level - WorldSeaFloor.Trough(depth)) Fail($"{plant.Model} at {xz.X:0},{xz.Y:0}, {depth:0.00} m deep, comes {top - level + WorldSeaFloor.Trough(depth):0.00} m out of a trough");
                        }
                        else if (top > level)
                        {
                            Fail($"{plant.Model} at {xz.X:0},{xz.Y:0}, {depth:0.00} m deep, stands {top - level:0.00} m out of the still water");
                        }
                    }
                }
            }

            int total = species.Values.Sum();
            Console.WriteLine($"seed {seed} ({world.Plan.Archetype.Id}): {total} under water, {shapes.Count} models, a giant kelp's crown at most {worstKelp:0.00} m over the water; "
                + string.Join(", ", species.OrderByDescending(p => p.Value).Take(14).Select(p => $"{p.Key} {p.Value}")));
            foreach (var family in Families)
            {
                if (Busiest(maps[family.name], n) is (int most, Vector2 at) && most > 0)
                {
                    float depth = world.WaterDepthAt(at);
                    Console.WriteLine($"  most {family.name}: {most} in a view at --at {at.X:0},{at.Y:0}, {depth:0.0} m deep");
                }
            }
            return failures;
        }

        // A model's highest point over its origin and the furthest it reaches from it across the ground, at a scale of one.
        private static (float top, float reach) Measure(FloraMesh mesh)
        {
            float top = 0f, reach = 0f;
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Vector3 p = mesh.Vertices[i].Position;
                top = MathF.Max(top, p.Y);
                reach = MathF.Max(reach, p.Xz.Length);
            }
            return (top, reach);
        }

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
