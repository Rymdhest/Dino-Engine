using Dino_Engine.Modelling.Procedural.Flora;

namespace Dino_Engine.World.Content
{
    /// <summary>Who nests at a nesting ground (PLACES.md): what got out of the pens and found it could breed.</summary>
    public enum NestKind
    {
        Raptors,    // A ring of mounds, each with its clutch laid in a circle, and what was dragged back to feed the young.
        Ravager,    // One great scrape, trampled wide, ringed with the bones of what it's eaten.
        Weaver,     // The Institute's eight-legged hunter's lair: webs strung between the trunks, egg sacs hung in them, and what it's caught wrapped up.
        Wader,      // Mounds of reeds and mud at the water's edge.
    }

    /// <summary>
    /// What a place's story number says (<see cref="WorldSite.Story"/>), which its name, its builder and whoever holds it in the game all read the same way, so that they can never
    /// disagree: a wreck named for one of the Reich's ships is one, and a weaver's lair is held by weavers. Headless.
    /// </summary>
    public static class PlaceStory
    {
        /// <summary>
        /// What the Reich catches to break, at a prison (PLACES.md): the beasts it fights with and the grazers, born free in the wild to what got out of its pens, and so in the wild's colours and
        /// no harness, caught nearly grown, as that's when they're worth breaking. Each kind's model, how many of it there are, its scale, and the cage it's held in (width, depth, height). Smaller
        /// than this they're a dark shape in a dark cage from the camera. The game grows every one at the loading screen (CharacterFactory), as it does anything a land can show.
        /// </summary>
        public static readonly (string Look, int Variants, float Scale, OpenTK.Mathematics.Vector3 Cage)[] Captives =
        {
            ("dino:hornback.", 4, 0.92f, new OpenTK.Mathematics.Vector3(6.4f, 7.6f, 5f)),
            ("dino:clubtail.", 3, 0.9f, new OpenTK.Mathematics.Vector3(6.6f, 7.8f, 4.4f)),
            ("dino:spikeback.", 3, 0.92f, new OpenTK.Mathematics.Vector3(6.2f, 8f, 5.4f)),
            ("dino:domehead.", 3, 1.1f, new OpenTK.Mathematics.Vector3(5f, 6.4f, 5f)),
            ("dino:strider.", 3, 1.1f, new OpenTK.Mathematics.Vector3(4.6f, 6.6f, 5.8f)),
        };

        /// <summary>
        /// Whether a plant is worth felling for timber: a tree with wood in it, and not a bamboo's canes, a palm's rings or anything green, which no logging camp would stand by.
        /// </summary>
        public static bool IsTimber(PlantSpecies plant) => plant.Kind == WorldAssetKind.Tree && plant.Form is TreeForm form && form.Bark.Kind is not (BarkKind.Cane or BarkKind.Stem or BarkKind.Ringed);

        /// <summary>The share of a biome's canopy, by weight, that's worth felling, which is what a logging camp counts of its woods.</summary>
        public static float TimberShare(BiomeDefinition biome)
        {
            float total = 0f, timber = 0f;
            foreach (Species species in biome.Canopy)
            {
                total += species.Weight;
                if (FloraCatalog.TryGet(species.Asset.Id, out PlantSpecies plant) && IsTimber(plant)) timber += species.Weight;
            }
            return total > 0f ? timber / total : 0f;
        }

        /// <summary>The tree worth felling that a biome's woods are mostly of, if they have one.</summary>
        public static PlantSpecies? TimberOf(BiomeDefinition biome)
        {
            PlantSpecies? found = null;
            float best = -1f;
            foreach (Species species in biome.Canopy)
            {
                if (species.Weight <= best || !FloraCatalog.TryGet(species.Asset.Id, out PlantSpecies plant) || !IsTimber(plant)) continue;
                best = species.Weight;
                found = plant;
            }
            return found;
        }

        /// <summary>Of wrecks, the share that are the Reich's iron ships rather than the land's own.</summary>
        public const float ReichWrecks = 0.68f;

        public static bool WreckIsReich(uint story) => Share(story, 1) < ReichWrecks;

        /// <summary>Who nests at a nesting ground: a wader only where there's water near, a weaver only where the woods are deep and it isn't cold, as nothing cold-blooded lasts the snow.</summary>
        public static NestKind NestOf(WorldData world, WorldSite site)
        {
            BiomeDefinition biome = world.Plan.Biomes[Math.Clamp(site.Biome, 0, world.Plan.Biomes.Count - 1)];
            bool wet = biome.Moisture >= 0.7f || world.Inland.Sample(site.Position) < site.Radius + 30f;
            bool warm = biome.Temperature > 0.35f;
            float roll = Share(site.Story, 2);
            if (wet && warm && roll < 0.4f) return NestKind.Wader;
            if (warm && biome.ForestCover >= 0.45f && roll < 0.72f) return NestKind.Weaver;
            return Share(site.Story, 3) < 0.65f ? NestKind.Raptors : NestKind.Ravager;
        }

        /// <summary>What the Ossuary is lifting at a dig: one of the great beasts, the bigger the likelier, as it's the big ones it wants.</summary>
        public static string ExcavatedOf(uint story)
        {
            float roll = Share(story, 4);
            return roll switch
            {
                < 0.32f => "tyrant",
                < 0.5f => "hornback",
                < 0.64f => "ravager",
                < 0.78f => "spikeback",
                < 0.9f => "clubtail",
                _ => "giant",
            };
        }

        /// <summary>A number between 0 and 1 from a story number, a different one for each <paramref name="part"/>.</summary>
        public static float Share(uint story, int part)
        {
            uint h = story ^ (uint)part * 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }
    }
}
