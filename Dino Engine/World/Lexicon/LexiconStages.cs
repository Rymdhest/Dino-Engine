using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>
    /// The room's land, in place of a land's landform, erosion, mountains, climate and biomes: everything those leave for what comes after, worked out from the plan rather than drawn. A level
    /// floor, a beach and the sea, a hill where a place is cut into one, no mountains and no rock of its own, and its biomes where the plan says.
    /// </summary>
    internal sealed class LexiconLandStage : IWorldStage
    {
        public string Description => "Laying out the lexicon";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            LexiconPlan plan = LexiconLayout.Current;
            float size = world.Size;
            WorldRandom random = world.Random.Fork("lexicon");
            var ground = new LexiconGround(plan, random);

            ScalarField elevation = ScalarField.ForWorld(size, WorldData.CoarseCell);
            elevation.Generate(ground.Height);
            world.Elevation = elevation;
            world.Inland = ScalarField.DistanceTo(elevation.Width, elevation.Depth, WorldData.CoarseCell, i => elevation.Data[i] <= WorldPlan.SeaLevel);
            world.Flow = ScalarField.ForWorld(size, WorldData.CoarseCell);
            world.Boundary = ScalarField.ForWorld(size, WorldData.CoarseCell);
            world.Highland = ScalarField.ForWorld(size, WorldData.CoarseCell);
            world.Massif = ScalarField.ForWorld(size, WorldData.CoarseCell);
            world.Crests = ScalarField.ForWorld(size, WorldData.CoarseCell);
            world.Geology = Geology.Draw(world.Plan, random.Fork("rock"));
            world.Drainage = new Drainage();
            world.Drainage.Index(size);

            // Biomes and the climate they make, at the climate's cells.
            IReadOnlyList<BiomeDefinition> biomes = world.Plan.Biomes;
            var weights = new ScalarField[biomes.Count];
            for (int b = 0; b < weights.Length; b++) weights[b] = ScalarField.ForWorld(size, WorldData.ClimateCell);
            ScalarField temperature = ScalarField.ForWorld(size, WorldData.ClimateCell);
            ScalarField moisture = ScalarField.ForWorld(size, WorldData.ClimateCell);
            Span<float> here = stackalloc float[biomes.Count];
            for (int z = 0; z < temperature.Depth; z++)
            {
                for (int x = 0; x < temperature.Width; x++)
                {
                    Vector2 at = temperature.WorldPosition(x, z);
                    ground.Weigh(biomes, at.X, at.Y, here);
                    float warm = 0f, wet = 0f;
                    for (int b = 0; b < biomes.Count; b++)
                    {
                        weights[b][x, z] = here[b];
                        warm += biomes[b].Temperature * here[b];
                        wet += biomes[b].Moisture * here[b];
                    }
                    temperature[x, z] = warm;
                    moisture[x, z] = wet;
                }
            }
            world.BiomeWeights = weights;
            world.Temperature = temperature;
            world.Moisture = moisture;
        }
    }

    /// <summary>
    /// The room's places, in place of a land's choosing where they go: the landing where heroes arrive, and every place on its plot, each the least its kind is ever laid out in, its way in to
    /// the south, which is where the camera is. Then what a land's sites stage leaves: what can be walked to, how far everything is, and the ground under each place eased to its height. The
    /// floor under everything else is laid level here, before the places lay out their own pads on it. Last, what can be built on is cut back to the plots, so that whatever a place's builder
    /// lays out, a village's fields as far out as they go, a farm's fences, stays on its own plot and off the walks: anything it can't fit there, it leaves out, as it would in a crowded land.
    /// <see cref="Whole"/> keeps what was there for <see cref="LexiconExhibitStage"/> to put back.
    /// </summary>
    internal sealed class LexiconSiteStage : IWorldStage
    {
        private const float MaxWalkableSlope = 0.62f;
        private const float FloorMargin = 8f;
        private const float PlotInset = 2f;     // Metres in from a plot's edge that a place can build to.
        private const float LandingGround = 28f; // Metres round the landing that its own builder can use.

        public string Description => "Marking out the plots";
        public float Cost => 1f;

        /// <summary>Everything that can be walked to, before it's cut back to the plots for the places to be built in.</summary>
        public ScalarField? Whole { get; private set; }

        public void Run(WorldData world)
        {
            LexiconPlan plan = LexiconLayout.Current;
            BuildReachable(world, plan.Arrival);
            BuildRemoteness(world, plan.Arrival);

            // The landing first, as a land's is: everything walkable is flooded out from it.
            AddSite(world, SiteCatalog.Landing, plan.Arrival, plan.ArrivalRadius, "The Landing");
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Exhibit.Kind != LexiconKind.Place) continue;
                SiteDefinition definition = SiteCatalog.Get(exhibit.Exhibit.Place);
                AddSite(world, definition, exhibit.Centre, exhibit.Radius, exhibit.Exhibit.Name);
            }
            LevelSites(world);
            // What can be walked to again, now the ground under each place is eased to its height: the room's beach is so gentle that easing a place by the sea to its height moves the
            // water's edge out by tens of metres, and what's laid out along it would find none of the new ground walkable, as a fishing village's houses and racks did.
            BuildReachable(world, plan.Arrival);
            LayFloor(world, plan);
            Whole = world.Reachable;
            world.Reachable = OnlyPlots(world.Reachable, plan);
        }

        // What can be walked to, but only on a place's plot, a little in from its edges, or round the landing: a builder builds on nothing else (SiteBuilder.GroundFits).
        private static ScalarField OnlyPlots(ScalarField whole, LexiconPlan plan)
        {
            var plots = plan.Exhibits.Where(exhibit => exhibit.Exhibit.Kind == LexiconKind.Place).Select(exhibit => exhibit.Plot.Grow(-PlotInset)).ToList();
            var only = new ScalarField(whole.Width, whole.Depth, whole.CellSize);
            for (int z = 0; z < whole.Depth; z++)
            {
                for (int x = 0; x < whole.Width; x++)
                {
                    Vector2 at = whole.WorldPosition(x, z);
                    if ((at - plan.Arrival).Length <= LandingGround || plots.Any(plot => plot.Contains(at))) only[x, z] = whole[x, z];
                }
            }
            return only;
        }

        private static void AddSite(WorldData world, SiteDefinition definition, Vector2 position, float radius, string name)
        {
            int id = world.Sites.Count;
            world.Sites.Add(new WorldSite
            {
                Id = id,
                Definition = definition,
                Position = position,
                Radius = radius,
                Height = MathF.Max(world.Elevation.Sample(position), WorldPlan.SeaLevel + 1f),
                Remoteness = world.Remoteness.Sample(position),
                Biome = world.DominantBiome(position),
                // Which way each place's story goes is the room's, so it's the same every visit.
                Story = (uint)new WorldRandom(LexiconWorld.Seed).Fork("story").Fork(definition.Kind.ToString()).NextULong(),
                Name = name,
                // Every place faces the camera: its way in, its gate and its front are to the south, as they would be with its road coming from there.
                RoadDirection = -Vector2.UnitY,
            });
        }

        // Level ground under all of it, the whole floor of the isle inside its beach, but for a hillside, which is the point of it: a land's ground has a roll to it, which would leave
        // puddles between the plots. The places' own pads go down after these, so whatever a place shapes is its own.
        private static void LayFloor(WorldData world, LexiconPlan plan)
        {
            var pieces = new List<GroundBox> { plan.Island.Grow(-LexiconPlan.Beach) };
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Diorama != Diorama.Hill) continue;
                // Where the hill rises from nothing to nothing again (LexiconGround.Hill): the floor round it meets it where it's level.
                GroundBox plot = exhibit.Plot;
                var hill = new GroundBox(new Vector2(plot.Min.X + 4f, exhibit.Centre.Y), new Vector2(plot.Max.X - 4f, plot.Max.Y - 4f));
                pieces = pieces.SelectMany(piece => Without(piece, hill)).ToList();
            }
            foreach (GroundBox piece in pieces) Pad(world, piece);
        }

        // What's left of a box once another is taken out of it: up to four boxes, south, north, west and east of the hole.
        private static IEnumerable<GroundBox> Without(GroundBox box, GroundBox hole)
        {
            Vector2 min = Vector2.ComponentMax(box.Min, hole.Min), max = Vector2.ComponentMin(box.Max, hole.Max);
            if (min.X >= max.X || min.Y >= max.Y)
            {
                yield return box;
                yield break;
            }
            if (min.Y > box.Min.Y) yield return new GroundBox(box.Min, new Vector2(box.Max.X, min.Y));
            if (max.Y < box.Max.Y) yield return new GroundBox(new Vector2(box.Min.X, max.Y), box.Max);
            if (min.X > box.Min.X) yield return new GroundBox(new Vector2(box.Min.X, min.Y), new Vector2(min.X, max.Y));
            if (max.X < box.Max.X) yield return new GroundBox(new Vector2(max.X, min.Y), new Vector2(box.Max.X, max.Y));
        }

        private static void Pad(WorldData world, GroundBox box)
        {
            world.Pads.Add(new GroundPad(Footprint.Box(box.Centre, box.Half, 0f), LexiconPlan.Ground, FloorMargin));
        }

        // As a land's sites stage does: the largest stretch of land that can be walked, flooded out from where heroes arrive.
        private static void BuildReachable(WorldData world, Vector2 from)
        {
            ScalarField height = world.Elevation;
            int width = height.Width, depth = height.Depth;
            var reachable = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            bool Open(int i) => height.Data[i] > WorldPlan.SeaLevel + 0.2f && height.SlopeAt(i % width, i / width) < MaxWalkableSlope;

            int startX = Math.Clamp((int)MathF.Round(from.X / WorldData.CoarseCell), 0, width - 1);
            int startZ = Math.Clamp((int)MathF.Round(from.Y / WorldData.CoarseCell), 0, depth - 1);
            var stack = new Stack<int>();
            int start = startZ * width + startX;
            int area = 0;
            if (Open(start))
            {
                reachable.Data[start] = 1f;
                stack.Push(start);
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                area++;
                int x = i % width, z = i / width;
                if (x > 0) Visit(i - 1);
                if (x < width - 1) Visit(i + 1);
                if (z > 0) Visit(i - width);
                if (z < depth - 1) Visit(i + width);
            }
            void Visit(int next)
            {
                if (reachable.Data[next] > 0f || !Open(next)) return;
                reachable.Data[next] = 1f;
                stack.Push(next);
            }
            world.Reachable = reachable;
            world.ReachableArea = area * WorldData.CoarseCell * WorldData.CoarseCell;
        }

        // How far from the landing, as a share of the farthest anyone can get. The room is open and level, so as the crow flies is as good as walking.
        private static void BuildRemoteness(WorldData world, Vector2 from)
        {
            ScalarField reachable = world.Reachable;
            float farthest = 1f;
            for (int z = 0; z < reachable.Depth; z++)
            {
                for (int x = 0; x < reachable.Width; x++)
                {
                    if (reachable[x, z] > 0.5f) farthest = MathF.Max(farthest, (reachable.WorldPosition(x, z) - from).Length);
                }
            }
            var remoteness = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            remoteness.Generate((x, z) => Saturate((new Vector2(x, z) - from).Length / farthest));
            world.Remoteness = remoteness;
        }

        // As a land's sites stage does: the ground of each place eased towards its height, as far as its kind levels it, so that what it lays out sits on it.
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

    /// <summary>
    /// What the room lays out once its places are built: nobody hostile anywhere, as nothing here is to be fought; the ground under every plot, gravel for what walks and tended beds for what
    /// grows, with a paved walk along the front of every row where its plaques are; and the woods a place needs round it, planted as a site plants its trees.
    /// </summary>
    internal sealed class LexiconExhibitStage : IWorldStage
    {
        private const float WoodSpacing = 6.5f;

        private readonly LexiconSiteStage _sites;

        public LexiconExhibitStage(LexiconSiteStage sites) => _sites = sites;

        public string Description => "Setting out the exhibits";
        public float Cost => 0.5f;

        public void Run(WorldData world)
        {
            LexiconPlan plan = LexiconLayout.Current;

            // The places are built, so the whole floor can be walked again, and is what the map and everything after know as the land.
            if (_sites.Whole != null) world.Reachable = _sites.Whole;

            // A place in the room is only looked at: whoever would hold it in a land stays away, and with them anything that counts it as held. What it shows of itself stays, a beast in its
            // cage, a merchant at a stall, a chest, a shrine's altar, a gate.
            foreach (WorldSite site in world.Sites) site.Spawns.RemoveAll(spawn => spawn.Role.IsHostile());

            foreach (GroundBox walk in plan.Walks) world.Paint.Add(new PaintPatch(GroundPaint.Paving, Footprint.Box(walk.Centre, walk.Half, 0f), 0.6f));
            foreach (GroundBox forecourt in plan.Forecourts) world.Paint.Add(new PaintPatch(GroundPaint.Paving, Footprint.Box(forecourt.Centre, forecourt.Half, 0f), 0.8f));
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                ExhibitGround ground = exhibit.Section.Section.Ground;
                if (exhibit.Exhibit.Kind == LexiconKind.Place || ground == ExhibitGround.Grass) continue;
                Vector2 half = exhibit.Plot.Half - new Vector2(0.5f);
                if (ground == ExhibitGround.Gravel) world.Paint.Add(new PaintPatch(GroundPaint.Plaza, Footprint.Box(exhibit.Plot.Centre, half, 0f), 0.8f));
                else world.Paint.Add(new PaintPatch(GroundPaint.Garden, Footprint.Circle(exhibit.Centre, MathF.Min(half.X, half.Y)), 1.2f));
            }

            PlantWoods(world, plan);
        }

        // A wood behind a logging camp and round a nesting ground, of the trees its biome grows, thick and uneven as a real one is, planted where nothing stands. They're planted with what
        // the places plant, which keeps them clear of anything built.
        private static void PlantWoods(WorldData world, LexiconPlan plan)
        {
            BiomeDefinition biome = LexiconWorld.Woods;
            if (biome.Canopy.Length == 0) return;
            float total = biome.Canopy.Sum(species => species.Weight);
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Diorama is not (Diorama.WoodsBehind or Diorama.WoodsAround)) continue;
                var random = new WorldRandom(LexiconWorld.Seed).Fork("woods").Fork(exhibit.Exhibit.Key);
                for (float z = exhibit.Plot.Min.Y + 2f; z < exhibit.Plot.Max.Y - 2f; z += WoodSpacing)
                {
                    for (float x = exhibit.Plot.Min.X + 2f; x < exhibit.Plot.Max.X - 2f; x += WoodSpacing)
                    {
                        Vector2 at = new Vector2(x, z) + new Vector2(random.Range(-0.45f, 0.45f), random.Range(-0.45f, 0.45f)) * WoodSpacing;
                        float pick = random.Float() * total;
                        float maturity = random.Float();
                        float yaw = random.Float() * MathF.Tau;
                        float wood = LexiconGround.InWood(exhibit, at);
                        if (wood < 0.5f || !exhibit.Plot.Contains(at, -1f)) continue;
                        WorldAsset asset = biome.Canopy[^1].Asset;
                        foreach (Species species in biome.Canopy)
                        {
                            pick -= species.Weight;
                            if (pick > 0f) continue;
                            asset = species.Asset;
                            break;
                        }
                        // Older and bigger towards the heart of the wood, as anywhere.
                        world.Plantings.Add(new Planting(asset, at, asset.Scale.Lerp(0.35f + 0.5f * wood * maturity + 0.15f * maturity), yaw));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The room's plants, in place of a land's woods: how thick the woods are, which only a place's own wood has, then every plant and stone on show, each exactly where its plot is, and then
    /// whatever the places planted and the woods round them, as a land plants what's put there on purpose. Nothing grows wild.
    /// </summary>
    internal sealed class LexiconPlantStage : IWorldStage
    {
        public string Description => "Planting the specimens";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            LexiconPlan plan = LexiconLayout.Current;
            var ground = new LexiconGround(plan, world.Random.Fork("lexicon"));
            ScalarField canopy = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            canopy.Generate(ground.Woods);
            world.Canopy = canopy;

            var objects = new WorldObjects(world.Size);
            world.Objects = objects;
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Exhibit.Kind == LexiconKind.Asset) PlantSpecimen(world, objects, exhibit);
            }
            VegetationStage.PlantOnPurpose(world, objects);
        }

        private const float LyingLift = 0.2f;   // Metres over the ground as it's reckoned that a card that lies on it is laid, to be over it as it's drawn.

        // One plant or stone on show, as its shelf sized its plot for: the first of its kind the room grows, at the size it says, with room kept round it so nothing planted after comes near.
        private static void PlantSpecimen(WorldData world, WorldObjects objects, PlacedExhibit exhibit)
        {
            if (!NatureCatalog.TryGet(exhibit.Exhibit.Look, out WorldAsset asset))
            {
                Console.WriteLine($"The lexicon has no asset called {exhibit.Exhibit.Look}.");
                return;
            }
            float scale = exhibit.Exhibit.Scale;
            bool tree = asset.Kind == WorldAssetKind.Tree;
            world.Placement.Add(Footprint.Circle(exhibit.Centre, MathF.Max(asset.Footprint * scale, 0.5f)), tree ? PlacementLayer.Tree : PlacementLayer.Shrub);
            if (asset.SolidRadius > 0f) world.Placement.Add(Footprint.Circle(exhibit.Centre, asset.SolidRadius * scale), PlacementLayer.None, solid: true);

            // Sunk as far as a land sinks each, but what lies flat on the ground, leaves and moss: the ground is drawn a little over where it's reckoned to be, which buries a card
            // laid as a land lays one (WorldGroundCover, 2 cm into it), so it's laid on top where it can be seen. Whether it shows in a wood is worth a look (LEXICON.md).
            bool lying = FloraCatalog.TryGet(exhibit.Exhibit.Look, out PlantSpecies species) && species.Form is GroundForm;
            float sink = asset.Kind == WorldAssetKind.Rock ? 0.2f * scale : tree ? 0.15f : lying ? -LyingLift : 0.05f;
            float height = world.Height.Sample(exhibit.Centre) - sink;
            Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, exhibit.Exhibit.Yaw);
            objects.Add(LexiconAssets.ModelOf(asset), new Vector3(exhibit.Centre.X, height, exhibit.Centre.Y), rotation, new Vector3(scale));
        }
    }

    /// <summary>Which model of an asset the room shows: always the first the room grows of it, so that its shelf can measure it before it's planted, and it's the same on every visit.</summary>
    public static class LexiconAssets
    {
        public static string ModelOf(WorldAsset asset) => asset.Variants <= 1 ? asset.Id : NatureCatalog.Variant(asset, LexiconWorld.Seed, 0);
    }
}
