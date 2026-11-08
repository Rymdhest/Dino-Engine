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
                    if (spawn.Role is SpawnRole.MapTable or SpawnRole.Dummy or SpawnRole.BetaVendor) Problem($"{site.Name} has a {spawn.Role}, which only the Haven has");
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
                    if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of {site.Name} can't be reached at {spawn.Position}");
                }
            }
        }

        // The places a land can ask to be freed: there have to be more than the most it ever asks for.
        private const int LeastPlaces = 6;
        private const int Dummies = 21;

        private void CheckLandPlaces(WorldData world)
        {
            int places = world.Sites.Count(site => site.Kind != SiteKind.Keep && site.Spawns.Any(s => s.Role.IsHostile()));
            if (places < LeastPlaces) Problem($"only {places} places to free");
            int gates = world.Sites.Count(site => site.Spawns.Any(s => s.Role == SpawnRole.Waygate));
            if (gates < 3) Problem($"only {gates} waygates");
        }

        // The Haven: one village, everything it's for standing where it can be reached, and nobody hostile anywhere.
        private const int LeastSpectators = 24;

        // An arena is one bowl with its tyrant in the middle, gates round it with something standing in each, a way in from a waygate, and its people up in the stands, above the floor.
        private void CheckArena(WorldData world)
        {
            if (world.Sites.Count != 1 || world.Sites[0].Kind != SiteKind.Arena)
            {
                Problem("the arena is not one arena");
                return;
            }
            if (world.Haunts.Count > 0) Problem($"the arena has {world.Haunts.Count} packs in it");

            WorldSite arena = world.Sites[0];
            int Count(SpawnRole role) => arena.Spawns.Count(s => s.Role == role);
            if (Count(SpawnRole.Boss) != 1) Problem($"the arena has {Count(SpawnRole.Boss)} tyrants");
            if (Count(SpawnRole.Rival) != 1) Problem($"the arena has {Count(SpawnRole.Rival)} places for a second tyrant");
            if (arena.Spawns.Any(s => s.Role.IsHostile() && s.Role is not (SpawnRole.Boss or SpawnRole.Rival))) Problem("the arena has someone hostile in it who isn't a tyrant");

            SiteSpawn[] waygates = arena.Spawns.Where(s => s.Role == SpawnRole.Waygate).ToArray();
            if (waygates.Length != 1) Problem($"the arena has {waygates.Length} waygates");
            else if (!Stands(world, StructureCatalog.Waygate, waygates[0].Position)) Problem("the arena's waygate has nothing standing on it");

            SiteSpawn[] gates = arena.Spawns.Where(s => s.Role == SpawnRole.ArenaGate).ToArray();
            if (gates.Length != ArenaBuilder.GateBearings.Length) Problem($"the arena has {gates.Length} gates, not {ArenaBuilder.GateBearings.Length}");
            foreach (SiteSpawn gate in gates)
            {
                if (!world.Objects.TryNearest(StructureCatalog.ArenaGate.Id, gate.Position, 6f, out _)) Problem($"the arena's gate at {gate.Position} has nothing standing at it");
            }

            float floor = world.Height.Sample(arena.Position);
            if (world.WaterDepthAt(arena.Position) > 0f) Problem("the arena's floor is under water");
            SiteSpawn[] watching = arena.Spawns.Where(s => s.Role == SpawnRole.Spectator).ToArray();
            if (watching.Length < LeastSpectators) Problem($"only {watching.Length} watch from the arena's stands");
            int low = watching.Count(s => world.Height.Sample(s.Position) < floor + ArenaBuilder.TierRise * 0.6f);
            if (low > 0) Problem($"{low} of those watching aren't up in the stands");
            foreach (SiteSpawn spawn in arena.Spawns)
            {
                if (spawn.Role is SpawnRole.Spectator) continue;
                if (!world.Passable.IsSet(spawn.Position)) Problem($"{spawn.Role} of the arena can't be reached at {spawn.Position}");
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
                    if (!world.Walkable.IsSet(at)) continue;
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

            int land = 0;
            int near = 0;
            for (float z = 12f; z < world.Size; z += 24f)
            {
                for (float x = 12f; x < world.Size; x += 24f)
                {
                    var at = new Vector2(x, z);
                    if (!world.Passable.IsSet(at)) continue;
                    land++;
                    foreach (WorldSite haunt in world.Haunts)
                    {
                        if ((haunt.Position - at).LengthSquared > PackReach * PackReach) continue;
                        near++;
                        break;
                    }
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
