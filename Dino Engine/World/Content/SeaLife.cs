using Dino_Engine.Modelling.Procedural.Flora;

namespace Dino_Engine.World.Content
{
    /// <summary>
    /// What lives in a coast's water (SEA.md): which plants and creatures, on what bottom, how deep and how thickly. A coast's biome says which by how warm it is (<see cref="Of"/>), unless
    /// it names one (<see cref="BiomeDefinition.Sea"/>), and still fresh water has its own. None of it has a say in how a world grows: <see cref="WorldSeaFloor"/> puts it down on the
    /// client as chunks stream in, as the rest of the ground cover is.
    /// </summary>
    public sealed class SeaLife
    {
        public string Id = "";
        public bool Fresh;

        public Species[] Meadow = Array.Empty<Species>();     // Soft bottoms, in meadows: seagrass, pondweed.
        public Species[] Reef = Array.Empty<Species>();       // Hard bottoms out of the waves' wash: kelp, corals, sponges.
        public Species[] Washed = Array.Empty<Species>();     // Hard bottoms in the first metre, where the waves wash over: wrack, sea lettuce.
        public Species[] Deep = Array.Empty<Species>();       // Hard bottoms past the reef: giant kelp, the biggest corals.
        public Species[] Critters = Array.Empty<Species>();   // Starfish and urchins, anywhere they can crawl.
        public Species[] Floating = Array.Empty<Species>();   // On still water: lilies.

        public float MeadowCover = 0.45f;       // Of soft bottoms, how much is meadow.
        public float MeadowDensity = 0.95f;     // How thickly a meadow grows, of its cells: its clumps reach into each other.
        public float ReefDensity = 0.55f;
        public float WashedDensity = 0.45f;
        public float DeepDensity = 0.3f;
        public float CritterDensity = 0.025f;
        public float FloatingCover = 0.35f;     // Of still shallows, how much has lilies on it.
        public float FloatingDensity = 0.8f;
        public float Patches;                   // Of sandy bottoms, how much is reef all the same: the patch reefs of the hot seas.
        // What grows on hard bottoms grows in masses, as reef heads, kelp beds and the barrens between them do, and never strewn evenly:
        public float ReefPatch = 12f;           // Metres across the largest masses.
        public float ReefCover = 0.6f;          // Of hard bottoms, how much is in one.
        public float ReefScatter = 0.15f;       // Of how thickly the reef grows in a mass, how much is left between them, for a few young colonies.
        public float Formations = 0.35f;        // How likely rock stands up off a rocky bottom out of anyone's reach, for each place it could.
        public float Deepest = 7f;              // Metres, past which nothing's worth growing: nobody can see that far down.

        /// <summary>Ponds, and the lakes of a land of still water: lilies on the shallows and pondweed under them.</summary>
        public static readonly SeaLife FreshWater = new SeaLife
        {
            Id = "fresh", Fresh = true,
            Meadow = new[] { new Species(Seas.PondweedBed) },
            Floating = new[] { new Species(Seas.Lily) },
            MeadowCover = 0.35f, MeadowDensity = 0.5f, Formations = 0.25f, Deepest = 5f,
        };

        /// <summary>The cold seas of the north: kelp on every rock and wrack where the waves wash, green urchins grazing, and only a little eelgrass in the sand.</summary>
        public static readonly SeaLife Cold = new SeaLife
        {
            Id = "cold",
            Meadow = new[] { new Species(Seas.Eelgrass) },
            Reef = new[] { new Species(Seas.Oarweed, 3f), new Species(Seas.SugarKelpPlant, 1.5f) },
            Washed = new[] { new Species(Seas.BladderWrack, 3f), new Species(Seas.SeaLettuce, 0.6f) },
            Deep = new[] { new Species(Seas.GiantKelpPlant) },
            Critters = new[] { new Species(Seas.GreenUrchin, 2f), new Species(Seas.OchreStar, 1f) },
            MeadowCover = 0.25f, ReefDensity = 0.8f, ReefPatch = 14f, ReefCover = 0.6f, Formations = 0.45f,
        };

        /// <summary>Temperate seas: eelgrass meadows on the sand, oarweed and sugar kelp on the rock, a kelp forest further out, wrack and sea lettuce in the wash, purple urchins and starfish.</summary>
        public static readonly SeaLife Temperate = new SeaLife
        {
            Id = "temperate",
            Meadow = new[] { new Species(Seas.Eelgrass) },
            Reef = new[] { new Species(Seas.Oarweed, 3f), new Species(Seas.SugarKelpPlant, 2f) },
            Washed = new[] { new Species(Seas.BladderWrack, 2.5f), new Species(Seas.SeaLettuce, 1.2f) },
            Deep = new[] { new Species(Seas.GiantKelpPlant) },
            Critters = new[] { new Species(Seas.PurpleUrchin, 1.5f), new Species(Seas.OchreStar, 1.5f) },
            ReefDensity = 0.8f, ReefPatch = 13f, ReefCover = 0.62f, Formations = 0.4f,
        };

        /// <summary>Warm seas, as round the cypresses and the maquis: dark seagrass meadows, weedy rock with a soft coral, a fan or a sponge on it, urchins and red starfish.</summary>
        public static readonly SeaLife Warm = new SeaLife
        {
            Id = "warm",
            Meadow = new[] { new Species(Seas.TurtleGrassBed) },
            Reef = new[] { new Species(Seas.BladderWrack, 2f), new Species(Seas.SeaLettuce, 0.6f), new Species(Seas.SoftCoral, 0.4f), new Species(Seas.SeaFanPlant, 0.5f), new Species(Seas.TubeSponge, 0.35f) },
            Washed = new[] { new Species(Seas.BladderWrack, 1f), new Species(Seas.SeaLettuce, 1f) },
            Critters = new[] { new Species(Seas.PurpleUrchin, 1f), new Species(Seas.OchreStar, 1f) },
            MeadowCover = 0.55f, ReefDensity = 0.75f, ReefPatch = 10f, ReefCover = 0.5f, Formations = 0.35f,
        };

        /// <summary>
        /// The hot seas: reefs of staghorn and table coral, brain and boulder corals, soft corals, sea fans and sponges on every hard bottom, patch reefs on the sand, turtle grass between,
        /// and black urchins and cushion stars about them.
        /// </summary>
        public static readonly SeaLife Tropical = new SeaLife
        {
            Id = "tropical",
            Meadow = new[] { new Species(Seas.TurtleGrassBed) },
            Reef = new[]
            {
                new Species(Seas.Staghorn, 2.2f), new Species(Seas.TableCoral, 1f), new Species(Seas.BrainCoral, 1.4f), new Species(Seas.BoulderCoral, 1.6f),
                new Species(Seas.SoftCoral, 1.2f), new Species(Seas.SeaFanPlant, 0.7f), new Species(Seas.BarrelSponge, 0.5f), new Species(Seas.TubeSponge, 0.5f),
            },
            Deep = new[] { new Species(Seas.BoulderCoral, 1f), new Species(Seas.TableCoral, 1f), new Species(Seas.SeaFanPlant, 0.6f) },
            Critters = new[] { new Species(Seas.BlackUrchin, 1.5f), new Species(Seas.CushionStar, 1.2f) },
            MeadowCover = 0.4f, ReefDensity = 1f, DeepDensity = 0.6f, ReefPatch = 8f, ReefCover = 0.45f, ReefScatter = 0.08f, Patches = 0.3f, Formations = 0.3f,
        };

        public static IReadOnlyList<SeaLife> All { get; } = new[] { FreshWater, Cold, Temperate, Warm, Tropical };

        /// <summary>What lives off a coast of this biome: what it names, or what its warmth calls for (a biome's warmth is absolute, as the weather reads it).</summary>
        public static SeaLife Of(BiomeDefinition biome) => biome.Sea ?? (biome.Temperature >= 0.8f ? Tropical : biome.Temperature >= 0.65f ? Warm : biome.Temperature >= 0.38f ? Temperate : Cold);

        /// <summary>Every species it can put down.</summary>
        public IEnumerable<Species> Everything => Meadow.Concat(Reef).Concat(Washed).Concat(Deep).Concat(Critters).Concat(Floating);
    }
}
