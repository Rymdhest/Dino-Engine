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

        public WorldAsset(string id, WorldAssetKind kind, float footprint, float solidRadius, FloatRange scale, float imposterDistance, bool sways)
        {
            Id = id;
            Kind = kind;
            Footprint = footprint;
            SolidRadius = solidRadius;
            Scale = scale;
            ImposterDistance = imposterDistance;
            Sways = sways;
        }
    }

    /// <summary>Every natural object a biome can scatter. Structures are listed by <see cref="StructureCatalog"/>.</summary>
    public static class NatureCatalog
    {
        private const float TreeImposter = 62f;
        private const float PlantImposter = 34f;
        private const float RockImposter = 70f;

        private static readonly Dictionary<string, WorldAsset> Assets = new();

        // Native sizes: pines and birches are 30 m tall, oaks 20 m. A character stands about 4.6 m tall.
        public static readonly WorldAsset PineTall = Tree("pine.tall", 6.5f, 0.9f, 0.55f, 0.85f);
        public static readonly WorldAsset PineMid = Tree("pine.mid", 6f, 0.9f, 0.5f, 0.8f);
        public static readonly WorldAsset PineSparse = Tree("pine.sparse", 5f, 0.85f, 0.45f, 0.75f);
        public static readonly WorldAsset PineFrosted = Tree("pine.frosted", 6f, 0.9f, 0.5f, 0.8f);
        public static readonly WorldAsset PineDead = Tree("pine.dead", 4f, 0.85f, 0.45f, 0.75f);
        public static readonly WorldAsset PineCharred = Tree("pine.charred", 4f, 0.85f, 0.45f, 0.75f);
        public static readonly WorldAsset Birch = Tree("birch", 6f, 0.7f, 0.5f, 0.8f);
        public static readonly WorldAsset BirchGold = Tree("birch.gold", 6f, 0.7f, 0.5f, 0.8f);
        public static readonly WorldAsset Oak = Tree("oak", 9f, 1.5f, 0.6f, 1.05f);
        public static readonly WorldAsset Maple = Tree("maple.crimson", 8.5f, 1.4f, 0.55f, 1f);
        public static readonly WorldAsset Blossom = Tree("blossom", 8f, 1.2f, 0.5f, 0.85f);
        public static readonly WorldAsset Bamboo = Tree("bamboo", 2.6f, 1.1f, 0.8f, 1.25f);

        public static readonly WorldAsset BigLeafBush = Plant("bush.bigleaf", WorldAssetKind.Shrub, 1.7f, 0.8f, 1.3f);
        public static readonly WorldAsset Fern = Plant("fern", WorldAssetKind.GroundCover, 1.1f, 0.75f, 1.3f);
        public static readonly WorldAsset FlowerYellow = Plant("flower.yellow", WorldAssetKind.GroundCover, 0.8f, 0.8f, 1.25f);
        public static readonly WorldAsset FlowerRed = Plant("flower.red", WorldAssetKind.GroundCover, 0.8f, 0.8f, 1.25f);
        public static readonly WorldAsset FlowerPurple = Plant("flower.purple", WorldAssetKind.GroundCover, 0.8f, 0.8f, 1.25f);
        public static readonly WorldAsset FlowerWhite = Plant("flower.white", WorldAssetKind.GroundCover, 0.8f, 0.8f, 1.25f);
        public static readonly WorldAsset Reeds = Plant("reeds", WorldAssetKind.GroundCover, 1.2f, 0.85f, 1.3f);
        public static readonly WorldAsset Pampas = Plant("pampas", WorldAssetKind.GroundCover, 1.3f, 0.8f, 1.35f);
        public static readonly WorldAsset Heather = Plant("heather", WorldAssetKind.GroundCover, 0.9f, 0.8f, 1.3f);
        public static readonly WorldAsset Scrub = Plant("scrub", WorldAssetKind.Shrub, 1.4f, 0.7f, 1.3f);
        public static readonly WorldAsset Mushrooms = Plant("mushrooms", WorldAssetKind.GroundCover, 0.5f, 0.7f, 1.4f, sways: false);

        public static readonly WorldAsset Boulder = Stone("rock.boulder", 2.2f, 2.0f, 0.8f, 2.4f);
        public static readonly WorldAsset BrownRock = Stone("rock.brown", 2.2f, 2.0f, 0.8f, 2.0f);
        public static readonly WorldAsset Pebbles = Add(new WorldAsset("rock.small", WorldAssetKind.GroundCover, 0.7f, 0f, new FloatRange(0.6f, 1.4f), 60f, false));

        public static readonly WorldAsset FallenPine = Add(new WorldAsset("log.pine", WorldAssetKind.Deadwood, 5.5f, 0.9f, new FloatRange(0.6f, 0.95f), 60f, false));
        public static readonly WorldAsset Stump = Add(new WorldAsset("stump.pine", WorldAssetKind.Deadwood, 1.1f, 0.8f, new FloatRange(0.7f, 1.2f), 60f, false));

        public static IEnumerable<WorldAsset> All => Assets.Values;
        public static WorldAsset Get(string id) => Assets[id];
        public static bool TryGet(string id, out WorldAsset asset) => Assets.TryGetValue(id, out asset!);

        private static WorldAsset Add(WorldAsset asset)
        {
            Assets.Add(asset.Id, asset);
            return asset;
        }

        private static WorldAsset Tree(string id, float footprint, float solid, float minScale, float maxScale)
        {
            return Add(new WorldAsset(id, WorldAssetKind.Tree, footprint, solid, new FloatRange(minScale, maxScale), TreeImposter, true));
        }

        private static WorldAsset Plant(string id, WorldAssetKind kind, float footprint, float minScale, float maxScale, bool sways = true)
        {
            return Add(new WorldAsset(id, kind, footprint, 0f, new FloatRange(minScale, maxScale), PlantImposter, sways));
        }

        private static WorldAsset Stone(string id, float footprint, float solid, float minScale, float maxScale)
        {
            return Add(new WorldAsset(id, WorldAssetKind.Rock, footprint, solid, new FloatRange(minScale, maxScale), RockImposter, false));
        }
    }
}
