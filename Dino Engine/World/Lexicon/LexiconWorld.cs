using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>
    /// The asset lexicon (LEXICON.md): a developer's room, reached only from the beta vendor in the Haven, where one of everything the game has stands on a flat isle in labelled rows, so that
    /// anything can be found and looked at from the game's own camera. It's a world like any other to the game, grown from a request on both ends, but it's laid out rather than drawn
    /// (<see cref="LexiconLayout"/>), from stages of its own (<see cref="Stages"/>). Everything about it is in this folder and the game's, and nothing else depends on it: take both away, and the
    /// lines marked "Lexicon" where the game reaches it, and the game is as it was.
    /// </summary>
    public static class LexiconWorld
    {
        public const string Id = "lexicon";

        /// <summary>The room's seed, which only moves its noise and which plant of a kind each specimen is: everything that matters is laid out.</summary>
        public const int Seed = 5170;

        /// <summary>What grows the room, which both ends of a connection ask for.</summary>
        public static WorldRequest Request => new WorldRequest(Seed, Id, 1);

        public static bool Is(in WorldRequest request) => request.ArchetypeId == Id;

        public static bool Is(WorldPlan plan) => plan.Archetype.Lexicon;

        /// <summary>What the room is made of, which every stone in it is shaded by.</summary>
        public const RockKind Rock = RockKind.Granite;

        /// <summary>What's said of the room on the way in, and in the chronicle.</summary>
        public const string Prologue = "A developer's room, and nothing to fight in it: one of everything the game has, each on its own plot with its name on a plaque, the small things to the west and the places to the east. The index lists everything and walks you to it.";

        /// <summary>
        /// The room's own ground: a short lawn with nothing wild on it, no trees, shrubs, stones, flowers or animals, so that nothing stands on it that isn't on show. Never drawn for a land, as it's in
        /// no archetype's list but this room's.
        /// </summary>
        public static readonly BiomeDefinition Floor = new BiomeDefinition
        {
            Id = "lexicon",
            Name = "Lexicon lawn",
            MapColour = new Vector3(0.5f, 0.62f, 0.34f),
            Temperature = 0.55f,
            Moisture = 0.5f,
            Mountains = MountainForm.Ledged,
            Bedrock = Rock,
            Ground = SurfaceMaterial.Grass,
            Understory = SurfaceMaterial.Soil,
            Slope = SurfaceMaterial.Rock,
            Shore = SurfaceMaterial.Sand,
            Path = SurfaceMaterial.Dirt,
            GrassCover = 0.95f,
            GrassHeight = 0.32f,    // Short enough that the smallest thing on show stands out of it, and long enough not to read as stubble from up here.
            GrassColour = new Vector3(0.15f, 0.27f, 0.06f),
            GrassDryness = 0.06f,
            ForestCover = 0f,
            LoneTrees = 0f,
            ShrubDensity = 0f,
            DeadwoodDensity = 0f,
            BoulderDensity = 0f,
            FloorDensity = 0f,
            OpenDensity = 0f,
            FogDensity = 0.004f,
            WindStrength = 0.7f,
            Motes = AmbientMotes.None,
        };

        /// <summary>The wood a place that needs one stands in or by: a pinewood, the commonest timber a land has.</summary>
        public static BiomeDefinition Woods => BiomeCatalog.PineForest;

        private static readonly Lazy<WorldArchetype> _archetype = new(Make, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>The room as a kind of world, sized to fit whatever the catalogue holds. Never drawn from a seed, as it isn't in <see cref="ArchetypeCatalog.All"/>.</summary>
        public static WorldArchetype Archetype => _archetype.Value;

        /// <summary>Every biome the room has: its lawn, and whatever its places need, in a fixed order. A plan holds them in the order it drew them, so read their indices from the plan.</summary>
        public static IReadOnlyList<BiomeDefinition> Biomes(LexiconPlan plan)
        {
            var biomes = new List<BiomeDefinition> { Floor };
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                BiomeDefinition? wanted = exhibit.Diorama switch
                {
                    Diorama.Snow => LexiconDioramas.ClimateOf(exhibit.Exhibit.Place),
                    Diorama.WoodsBehind or Diorama.WoodsAround => Woods,
                    _ => null,
                };
                if (wanted != null && !biomes.Contains(wanted)) biomes.Add(wanted);
            }
            return biomes;
        }

        private static WorldArchetype Make()
        {
            LexiconPlan plan = LexiconLayout.Current;
            BiomeDefinition[] biomes = Biomes(plan).ToArray();
            return new WorldArchetype
            {
                Id = Id,
                Name = "Asset Lexicon",
                Titles = new[] { "The Asset Lexicon" },
                Landform = LandformKind.Island,
                Size = new FloatRange(plan.Size, plan.Size),
                BaseHeight = new FloatRange(LexiconPlan.Ground, LexiconPlan.Ground),
                HillHeight = new FloatRange(0f, 0f),
                MountainHeight = new FloatRange(0f, 0f),
                MountainCoverage = new FloatRange(0f, 0f),
                Cliffs = new FloatRange(0f, 0f),
                Erosion = new FloatRange(0f, 0f),
                Bedrock = Rock,
                Biomes = biomes,
                BiomeCount = new IntRange(biomes.Length, biomes.Length),
                Settlements = 0f,
                Wilds = 0f,
                Dangers = 0f,
                Lexicon = true,
                // A clear morning, the sun high behind the camera's left, as the picture behind the menu has it: everything lit, and its shadow falling away from the camera.
                Light = "Clear morning",
                SunAzimuth = 5.75f,
                SunHeight = 0.62f,
                Weather = new WeatherSample { Gale = 0.6f },
            };
        }

        private const float StandRoom = 1f;     // Metres someone sent to look at something keeps from anything built or set down.
        private const float StandStep = 1.5f, StandSearch = 24f;   // How the game looks for ground round a point (GameServer.TryFindGround).

        /// <summary>Metres south of the middle of what a place has built that whoever's sent to it is stood: the camera sees further ahead than behind, so most of it's in view.</summary>
        public const float PlaceView = 6f;

        /// <summary>
        /// Whether someone the index sends somewhere can be stood here: open ground, and a step clear of anything built, set down, fenced or sown, as the middle of a village has a well in
        /// it that isn't solid.
        /// </summary>
        public static bool Standable(WorldData world, Vector2 at) =>
            world.Passable.IsSet(at) && !world.Placement.Overlaps(Footprint.Circle(at, StandRoom), PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Field);

        /// <summary>
        /// Where the game stands someone the index sends to a point (LexiconServer), or null if there's nowhere near: the nearest <see cref="Standable"/> ground, looked for as the game
        /// looks for ground anywhere. A point on a place's plot means the place, and is moved first to a little south of the middle of what its builder put down, which only the grown
        /// world knows, as a fishing village is laid out along its shore wherever the water is. The check holds every exhibit to there being somewhere.
        /// </summary>
        public static Vector2? StandFor(WorldData world, Vector2 asked)
        {
            PlacedExhibit? place = LexiconLayout.Current.Exhibits.FirstOrDefault(exhibit => exhibit.Exhibit.Kind == LexiconKind.Place && exhibit.Plot.Contains(asked));
            Vector2 near = place != null ? BuiltMiddle(world, place) - new Vector2(0f, PlaceView) : asked;
            if (Standable(world, near)) return near;
            for (float radius = StandStep; radius <= StandSearch; radius += StandStep)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * (MathF.Tau / 16f);
                    Vector2 at = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (world.InBounds(at) && Standable(world, at)) return at;
                }
            }
            return null;
        }

        // The middle of what a place's builder put down on its plot: its buildings, its pieces, its fences and fields, but not what grows, as a plant's model is one of its kind's,
        // which '#' marks, and the wood round a place isn't the place.
        private static Vector2 BuiltMiddle(WorldData world, PlacedExhibit place)
        {
            Vector2 sum = Vector2.Zero;
            int count = 0;
            foreach (WorldObject item in world.Objects.All())
            {
                Vector2 at = item.Position.Xz;
                if (!place.Plot.Contains(at) || world.Objects.AssetIds[item.Asset].Contains('#')) continue;
                sum += at;
                count++;
            }
            return count > 0 ? sum / count : place.Centre;
        }

        /// <summary>
        /// What grows the room: its own stages where a land's would draw, and a land's own wherever what's laid out is built, baked, watered and settled as anywhere. While the places are
        /// built only their own plots can be built on (<see cref="LexiconSiteStage"/>), and the exhibits' stage after puts the rest of the floor back.
        /// </summary>
        public static List<IWorldStage> Stages()
        {
            var sites = new LexiconSiteStage();
            return new List<IWorldStage>
            {
                new LexiconLandStage(),
                sites,
                new SettlementStage(),
                new LexiconExhibitStage(sites),
                new TerrainBakeStage(),
                new WaterStage(),
                new LexiconPlantStage(),
                new FinalizeStage(),
            };
        }
    }

    /// <summary>
    /// The shape of the room's ground, worked out from the plan: a level floor <see cref="LexiconPlan.Ground"/> above the sea, a beach all round it and the sea beyond, a hillside behind each place
    /// that's cut into one, and where each biome is: the lawn, and the snow or the wood round each place that needs one. Headless, and the same on any thread.
    /// </summary>
    internal sealed class LexiconGround
    {
        private const float CornerRadius = 56f;
        private const float HillRise = 17f;         // Metres a hillside climbs behind its place, before the place's own levelling takes some of it back.
        private const float BiomeFade = 12f;        // Metres in from a plot's edge over which its biome comes in.

        private readonly LexiconPlan _plan;
        private readonly WorldNoise _noise;
        private readonly List<PlacedExhibit> _hills = new();
        private readonly List<(PlacedExhibit Exhibit, BiomeDefinition Biome)> _patches = new();

        public LexiconGround(LexiconPlan plan, WorldRandom random)
        {
            _plan = plan;
            _noise = new WorldNoise(random.Fork("lexicon ground"));
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Diorama == Diorama.Hill) _hills.Add(exhibit);
                BiomeDefinition? biome = exhibit.Diorama switch
                {
                    Diorama.Snow => LexiconDioramas.ClimateOf(exhibit.Exhibit.Place),
                    Diorama.WoodsBehind or Diorama.WoodsAround => LexiconWorld.Woods,
                    _ => null,
                };
                if (biome != null) _patches.Add((exhibit, biome));
            }
        }

        /// <summary>Metres inside the water's edge, less than nothing out at sea, along a shore that wanders a little, as a real one does.</summary>
        public float Inland(float x, float z)
        {
            GroundBox island = _plan.Island;
            Vector2 half = island.Half - new Vector2(CornerRadius);
            Vector2 local = new Vector2(MathF.Abs(x - island.Centre.X), MathF.Abs(z - island.Centre.Y)) - half;
            float outside = new Vector2(MathF.Max(local.X, 0f), MathF.Max(local.Y, 0f)).Length + MathF.Min(MathF.Max(local.X, local.Y), 0f) - CornerRadius;
            return -outside + _noise.Fbm(x + 311f, z - 97f, 140f, 3) * 3f;
        }

        public float Height(float x, float z)
        {
            float inland = Inland(x, z);
            if (inland < 0f) return -0.15f - MathF.Min(-inland * 0.07f, 26f);
            float height = Lerp(0.04f * inland, LexiconPlan.Ground, Smoothstep(0f, LexiconPlan.Beach, inland));
            foreach (PlacedExhibit hill in _hills) height += Hill(hill, x, z);
            return height;
        }

        // A hillside behind a place, rising from a little north of its middle to the back of its plot, and falling away again at its sides and back, so that it's a hill and not a step.
        private static float Hill(PlacedExhibit exhibit, float x, float z)
        {
            GroundBox plot = exhibit.Plot;
            if (!plot.Contains(new Vector2(x, z))) return 0f;
            float north = z - exhibit.Centre.Y;
            float start = exhibit.Radius * 0.35f;
            float rise = Smoothstep(start, start + 34f, north);
            float back = 1f - Smoothstep(plot.Max.Y - 30f, plot.Max.Y - 4f, z);
            float side = MathF.Min(x - plot.Min.X, plot.Max.X - x);
            float sides = Smoothstep(4f, 26f, side);
            return HillRise * rise * back * sides;
        }

        /// <summary>How much of each of the plan's biomes is at a point, in the order the plan holds them; they add up to one.</summary>
        public void Weigh(IReadOnlyList<BiomeDefinition> biomes, float x, float z, Span<float> weights)
        {
            weights.Clear();
            var point = new Vector2(x, z);
            float others = 0f;
            foreach ((PlacedExhibit exhibit, BiomeDefinition biome) in _patches)
            {
                int index = IndexOf(biomes, biome);
                if (index < 0) continue;
                float inside = Inside(exhibit.Plot, point);
                if (inside <= 0f) continue;
                float weight = Smoothstep(0f, BiomeFade, inside);
                if (weight <= weights[index]) continue;
                others += weight - weights[index];
                weights[index] = weight;
            }
            int floor = IndexOf(biomes, LexiconWorld.Floor);
            if (others > 1f)
            {
                for (int i = 0; i < weights.Length; i++) weights[i] /= others;
                others = 1f;
            }
            if (floor >= 0) weights[floor] = 1f - others;
            else if (others <= 0f && weights.Length > 0) weights[0] = 1f;
        }

        /// <summary>How thick the woods are at a point, 0..1: where a place's wood is planted.</summary>
        public float Woods(float x, float z)
        {
            var point = new Vector2(x, z);
            float woods = 0f;
            foreach ((PlacedExhibit exhibit, BiomeDefinition _) in _patches)
            {
                if (exhibit.Diorama is not (Diorama.WoodsBehind or Diorama.WoodsAround)) continue;
                woods = MathF.Max(woods, InWood(exhibit, point));
            }
            return woods;
        }

        /// <summary>
        /// How far into a place's wood a point is, 0..1: behind a logging camp, or round a nesting ground but its clearing, on every side but the south, where the camera is, as trees in front of
        /// it would hide it.
        /// </summary>
        public static float InWood(PlacedExhibit exhibit, Vector2 point)
        {
            float inside = Inside(exhibit.Plot, point);
            if (inside <= 0f) return 0f;
            float edge = Smoothstep(0f, 6f, inside);
            float radius = exhibit.Radius;
            float north = point.Y - exhibit.Centre.Y;
            float clear = exhibit.Diorama == Diorama.WoodsBehind
                ? Smoothstep(radius * 0.95f, radius * 1.2f, north)
                : Smoothstep(radius * 0.85f, radius * 1.05f, (point - exhibit.Centre).Length) * Smoothstep(-radius * 0.5f, -radius * 0.1f, north);
            return edge * clear;
        }

        // Metres in from a box's edge, less than nothing outside it.
        private static float Inside(GroundBox box, Vector2 point) => MathF.Min(MathF.Min(point.X - box.Min.X, box.Max.X - point.X), MathF.Min(point.Y - box.Min.Y, box.Max.Y - point.Y));

        private static int IndexOf(IReadOnlyList<BiomeDefinition> biomes, BiomeDefinition biome)
        {
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i] == biome) return i;
            }
            return -1;
        }
    }
}
