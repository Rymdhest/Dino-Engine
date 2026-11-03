using Dino_Engine.World.Content;

namespace Dino_Engine.World
{
    /// <summary>What to generate: a seed, and optionally which kind of world. The same request always gives the same world.</summary>
    public readonly struct WorldRequest
    {
        public readonly int Seed;
        public readonly string? ArchetypeId;    // Null: drawn from the seed.
        public readonly int Level;              // How dangerous the world is, 1 and up.

        public WorldRequest(int seed, string? archetypeId = null, int level = 1)
        {
            Seed = seed;
            ArchetypeId = archetypeId;
            Level = level;
        }
    }

    /// <summary>
    /// The concrete values of one world, drawn from its archetype. Cheap to make, so a world can be described (its name,
    /// its biomes) before anything is generated.
    /// </summary>
    public sealed class WorldPlan
    {
        public const float SeaLevel = 0f;

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
                Size = MathF.Round(random.Range(archetype.Size) / 8f) * 8f,
                BaseHeight = random.Range(archetype.BaseHeight),
                HillHeight = random.Range(archetype.HillHeight),
                MountainHeight = random.Range(archetype.MountainHeight),
                MountainCoverage = random.Range(archetype.MountainCoverage),
                Cliffs = random.Range(archetype.Cliffs),
                Terraces = random.Range(archetype.Terraces),
                Erosion = random.Range(archetype.Erosion),
                Biomes = biomes,
                BiomeShares = shares,
                Mood = WorldMood.Draw(biomes, random.Fork("mood")),
            };
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
