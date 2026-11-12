using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Plants the world, following the structure of Horizon Zero Dawn's placement system. A density map says where forest
    /// stands; it is decoded into layers, placed largest first: canopy, young trees and shrubs along forest edges, then
    /// deadwood and boulders. Every layer checks the shared <see cref="PlacementIndex"/>, across the whole world at once, so
    /// no tree grows inside another and nothing stands on a road or in a house. The trees of a forest are older and larger
    /// towards its heart, and species grow in patches, because a real wood is not a uniform mix.
    /// Small plants aren't placed here: there are too many, so <see cref="WorldGroundCover"/> grows them as chunks stream in.
    /// </summary>
    public sealed class VegetationStage : IWorldStage
    {
        private const float CanopySpacing = 4f;
        private const float ShrubSpacing = 3.2f;
        private const float DeadwoodSpacing = 11f;
        private const float BoulderSpacing = 7f;
        private const float OutcropSpacing = 24f;
        private const float RunoutSpacing = 5f;
        private const float SolidFrom = 0.45f;  // Metres a stone stands out of the ground before anyone has to go round it.
        private const float LowSolidHeight = 1.6f;  // How tall a boulder or a fallen trunk is, for its width.
        private const float MinAboveWater = 0.55f;
        private const float MaxTreeSlope = 0.8f;

        public string Description => "Growing the forests";
        public float Cost => 3f;

        public void Run(WorldData world)
        {
            BuildCanopy(world);

            WorldRandom random = world.Random.Fork("vegetation");
            var patches = new WorldNoise(random.Fork("patches"));
            var objects = new WorldObjects(world.Size);
            world.Objects = objects;

            PlantOnPurpose(world, objects);
            PlantTrees(world, objects, random.Fork("trees"), patches);
            PlantShrubs(world, objects, random.Fork("shrubs"), patches);
            PlaceDeadwood(world, objects, random.Fork("deadwood"));
            PlaceBoulders(world, objects, random.Fork("boulders"), patches);
            PlaceOutcrops(world, objects, random.Fork("outcrops"));
            PlaceRunout(world, objects, random.Fork("runout"));
        }

        // The land's own rock coming up through it in companies, by the form its mountains take, wherever the land is rocky (GroundRock): low slabs in rows along the strike where
        // beds lie level, rounded boulders and a low tor among domes, a stone forest of little pinnacles where towers stand, small hoodoos where canyons are, column stumps where
        // the rock is basalt, slabs stood on edge where its beds are. None of it tall enough to hide anyone, and whatever stands out of the ground is solid.
        private static void PlaceOutcrops(WorldData world, WorldObjects objects, WorldRandom random)
        {
            GroundRock? ground = world.GroundRock;
            if (ground == null) return;
            Geology rock = world.Geology;
            Vector2 strike = new Vector2(MathF.Cos(rock.Strike), MathF.Sin(rock.Strike));
            Vector2 across = new Vector2(-strike.Y, strike.X);
            foreach (Vector2 seed in Candidates(world, OutcropSpacing, random))
            {
                float rocky = ground.Rockiness(seed.X, seed.Y);
                float chance = random.Float();
                if (chance >= rocky * 0.55f) continue;
                if (!GroundFits(world, seed, 0.5f)) continue;

                MountainForm form = MountainForms.At(world, seed.X, seed.Y).Strongest;
                int company = 3 + (int)(random.Float() * (4f + rocky * 6f));
                for (int i = 0; i < company; i++)
                {
                    Vector2 at = seed + strike * random.Range(-8f, 8f) + across * random.Range(-3f, 3f);
                    float pick = random.Float();
                    float size = random.Float();
                    float turn = random.Float();
                    float lean = random.Float();
                    if (!world.InBounds(at) || ground.Rockiness(at.X, at.Y) <= 0.02f || !GroundFits(world, at, 0.6f)) continue;
                    (RockForm piece, FloatRange scales) = PieceOf(form, pick);
                    TryOutcrop(world, objects, piece, scales.Lerp(size), at, form, strike, turn, lean);
                }
            }
        }

        // What a form's land has coming up through it, and how big, at the stone's own scale: never so tall that someone behind it is lost.
        private static (RockForm piece, FloatRange scales) PieceOf(MountainForm form, float pick) => form switch
        {
            MountainForm.Ledged => pick < 0.7f ? (RockForm.Slab, new FloatRange(0.7f, 1.3f)) : (RockForm.Block, new FloatRange(0.5f, 0.9f)),
            MountainForm.Jagged => pick < 0.6f ? (RockForm.Block, new FloatRange(0.5f, 1f)) : (RockForm.Boulder, new FloatRange(0.5f, 0.9f)),
            MountainForm.Towers => pick < 0.6f ? (RockForm.Spire, new FloatRange(0.07f, 0.13f)) : (RockForm.Block, new FloatRange(0.5f, 0.85f)),
            MountainForm.Domes => pick < 0.6f ? (RockForm.Boulder, new FloatRange(0.6f, 1.2f)) : pick < 0.78f ? (RockForm.Tor, new FloatRange(0.24f, 0.34f)) : (RockForm.Slab, new FloatRange(0.6f, 1.1f)),
            MountainForm.Canyons => pick < 0.4f ? (RockForm.Hoodoo, new FloatRange(0.16f, 0.28f)) : pick < 0.8f ? (RockForm.Slab, new FloatRange(0.7f, 1.2f)) : (RockForm.Block, new FloatRange(0.5f, 0.85f)),
            MountainForm.Columns => pick < 0.5f ? (RockForm.Columns, new FloatRange(0.2f, 0.3f)) : (RockForm.Block, new FloatRange(0.5f, 0.9f)),
            _ => pick < 0.7f ? (RockForm.Slab, new FloatRange(0.7f, 1.2f)) : pick < 0.85f ? (RockForm.Fin, new FloatRange(0.16f, 0.24f)) : (RockForm.Block, new FloatRange(0.5f, 0.85f)),
        };

        private static void TryOutcrop(WorldData world, WorldObjects objects, RockForm piece, float scale, Vector2 at, MountainForm form, Vector2 strike, float turn, float lean)
        {
            PlacementIndex placement = world.Placement;
            const PlacementLayer Avoid = PlacementLayer.Rock | PlacementLayer.Deadwood | PlacementLayer.Shrub | PlacementLayer.Tree | PlacementLayer.Built | PlacementLayer.Road;
            WorldAsset asset = NatureCatalog.RockOf(world.Geology.Kind, piece);
            float reach = MathF.Max(asset.Footprint * scale, 0.5f);
            Footprint footprint = Footprint.Circle(at, reach);
            if (placement.Overlaps(footprint, Avoid) || TouchesSolid(placement, at, reach)) return;

            // Slabs lie along the strike, nearly flat where beds lie level and stood up where they stand on end; anything else squares up to the strike.
            float yaw = MathF.Atan2(strike.X, strike.Y) + (turn - 0.5f) * 0.5f;
            Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
            float sink = 0.15f;
            if (piece == RockForm.Slab)
            {
                float tip = form == MountainForm.Blades ? 0.55f + lean * 0.45f : (lean - 0.5f) * 0.25f;
                rotation *= Quaternion.FromAxisAngle(Vector3.UnitZ, tip);
                sink = form == MountainForm.Blades ? 0.9f : 0.3f;
            }
            else if (piece is RockForm.Block or RockForm.Boulder)
            {
                rotation *= Quaternion.FromAxisAngle(Vector3.UnitX, (lean - 0.5f) * 0.4f);
                sink = 0.3f;
            }
            float top = RockForge.TopOf(piece) * scale - sink * scale;
            if (top > SolidFrom)
            {
                float solid = reach * 0.8f;
                if (placement.OverlapsSolid(Footprint.Circle(at, solid + WorldData.CharacterRadius))) return;
                placement.Add(Footprint.Circle(at, solid), PlacementLayer.None, solid: true, height: top);
            }
            placement.Add(footprint, PlacementLayer.Rock);
            float height = world.Height.Sample(at) - sink * scale;
            objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, at), new Vector3(at.X, height, at.Y), rotation, new Vector3(scale));
        }

        // What's come down off a rock wall and rolled out into the land at its foot: more the nearer the wall, a boulder or a block here and there, solid, and never on anything built.
        private static void PlaceRunout(WorldData world, WorldObjects objects, WorldRandom random)
        {
            GroundRock? ground = world.GroundRock;
            if (ground == null || world.RockNear == null || world.Plan.Archetype.Enclosed) return;
            PlacementIndex placement = world.Placement;
            const PlacementLayer Avoid = PlacementLayer.Rock | PlacementLayer.Deadwood | PlacementLayer.Shrub | PlacementLayer.Tree | PlacementLayer.Built | PlacementLayer.Road;
            RockKind kind = world.Geology.Kind;
            foreach (Vector2 point in Candidates(world, RunoutSpacing, random))
            {
                float pick = random.Float();
                float size = random.Float();
                float turn = random.Float();
                float near = world.RockNear.Sample(point);
                if (near > 12f || near < 1.5f) continue;
                if (pick >= (1f - Smoothstep(2f, 12f, near)) * 0.22f) continue;
                if (ground.RoomAt(point.X, point.Y) < 5f || world.Rock.Sample(point) > 0.4f || !GroundFits(world, point, 0.7f)) continue;

                WorldAsset asset = NatureCatalog.RockOf(kind, turn < 0.55f ? RockForm.Boulder : RockForm.Block);
                // Big ones roll furthest.
                float scale = asset.Scale.Lerp(size * size) * Lerp(0.6f, 1f, Smoothstep(1.5f, 10f, near));
                float reach = asset.Footprint * scale;
                Footprint footprint = Footprint.Circle(point, reach);
                if (placement.Overlaps(footprint, Avoid) || TouchesSolid(placement, point, reach)) continue;
                placement.Add(footprint, PlacementLayer.Rock);
                placement.Add(Footprint.Circle(point, asset.SolidRadius * scale * 0.85f), PlacementLayer.None, solid: true, height: asset.SolidRadius * scale * LowSolidHeight);
                Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, turn * MathF.Tau) * Quaternion.FromAxisAngle(Vector3.UnitX, (pick * 4.7f % 1f - 0.5f) * 0.7f);
                objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, world.Height.Sample(point) - 0.25f * scale, point.Y), rotation, new Vector3(scale));
            }
        }

        // How thickly trees stand everywhere: each biome's forest cover, shaped into woods and clearings by noise.
        private static void BuildCanopy(WorldData world)
        {
            var noise = new WorldNoise(world.Random.Fork("canopy"));
            ScalarField canopy = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            ScalarField clearing = BuildClearings(world);
            IReadOnlyList<BiomeDefinition> biomes = world.Plan.Biomes;

            // The picture behind the menu says where its woods are.
            Vista? vista = world.Plan.Archetype.Vista ? new Vista(world.Random.Fork("vista")) : null;
            canopy.Generate((x, z) =>
            {
                if (vista != null) return vista.Woods(x, z) * (1f - Smoothstep(0.55f, 0.95f, world.Elevation.SlopeAt(new Vector2(x, z))));
                float cover = 0f;
                float patchSize = 0f;
                for (int b = 0; b < biomes.Count; b++)
                {
                    float weight = world.BiomeWeights[b].Sample(x, z);
                    cover += biomes[b].ForestCover * weight;
                    patchSize += biomes[b].ForestPatchSize * weight;
                }
                if (cover <= 0.001f) return 0f;

                // Noise piles up around its middle; spreading it out makes a cover of 0.3 mean about 30% of the land.
                float value = Smoothstep(0.2f, 0.8f, noise.Fbm01(x, z, patchSize, 4));
                float wet = world.Flow.Sample(x, z) * 0.18f;
                float density = Smoothstep(1f - cover - 0.1f, 1f - cover + 0.14f, value + wet);

                density *= 1f - clearing.Sample(x, z);
                density *= 1f - Smoothstep(0.55f, 0.95f, world.Elevation.SlopeAt(new Vector2(x, z)));
                return density;
            });
            world.Canopy = canopy;
        }

        private static ScalarField BuildClearings(WorldData world)
        {
            ScalarField field = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            foreach (Clearing clearing in world.Clearings)
            {
                float reach = clearing.Radius + clearing.Falloff;
                int minX = Math.Max((int)((clearing.Centre.X - reach) / field.CellSize), 0);
                int maxX = Math.Min((int)((clearing.Centre.X + reach) / field.CellSize) + 1, field.Width - 1);
                int minZ = Math.Max((int)((clearing.Centre.Y - reach) / field.CellSize), 0);
                int maxZ = Math.Min((int)((clearing.Centre.Y + reach) / field.CellSize) + 1, field.Depth - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = (field.WorldPosition(x, z) - clearing.Centre).Length;
                        float value = 1f - Smoothstep(clearing.Radius, reach, distance);
                        field[x, z] = MathF.Max(field[x, z], value);
                    }
                }
            }
            return field;
        }

        // Every candidate point of a layer, in a random order: walking the grid in rows would let earlier rows crowd out later ones.
        private static List<Vector2> Candidates(WorldData world, float spacing, WorldRandom random)
        {
            int steps = (int)(world.Size / spacing);
            var points = new List<Vector2>(steps * steps);
            for (int z = 0; z < steps; z++)
            {
                for (int x = 0; x < steps; x++)
                {
                    points.Add(new Vector2(x + random.Float(), z + random.Float()) * spacing);
                }
            }
            random.Shuffle(points);
            return points;
        }

        private static bool GroundFits(WorldData world, Vector2 point, float maxSlope)
        {
            if (world.ShoreHeightAt(point) < MinAboveWater) return false;
            return world.Height.SlopeAt(point) <= maxSlope;
        }

        // The biome a plant at this spot belongs to: drawn by weight, so that across a border the species mingle.
        private static BiomeDefinition PickBiome(WorldData world, Vector2 point, WorldRandom random)
        {
            float pick = random.Float();
            for (int b = 0; b < world.BiomeWeights.Length; b++)
            {
                pick -= world.BiomeWeights[b].Sample(point);
                if (pick <= 0f) return world.Plan.Biomes[b];
            }
            return world.Plan.Biomes[^1];
        }

        // Species grow in stands: nearby trees read the same patch of noise, so they tend to draw the same entry.
        private static WorldAsset? PickSpecies(Species[] list, float patch, WorldRandom random)
        {
            if (list.Length == 0) return null;
            float total = 0f;
            foreach (Species species in list) total += species.Weight;
            float pick = (patch * 0.75f + random.Float() * 0.25f) * total;
            foreach (Species species in list)
            {
                pick -= species.Weight;
                if (pick <= 0f) return species.Asset;
            }
            return list[^1].Asset;
        }

        // What sites asked for, first, so that the woods grow around a garden and not through it. They keep to the same room as anything planted, so a tree too big for its spot is grown smaller,
        // and one that still won't fit isn't planted.
        private static void PlantOnPurpose(WorldData world, WorldObjects objects)
        {
            PlacementIndex placement = world.Placement;
            foreach (Planting planting in world.Plantings)
            {
                WorldAsset asset = planting.Asset;
                bool tree = asset.Kind == WorldAssetKind.Tree;
                PlacementLayer layer = tree ? PlacementLayer.Tree : PlacementLayer.Shrub;
                // A bed is planted on purpose, so what a site plants goes in one, though nothing wild does.
                const PlacementLayer Built = PlacementLayer.Built & ~PlacementLayer.Garden;
                PlacementLayer avoid = tree ? PlacementLayer.Tree | Built | PlacementLayer.Road : PlacementLayer.Shrub | PlacementLayer.Rock | Built | PlacementLayer.Road;
                if (!GroundFits(world, planting.Position, MaxTreeSlope)) continue;

                float scale = planting.Scale;
                if (placement.Overlaps(Footprint.Circle(planting.Position, asset.Footprint * scale), avoid)) scale = asset.Scale.Min;
                if (placement.Overlaps(Footprint.Circle(planting.Position, asset.Footprint * scale), avoid)) continue;

                placement.Add(Footprint.Circle(planting.Position, asset.Footprint * scale), layer);
                if (asset.SolidRadius > 0f) placement.Add(Footprint.Circle(planting.Position, asset.SolidRadius * scale), PlacementLayer.None, solid: true);
                AddPlant(world, objects, asset, planting.Position, scale, planting.Yaw, sink: tree ? 0.15f : 0.05f);
            }
        }

        private static void PlantTrees(WorldData world, WorldObjects objects, WorldRandom random, WorldNoise patches)
        {
            PlacementIndex placement = world.Placement;
            ScalarField canopy = world.Canopy;
            const PlacementLayer Avoid = PlacementLayer.Tree | PlacementLayer.Built | PlacementLayer.Road;

            foreach (Vector2 point in Candidates(world, CanopySpacing, random))
            {
                float density = canopy.Sample(point);
                BiomeDefinition biome = PickBiome(world, point, random);
                float chance = density * 0.95f + biome.LoneTrees * 0.03f;
                if (random.Float() >= chance) continue;
                if (!GroundFits(world, point, MaxTreeSlope)) continue;

                // Edges are where the forest thins and young trees reach for the light.
                bool edge = density < 0.55f;
                Species[] list = edge && biome.Edge.Length > 0 ? biome.Edge : biome.Canopy;
                float patch = Smoothstep(0.2f, 0.8f, patches.Fbm01(point.X, point.Y, 85f, 3));
                WorldAsset? asset = PickSpecies(list, patch, random);
                if (asset == null) continue;

                float maturity = Smoothstep(0.25f, 0.95f, density) * 0.7f + random.Float() * 0.3f;
                float scale = asset.Scale.Lerp(maturity);
                float footprint = asset.Footprint * scale * biome.TreeSpacing;

                if (placement.Overlaps(Footprint.Circle(point, footprint), Avoid)) continue;

                placement.Add(Footprint.Circle(point, footprint), PlacementLayer.Tree);
                placement.Add(Footprint.Circle(point, asset.SolidRadius * scale), PlacementLayer.None, solid: true);
                AddPlant(world, objects, asset, point, scale, random.Float() * MathF.Tau, sink: 0.15f);
            }
        }

        private static void PlantShrubs(WorldData world, WorldObjects objects, WorldRandom random, WorldNoise patches)
        {
            PlacementIndex placement = world.Placement;
            ScalarField canopy = world.Canopy;
            const PlacementLayer Avoid = PlacementLayer.Shrub | PlacementLayer.Rock | PlacementLayer.Built | PlacementLayer.Road;

            foreach (Vector2 point in Candidates(world, ShrubSpacing, random))
            {
                float density = canopy.Sample(point);
                BiomeDefinition biome = PickBiome(world, point, random);
                if (biome.Shrubs.Length == 0) continue;

                // Shrubs crowd the sunny rim of a wood, thin out under its canopy and dot the open ground.
                float rim = 1f - MathF.Abs(density - 0.4f) / 0.4f;
                float chance = biome.ShrubDensity * (0.012f + 0.2f * Saturate(rim) + 0.035f * density);
                if (random.Float() >= chance) continue;
                if (!GroundFits(world, point, 0.7f)) continue;

                float patch = Smoothstep(0.2f, 0.8f, patches.Fbm01(point.X + 400f, point.Y, 45f, 2));
                WorldAsset? asset = PickSpecies(biome.Shrubs, patch, random);
                if (asset == null) continue;

                float scale = random.Range(asset.Scale);
                Footprint footprint = Footprint.Circle(point, asset.Footprint * scale);
                if (placement.Overlaps(footprint, Avoid)) continue;
                // A shrub can stand near a trunk, but not on it.
                if (TouchesSolid(placement, point, asset.Footprint * scale * 0.5f)) continue;

                placement.Add(footprint, PlacementLayer.Shrub);
                AddPlant(world, objects, asset, point, scale, random.Float() * MathF.Tau, sink: 0.05f);
            }
        }

        private static void PlaceDeadwood(WorldData world, WorldObjects objects, WorldRandom random)
        {
            PlacementIndex placement = world.Placement;
            ScalarField canopy = world.Canopy;
            const PlacementLayer Avoid = PlacementLayer.Deadwood | PlacementLayer.Rock | PlacementLayer.Built | PlacementLayer.Road;

            foreach (Vector2 point in Candidates(world, DeadwoodSpacing, random))
            {
                float density = canopy.Sample(point);
                BiomeDefinition biome = PickBiome(world, point, random);
                if (biome.Deadwood.Length == 0) continue;
                if (random.Float() >= biome.DeadwoodDensity * (0.02f + 0.3f * density)) continue;
                if (!GroundFits(world, point, 0.6f)) continue;

                WorldAsset? asset = PickSpecies(biome.Deadwood, random.Float(), random);
                if (asset == null) continue;

                float scale = random.Range(asset.Scale);
                Footprint footprint = Footprint.Circle(point, asset.Footprint * scale);
                if (placement.Overlaps(footprint, Avoid)) continue;
                if (TouchesSolid(placement, point, asset.Footprint * scale)) continue;

                placement.Add(footprint, PlacementLayer.Deadwood);

                // The way it lies has to be known here, as a log is solid along all of itself and not just at its middle.
                float yaw = random.Float() * MathF.Tau;
                if (asset.SolidRadius > 0f)
                {
                    float height = asset.SolidRadius * scale * LowSolidHeight;
                    Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)) * (asset.SolidLength * scale * 0.5f);
                    Footprint solid = asset.SolidLength > 0f ? Footprint.Segment(point - along, point + along, asset.SolidRadius * scale) : Footprint.Circle(point, asset.SolidRadius * scale);
                    placement.Add(solid, PlacementLayer.None, solid: true, height: height);
                }
                AddPlant(world, objects, asset, point, scale, yaw, sink: 0.1f);
            }
        }

        private static void PlaceBoulders(WorldData world, WorldObjects objects, WorldRandom random, WorldNoise patches)
        {
            PlacementIndex placement = world.Placement;
            const PlacementLayer Avoid = PlacementLayer.Rock | PlacementLayer.Deadwood | PlacementLayer.Shrub | PlacementLayer.Built | PlacementLayer.Road;

            foreach (Vector2 point in Candidates(world, BoulderSpacing, random))
            {
                BiomeDefinition biome = PickBiome(world, point, random);
                if (biome.Boulders.Length == 0) continue;

                // Boulders come in outcrops, and more of them where the slope has been shedding rock.
                float slope = world.Height.SlopeAt(point);
                float outcrop = Smoothstep(0.62f, 0.82f, patches.Fbm01(point.X - 900f, point.Y + 300f, 60f, 3));
                float chance = biome.BoulderDensity * (0.004f + 0.3f * outcrop + 0.2f * Smoothstep(0.3f, 0.8f, slope));
                if (random.Float() >= chance) continue;
                if (world.Height.Sample(point) - WorldPlan.SeaLevel < -0.4f) continue;

                WorldAsset? asset = PickSpecies(biome.Boulders, random.Float(), random);
                if (asset == null) continue;
                asset = NatureCatalog.Resolve(asset, world.Geology.Kind);

                float scale = random.Range(asset.Scale) * (1f + outcrop * 0.5f);
                Footprint footprint = Footprint.Circle(point, asset.Footprint * scale);
                if (placement.Overlaps(footprint, Avoid)) continue;
                if (TouchesSolid(placement, point, asset.Footprint * scale)) continue;

                placement.Add(footprint, PlacementLayer.Rock);
                placement.Add(Footprint.Circle(point, asset.SolidRadius * scale * 0.85f), PlacementLayer.None, solid: true, height: asset.SolidRadius * scale * LowSolidHeight);

                float height = world.Height.Sample(point) - 0.25f * scale;
                Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, random.Float() * MathF.Tau);
                Vector3 size = new Vector3(scale, scale * random.Range(0.7f, 1.2f), scale);
                objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), rotation, size);
            }
        }

        private static bool TouchesSolid(PlacementIndex placement, Vector2 point, float radius)
        {
            return placement.OverlapsSolid(Footprint.Circle(point, radius));
        }

        private static void AddPlant(WorldData world, WorldObjects objects, WorldAsset asset, Vector2 point, float scale, float yaw, float sink)
        {
            float height = world.Height.Sample(point) - sink;
            Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
            objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), rotation, new Vector3(scale));
        }
    }
}
