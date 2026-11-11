using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Finds where the world's places go. A candidate is scored for each kind of site with the interest function of Emilien et
    /// al. (2012): every criterion gives -1..1, a hard limit rules the place out, and the rest is a weighted sum, so a watchtower
    /// takes a hilltop and a farm good flat land without either being told where to go. Sites then take the best of a random
    /// sample rather than the best overall, which keeps two worlds from solving the same problem the same way.
    /// </summary>
    public sealed class SiteStage : IWorldStage
    {
        private const float CandidateSpacing = 28f;
        private const int SampleSize = 48;
        private const float MaxWalkableSlope = 0.62f;
        private const float BoundaryLimit = 0.3f;

        private struct Candidate
        {
            public Vector2 Position;
            public float Height;
            public float Inland;
            public float Slope;         // Steepest of the samples within 40 m.
            public float Domination;    // How far it stands above its surroundings, about -1..1.
            public float Fertility;     // Open farmland, 0..1.
            public float Remoteness;
        }

        public string Description => "Founding settlements";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("sites");

            BuildReachable(world);
            List<Candidate> candidates = GatherCandidates(world, random.Fork("candidates"));
            if (candidates.Count == 0) throw new InvalidOperationException("World has no usable land.");

            // The Haven is one village, and an arena one arena, where the island is broadest and the ground kindest, and nothing else.
            if (world.Plan.Archetype.Enclosed)
            {
                Candidate heart = PickHeart(world, candidates);
                BuildRemoteness(world, heart.Position);
                if (world.Plan.Archetype.Peaceful) AddSite(world, SiteCatalog.Haven, heart, random.Fork("haven"));
                else AddSite(world, SiteCatalog.Arena, heart, random.Fork("arena"));
                LevelSites(world);
                return;
            }

            Candidate landing = PickLanding(world, candidates, random.Fork("landing"));
            BuildRemoteness(world, landing.Position);
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                candidate.Remoteness = world.Remoteness.Sample(candidate.Position);
                candidates[i] = candidate;
            }

            AddSite(world, SiteCatalog.Landing, landing, random.Fork("landing"));

            float landSquareKilometres = world.ReachableArea / 1_000_000f;
            foreach (SiteDefinition definition in SiteCatalog.All)
            {
                if (definition.Kind == SiteKind.Landing) continue;

                WorldRandom kindRandom = random.Fork(definition.Label);
                int count = TargetCount(world.Plan.Archetype, definition, landSquareKilometres, kindRandom);
                int placed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (!TryPick(world, definition, candidates, kindRandom, out Candidate pick)) break;
                    AddSite(world, definition, pick, kindRandom);
                    placed++;
                }

                // A land too small for what has to be on it, such as its keep, looks again a little less fussily. Its own fork, so that no land that didn't need it changes.
                if (placed < definition.Limits.Min)
                {
                    WorldRandom again = kindRandom.Fork("relaxed");
                    SiteDefinition relaxed = definition.Relaxed();
                    for (int i = placed; i < definition.Limits.Min; i++)
                    {
                        if (!TryPick(world, relaxed, candidates, again, out Candidate pick)) break;
                        AddSite(world, definition, pick, again);
                    }
                }
            }

            MakeEnough(world, candidates, random.Fork("enough"));
            LevelSites(world);
        }

        /// <summary>The fewest places a land can ask to be freed, which is the most it ever asks for (the game's <c>LandTerms.MostPlaces</c>), and the fewest waygates to travel between.</summary>
        public const int LeastHeld = 5;
        public const int LeastWaygates = 2;

        private static readonly SiteDefinition[] HeldToFill = { SiteCatalog.Shrine, SiteCatalog.Ruins, SiteCatalog.BanditCamp, SiteCatalog.Watchtower };

        /// <summary>Whether a kind of place is held by the Reich or its like until it's freed: everything but where people still live, the waygates, the landing and the keep.</summary>
        public static bool IsHeld(SiteKind kind) => kind is not (SiteKind.Village or SiteKind.Waygate or SiteKind.Landing or SiteKind.Keep or SiteKind.Haven or SiteKind.Arena);

        // However little ground a land has, it has to have enough places to free and enough waygates. What's short is looked for again, as loosely as anything is, and twice as loosely after
        // that. Its own fork, so that a land that has enough doesn't change.
        private static void MakeEnough(WorldData world, List<Candidate> candidates, WorldRandom random)
        {
            int held = world.Sites.Count(site => IsHeld(site.Kind));
            for (int attempt = 0; held < LeastHeld && attempt < HeldToFill.Length * 2; attempt++)
            {
                SiteDefinition definition = HeldToFill[attempt % HeldToFill.Length];
                SiteDefinition loose = attempt < HeldToFill.Length ? definition.Relaxed() : definition.Relaxed().Relaxed();
                if (!TryPick(world, loose, candidates, random.Fork(attempt), out Candidate pick)) continue;
                AddSite(world, definition, pick, random.Fork(attempt));
                held++;
            }

            int gates = world.Sites.Count(site => site.Kind is SiteKind.Waygate or SiteKind.Village or SiteKind.Landing);
            for (int attempt = 0; gates < LeastWaygates && attempt < 2; attempt++)
            {
                SiteDefinition loose = attempt == 0 ? SiteCatalog.Waygate.Relaxed() : SiteCatalog.Waygate.Relaxed().Relaxed();
                if (!TryPick(world, loose, candidates, random.Fork("gate").Fork(attempt), out Candidate pick)) continue;
                AddSite(world, SiteCatalog.Waygate, pick, random.Fork("gate").Fork(attempt));
                gates++;
            }
        }

        private static int TargetCount(WorldArchetype archetype, SiteDefinition definition, float squareKilometres, WorldRandom random)
        {
            float multiplier = definition.IsSettlement ? archetype.Settlements : definition.IsDanger ? archetype.Dangers : archetype.Wilds;
            float expected = definition.PerSquareKilometre * squareKilometres * multiplier * random.Range(0.8f, 1.2f);
            return Math.Clamp((int)MathF.Round(expected), definition.Limits.Min, definition.Limits.Max);
        }

        // The largest connected stretch of land a character could walk. Anything else (an islet, a ledge) stays scenery.
        private static void BuildReachable(WorldData world)
        {
            ScalarField height = world.Elevation;
            int width = height.Width;
            int depth = height.Depth;
            bool[] open = new bool[width * depth];
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    open[i] = height.Data[i] > WorldPlan.SeaLevel + 0.2f
                        && height.SlopeAt(x, z) < MaxWalkableSlope
                        && world.Boundary.Data[i] < BoundaryLimit
                        && world.Massif.Data[i] < 0.5f;
                }
            }

            int[] component = new int[open.Length];
            var stack = new Stack<int>();
            int bestComponent = 0;
            int bestSize = 0;
            int next = 0;
            for (int start = 0; start < open.Length; start++)
            {
                if (!open[start] || component[start] != 0) continue;
                int id = ++next;
                int size = 0;
                component[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % width;
                    int z = i / width;
                    if (x > 0) Visit(i - 1);
                    if (x < width - 1) Visit(i + 1);
                    if (z > 0) Visit(i - width);
                    if (z < depth - 1) Visit(i + width);
                }
                if (size > bestSize)
                {
                    bestSize = size;
                    bestComponent = id;
                }

                void Visit(int neighbour)
                {
                    if (!open[neighbour] || component[neighbour] != 0) return;
                    component[neighbour] = id;
                    stack.Push(neighbour);
                }
            }

            ScalarField reachable = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            for (int i = 0; i < component.Length; i++)
            {
                if (component[i] == bestComponent && bestComponent != 0) reachable.Data[i] = 1f;
            }
            world.Reachable = reachable;
            world.ReachableArea = bestSize * WorldData.CoarseCell * WorldData.CoarseCell;
        }

        private static List<Candidate> GatherCandidates(WorldData world, WorldRandom random)
        {
            var candidates = new List<Candidate>();
            int steps = (int)(world.Size / CandidateSpacing);
            ScalarField height = world.Elevation;

            for (int z = 0; z < steps; z++)
            {
                for (int x = 0; x < steps; x++)
                {
                    Vector2 position = new Vector2(x + random.Range(0.15f, 0.85f), z + random.Range(0.15f, 0.85f)) * CandidateSpacing;
                    if (world.Reachable.Sample(position) < 0.99f) continue;

                    float h = height.Sample(position);
                    float steepest = height.SlopeAt(position);
                    float around = 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * (MathF.Tau / 8f);
                        Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        steepest = MathF.Max(steepest, height.SlopeAt(position + direction * 22f));
                        steepest = MathF.Max(steepest, height.SlopeAt(position + direction * 44f));
                        around += height.Sample(position + direction * 85f);
                    }

                    float forest = 0f;
                    for (int b = 0; b < world.BiomeWeights.Length; b++)
                    {
                        forest += world.Plan.Biomes[b].ForestCover * world.BiomeWeights[b].Sample(position);
                    }

                    candidates.Add(new Candidate
                    {
                        Position = position,
                        Height = h,
                        Inland = world.Inland.Sample(position),
                        Slope = steepest,
                        Domination = Math.Clamp((h - around / 8f) / 9f, -1f, 1f),
                        Fertility = (1f - forest) * (1f - Smoothstep(0.05f, 0.3f, steepest)),
                    });
                }
            }
            return candidates;
        }

        // Flat, open ground well in from the shore, near the middle of the land, with room all round for a village and its fields.
        private static Candidate PickHeart(WorldData world, List<Candidate> candidates)
        {
            Vector2 centre = new Vector2(world.Size * 0.5f);
            float bestScore = float.NegativeInfinity;
            Candidate best = candidates[0];
            foreach (Candidate candidate in candidates)
            {
                if (candidate.Inland < 70f) continue;
                float fromCentre = (candidate.Position - centre).Length / (world.Size * 0.5f);
                float score = -candidate.Slope * 6f - fromCentre * 1.5f - MathF.Abs(candidate.Domination) + Smoothstep(70f, 160f, candidate.Inland) * 0.5f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        // The landing sits at the edge of the land: a shore if there is one, or else the foot of the mountains.
        private static Candidate PickLanding(WorldData world, List<Candidate> candidates, WorldRandom random)
        {
            Vector2 centre = new Vector2(world.Size * 0.5f);
            float bestScore = float.NegativeInfinity;
            Candidate best = candidates[0];
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                if (candidate.Slope > SiteCatalog.Landing.MaxSlope || candidate.Inland < SiteCatalog.Landing.MinInland) continue;

                float fromCentre = (candidate.Position - centre).Length / (world.Size * 0.5f);
                float nearShore = 1f - Smoothstep(20f, 180f, candidate.Inland);
                float score = fromCentre + nearShore * 0.7f - candidate.Slope + random.Float() * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        // Walking distance from the landing, as a share of the longest walk there is. Worlds are read by how far you have
        // come, and straight-line distance would call the far side of a bay "close".
        private static void BuildRemoteness(WorldData world, Vector2 landing)
        {
            ScalarField reachable = world.Reachable;
            int width = reachable.Width;
            int depth = reachable.Depth;
            float[] distance = new float[width * depth];
            Array.Fill(distance, float.PositiveInfinity);

            Vector2i start = reachable.NearestNode(landing);
            var frontier = new PriorityQueue<int, float>();
            int startIndex = reachable.Index(start.X, start.Y);
            distance[startIndex] = 0f;
            frontier.Enqueue(startIndex, 0f);

            ReadOnlySpan<(int x, int z, float cost)> steps = new (int, int, float)[]
            {
                (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
                (1, 1, 1.4142f), (-1, 1, 1.4142f), (1, -1, 1.4142f), (-1, -1, 1.4142f),
            };

            float farthest = 1f;
            while (frontier.TryDequeue(out int current, out float priority))
            {
                if (priority > distance[current]) continue;
                farthest = MathF.Max(farthest, priority);
                int x = current % width;
                int z = current / width;
                foreach ((int dx, int dz, float cost) in steps)
                {
                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
                    int neighbour = nz * width + nx;
                    if (reachable.Data[neighbour] < 0.5f) continue;
                    float next = priority + cost;
                    if (next < distance[neighbour])
                    {
                        distance[neighbour] = next;
                        frontier.Enqueue(neighbour, next);
                    }
                }
            }

            ScalarField remoteness = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            for (int i = 0; i < distance.Length; i++)
            {
                remoteness.Data[i] = float.IsInfinity(distance[i]) ? 1f : distance[i] / farthest;
            }
            world.Remoteness = remoteness;
        }

        private static bool TryPick(WorldData world, SiteDefinition definition, List<Candidate> candidates, WorldRandom random, out Candidate pick)
        {
            pick = default;
            float bestInterest = 0f;
            int considered = 0;

            // Random order, stopping after SampleSize usable spots: this is where one world's map differs from the next.
            int total = candidates.Count;
            int offset = random.Int(total);
            int stride = StrideFor(total, random);
            for (int n = 0; n < total && considered < SampleSize; n++)
            {
                Candidate candidate = candidates[(int)((offset + (long)n * stride) % total)];
                float radius = definition.Radius.Max;
                if (!MeetsLimits(world, definition, candidate, radius)) continue;
                considered++;

                float interest = Interest(definition, candidate);
                if (interest > bestInterest)
                {
                    bestInterest = interest;
                    pick = candidate;
                }
            }
            return bestInterest > 0f;
        }

        // A stride that shares no factor with the count visits every candidate exactly once.
        private static int StrideFor(int total, WorldRandom random)
        {
            int stride = random.Int(1, Math.Max(1, total - 1));
            while (GreatestCommonDivisor(stride, total) != 1) stride++;
            return stride;
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0) (a, b) = (b, a % b);
            return a;
        }

        private static bool MeetsLimits(WorldData world, SiteDefinition definition, Candidate candidate, float radius)
        {
            if (candidate.Slope > definition.MaxSlope) return false;
            if (candidate.Inland < definition.MinInland || candidate.Inland > definition.MaxInland) return false;
            if (candidate.Remoteness < definition.Remoteness.Min || candidate.Remoteness > definition.Remoteness.Max) return false;

            foreach (WorldSite site in world.Sites)
            {
                float distance = (site.Position - candidate.Position).Length;
                if (distance < radius + site.Radius + definition.Margin) return false;
                if (site.Kind == definition.Kind && distance < definition.Spacing * world.Plan.Closeness) return false;
            }
            return true;
        }

        private static float Interest(SiteDefinition definition, Candidate candidate)
        {
            float flat = 1f - 2f * Saturate(candidate.Slope / MathF.Max(definition.MaxSlope, 0.01f));
            float water = 1f - 2f * Smoothstep(30f, 320f, candidate.Inland);
            float fertile = candidate.Fertility * 2f - 1f;

            // The aim of the remoteness range is its middle, so sites spread through it rather than bunching at one end.
            float aim = (definition.Remoteness.Min + definition.Remoteness.Max) * 0.5f;
            float span = MathF.Max(definition.Remoteness.Max - definition.Remoteness.Min, 0.1f);
            float remoteFit = 1f - MathF.Abs(candidate.Remoteness - aim) / span;

            float interest = definition.Flatness * flat
                + definition.HighGround * candidate.Domination * 2f
                + definition.WaterNearby * water
                + definition.Fertility * fertile
                + remoteFit * 0.4f;
            return MathF.Max(interest + 1.5f, 0.01f);
        }

        // A keep's arena is dug down from the ground its walls stand on, the deepest of them nearly eight metres, and its floor has to stay three clear of the sea: a keep on low ground stands on a
        // mound raised that far, which the land is eased up to, or the arena would stand out of a plateau the camp round it couldn't walk off (ARENAS.md).
        private const float KeepAboveSea = 13f;

        private static void AddSite(WorldData world, SiteDefinition definition, Candidate candidate, WorldRandom random)
        {
            int id = world.Sites.Count;
            world.Sites.Add(new WorldSite
            {
                Id = id,
                Definition = definition,
                Position = candidate.Position,
                Radius = random.Range(definition.Radius),
                Height = MathF.Max(candidate.Height, WorldPlan.SeaLevel + (definition.Kind == SiteKind.Keep ? KeepAboveSea : 1f)),
                Remoteness = world.Remoteness?.Sample(candidate.Position) ?? 0f,
                Biome = world.DominantBiome(candidate.Position),
                Name = NameFor(world, definition, random.Fork("name")),
            });
        }

        private static string NameFor(WorldData world, SiteDefinition definition, WorldRandom random)
        {
            int seed = world.Plan.Seed;
            switch (definition.Kind)
            {
                case SiteKind.Village:
                case SiteKind.Hamlet:
                case SiteKind.FishingHamlet:
                    return PlaceNames.Settlement(seed, random);
                case SiteKind.Farmstead:
                    return PlaceNames.Root(seed, random) + " Farm";
                case SiteKind.LumberCamp:
                    return PlaceNames.Wood(seed, random) + " Camp";
                case SiteKind.Watchtower:
                    return PlaceNames.Height(seed, random) + " Tower";
                case SiteKind.Shrine:
                    return "Shrine of " + PlaceNames.Root(seed, random);
                case SiteKind.Ruins:
                    return "Ruins of " + PlaceNames.Root(seed, random);
                case SiteKind.Graveyard:
                    return PlaceNames.Root(seed, random) + " Churchyard";
                case SiteKind.BanditCamp:
                    return PlaceNames.Wood(seed, random) + " Hideout";
                case SiteKind.StandingStones:
                    return "The Stones of " + PlaceNames.Root(seed, random);
                case SiteKind.Keep:
                    return "Fort " + PlaceNames.Root(seed, random);
                case SiteKind.Waygate:
                    return PlaceNames.Height(seed, random) + " Gate";
                case SiteKind.Haven:
                    return "The Haven";
                case SiteKind.Arena:
                    return "The Arena";
                default:
                    return "The Landing";
            }
        }

        // Eases the ground of each site towards one height, so that roads arriving later already meet it.
        private static void LevelSites(WorldData world)
        {
            ScalarField height = world.Elevation;
            foreach (WorldSite site in world.Sites)
            {
                float reach = site.Radius * 1.5f + 18f;
                int minX = Math.Max((int)((site.Position.X - reach) / height.CellSize), 0);
                int maxX = Math.Min((int)((site.Position.X + reach) / height.CellSize) + 1, height.Width - 1);
                int minZ = Math.Max((int)((site.Position.Y - reach) / height.CellSize), 0);
                int maxZ = Math.Min((int)((site.Position.Y + reach) / height.CellSize) + 1, height.Depth - 1);

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = (height.WorldPosition(x, z) - site.Position).Length;
                        float weight = 1f - Smoothstep(site.Radius * 0.9f, reach, distance);
                        if (weight <= 0f) continue;
                        int i = height.Index(x, z);
                        height.Data[i] = Lerp(height.Data[i], site.Height, weight * site.Definition.Levelling);
                    }
                }
            }
        }
    }
}
