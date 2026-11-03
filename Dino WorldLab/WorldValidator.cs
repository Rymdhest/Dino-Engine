using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Checks a generated world against what generation promises. These checks are written separately from the generator on
    /// purpose, with their own brute-force geometry, so that a bug in the placement code can't hide from them.
    /// </summary>
    internal sealed class WorldValidator
    {
        private const float CharacterRadius = 1.1f;
        private const float RoadGradeLimit = 0.3f;
        private const float BareStretchLimit = 330f;    // 30 seconds at a run.
        private const float GradeWindow = 4f;           // Grades are measured over at least this much road, as walked.

        public readonly List<string> Problems = new();
        public float LongestBareStretch { get; private set; }
        public float SteepestRoad { get; private set; }
        public int WalkableCellCount { get; private set; }

        public void Check(WorldData world)
        {
            CheckFields(world);
            CheckFootprints(world);
            CheckObjectsOnGround(world);
            CheckRoads(world);
            CheckBiomes(world);
            CheckTravel(world);
            CheckBareStretches(world);
            CheckPlayable(world);
        }

        private void Problem(string text)
        {
            if (Problems.Count < 40) Problems.Add(text);
        }

        private void CheckFields(WorldData world)
        {
            (string name, float[] data)[] fields =
            {
                ("Height", world.Height.Data), ("Elevation", world.Elevation.Data), ("Canopy", world.Canopy.Data),
                ("Temperature", world.Temperature.Data), ("Moisture", world.Moisture.Data), ("Flow", world.Flow.Data),
            };
            foreach ((string name, float[] data) in fields)
            {
                foreach (float value in data)
                {
                    if (float.IsFinite(value)) continue;
                    Problem($"{name} holds {value}");
                    break;
                }
            }

            ScalarField first = world.BiomeWeights[0];
            for (int i = 0; i < first.Data.Length; i += 97)
            {
                float total = 0f;
                foreach (ScalarField weights in world.BiomeWeights) total += weights.Data[i];
                if (MathF.Abs(total - 1f) < 0.02f) continue;
                Problem($"Biome weights add up to {total} at node {i}");
                break;
            }
        }

        // Pairs of layers that must never touch, and how much they may overlap to allow for touching edges.
        private static readonly (PlacementLayer a, PlacementLayer b)[] Forbidden =
        {
            (PlacementLayer.Tree, PlacementLayer.Tree),
            (PlacementLayer.Tree, PlacementLayer.Structure),
            (PlacementLayer.Tree, PlacementLayer.Prop),
            (PlacementLayer.Tree, PlacementLayer.Road),
            (PlacementLayer.Tree, PlacementLayer.Fence),
            (PlacementLayer.Shrub, PlacementLayer.Structure),
            (PlacementLayer.Shrub, PlacementLayer.Road),
            (PlacementLayer.Rock, PlacementLayer.Structure),
            (PlacementLayer.Rock, PlacementLayer.Road),
            (PlacementLayer.Structure, PlacementLayer.Structure),
            (PlacementLayer.Structure, PlacementLayer.Road),
            (PlacementLayer.Prop, PlacementLayer.Road),
            (PlacementLayer.Field, PlacementLayer.Structure),
            (PlacementLayer.Field, PlacementLayer.Road),
        };

        private void CheckFootprints(WorldData world)
        {
            var entries = new List<(Footprint footprint, PlacementLayer layer)>();
            world.Placement.ForEach((footprint, layer, solid) =>
            {
                if (layer != PlacementLayer.None) entries.Add((footprint, layer));
            });

            // Own grid, big enough that the largest footprint spans few cells.
            const float Cell = 24f;
            var grid = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < entries.Count; i++)
            {
                Footprint footprint = entries[i].footprint;
                float reach = footprint.BoundingRadius;
                int minX = (int)MathF.Floor((footprint.Centre.X - reach) / Cell);
                int maxX = (int)MathF.Floor((footprint.Centre.X + reach) / Cell);
                int minZ = (int)MathF.Floor((footprint.Centre.Y - reach) / Cell);
                int maxZ = (int)MathF.Floor((footprint.Centre.Y + reach) / Cell);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!grid.TryGetValue((x, z), out List<int>? list)) grid[(x, z)] = list = new List<int>();
                        list.Add(i);
                    }
                }
            }

            var seen = new HashSet<(int, int)>();
            foreach (List<int> cell in grid.Values)
            {
                for (int i = 0; i < cell.Count; i++)
                {
                    for (int j = i + 1; j < cell.Count; j++)
                    {
                        int a = Math.Min(cell[i], cell[j]);
                        int b = Math.Max(cell[i], cell[j]);
                        if (!IsForbidden(entries[a].layer, entries[b].layer)) continue;
                        if (!seen.Add((a, b))) continue;
                        if (!Touch(entries[a].footprint, entries[b].footprint)) continue;
                        Problem($"{entries[a].layer} at {entries[a].footprint.Centre} overlaps {entries[b].layer} at {entries[b].footprint.Centre}");
                    }
                }
            }
        }

        private static bool IsForbidden(PlacementLayer a, PlacementLayer b)
        {
            foreach ((PlacementLayer x, PlacementLayer y) in Forbidden)
            {
                if ((a == x && b == y) || (a == y && b == x)) return true;
            }
            return false;
        }

        // Independent of Footprint.Overlaps: sample one outline and test the points against the other shape.
        private static bool Touch(in Footprint a, in Footprint b)
        {
            return AnyOutlinePointInside(a, b) || AnyOutlinePointInside(b, a);
        }

        private static bool AnyOutlinePointInside(in Footprint outline, in Footprint shape)
        {
            const int Samples = 24;
            for (int i = 0; i < Samples; i++)
            {
                Vector2 point;
                if (outline.IsCircle)
                {
                    float angle = i * MathF.Tau / Samples;
                    point = outline.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * outline.HalfExtents.X * 0.995f;
                }
                else
                {
                    float t = (i % 6) / 6f * 2f - 1f;
                    Vector2 half = outline.HalfExtents * 0.995f;
                    Vector2 local = (i / 6) switch
                    {
                        0 => new Vector2(t * half.X, -half.Y),
                        1 => new Vector2(half.X, t * half.Y),
                        2 => new Vector2(-t * half.X, half.Y),
                        _ => new Vector2(-half.X, -t * half.Y),
                    };
                    point = outline.ToWorld(local);
                }
                if (Contains(shape, point)) return true;
            }
            return Contains(shape, outline.Centre);
        }

        private static bool Contains(in Footprint shape, Vector2 point)
        {
            if (shape.IsCircle) return (point - shape.Centre).Length < shape.HalfExtents.X;
            Vector2 local = shape.ToLocal(point);
            return MathF.Abs(local.X) < shape.HalfExtents.X && MathF.Abs(local.Y) < shape.HalfExtents.Y;
        }

        private void CheckObjectsOnGround(WorldData world)
        {
            foreach (WorldObject item in world.Objects.All())
            {
                string id = world.Objects.AssetIds[item.Asset];
                if (!float.IsFinite(item.Position.Y)) Problem($"{id} has no height at {item.Position.Xz}");

                bool plant = NatureCatalog.TryGet(id, out WorldAsset asset) && asset.Kind is WorldAssetKind.Tree or WorldAssetKind.Shrub or WorldAssetKind.Deadwood;
                if (plant && world.WaterDepthAt(item.Position.Xz) > 0.2f)
                {
                    Problem($"{id} stands in {world.WaterDepthAt(item.Position.Xz):0.0} m of water at {item.Position.Xz}");
                }
                if (id.Contains('.') && id.Split('.').Length == 3 && !id.StartsWith("flower"))
                {
                    // Styled buildings: the pad under them has to have held.
                    float ground = world.Height.Sample(item.Position.Xz);
                    if (MathF.Abs(ground - item.Position.Y) > 1.6f)
                    {
                        Problem($"{id} floats {item.Position.Y - ground:0.0} m off the ground at {item.Position.Xz}");
                    }
                }
            }
        }

        private void CheckRoads(WorldData world)
        {
            foreach (WorldRoad road in world.Roads)
            {
                int start = 0;
                float walked = 0f;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 to = road.Points[i].Xz;
                    walked += (to - road.Points[i - 1].Xz).Length;
                    if (world.WaterDepthAt(to) > 0.25f) Problem($"{road.Class} under {world.WaterDepthAt(to):0.0} m of water at {to}");

                    // The distance along the road, not across it: a straight line over a hairpin bend is short, and says nothing about the climb.
                    while (start + 1 < i && walked - (road.Points[start + 1].Xz - road.Points[start].Xz).Length >= GradeWindow)
                    {
                        walked -= (road.Points[start + 1].Xz - road.Points[start].Xz).Length;
                        start++;
                    }
                    if (walked < GradeWindow) continue;

                    Vector2 from = road.Points[start].Xz;
                    float grade = MathF.Abs(world.Height.Sample(to) - world.Height.Sample(from)) / walked;
                    SteepestRoad = MathF.Max(SteepestRoad, grade);
                    if (grade > RoadGradeLimit)
                    {
                        float planned = MathF.Abs(road.Points[i].Y - road.Points[start].Y) / walked;
                        float offCentre = MathF.Abs(world.Height.Sample(to) - road.Points[i].Y);
                        Problem($"{road.Class} grade {grade:0.00} (planned {planned:0.00}, off by {offCentre:0.00}, over {walked:0.0} m, road {road.Length:0} m, point {i}/{road.Points.Count}) at {from}");
                    }
                }
            }
        }

        // What the game takes for granted of every world: somewhere to arrive and trade, a way out, someone to defeat, and everyone
        // who lives there standing where they can be reached.
        private void CheckPlayable(WorldData world)
        {
            if (world.Sites.Count == 0 || world.Sites[0].Kind != SiteKind.Landing)
            {
                Problem("the first site is not the landing");
                return;
            }

            WorldSite landing = world.Sites[0];
            if (!landing.Spawns.Any(s => s.Role == SpawnRole.Waygate)) Problem("the landing has no waygate");
            if (!landing.Spawns.Any(s => s.Role == SpawnRole.Merchant)) Problem("the landing has no merchant");

            int keeps = world.Sites.Count(s => s.Kind == SiteKind.Keep);
            if (keeps == 0) Problem("no keep");
            if (keeps > 0 && !world.Sites.Any(s => s.Kind == SiteKind.Keep && s.Spawns.Any(p => p.Role == SpawnRole.Boss))) Problem("no warlord in the keep");

            for (int i = 0; i < world.Sites.Count; i++)
            {
                WorldSite site = world.Sites[i];
                if (site.Id != i) Problem($"{site.Name} has id {site.Id} at index {i}");
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of {site.Name} can't be reached at {spawn.Position}");
                }
            }
        }

        private void CheckBiomes(WorldData world)
        {
            int[] area = new int[world.Plan.Biomes.Count];
            int land = 0;
            for (float z = 0f; z < world.Size; z += 16f)
            {
                for (float x = 0f; x < world.Size; x += 16f)
                {
                    Vector2 position = new Vector2(x, z);
                    if (world.Reachable.Sample(position) < 0.5f) continue;
                    area[world.DominantBiome(position)]++;
                    land++;
                }
            }
            for (int b = 0; b < area.Length; b++)
            {
                float share = (float)area[b] / Math.Max(land, 1);
                if (share < 0.04f) Problem($"{world.Plan.Biomes[b].Name} covers only {share:P1} of the land");
            }
        }

        // The real test of a layout: flood outwards from the landing at character width, around every solid thing in the world.
        private void CheckTravel(WorldData world)
        {
            const float Cell = 1f;
            int size = (int)(world.Size / Cell) + 1;
            bool[] blocked = new bool[size * size];

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    blocked[z * size + x] = !world.Walkable.IsSet(new Vector2(x, z) * Cell);
                }
            }

            world.Placement.ForEach((footprint, layer, solid) =>
            {
                if (!solid) return;
                float reach = footprint.BoundingRadius + CharacterRadius;
                int minX = Math.Max((int)((footprint.Centre.X - reach) / Cell), 0);
                int maxX = Math.Min((int)((footprint.Centre.X + reach) / Cell) + 1, size - 1);
                int minZ = Math.Max((int)((footprint.Centre.Y - reach) / Cell), 0);
                int maxZ = Math.Min((int)((footprint.Centre.Y + reach) / Cell) + 1, size - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (footprint.Overlaps(Footprint.Circle(new Vector2(x, z) * Cell, CharacterRadius))) blocked[z * size + x] = true;
                    }
                }
            });

            Vector2 landing = world.Sites[0].Position;
            bool[] reached = new bool[size * size];
            var stack = new Stack<int>();
            int start = NearestOpen(blocked, size, landing, Cell, 12);
            if (start < 0)
            {
                Problem("The landing itself is blocked");
                return;
            }
            reached[start] = true;
            stack.Push(start);
            int count = 0;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                count++;
                int x = i % size;
                int z = i / size;
                if (x > 0) Visit(i - 1);
                if (x < size - 1) Visit(i + 1);
                if (z > 0) Visit(i - size);
                if (z < size - 1) Visit(i + size);
            }
            WalkableCellCount = count;

            void Visit(int next)
            {
                if (blocked[next] || reached[next]) return;
                reached[next] = true;
                stack.Push(next);
            }

            foreach (WorldSite site in world.Sites)
            {
                // A site counts as reached if a character can get to its edge; what is inside is checked per spawn below.
                if (!AnyReachedNear(reached, size, site.Position, Cell, site.Radius))
                {
                    Problem($"{site.Definition.Label} {site.Name} at {site.Position} can't be reached on foot");
                }

                foreach (SiteSpawn spawn in site.Spawns)
                {
                    if (!AnyReachedNear(reached, size, spawn.Position, Cell, 3f))
                    {
                        Problem($"{spawn.Role} of {site.Definition.Label} {site.Name} at {spawn.Position} can't be reached on foot");
                    }
                }
            }
        }

        private static int NearestOpen(bool[] blocked, int size, Vector2 position, float cell, int reach)
        {
            int cx = (int)(position.X / cell);
            int cz = (int)(position.Y / cell);
            for (int r = 0; r <= reach; r++)
            {
                for (int z = cz - r; z <= cz + r; z++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || z < 0 || x >= size || z >= size) continue;
                        if (!blocked[z * size + x]) return z * size + x;
                    }
                }
            }
            return -1;
        }

        private static bool AnyReachedNear(bool[] reached, int size, Vector2 position, float cell, float radius)
        {
            int reach = (int)MathF.Ceiling(radius / cell);
            int cx = (int)(position.X / cell);
            int cz = (int)(position.Y / cell);
            for (int z = Math.Max(cz - reach, 0); z <= Math.Min(cz + reach, size - 1); z++)
            {
                for (int x = Math.Max(cx - reach, 0); x <= Math.Min(cx + reach, size - 1); x++)
                {
                    if (reached[z * size + x]) return true;
                }
            }
            return false;
        }

        // Ghost of Tsushima's rule of thumb: something should call to the traveller at least every 30 seconds.
        private void CheckBareStretches(WorldData world)
        {
            var landmarks = new List<Vector2>();
            foreach (WorldSite site in world.Sites) landmarks.Add(site.Position);
            foreach (WorldObject item in world.Objects.All())
            {
                string id = world.Objects.AssetIds[item.Asset];
                if (id is "lantern.stone" or "cairn" or "signpost" or "cart" or "bench") landmarks.Add(item.Position.Xz);
            }

            foreach (WorldRoad road in world.Roads)
            {
                float bare = 0f;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 point = road.Points[i].Xz;
                    bare += (point - road.Points[i - 1].Xz).Length;
                    foreach (Vector2 mark in landmarks)
                    {
                        if ((mark - point).LengthSquared < 45f * 45f)
                        {
                            bare = 0f;
                            break;
                        }
                    }
                    LongestBareStretch = MathF.Max(LongestBareStretch, bare);
                }
            }
            if (LongestBareStretch > BareStretchLimit) Problem($"{LongestBareStretch:0} m of road with nothing to see");
        }
    }
}
