using Dino_Engine.World.Content;

namespace Dino_Engine.World
{
    /// <summary>What to generate: a seed, and optionally which kind of world. The same request always gives the same world.</summary>
    public readonly struct WorldRequest
    {
        public readonly int Seed;
        public readonly string? ArchetypeId;    // Null: drawn from the seed.
        public readonly int Level;              // How dangerous the world is, 1 and up.
        public readonly float MorePeople;       // How much bigger every pack and garrison is than usual, as a share: 0.5 is half as many again.
        public readonly float MorePacks;        // How many more packs stand on the land, as a share, or NoPacks. Neither touches anything but PackStage.
        public readonly float Larger;           // How much larger than its usual size the land is, as a share, never past the most its kind can be.

        /// <summary>Asked of <see cref="MorePacks"/>: nobody stands on the roads, in the wild or at a camp, and only places are held.</summary>
        public const float NoPacks = -1f;

        /// <summary>Whether this asks for the land between places to be empty.</summary>
        public bool Emptied => MorePacks <= NoPacks;

        public WorldRequest(int seed, string? archetypeId = null, int level = 1, float morePeople = 0f, float morePacks = 0f, float larger = 0f)
        {
            Seed = seed;
            ArchetypeId = archetypeId;
            Level = level;
            MorePeople = morePeople;
            MorePacks = morePacks;
            Larger = larger;
        }
    }

    /// <summary>
    /// The concrete values of one world, drawn from its archetype. Cheap to make, so a world can be described (its name,
    /// its biomes) before anything is generated.
    /// </summary>
    public sealed class WorldPlan
    {
        public const float SeaLevel = 0f;


        /// <summary>
        /// How much closer together places of a kind stand than on a land as large as its kind can be: a land is a fraction of that, and still has to hold everything a land needs. Never closer than
        /// <see cref="ClosestSpacing"/> of the usual.
        /// </summary>
        public float Closeness => Math.Clamp(Size / MathF.Max(Archetype.Size.Max, 1f), ClosestSpacing, 1f);

        private const float ClosestSpacing = 0.3f;

        public WorldRequest Request { get; private init; }
        public WorldArchetype Archetype { get; private init; } = null!;
        public string Name { get; private init; } = "";
        public float Size { get; private init; }

        public float BaseHeight { get; private init; }
        public float HillHeight { get; private init; }
        public float MountainHeight { get; private init; }
        public float MountainCoverage { get; private init; }
        public float Cliffs { get; private init; }
        public float Terraces { get; private init; }
        public float Erosion { get; private init; }

        /// <summary>The biomes of this world, most prominent first.</summary>
        public IReadOnlyList<BiomeDefinition> Biomes { get; private init; } = Array.Empty<BiomeDefinition>();
        /// <summary>Share of the land each biome aims for, in the order of <see cref="Biomes"/>.</summary>
        public IReadOnlyList<float> BiomeShares { get; private init; } = Array.Empty<float>();

        /// <summary>The light and weather the world is seen in.</summary>
        public WorldMood Mood { get; private init; } = null!;

        public int Seed => Request.Seed;
        public int Level => Request.Level;
        /// <summary>Which of <see cref="PlaceNames"/>' languages the world speaks. Its people build in the style that goes with it.</summary>
        public int Language => PlaceNames.LanguageIndex(Seed);

        public static WorldPlan Create(WorldRequest request)
        {
            var root = new WorldRandom(request.Seed);
            var random = root.Fork("plan");

            WorldArchetype archetype = request.ArchetypeId != null
                ? ArchetypeCatalog.Get(request.ArchetypeId)
                : random.Pick(ArchetypeCatalog.All);

            List<BiomeDefinition> biomes = DrawBiomes(archetype, random.Fork("biomes"));
            float[] shares = new float[biomes.Count];
            float total = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                // The first biome is what the world is known for.
                shares[i] = random.Range(0.7f, 1.3f) * (i == 0 ? 1.5f : 1f);
                total += shares[i];
            }
            for (int i = 0; i < shares.Length; i++) shares[i] /= total;

            return new WorldPlan
            {
                Request = request,
                Archetype = archetype,
                Name = PlaceNames.WorldName(archetype, request.Seed, random.Fork("name")),
                // A whole number of 8 m climate cells, so the coarser maps end exactly on the world's edge.
                Size = MathF.Round(SizeOf(archetype, random.Range(archetype.Size), root.Fork("extent"), request.Larger) / 8f) * 8f,
                BaseHeight = random.Range(archetype.BaseHeight),
                HillHeight = random.Range(archetype.HillHeight),
                MountainHeight = random.Range(archetype.MountainHeight),
                MountainCoverage = random.Range(archetype.MountainCoverage),
                Cliffs = random.Range(archetype.Cliffs),
                Terraces = random.Range(archetype.Terraces),
                Erosion = random.Range(archetype.Erosion),
                Biomes = biomes,
                BiomeShares = shares,
                Mood = WorldMood.Draw(biomes, random.Fork("mood"), archetype),
            };
        }

        // A land is its kind's usual size, larger as orders ask, up to the most this one could be, drawn from what its kind can be. The Haven and an arena are always what they are, as is a kind with
        // no usual size. The usual size has a fork of its own, so that nothing else about a world moves with it.
        private static float SizeOf(WorldArchetype archetype, float most, WorldRandom random, float larger)
        {
            if (archetype.Peaceful || archetype.Arena || archetype.UsualSize.Max <= 0f) return most;
            return MathF.Min(random.Range(archetype.UsualSize) * (1f + MathF.Max(larger, 0f)), most);
        }

        private static List<BiomeDefinition> DrawBiomes(WorldArchetype archetype, WorldRandom random)
        {
            var pool = new List<BiomeDefinition>(archetype.Biomes);
            int count = Math.Min(random.Int(archetype.BiomeCount), pool.Count);

            var drawn = new List<BiomeDefinition>(count);
            var weights = new List<float>(pool.Count);
            while (drawn.Count < count)
            {
                weights.Clear();
                for (int i = 0; i < pool.Count; i++) weights.Add(1f / (1f + 0.45f * i));
                int pick = random.PickWeighted(weights);
                drawn.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return drawn;
        }
    }
}
