using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World;
using Dino_Engine.World.Generation;
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
        private const float FenceLean = 3f;             // Degrees a fence's uprights may lean by: a span is built for its slope, and only what's left over tilts it.

        public readonly List<string> Problems = new();
        public float LongestBareStretch { get; private set; }
        public float SteepestRoad { get; private set; }
        public int WalkableCellCount { get; private set; }

        public void Check(WorldData world)
        {
            CheckFields(world);
            CheckFootprints(world);
            CheckObjectsOnGround(world);
            CheckFences(world);
            CheckRoads(world);
            CheckBiomes(world);
            CheckTravel(world);
            CheckBareStretches(world);
            CheckPlayable(world);
            CheckPacks(world);
            CheckMountains(world);
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

        // Every post and every span stands upright, as anything leaning further than the leftover of a slope step reads as a fence knocked flat.
        private void CheckFences(WorldData world)
        {
            int leaning = 0;
            float worst = 0f;
            string where = "";
            foreach (WorldObject item in world.Objects.All())
            {
                string id = world.Objects.AssetIds[item.Asset];
                if (!FenceSpec.TryParse(id, out _)) continue;
                Vector3 up = Vector3.Transform(Vector3.UnitY, item.Rotation);
                float lean = MathHelper.RadiansToDegrees(MathF.Acos(Math.Clamp(up.Y, -1f, 1f)));
                if (lean <= FenceLean) continue;
                leaning++;
                if (lean > worst)
                {
                    worst = lean;
                    where = $"{id} at {item.Position.Xz}";
                }
            }
            if (leaning > 0) Problem($"{leaning} pieces of fence lean more than {FenceLean} degrees, the worst {worst:0} degrees: {where}");
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
            if (world.Plan.Archetype.Peaceful)
            {
                CheckHaven(world);
                return;
            }
            if (world.Halls.Count > 0) Problem($"{world.Halls.Count} halls stand outside the Haven");
            if (world.Plan.Archetype.Arena)
            {
                CheckArena(world);
                return;
            }
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
            foreach (WorldSite keep in world.Sites.Where(s => s.Kind == SiteKind.Keep)) CheckKeepArena(world, keep);

            for (int i = 0; i < world.Sites.Count; i++)
            {
                WorldSite site = world.Sites[i];
                if (site.Id != i) Problem($"{site.Name} has id {site.Id} at index {i}");
                if (site.Kind == SiteKind.Graveyard && !HasChurch(world, site)) Problem($"{site.Name} has no church");
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    if (spawn.Role == SpawnRole.Altar && !Stands(world, StructureCatalog.Altar, spawn.Position)) Problem($"{site.Name} can be prayed at, but has no altar");
                    if (spawn.Role == SpawnRole.Waygate && !Stands(world, StructureCatalog.Waygate, spawn.Position)) Problem($"{site.Name} can be left by, but has no waygate");
                    if (spawn.Role == SpawnRole.Stash && !Stands(world, StructureCatalog.Strongbox, spawn.Position)) Problem($"{site.Name} has a stash, but no strongbox");
                    if (spawn.Role is SpawnRole.MapTable or SpawnRole.Dummy or SpawnRole.BetaVendor or SpawnRole.Workbench or SpawnRole.GamingTable or SpawnRole.Keeper) Problem($"{site.Name} has a {spawn.Role}, which only the Haven has");
                }

                // A village is where a land's people hide, and where a hero can rest, trade, put things away and go home.
                if (site.Kind == SiteKind.Village)
                {
                    foreach (SpawnRole needed in new[] { SpawnRole.Waygate, SpawnRole.Stash, SpawnRole.Altar, SpawnRole.Merchant })
                    {
                        if (!site.Spawns.Any(s => s.Role == needed)) Problem($"{site.Name} is a village with no {needed}");
                    }
                    if (site.Spawns.Any(s => s.Role.IsHostile())) Problem($"{site.Name} is a village with hostiles in it");
                }

                // Every place a land asks to be freed is held, and has a chest of what they took.
                if (site.Kind != SiteKind.Keep && site.Spawns.Any(s => s.Role.IsHostile()) && !site.Spawns.Any(s => s.Role == SpawnRole.Chest)) Problem($"{site.Name} is held, but has no chest");
                if (site.Kind == SiteKind.Waygate && !site.Spawns.Any(s => s.Role == SpawnRole.Waygate)) Problem($"{site.Name} is a waygate with no gate");
                if (site.Kind == SiteKind.Graveyard)
                {
                    foreach (SiteSpawn spawn in site.Spawns)
                    {
                        // The wall wanders up to 8% either side of the radius.
                        float out_ = (spawn.Position - site.Position).Length - site.Radius * 1.08f;
                        if (out_ > 0f) Problem($"{spawn.Role} of {site.Name} was pushed {out_:0.0} m out of the churchyard, to {spawn.Position}");
                    }
                }
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    // Whoever watches from a keep's arena stands where nobody else can go, and is meant to.
                    if (spawn.Role == SpawnRole.Spectator) continue;
                    if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of {site.Name} can't be reached at {spawn.Position}");
                }
            }
        }

        /// <summary>
        /// The arena a land's keep holds (ARENAS.md): its tyrant on its floor and nobody else hostile there, a gate to go up by, its arrival somewhere to stand on its floor, and its way down on the land
        /// where anyone can walk to it from the landing; and its seal holding both ways, so that nothing on its floor walks off it and nothing on the land walks onto it.
        /// </summary>
        private void CheckKeepArena(WorldData world, WorldSite keep)
        {
            if (keep.Arena is not ArenaPlan plan)
            {
                Problem($"{keep.Name} has no arena");
                return;
            }
            if (!plan.Keep) Problem($"{keep.Name}'s arena is planned as a boss map's");
            if (plan.Way is not ArenaEntry way || string.IsNullOrEmpty(way.Model)) Problem($"{keep.Name}'s arena has no gate to go up by");
            int tyrants = keep.Spawns.Count(s => s.Role == SpawnRole.Boss);
            if (tyrants != 1 || keep.Spawns.Any(s => s.Role == SpawnRole.Boss && !plan.Contains(s.Position))) Problem($"{keep.Name} has {tyrants} tyrants, not one on its arena's floor");
            if (keep.Spawns.Any(s => s.Role == SpawnRole.Rival)) Problem($"{keep.Name}'s arena has a place for a second tyrant");
            SiteSpawn? stray = keep.Spawns.Where(s => s.Role.IsHostile() && s.Role != SpawnRole.Boss && plan.Contains(s.Position, 1.5f)).Cast<SiteSpawn?>().FirstOrDefault();
            if (stray is SiteSpawn hostile) Problem($"{keep.Name}'s arena has a {hostile.Role} on or by its floor at {hostile.Position}, who isn't its tyrant");
            if (!plan.Contains(plan.Arrival) || !world.Passable.IsSet(plan.Arrival)) Problem($"{keep.Name}'s arena has nowhere to stand where it's come down to, at {plan.Arrival}");
            if (!plan.Contains(plan.Exit, 1f)) Problem($"{keep.Name}'s way up at {plan.Exit} isn't on its floor");

            // The land as anyone walks it from the landing: it reaches the way down, and nowhere on the floor.
            var land = new HashSet<(int, int)>();
            int onto = 0;
            Vector2 first = default;
            foreach (Vector2 cell in Flood(world.Passable, world.Sites[0].Landing))
            {
                land.Add(((int)MathF.Round(cell.X), (int)MathF.Round(cell.Y)));
                if (!plan.Contains(cell)) continue;
                if (onto++ == 0) first = cell;
            }
            if (onto > 0) Problem($"{keep.Name}'s arena can be walked onto from the land: {onto} m2 of its floor, the first at {first}");
            bool door = false;
            for (int dx = -2; dx <= 2 && !door; dx++)
            {
                for (int dz = -2; dz <= 2 && !door; dz++) door = land.Contains(((int)MathF.Round(plan.Door.X) + dx, (int)MathF.Round(plan.Door.Y) + dz));
            }
            if (!door) Problem($"{keep.Name}'s way down at {plan.Door} can't be walked to from the landing");
            CheckSealed(world, plan, plan.Arrival, $"{keep.Name}'s arena");
        }

        // The places a land can ask to be freed: there have to be at least the most it ever asks for (LandTerms.MostPlaces). And waygates, the landing's among them: a land is small enough now
        // that two are enough to travel between.
        private const int LeastPlaces = SiteStage.LeastHeld;
        private const int LeastWaygates = SiteStage.LeastWaygates;
        private const int Dummies = 21;

        private void CheckLandPlaces(WorldData world)
        {
            int places = world.Sites.Count(site => site.Kind != SiteKind.Keep && site.Spawns.Any(s => s.Role.IsHostile()));
            if (places < LeastPlaces) Problem($"only {places} places to free");
            int gates = world.Sites.Count(site => site.Spawns.Any(s => s.Role == SpawnRole.Waygate));
            if (gates < LeastWaygates) Problem($"only {gates} waygates");
        }

        // The Haven: one village, everything it's for standing where it can be reached, and nobody hostile anywhere.
        private const int LeastSpectators = 24;

        // An arena is one place, sealed round its floor: its tyrant, its waygate and the ways the brood comes in are all on the floor, its people up in the stands and off it, and nothing that walks is
        // ever outside the seal (ARENAS.md).
        private void CheckArena(WorldData world)
        {
            if (world.Sites.Count != 1 || world.Sites[0].Kind != SiteKind.Arena)
            {
                Problem("the arena is not one arena");
                return;
            }
            if (world.Haunts.Count > 0) Problem($"the arena has {world.Haunts.Count} packs in it");

            WorldSite arena = world.Sites[0];
            if (arena.Arena is not ArenaPlan plan)
            {
                Problem("the arena has no plan");
                return;
            }
            if (plan.Kind != world.Plan.Archetype.ArenaKind) Problem($"the arena is a {plan.Kind}, in a world of {world.Plan.Archetype.ArenaKind}");
            if (plan.Keep) Problem("a boss map's arena is planned as a keep's");
            int Count(SpawnRole role) => arena.Spawns.Count(s => s.Role == role);
            if (Count(SpawnRole.Boss) != 1) Problem($"the arena has {Count(SpawnRole.Boss)} tyrants");
            if (Count(SpawnRole.Rival) != 1) Problem($"the arena has {Count(SpawnRole.Rival)} places for a second tyrant");
            if (arena.Spawns.Any(s => s.Role.IsHostile() && s.Role is not (SpawnRole.Boss or SpawnRole.Rival))) Problem("the arena has someone hostile in it who isn't a tyrant");

            SiteSpawn[] waygates = arena.Spawns.Where(s => s.Role == SpawnRole.Waygate).ToArray();
            if (waygates.Length != 1) Problem($"the arena has {waygates.Length} waygates");
            else if (!Stands(world, StructureCatalog.Waygate, waygates[0].Position)) Problem("the arena's waygate has nothing standing on it");

            if (plan.Entries.Count < LeastEntries) Problem($"the arena has {plan.Entries.Count} ways for the brood to come in");
            foreach (ArenaEntry entry in plan.Entries)
            {
                if (!plan.Contains(entry.Out)) Problem($"the arena's {entry.Kind} at {entry.Out} lets the brood out off its floor");
                if (!world.Passable.IsSet(entry.Out)) Problem($"the arena's {entry.Kind} at {entry.Out} lets the brood out where nobody can stand");
                if (entry.Kind != ArenaEntryKind.Rising && string.IsNullOrEmpty(entry.Model)) Problem($"the arena's {entry.Kind} at {entry.Out} has nothing to open");
            }

            if (world.WaterDepthAt(arena.Position) > 0f) Problem("the arena's floor is under water");
            SiteSpawn[] watching = arena.Spawns.Where(s => s.Role == SpawnRole.Spectator).ToArray();
            if (watching.Length < LeastSpectators) Problem($"only {watching.Length} watch from the arena's stands");
            SiteSpawn[] low = watching.Where(s => world.Height.Sample(s.Position) < plan.Floor + 1f).ToArray();
            if (low.Length > 0) Problem($"{low.Length} of those watching aren't up off the floor, the first at {low[0].Position}");
            int on = watching.Count(s => plan.Contains(s.Position, 0.5f));
            if (on > 0) Problem($"{on} of those watching stand on the floor");
            foreach (SiteSpawn spawn in arena.Spawns)
            {
                if (spawn.Role is SpawnRole.Spectator) continue;
                if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of the arena can't be reached at {spawn.Position}");
                if (!plan.Contains(spawn.Position)) Problem($"{spawn.Role} of the arena is off its floor at {spawn.Position}");
            }
            CheckSealed(world, plan, arena.Landing, "the arena");
        }

        private const int LeastEntries = 2;

        /// <summary>
        /// That an arena's seal holds, by brute force on the world's own maps: everything passable that can be reached from where its fighters stand is inside its bounds, and everything walkable is too,
        /// give or take what a walkable map two metres to a cell can't say. Passable is what decides, as the seal is solid; walkable is what the floor sunk below everything round it adds.
        /// </summary>
        private void CheckSealed(WorldData world, ArenaPlan plan, Vector2 from, string name)
        {
            int outside = 0;
            Vector2 first = default;
            foreach (Vector2 cell in Flood(world.Passable, from))
            {
                if (plan.Contains(cell, 1.2f)) continue;
                if (outside++ == 0) first = cell;
            }
            if (outside > 0) Problem($"{name}'s seal leaks: {outside} m2 can be walked to outside it, the first at {first}");

            // Walkable ground goes no further out than the seal's own thickness and the slope a step is laid as: where it does, say where it crosses, which is where to look.
            int strayed = 0;
            Vector2 crossing = default;
            foreach ((Vector2 cell, Vector2 came) in Flood(world.Walkable, from, cell => plan.Contains(cell)))
            {
                if (plan.Contains(cell, 3f)) continue;
                if (strayed++ == 0) crossing = came;
            }
            if (strayed > 0) Problem($"{name}'s floor isn't sunk below what's round it: {strayed * 4} m2 of walkable ground joins it outside the seal, crossing out by {crossing}");
        }

        // Every cell of a map that's set and joined to a point by cells that are set, side by side, each with the last cell the flood came through that was still `inside`.
        private static IEnumerable<(Vector2 Cell, Vector2 Came)> Flood(ByteField field, Vector2 from, Func<Vector2, bool> inside)
        {
            int width = field.Width, depth = field.Depth;
            float size = field.CellSize;
            var last = new Dictionary<int, Vector2>();
            int startX = Math.Clamp((int)MathF.Round(from.X / size), 0, width - 1), startZ = Math.Clamp((int)MathF.Round(from.Y / size), 0, depth - 1);
            int start = -1;
            for (int r = 0; r <= 4 && start < 0; r++)
            {
                for (int z = startZ - r; z <= startZ + r && start < 0; z++)
                {
                    for (int x = startX - r; x <= startX + r; x++)
                    {
                        if (x < 0 || z < 0 || x >= width || z >= depth || field.Data[z * width + x] <= 127) continue;
                        start = z * width + x;
                        break;
                    }
                }
            }
            if (start < 0) yield break;
            var seen = new bool[width * depth];
            var queue = new Queue<int>();
            queue.Enqueue(start);
            seen[start] = true;
            last[start] = new Vector2(start % width, start / width) * size;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % width, z = i / width;
                Vector2 here = new Vector2(x, z) * size;
                Vector2 came = inside(here) ? here : last[i];
                yield return (here, came);
                foreach (int next in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, z > 0 ? i - width : -1, z < depth - 1 ? i + width : -1 })
                {
                    if (next < 0 || seen[next] || field.Data[next] <= 127) continue;
                    seen[next] = true;
                    last[next] = came;
                    queue.Enqueue(next);
                }
            }
        }

        // Every cell of a map that's set and joined to a point by cells that are set, side by side.
        private static IEnumerable<Vector2> Flood(ByteField field, Vector2 from)
        {
            int width = field.Width, depth = field.Depth;
            float cell = field.CellSize;
            int startX = Math.Clamp((int)MathF.Round(from.X / cell), 0, width - 1), startZ = Math.Clamp((int)MathF.Round(from.Y / cell), 0, depth - 1);
            // Start from the nearest set cell, as where someone stands can be a little off the grid's.
            int start = -1;
            for (int r = 0; r <= 4 && start < 0; r++)
            {
                for (int z = startZ - r; z <= startZ + r && start < 0; z++)
                {
                    for (int x = startX - r; x <= startX + r; x++)
                    {
                        if (x < 0 || z < 0 || x >= width || z >= depth || field.Data[z * width + x] <= 127) continue;
                        start = z * width + x;
                        break;
                    }
                }
            }
            if (start < 0) yield break;
            var seen = new bool[width * depth];
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % width, z = i / width;
                yield return new Vector2(x, z) * cell;
                foreach (int next in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, z > 0 ? i - width : -1, z < depth - 1 ? i + width : -1 })
                {
                    if (next < 0 || seen[next] || field.Data[next] <= 127) continue;
                    seen[next] = true;
                    stack.Push(next);
                }
            }
        }

        private void CheckHaven(WorldData world)
        {
            if (world.Sites.Count != 1 || world.Sites[0].Kind != SiteKind.Haven)
            {
                Problem("the Haven is not one village");
                return;
            }
            if (world.Haunts.Count > 0) Problem($"the Haven has {world.Haunts.Count} packs in it");

            WorldSite haven = world.Sites[0];
            (SpawnRole role, StructureDefinition? on)[] needed =
            {
                (SpawnRole.Waygate, StructureCatalog.GreatWaygate), (SpawnRole.MapTable, StructureCatalog.MapTable), (SpawnRole.Stash, StructureCatalog.Strongbox),
                (SpawnRole.Merchant, null), (SpawnRole.BetaVendor, null),
            };
            if (haven.Spawns.Any(s => s.Role == SpawnRole.Altar)) Problem("the Haven has a shrine, and nothing to be blessed for");
            foreach ((SpawnRole role, StructureDefinition? on) in needed)
            {
                SiteSpawn[] spawns = haven.Spawns.Where(s => s.Role == role).ToArray();
                if (spawns.Length != 1) Problem($"the Haven has {spawns.Length} of {role}");
                if (on != null && spawns.Length > 0 && !Stands(world, on, spawns[0].Position)) Problem($"the Haven's {role} has no {on.Id}");
            }
            int dummies = haven.Spawns.Count(s => s.Role == SpawnRole.Dummy);
            if (dummies != Dummies) Problem($"the Haven has {dummies} dummies, not {Dummies}");
            if (haven.Spawns.Any(s => s.Role.IsHostile())) Problem("the Haven has hostiles in it");
            foreach (SiteSpawn spawn in haven.Spawns)
            {
                if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of the Haven can't be reached at {spawn.Position}");
            }
            CheckHalls(world, haven);
        }

        // The halls (HALLS.md): one of each, drawn where they stand, on level ground with nothing else standing or growing in them, their doors open onto the village and
        // their walls closed to it, and whatever is used, kept or read in them within reach. Measured on the finished world, from the design's own numbers.
        private const float HallLevel = 0.05f;      // Metres the ground under a floor may be off it: people stand on that ground, and the floor is laid 3 cm over it.
        private const float WallFooting = 2.2f;     // Metres below a floor the ground outside may fall to: walls go down 2.5, and daylight would show under them.
        private const float SillStep = 0.3f;        // Metres the ground at a door may be off the floor, for its sill and step to meet it.
        private const float LeastOpenRoom = 0.25f;  // Of a room, that someone can stand in.
        private const float ExhibitReach = 6f;      // Metres from a display that someone has to be able to stand to read it (ArchivePlaques.Reach is 7.5).

        private void CheckHalls(WorldData world, WorldSite haven)
        {
            foreach (HallKind kind in Enum.GetValues<HallKind>())
            {
                WorldHall[] halls = world.Halls.Where(h => h.Kind == kind).ToArray();
                if (halls.Length != 1)
                {
                    Problem($"the Haven has {halls.Length} of the {kind}");
                    continue;
                }
                WorldHall hall = halls[0];
                HallDesign d = hall.Design;
                string name = d.Name;

                // Its two models, drawn where it stands, which is where its walls stop people; and nothing else in it.
                foreach (string asset in new[] { d.ShellAsset, d.InsideAsset })
                {
                    WorldObject[] models = world.Objects.All().Where(o => world.Objects.AssetIds[o.Asset] == asset).ToArray();
                    if (models.Length != 1)
                    {
                        Problem($"{name} has {models.Length} of {asset}");
                        continue;
                    }
                    WorldObject model = models[0];
                    if (MathF.Abs(model.Position.Y - hall.Floor) > 0.05f) Problem($"{asset} is drawn {model.Position.Y - hall.Floor:+0.00;-0.00} m off {name}'s floor");
                    foreach (Vector2 corner in new[] { d.Half, -d.Half, new Vector2(d.Half.X, -d.Half.Y) })
                    {
                        Vector3 drawn = model.Position + Vector3.Transform(new Vector3(corner.X * model.Scale.X, 0f, corner.Y * model.Scale.Z), model.Rotation);
                        float off = (drawn.Xz - hall.ToWorld(corner)).Length;
                        if (off <= 0.05f) continue;
                        Problem($"{asset} is drawn {off:0.00} m from where {name} stands");
                        break;
                    }
                }
                foreach (WorldObject item in world.Objects.All())
                {
                    string id = world.Objects.AssetIds[item.Asset];
                    if (id != d.ShellAsset && id != d.InsideAsset && hall.Contains(item.Position.Xz, d.Wall)) Problem($"{id} stands in {name}, at {item.Position.Xz}");
                }
                foreach (Planting planting in world.Plantings)
                {
                    if (hall.Contains(planting.Position, d.Wall)) Problem($"something grows in {name}, at {planting.Position}");
                }

                // Level ground under its floor, which is where people stand: none over it, nor so far under that feet go through the boards. Dry, with no rock in it and no
                // road through it, and room to stand in.
                float highest = float.MinValue, lowest = float.MaxValue;
                Vector2 highAt = hall.Centre, lowAt = hall.Centre;
                int cells = 0, open = 0;
                bool wet = false, rock = false, road = false;
                for (float x = -d.Inner.X + 0.25f; x < d.Inner.X; x += 0.5f)
                {
                    for (float z = -d.Inner.Y + 0.25f; z < d.Inner.Y; z += 0.5f)
                    {
                        Vector2 at = hall.ToWorld(new Vector2(x, z));
                        float height = world.Height.Sample(at);
                        if (height > highest) (highest, highAt) = (height, at);
                        if (height < lowest) (lowest, lowAt) = (height, at);
                        wet |= world.WaterDepthAt(at) > 0f;
                        rock |= world.Rock.IsSet(at);
                        bool deep = MathF.Abs(x) < d.Inner.X - 3f && MathF.Abs(z) < d.Inner.Y - 3f;
                        road |= deep && (world.PavedRoad.IsSet(at) || world.TrailRoad.IsSet(at));
                        cells++;
                        if (world.Passable.IsSet(at)) open++;
                    }
                }
                if (highest > hall.Floor + HallLevel) Problem($"the ground comes up through {name}'s floor, {highest - hall.Floor:0.00} m over it at {highAt}");
                if (lowest < hall.Floor - HallLevel) Problem($"the ground under {name}'s floor falls {hall.Floor - lowest:0.00} m below it at {lowAt}, and feet with it");
                if (wet) Problem($"{name} has water in it");
                if (rock) Problem($"{name} has rock in it");
                if (road) Problem($"a road runs through {name}");
                if (open < cells * LeastOpenRoom) Problem($"only {open * 100 / Math.Max(cells, 1)}% of {name} can be stood in");

                // The ground round its walls, which go down 2.5 m below its floor.
                float outside = float.MaxValue;
                for (float x = -d.Half.X; x <= d.Half.X; x += 1f)
                {
                    outside = MathF.Min(outside, world.Height.Sample(hall.ToWorld(new Vector2(x, d.Half.Y + 0.4f))));
                    outside = MathF.Min(outside, world.Height.Sample(hall.ToWorld(new Vector2(x, -d.Half.Y - 0.4f))));
                }
                for (float z = -d.Half.Y; z <= d.Half.Y; z += 1f)
                {
                    outside = MathF.Min(outside, world.Height.Sample(hall.ToWorld(new Vector2(d.Half.X + 0.4f, z))));
                    outside = MathF.Min(outside, world.Height.Sample(hall.ToWorld(new Vector2(-d.Half.X - 0.4f, z))));
                }
                if (outside < hall.Floor - WallFooting) Problem($"the ground falls {hall.Floor - outside:0.0} m below {name}'s floor outside its walls, which go down 2.5");

                // Its doors: walked through from the village into the room, every step of it, over ground that meets the sill.
                foreach (HallOpening door in d.Openings.Where(o => o.Door))
                {
                    Vector2 outward = door.Side switch { HallSide.Front => Vector2.UnitY, HallSide.Back => -Vector2.UnitY, HallSide.PlusX => Vector2.UnitX, _ => -Vector2.UnitX };
                    Vector2 along = new Vector2(MathF.Abs(outward.Y), MathF.Abs(outward.X));
                    float face = outward.X != 0f ? d.Half.X : d.Half.Y;
                    for (float out_ = 3f; out_ >= -(d.Wall + 2.5f); out_ -= 0.25f)
                    {
                        Vector2 at = hall.ToWorld(along * door.Along + outward * (face + out_));
                        if (world.Passable.IsSet(at)) continue;
                        Problem($"{name}'s {door.Side} door can't be walked through, at {at}");
                        break;
                    }
                    for (float out_ = 0.3f; out_ <= 2.4f; out_ += 0.7f)
                    {
                        Vector2 at = hall.ToWorld(along * door.Along + outward * (face + out_));
                        float off = world.Height.Sample(at) - hall.Floor;
                        if (MathF.Abs(off) <= SillStep) continue;
                        Problem($"the ground at {name}'s {door.Side} door is {off:+0.00;-0.00} m off its floor");
                        break;
                    }
                }

                // Its walls: closed everywhere but its doors.
                foreach (HallSide side in Enum.GetValues<HallSide>())
                {
                    bool alongX = side is HallSide.Front or HallSide.Back;
                    float length = alongX ? d.Half.X : d.Half.Y;
                    float across = ((alongX ? d.Half.Y : d.Half.X) - d.Wall * 0.5f) * (side is HallSide.Front or HallSide.PlusX ? 1f : -1f);
                    for (float t = -length; t <= length; t += 0.5f)
                    {
                        if (d.Openings.Any(o => o.Door && o.Side == side && MathF.Abs(t - o.Along) < o.HalfWidth + 1.5f)) continue;
                        Vector2 at = hall.ToWorld(alongX ? new Vector2(t, across) : new Vector2(across, t));
                        if (!world.Passable.IsSet(at)) continue;
                        Problem($"{name}'s {side} wall can be walked through, at {at}");
                        break;
                    }
                }

                // What's used in it and whoever keeps it, where the design puts them; where the village walks to in it; and every record on a display that can be got near.
                SpawnRole? used = kind switch { HallKind.Forge => SpawnRole.Workbench, HallKind.GamingHouse => SpawnRole.GamingTable, _ => null };
                if (used is SpawnRole role && !d.Spots.Any(s => s.Role == role)) Problem($"{name} has no {role}");
                if (!d.Spots.Any(s => s.Role == SpawnRole.Keeper)) Problem($"nobody keeps {name}");
                foreach (HallSpot spot in d.Spots)
                {
                    Vector2 at = hall.ToWorld(spot.At);
                    if (!hall.Contains(at)) Problem($"{name}'s {spot.Role} is outside it, at {at}");
                    if (!haven.Spawns.Any(s => s.Role == spot.Role && (s.Position - at).Length < 1.5f)) Problem($"{name}'s {spot.Role} isn't where it was put, at {at}");
                }
                if (d.Visits.Count == 0) Problem($"nobody visits {name}");
                foreach (Vector2 visit in d.Visits)
                {
                    Vector2 at = hall.ToWorld(visit);
                    if (!hall.Contains(at) || !world.Passable.IsSet(at)) Problem($"{name} is visited at {at}, which nobody can reach");
                }
                if (kind == HallKind.Archive)
                {
                    foreach (ExhibitKind record in Enum.GetValues<ExhibitKind>())
                    {
                        int count = d.Exhibits.Count(e => e.Kind == record);
                        if (count != 1) Problem($"{name} has {count} displays of {record}");
                    }
                }
                foreach (HallExhibit exhibit in d.Exhibits)
                {
                    Vector2 at = hall.ToWorld(exhibit.At);
                    bool near = false;
                    for (float r = 1f; r <= ExhibitReach && !near; r += 0.5f)
                    {
                        for (int k = 0; k < 32 && !near; k++)
                        {
                            Vector2 by = at + new Vector2(MathF.Cos(k * MathF.Tau / 32f), MathF.Sin(k * MathF.Tau / 32f)) * r;
                            near = hall.Contains(by) && world.Passable.IsSet(by);
                        }
                    }
                    if (!near) Problem($"nobody can get near enough to {name}'s {exhibit.Kind} to read it");
                }
            }
        }

        // The game's camera, which looks along +z from behind and above at a fixed angle (ArpgGame.SpawnCameras).
        private static readonly float CameraRise = MathF.Tan(MathF.PI / 2.7f);
        private const float CameraReach = 14f;      // Metres of ground between the hero and the point under the camera, at its usual distance.
        private const float HeroWaist = 1f;         // Seeing this much of someone is seeing them.
        private const float LeastRockShare = 0.015f;// Of the land, in anything that isn't a marsh.
        private const float LeastRockSlope = 0.8f;  // Median, of bare rock. A smooth hill with rock painted on it comes in under 0.7.

        // What rock has to leave alone and what it has to be, measured from the finished ground and knowing nothing of how it was made.
        private void CheckMountains(WorldData world)
        {
            ScalarField reachable = world.Reachable;

            // Everything anyone uses stays as it was. Rock starts at the edge of the land, so only go by what's a little way in from it.
            int touched = 0;
            for (int z = 2; z < reachable.Depth - 2; z++)
            {
                for (int x = 2; x < reachable.Width - 2; x++)
                {
                    if (reachable[x, z] < 0.5f || reachable[x - 2, z] < 0.5f || reachable[x + 2, z] < 0.5f || reachable[x, z - 2] < 0.5f || reachable[x, z + 2] < 0.5f) continue;
                    if (world.Rock.Sample(reachable.WorldPosition(x, z)) > 0f) touched++;
                }
            }
            if (touched > 0) Problem($"rock has been sculpted into {touched} cells of the land people walk on");

            // Nobody can be hidden by the ground. A hero stands anywhere walkable, and the line from them to the camera has to clear everything on the way.
            int hidden = 0;
            Vector2 first = default;
            for (float z = 0f; z < world.Size; z += 6f)
            {
                for (float x = 0f; x < world.Size; x += 6f)
                {
                    Vector2 at = new Vector2(x, z);
                    // A hero's middle is never within their width of anything solid (CharacterMover), which is what passable ground keeps clear: against a wall that holds up ground behind
                    // it, as an arena's does, the strip a body can't stand on is the strip the ground behind would hide.
                    if (!world.Walkable.IsSet(at) || !world.Passable.IsSet(at)) continue;
                    float waist = world.Height.Sample(at) + HeroWaist;
                    for (float back = 1f; back <= CameraReach; back += 1f)
                    {
                        if (world.Height.Sample(at - new Vector2(0f, back)) <= waist + back * CameraRise) continue;
                        if (hidden++ == 0) first = at;
                        break;
                    }
                }
            }
            if (hidden > 0) Problem($"the ground hides a hero from the camera in {hidden} places, the first at {first}");

            // Whatever a world's mountains are, it has some, and they stand up.
            int land = 0;
            var slopes = new List<float>();
            for (float z = 0f; z < world.Size; z += 4f)
            {
                for (float x = 0f; x < world.Size; x += 4f)
                {
                    Vector2 at = new Vector2(x, z);
                    if (world.Height.Sample(at) <= WorldPlan.SeaLevel) continue;
                    land++;
                    if (world.Rock.Sample(at) > 0.9f) slopes.Add(world.Height.SlopeAt(at));
                }
            }
            float share = slopes.Count / (float)Math.Max(land, 1);
            // The Haven is a garden on a small isle, and needs none.
            // The Haven is a garden on a small isle, and an arena a bowl in level ground, and neither needs any.
            if (share < LeastRockShare && !world.Plan.Archetype.Enclosed) Problem($"only {share:P1} of the land is rock");
            if (slopes.Count > 0)
            {
                slopes.Sort();
                float median = slopes[slopes.Count / 2];
                if (median < LeastRockSlope) Problem($"the rock lies back at a median slope of {median:0.00}");
            }
        }

        // Worked out from the finished world, not from what PackStage meant to do. The numbers are what a player would feel: anything looser is a walk.
        private const float MostLandingWalk = 170f;     // Metres from the landing to the nearest pack.
        private const float LeastSpread = 0.5f;         // Share of the land with a pack near it.
        private const float PackReach = 48f;
        private const float MostPackRadius = 20f;
        private const float LeastPackGap = 40f;         // Centre to centre: closer and two packs can't be fought one at a time.

        private void CheckPacks(WorldData world)
        {
            if (world.Plan.Archetype.Enclosed) return;
            CheckLandPlaces(world);
            if (world.Plan.Request.Emptied) return;
            if (world.Haunts.Count == 0)
            {
                Problem("nothing to fight between the sites");
                return;
            }

            WorldSite landing = world.Sites[0];
            float nearest = float.PositiveInfinity;
            foreach (WorldSite haunt in world.Haunts)
            {
                nearest = MathF.Min(nearest, (haunt.Position - landing.Position).Length);
                if (haunt.Haunt == HauntKind.None) Problem($"a haunt at {haunt.Position} has no reason to be there");
                if (!haunt.Spawns.Any(s => s.Role.IsHostile())) Problem($"a haunt at {haunt.Position} has nobody in it");

                foreach (SiteSpawn spawn in haunt.Spawns)
                {
                    if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of a {haunt.Haunt} can't be reached at {spawn.Position}");
                    if (world.WaterDepthAt(spawn.Position) > 0.35f) Problem($"{spawn.Role} of a {haunt.Haunt} is standing in water at {spawn.Position}");
                    if ((spawn.Position - haunt.Position).Length > MostPackRadius * MathF.Sqrt(1f + world.Plan.Request.MorePeople)) Problem($"{spawn.Role} of a {haunt.Haunt} at {haunt.Position} has wandered off to {spawn.Position}");
                }

                foreach (WorldSite site in world.Sites)
                {
                    float margin = site.Kind == SiteKind.Landing ? 40f : site.Definition.IsSettlement ? 30f : 0f;
                    if (margin > 0f && haunt.Haunt != HauntKind.Camp && (haunt.Position - site.Position).Length < site.Radius + margin) Problem($"a {haunt.Haunt} at {haunt.Position} is on the doorstep of {site.Name}");
                }
            }
            if (nearest > MostLandingWalk) Problem($"the first pack is {nearest:0} m from the landing");

            // More packs have been paid for, and they stand closer. Anything closer than that is a fault as it always was.
            float leastGap = LeastPackGap * PackStage.Spacing(world.Plan.Level, world.Plan.Request.MorePacks) / PackStage.Spacing(world.Plan.Level);
            var packs = world.Haunts.Where(h => h.Haunt != HauntKind.Camp).ToList();
            for (int i = 0; i < packs.Count; i++)
            {
                for (int j = i + 1; j < packs.Count; j++)
                {
                    if ((packs[i].Position - packs[j].Position).LengthSquared < leastGap * leastGap) Problem($"packs at {packs[i].Position} and {packs[j].Position} can't be fought apart");
                }
            }

            // A place that's held is a fight as much as a pack is, and packs keep off its doorstep, so ground beside one has something to fight near it too. A smaller land is more of that.
            var held = world.Sites.Where(site => site.Spawns.Any(spawn => spawn.Role.IsHostile())).ToList();
            int land = 0;
            int near = 0;
            for (float z = 12f; z < world.Size; z += 24f)
            {
                for (float x = 12f; x < world.Size; x += 24f)
                {
                    var at = new Vector2(x, z);
                    if (!world.Passable.IsSet(at)) continue;
                    land++;
                    if (world.Haunts.Any(haunt => (haunt.Position - at).LengthSquared <= PackReach * PackReach) || held.Any(site => (site.Position - at).Length <= site.Radius + PackReach)) near++;
                }
            }
            if (land > 0 && (float)near / land < LeastSpread) Problem($"only {(float)near / land:0%} of the land has a pack near it");
        }

        // Whether what a spawn is there to use is really there, as a prompt with nothing to see is the worst a place can do.
        private const float MostSpawnStray = 8f;

        private static bool Stands(WorldData world, StructureDefinition definition, Vector2 spawn)
        {
            foreach (WorldObject item in world.Objects.All())
            {
                if (world.Objects.AssetIds[item.Asset] == definition.Id && (item.Position.Xz - spawn).Length < MostSpawnStray) return true;
            }
            return false;
        }

        private static bool HasChurch(WorldData world, WorldSite site)
        {
            foreach (WorldObject item in world.Objects.All())
            {
                if (world.Objects.AssetIds[item.Asset] == StructureCatalog.Church.Id && (item.Position.Xz - site.Position).Length < site.Radius) return true;
            }
            return false;
        }

        private void CheckBiomes(WorldData world)
        {
            // An arena is one bowl on a little level ground: its biomes are only what it's seen against.
            if (world.Plan.Archetype.Arena) return;
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

            Vector2 landing = world.Sites[0].Landing;
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
            // A keep's arena is sealed from the land, and reached by its way down: whoever can walk to the door is carried to its floor.
            foreach (WorldSite site in world.Sites)
            {
                if (site.Arena is not { Keep: true } arena || !AnyReachedNear(reached, size, arena.Door, Cell, 3f)) continue;
                int floor = NearestOpen(blocked, size, arena.Arrival, Cell, 6);
                if (floor < 0 || reached[floor]) continue;
                reached[floor] = true;
                stack.Push(floor);
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
                    // Those watching from an arena's stands are where nobody can reach them, on purpose.
                    if (spawn.Role == SpawnRole.Spectator) continue;
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
                if (id is "lantern.stone" or "cairn" or "signpost" or "cart" or "bench" or "shrine.wayside" or "milestone" or "stand.hunting" or "fossil" or "windmill" or "dig" or "pen.banner") landmarks.Add(item.Position.Xz);
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
