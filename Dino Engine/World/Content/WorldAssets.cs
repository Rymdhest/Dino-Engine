using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    public enum WorldAssetKind
    {
        Tree,
        Shrub,
        GroundCover,
        Rock,
        Deadwood,
        Structure,
        Prop,
        Crop,
        Fence,
    }

    /// <summary>
    /// What world generation needs to know about a placeable object. Generation never touches the GPU: it works with these
    /// numbers and the id, which <see cref="WorldAssetLibrary"/> turns into a model when the world is entered.
    /// </summary>
    public sealed class WorldAsset
    {
        public readonly string Id;
        public readonly WorldAssetKind Kind;
        public readonly float Footprint;        // Metres at scale 1 that other objects stay clear of, from the object's centre.
        public readonly float SolidRadius;      // Metres at scale 1 that block movement; 0 lets characters walk through.
        public readonly FloatRange Scale;
        public readonly float ImposterDistance; // Metres from the camera where the model turns into an imposter; 0 never.
        public readonly bool Sways;             // Whether it bends: how far each part does is the PlantJoints it's built with.
        public readonly int Variants;           // How many different models of it a world has. Everything alive is grown, and no two are alike.
        public readonly float SolidLength;      // Metres at scale 1 along its x axis that it's solid for, if it isn't round: a log. What looks solid has to be.

        public WorldAsset(string id, WorldAssetKind kind, float footprint, float solidRadius, FloatRange scale, float imposterDistance, bool sways, int variants = 1, float solidLength = 0f)
        {
            Id = id;
            Kind = kind;
            Footprint = footprint;
            SolidRadius = solidRadius;
            Scale = scale;
            ImposterDistance = imposterDistance;
            Sways = sways;
            Variants = variants;
            SolidLength = solidLength;
        }
    }

    /// <summary>
    /// Every natural object a biome can scatter: the stones here, and every plant in <see cref="FloraCatalog"/>. Structures are listed by <see cref="StructureCatalog"/>.
    /// A plant's model id is the species with a seed after a hash, as "oak#1f3a", which is all it takes to grow that one again, and a stone's is its rock and form, as "rock.granite-crag#1f3a".
    /// A biome only says it has boulders: what they're made of is whatever the world is, which <see cref="Resolve"/> settles.
    /// </summary>
    public static class NatureCatalog
    {
        private const float RockImposter = 70f;
        private const int StoneVariants = 5;
        private const char SeedMark = '#';

        private static readonly Dictionary<string, WorldAsset> Stones = new();

        private static readonly Dictionary<(RockKind, RockForm), WorldAsset> Rocks = BuildRocks();
        private static readonly Dictionary<string, (RockKind kind, RockForm form)> RockIds = Rocks.ToDictionary(rock => rock.Value.Id, rock => rock.Key);

        // What a biome asks for. These are never built: see Resolve.
        public static readonly WorldAsset Boulder = Stone("rock.boulder", 2.2f, 2.0f, 0.8f, 2.4f);
        public static readonly WorldAsset BrownRock = Stone("rock.brown", 2.2f, 2.0f, 0.8f, 2.0f);
        public static readonly WorldAsset Pebbles = Add(new WorldAsset("rock.small", WorldAssetKind.GroundCover, 0.7f, 0f, new FloatRange(0.6f, 1.4f), 60f, false));

        public static IEnumerable<WorldAsset> All => Stones.Values.Concat(FloraCatalog.Species.Select(species => species.Asset));

        private static Dictionary<(RockKind, RockForm), WorldAsset> BuildRocks()
        {
            var rocks = new Dictionary<(RockKind, RockForm), WorldAsset>();
            foreach (RockKind kind in Enum.GetValues<RockKind>())
            {
                foreach (RockForm form in Enum.GetValues<RockForm>())
                {
                    (float footprint, float solid, FloatRange scale, int variants) = form switch
                    {
                        RockForm.Pebble => (0.3f, 0f, new FloatRange(0.5f, 1.5f), StoneVariants),
                        RockForm.Chip => (0.5f, 0f, new FloatRange(0.55f, 1.5f), StoneVariants),
                        RockForm.Boulder => (1.6f, 1.2f, new FloatRange(0.6f, 1.8f), StoneVariants),
                        RockForm.Block => (1.9f, 1.5f, new FloatRange(0.6f, 1.6f), StoneVariants),
                        RockForm.Slab => (2.3f, 1.7f, new FloatRange(0.6f, 1.3f), 3),
                        RockForm.Outcrop => (4.6f, 3.6f, new FloatRange(0.7f, 1.5f), 4),
                        RockForm.Crag => (7f, 0f, new FloatRange(0.6f, 1.5f), 4),
                        _ => (4f, 0f, new FloatRange(0.55f, 1.25f), 3),
                    };
                    WorldAssetKind assetKind = form is RockForm.Pebble or RockForm.Chip ? WorldAssetKind.GroundCover : WorldAssetKind.Rock;
                    var asset = new WorldAsset($"rock.{kind}-{form}".ToLowerInvariant(), assetKind, footprint, solid, scale, RockImposter, false, variants);
                    Stones.Add(asset.Id, asset);
                    rocks.Add((kind, form), asset);
                }
            }
            return rocks;
        }

        public static WorldAsset RockOf(RockKind kind, RockForm form) => Rocks[(kind, form)];

        /// <summary>Whether a species is a stone, and which.</summary>
        public static bool TryGetRock(string species, out RockKind kind, out RockForm form)
        {
            bool found = RockIds.TryGetValue(species, out (RockKind kind, RockForm form) rock);
            kind = rock.kind;
            form = rock.form;
            return found;
        }

        /// <summary>What a biome's list really means in a world of this rock.</summary>
        public static WorldAsset Resolve(WorldAsset asset, RockKind kind)
        {
            if (asset == Boulder) return RockOf(kind, RockForm.Boulder);
            if (asset == BrownRock) return RockOf(kind, RockForm.Block);
            if (asset == Pebbles) return RockOf(kind, RockForm.Pebble);
            return asset;
        }
        public static WorldAsset Get(string id) => TryGet(id, out WorldAsset asset) ? asset : throw new KeyNotFoundException(id);

        public static bool TryGet(string id, out WorldAsset asset)
        {
            string species = SpeciesOf(id, out _);
            if (Stones.TryGetValue(species, out asset!)) return true;
            if (!FloraCatalog.TryGet(species, out PlantSpecies plant)) return false;
            asset = plant.Asset;
            return true;
        }

        /// <summary>What a model id is a plant of, and which plant.</summary>
        public static string SpeciesOf(string id, out ulong seed)
        {
            int mark = id.IndexOf(SeedMark);
            if (mark < 0)
            {
                seed = 0;
                return id;
            }
            seed = Convert.ToUInt64(id.Substring(mark + 1), 16);
            return id.Substring(0, mark);
        }

        /// <summary>Which of a world's plants of this kind stands somewhere. It goes by where, so that nothing is drawn from a stream that anything else depends on.</summary>
        public static string ModelAt(WorldAsset asset, int worldSeed, Vector2 at)
        {
            if (asset.Variants <= 1) return asset.Id;
            uint here = FloraNoise.Hash((uint)(int)(at.X * 8f) * 0x9E3779B1u ^ FloraNoise.Hash((uint)(int)(at.Y * 8f)));
            return Variant(asset, worldSeed, (int)(here % (uint)asset.Variants));
        }

        /// <summary>The <paramref name="index"/>th plant of a kind in a world. A new world is a new seed, and so plants nobody has seen.</summary>
        public static string Variant(WorldAsset asset, int worldSeed, int index)
        {
            return $"{asset.Id}{SeedMark}{FloraNoise.Hash((uint)worldSeed * 0x85EBCA6Bu + (uint)index * 0xC2B2AE35u + 1u):x}";
        }

        /// <summary>Every model of an asset a world can ask for.</summary>
        public static IEnumerable<string> ModelsOf(WorldAsset asset, int worldSeed)
        {
            if (asset.Variants <= 1)
            {
                yield return asset.Id;
                yield break;
            }
            for (int i = 0; i < asset.Variants; i++) yield return Variant(asset, worldSeed, i);
        }

        private static WorldAsset Add(WorldAsset asset)
        {
            Stones.Add(asset.Id, asset);
            return asset;
        }

        private static WorldAsset Stone(string id, float footprint, float solid, float minScale, float maxScale)
        {
            return Add(new WorldAsset(id, WorldAssetKind.Rock, footprint, solid, new FloatRange(minScale, maxScale), RockImposter, false));
        }
    }
}
