using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>Everything under the trees: shrubs, ferns, grasses, flowers, moss and fungi. There are a lot of these on screen at once, so what they can spend is a fraction of what a tree can.</summary>
    [FloraCollection]
    public static class Undergrowth
    {
        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        // --- Colours ---

        private static Palette Green() => new Palette { Shade = V(0.04f, 0.09f, 0.04f), Body = V(0.11f, 0.21f, 0.07f), Sun = V(0.24f, 0.34f, 0.09f), Drift = 0.12f };
        private static Palette DeepGreen() => new Palette { Shade = V(0.025f, 0.065f, 0.04f), Body = V(0.07f, 0.16f, 0.08f), Sun = V(0.15f, 0.27f, 0.1f), Drift = 0.1f };
        private static Palette Straw() => new Palette { Shade = V(0.13f, 0.09f, 0.035f), Body = V(0.3f, 0.22f, 0.09f), Sun = V(0.44f, 0.34f, 0.15f), Drift = 0.12f };
        private static Palette Dusty() => new Palette { Shade = V(0.08f, 0.09f, 0.06f), Body = V(0.2f, 0.22f, 0.14f), Sun = V(0.36f, 0.36f, 0.22f), Drift = 0.12f };

        // --- Cards ---

        public static readonly PinnateFrond SwordFern = new PinnateFrond { Id = "frond.sword", Pairs = 28, Longest = 0.46f, Skew = 0.62f, Reduce = 0.55f, Slope = 1.28f, Pinna = new LeafForm { Width = 0.2f, Skew = 0.72f, Round = 0.62f, Teeth = 9, ToothDepth = 0.05f, Veins = 0, VeinStrength = 0f, Mottle = 0.09f } };
        public static readonly PinnateFrond LadyFern = new PinnateFrond { Id = "frond.lady", Pairs = 21, Longest = 0.47f, Skew = 0.66f, Reduce = 0.72f, Slope = 1.2f, Pinnules = 9, Pinna = new LeafForm { Width = 0.34f, Skew = 0.8f, Round = 0.7f, Lobes = 3, LobeDepth = 0.4f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f } };
        public static readonly PinnateFrond Bracken = new PinnateFrond { Id = "frond.bracken", Pairs = 12, Longest = 0.49f, Skew = 0.58f, Reduce = 0.8f, Slope = 1.1f, Pinnules = 13, Pinna = new LeafForm { Width = 0.28f, Skew = 0.8f, Round = 0.72f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f } };
        public static readonly LeafSpray ShrubLeaves = new LeafSpray { Id = "spray.shrub", Leaf = new LeafForm { Width = 0.32f, Skew = 0.86f, Round = 0.72f, DripTip = 0.2f, Teeth = 8, ToothDepth = 0.03f, Veins = 6, VeinAngle = 0.72f }, LeafLength = 0.14f, LeavesPerShoot = new IntRange(7, 10), Shoots = new IntRange(10, 13), ShootLength = 0.48f, LeafAngle = 0.7f };
        public static readonly LeafSpray HerbLeaf = new LeafSpray { Id = "leaf.herb", Leaf = new LeafForm { Width = 0.36f, Skew = 0.86f, Round = 0.72f, Teeth = 6, ToothDepth = 0.05f, Veins = 5 }, LeafLength = 0.52f, LeavesPerShoot = new IntRange(1, 1), Shoots = new IntRange(1, 2), ShootLength = 0.3f };
        public static readonly BroadLeaf BananaLeaf = new BroadLeaf { Id = "leaf.banana", Tatters = 0.7f, Leaf = new LeafForm { Width = 0.48f, Skew = 0.95f, Round = 0.42f, Veins = 26, VeinAngle = 0.2f, VeinStrength = 0.32f, Mottle = 0.08f } };
        public static readonly BroadLeaf Hosta = new BroadLeaf { Id = "leaf.hosta", Leaf = new LeafForm { Width = 0.46f, Skew = 0.78f, Round = 0.7f, DripTip = 0.35f, Veins = 9, VeinAngle = 0.25f, VeinStrength = 0.45f, Mottle = 0.08f } };
        public static readonly BroadLeaf Blade = new BroadLeaf { Id = "leaf.blade", Leaf = new LeafForm { Width = 0.46f, Skew = 0.62f, Round = 0.58f, Veins = 0, VeinStrength = 0f, Mottle = 0.07f, Teeth = 5, ToothDepth = 0.015f } };
        public static readonly Plume Pampas = new Plume { Id = "plume.pampas", Strands = 520, Length = 0.5f, Spread = 0.85f, Thickness = 0.0044f };
        public static readonly Plume ReedHead = new Plume { Id = "plume.reed", Strands = 300, Length = 0.34f, Spread = 0.5f, Thickness = 0.0055f };
        public static readonly Cushion Moss = new Cushion { Id = "cushion.moss" };
        public static readonly HangingStrands Beard = new HangingStrands { Id = "strands.beard" };
        public static readonly BlossomSpray HeatherSprig = new BlossomSpray { Id = "sprig.heather", Shoots = new IntRange(10, 13), ShootLength = 0.52f, FlowersPerShoot = 26, FlowerSize = 0.028f, PetalCount = 4, Heart = V(0.8f, 0.55f, 0.7f), YoungLeaf = new LeafForm { Width = 0.12f, Round = 0.6f, Veins = 0, VeinStrength = 0f }, YoungLeaves = 0.34f };
        public static readonly BlossomSpray AzaleaBloom = new BlossomSpray { Id = "bloom.azalea", Shoots = new IntRange(9, 12), ShootLength = 0.5f, FlowersPerShoot = 15, FlowerSize = 0.07f, Heart = V(0.7f, 0.4f, 0.1f), YoungLeaf = new LeafForm { Width = 0.28f, Skew = 0.9f, Round = 0.7f, Veins = 0, VeinStrength = 0.1f }, YoungLeaves = 0.28f, Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.5f, Skew = 1.4f, Round = 0.62f, Notch = 0.08f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f, Tone = new Vector3(1f, 0.98f, 0.98f) } };

        public static readonly FlowerHead Daisy = new FlowerHead { Id = "flower.daisy", Petals = 15, Whorls = 2, Heart = 0.16f, HeartColour = V(0.78f, 0.52f, 0.06f) };
        public static readonly FlowerHead Poppy = new FlowerHead { Id = "flower.poppy", Petals = 5, Whorls = 2, Heart = 0.11f, HeartColour = V(0.06f, 0.05f, 0.05f), Length = 0.48f, Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.62f, Skew = 1.5f, Round = 0.5f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f, Tone = new Vector3(1f, 0.97f, 0.96f) } };
        public static readonly FlowerHead SpiderLily = new FlowerHead { Id = "flower.spider", Petals = 12, Whorls = 2, Heart = 0.05f, HeartColour = V(0.65f, 0.1f, 0.06f), Length = 0.48f, Fan = 0.18f, Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.13f, Skew = 1.1f, Round = 0.7f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f } };
        public static readonly FlowerHead Cluster = new FlowerHead { Id = "flower.cluster", Petals = 5, Whorls = 1, Heart = 0.12f, Clustered = 22, HeartColour = V(0.7f, 0.6f, 0.15f) };
        public static readonly FlowerHead Aster = new FlowerHead { Id = "flower.aster", Petals = 22, Whorls = 2, Heart = 0.14f, HeartColour = V(0.75f, 0.58f, 0.1f), Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.17f, Skew = 1.15f, Round = 0.7f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f } };
        public static readonly FlowerSpike Lupine = new FlowerSpike { Id = "flower.spike", Florets = 54, Size = 0.11f, Width = 0.24f };

        // --- Shrubs: small trees with several stems. ---

        private static TreeForm Shrub(float height, float width, CardForm card, Palette colours, BarkForm? bark = null, float cardSize = 1.5f, int cards = 130, CrownShape crown = CrownShape.Spherical) => new TreeForm
        {
            Height = new FloatRange(height * 0.8f, height * 1.2f),
            Trunk = new TrunkForm { Stems = new IntRange(4, 7), StemSpread = new FloatRange(width * height * 0.12f, width * height * 0.26f), Reach = new FloatRange(0.5f, 0.7f), Girth = new FloatRange(0.012f, 0.018f), Lean = new FloatRange(0.1f, 0.34f), Curve = new StemCurve { Wander = 0.3f, Waves = 2f }, Flare = new FloatRange(0f, 0.1f), Lobes = new IntRange(0, 0), Step = 0.28f },
            Crown = crown,
            CrownBase = new FloatRange(0.06f, 0.14f),
            CrownRadius = new FloatRange(width * 0.9f, width * 1.1f),
            CrownBumps = 0.22f,
            Limbs = Array.Empty<LimbForm>(),
            Buds = new BudForm { Years = new IntRange(9, 11), Internode = 0.2f, MostNodes = 900, Perception = 1.2f, Occupancy = 0.26f, ClumpSize = 1.3f, Gaps = 0.26f, Hollow = 0.5f, MarkerDensity = 16f, ScaffoldBuds = 1f, BranchAngle = new FloatRange(0.55f, 0.85f) },
            TipRadius = 0.012f,
            WoodBudget = 34f,
            MostAxes = 40,
            Walkable = true,
            Bark = bark ?? new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.42f, 0.34f, 0.28f), YoungColour = V(0.34f, 0.26f, 0.2f), Bumps = 0.04f, MostSides = 5 },
            Foliage = new FoliageForm { Card = card, CardSize = new FloatRange(cardSize * 0.85f, cardSize * 1.15f), MostCards = cards, Spacing = 0.24f, Shade = 0f, Droop = 0.2f, Cross = 0.25f, Colours = colours },
        };

        public static readonly PlantSpecies BroadShrub = new PlantSpecies("shrub.broad", WorldAssetKind.Shrub, Shrub(3f, 0.62f, ShrubLeaves, Green()))
        { Footprint = 1.8f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Boxwood = new PlantSpecies("shrub.box", WorldAssetKind.Shrub, Shrub(2.2f, 0.6f, Broadleaves.SmallLeaves, DeepGreen(), cardSize: 1.2f, cards: 150))
        { Footprint = 1.5f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Laurel = new PlantSpecies("shrub.laurel", WorldAssetKind.Shrub, Shrub(3.6f, 0.56f, Broadleaves.LaurelSpray, DeepGreen(), cardSize: 1.8f))
        { Footprint = 2f, Scale = new FloatRange(0.8f, 1.25f) };

        public static readonly PlantSpecies Azalea = new PlantSpecies("shrub.azalea", WorldAssetKind.Shrub, Shrub(2.3f, 0.66f, AzaleaBloom, new Palette { Shade = V(0.32f, 0.06f, 0.16f), Body = V(0.68f, 0.16f, 0.34f), Sun = V(0.86f, 0.34f, 0.5f), Drift = 0.12f }, cardSize: 1.4f, cards: 150))
        { Footprint = 1.7f, Scale = new FloatRange(0.8f, 1.25f) };

        public static readonly PlantSpecies WhiteAzalea = new PlantSpecies("shrub.azalea.white", WorldAssetKind.Shrub, Shrub(2.3f, 0.66f, AzaleaBloom, new Palette { Shade = V(0.36f, 0.34f, 0.32f), Body = V(0.76f, 0.73f, 0.7f), Sun = V(0.92f, 0.9f, 0.86f), Drift = 0.06f }, cardSize: 1.4f, cards: 150))
        { Footprint = 1.7f, Scale = new FloatRange(0.8f, 1.25f) };

        public static readonly PlantSpecies Gorse = new PlantSpecies("shrub.gorse", WorldAssetKind.Shrub, Shrub(2f, 0.72f, Conifers.ShortNeedles, new Palette { Shade = V(0.05f, 0.09f, 0.04f), Body = V(0.15f, 0.22f, 0.06f), Sun = V(0.58f, 0.46f, 0.06f), Drift = 0.14f }, cardSize: 1.25f))
        { Footprint = 1.5f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Juniper = new PlantSpecies("shrub.juniper", WorldAssetKind.Shrub, Shrub(2.6f, 0.46f, Conifers.Plumes, new Palette { Shade = V(0.02f, 0.05f, 0.05f), Body = V(0.06f, 0.13f, 0.1f), Sun = V(0.14f, 0.23f, 0.14f), Drift = 0.1f }, cardSize: 1.5f, crown: CrownShape.Flame))
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Sagebrush = new PlantSpecies("shrub.sage", WorldAssetKind.Shrub, Shrub(1.6f, 0.78f, Broadleaves.SmallLeaves, new Palette { Shade = V(0.1f, 0.12f, 0.09f), Body = V(0.26f, 0.3f, 0.21f), Sun = V(0.44f, 0.46f, 0.32f), Drift = 0.1f }, cardSize: 1f, cards: 110))
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.35f) };

        public static readonly PlantSpecies DeadBush = new PlantSpecies("shrub.dead", WorldAssetKind.Shrub, Shrub(1.7f, 0.66f, Broadleaves.Twigs, new Palette { Shade = V(0.6f, 0.56f, 0.5f), Body = V(0.85f, 0.8f, 0.72f), Sun = V(1f, 0.95f, 0.88f), Drift = 0.05f }, cardSize: 1.3f, cards: 50))
        { Footprint = 1.3f, Scale = new FloatRange(0.8f, 1.35f) };

        // --- Ferns ---

        public static readonly PlantSpecies SwordFernPlant = new PlantSpecies("fern.sword", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = SwordFern, Leaves = new IntRange(9, 13), Length = new FloatRange(1.6f, 2.5f), Aspect = 0.3f, Lean = new FloatRange(0.2f, 0.95f), Arch = new FloatRange(0.8f, 1.5f), Colours = DeepGreen(), Crozier = 1,
        })
        { Footprint = 1.3f, Scale = new FloatRange(0.75f, 1.3f) };

        public static readonly PlantSpecies LadyFernPlant = new PlantSpecies("fern.lady", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = LadyFern, Leaves = new IntRange(8, 12), Length = new FloatRange(1.5f, 2.3f), Aspect = 0.44f, Lean = new FloatRange(0.22f, 0.9f), Arch = new FloatRange(0.9f, 1.6f), Colours = Green(), Crozier = 2,
        })
        { Footprint = 1.2f, Scale = new FloatRange(0.75f, 1.3f) };

        public static readonly PlantSpecies BrackenPlant = new PlantSpecies("fern.bracken", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Bracken, Leaves = new IntRange(4, 7), Length = new FloatRange(1.7f, 2.6f), Aspect = 0.7f, Lean = new FloatRange(0.15f, 0.7f), Arch = new FloatRange(0.9f, 1.4f), Curl = 2f, Fold = 0.12f, Colours = new Palette { Shade = V(0.07f, 0.1f, 0.03f), Body = V(0.2f, 0.26f, 0.07f), Sun = V(0.42f, 0.42f, 0.1f), Drift = 0.2f },
        })
        { Footprint = 1.3f, Scale = new FloatRange(0.75f, 1.3f) };

        public static readonly PlantSpecies HostaPlant = new PlantSpecies("hosta", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Hosta, Leaves = new IntRange(9, 14), Length = new FloatRange(0.8f, 1.2f), Aspect = 0.72f, Lean = new FloatRange(0.35f, 1.15f), Arch = new FloatRange(0.3f, 0.8f), Fold = 0.14f, Joint = PlantJoint.Leaf, Links = 2, Segments = 3, Colours = Green(),
        })
        { Footprint = 1f, Scale = new FloatRange(0.8f, 1.3f) };

        // --- Grasses ---

        public static readonly PlantSpecies PampasGrass = new PlantSpecies("pampas", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(20, 30), BladeLength = new FloatRange(2.2f, 3.4f), BladeWidth = 0.055f, BladeArch = new FloatRange(0.9f, 2f), Spread = 0.42f,
            Stalks = new IntRange(5, 8), StalkHeight = new FloatRange(3.6f, 5.2f), Head = SeedHead.Plume, HeadCard = Pampas, HeadLength = 1.5f, HeadWidth = 1.05f, HeadColour = V(0.76f, 0.68f, 0.52f), Colours = Straw(),
        })
        { Footprint = 1.3f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies SilverGrass = new PlantSpecies("susuki", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(16, 24), BladeLength = new FloatRange(1.5f, 2.3f), BladeWidth = 0.04f, BladeArch = new FloatRange(1f, 2.1f), Spread = 0.34f,
            Stalks = new IntRange(5, 9), StalkHeight = new FloatRange(2.4f, 3.4f), Head = SeedHead.Plume, HeadCard = ReedHead, HeadLength = 1f, HeadWidth = 0.6f, HeadColour = V(0.74f, 0.68f, 0.62f), Colours = Green(),
        })
        { Footprint = 1f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Reeds = new PlantSpecies("reeds", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(12, 18), BladeLength = new FloatRange(2f, 3f), BladeWidth = 0.05f, BladeArch = new FloatRange(0.4f, 1.2f), Spread = 0.8f,
            Stalks = new IntRange(8, 13), StalkHeight = new FloatRange(2.8f, 4.2f), Head = SeedHead.Plume, HeadCard = ReedHead, HeadLength = 0.85f, HeadWidth = 0.5f, HeadColour = V(0.34f, 0.24f, 0.16f), Colours = Green(),
        })
        { Footprint = 1.2f, Scale = new FloatRange(0.85f, 1.3f) };

        public static readonly PlantSpecies Bulrush = new PlantSpecies("bulrush", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(10, 16), BladeLength = new FloatRange(1.8f, 2.8f), BladeWidth = 0.05f, BladeArch = new FloatRange(0.3f, 0.9f), Spread = 0.6f,
            Stalks = new IntRange(5, 9), StalkHeight = new FloatRange(2.4f, 3.4f), Head = SeedHead.Catkin, HeadLength = 0.6f, HeadWidth = 0.14f, HeadColour = V(0.2f, 0.11f, 0.06f), Colours = Green(),
        })
        { Footprint = 1.1f, Scale = new FloatRange(0.85f, 1.3f) };

        public static readonly PlantSpecies DuneGrass = new PlantSpecies("grass.dune", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(16, 24), BladeLength = new FloatRange(1.1f, 1.8f), BladeWidth = 0.03f, BladeArch = new FloatRange(1f, 2.3f), Spread = 0.5f, Stalks = new IntRange(0, 2), StalkHeight = new FloatRange(1.5f, 2f), Head = SeedHead.Ear, HeadLength = 0.34f, HeadWidth = 0.06f, HeadColour = V(0.6f, 0.5f, 0.3f), Colours = Straw(),
        })
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.35f) };

        public static readonly PlantSpecies Sedge = new PlantSpecies("sedge", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(16, 26), BladeLength = new FloatRange(0.9f, 1.5f), BladeWidth = 0.03f, BladeArch = new FloatRange(1f, 2f), Spread = 0.3f, Stalks = new IntRange(0, 0), Head = SeedHead.None, Colours = Green(),
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.8f, 1.35f) };

        // --- Flowers ---

        private static PlantSpecies Flower(string id, CardForm head, Vector3 colour, float height = 0.9f, float size = 0.42f, Inflorescence carried = Inflorescence.Solitary, float spike = 0.55f, int leaves = 3) => new PlantSpecies(id, WorldAssetKind.GroundCover, new HerbForm
        {
            Height = new FloatRange(height * 0.72f, height * 1.25f), LeafCard = HerbLeaf, LeavesPerStem = leaves, Flower = head, Carried = carried, FlowerSize = new FloatRange(size * 0.8f, size * 1.2f), SpikeLength = spike, Foliage = Green(),
            Bloom = new Palette { Shade = colour * 0.5f, Body = colour * 0.82f, Sun = Vector3.ComponentMin(colour * 1.16f, Vector3.One), Drift = 0.14f },
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.8f, 1.25f) };

        public static readonly PlantSpecies WhiteDaisy = Flower("flower.white", Daisy, V(0.8f, 0.78f, 0.74f));
        public static readonly PlantSpecies YellowFlower = Flower("flower.yellow", Aster, V(0.78f, 0.56f, 0.04f));
        public static readonly PlantSpecies RedPoppy = Flower("flower.red", Poppy, V(0.66f, 0.06f, 0.035f), size: 0.5f);
        public static readonly PlantSpecies PurpleAster = Flower("flower.purple", Aster, V(0.32f, 0.17f, 0.56f));
        public static readonly PlantSpecies RedSpiderLily = Flower("flower.spiderlily", SpiderLily, V(0.72f, 0.05f, 0.03f), height: 1.2f, size: 0.62f, leaves: 0);
        public static readonly PlantSpecies WhiteSpiderLily = Flower("flower.spiderlily.white", SpiderLily, V(0.82f, 0.8f, 0.76f), height: 1.2f, size: 0.62f, leaves: 0);
        public static readonly PlantSpecies BlueLupine = Flower("flower.lupine", Lupine, V(0.16f, 0.2f, 0.62f), height: 1.1f, size: 0.3f, carried: Inflorescence.Spike, spike: 0.8f);
        public static readonly PlantSpecies PinkLupine = Flower("flower.lupine.pink", Lupine, V(0.68f, 0.18f, 0.36f), height: 1.1f, size: 0.3f, carried: Inflorescence.Spike, spike: 0.8f);
        public static readonly PlantSpecies Lavender = Flower("flower.lavender", Lupine, V(0.34f, 0.24f, 0.62f), height: 0.8f, size: 0.13f, carried: Inflorescence.Spike, spike: 0.34f, leaves: 2);
        public static readonly PlantSpecies Yarrow = Flower("flower.yarrow", Cluster, V(0.72f, 0.68f, 0.56f), height: 1f, size: 0.5f);
        public static readonly PlantSpecies Bluebell = Flower("flower.bluebell", Cluster, V(0.2f, 0.24f, 0.68f), height: 0.62f, size: 0.34f, leaves: 2);

        public static readonly PlantSpecies Heather = new PlantSpecies("heather", WorldAssetKind.GroundCover, Shrub(0.95f, 0.9f, HeatherSprig, new Palette { Shade = V(0.13f, 0.06f, 0.15f), Body = V(0.32f, 0.14f, 0.34f), Sun = V(0.5f, 0.24f, 0.46f), Drift = 0.14f }, cardSize: 0.72f, cards: 46))
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.3f) };

        // --- What only covers the ground ---

        public static readonly PlantSpecies MossCushion = new PlantSpecies("moss", WorldAssetKind.GroundCover, new GroundForm { Card = Moss, Size = new FloatRange(1.4f, 2.6f), Layers = 2, Lift = 0.05f, Colours = new Palette { Shade = V(0.05f, 0.1f, 0.03f), Body = V(0.13f, 0.24f, 0.05f), Sun = V(0.24f, 0.36f, 0.07f) } })
        { Footprint = 1f, Scale = new FloatRange(0.8f, 1.4f), Sways = false, Variants = 3 };

        // --- Fungi ---

        public static readonly PlantSpecies FlyAgaric = new PlantSpecies("mushroom.fly", WorldAssetKind.GroundCover, new FungusForm { Count = new IntRange(3, 6), Height = new FloatRange(0.3f, 0.62f), CapRadius = new FloatRange(0.2f, 0.34f), Cap = V(0.46f, 0.05f, 0.025f), CapTop = V(0.38f, 0.03f, 0.02f), Spots = 0.5f })
        { Footprint = 0.5f, Scale = new FloatRange(0.7f, 1.4f), Sways = false, Variants = 3 };

        public static readonly PlantSpecies BrownCaps = new PlantSpecies("mushroom.brown", WorldAssetKind.GroundCover, new FungusForm { Count = new IntRange(4, 7), Height = new FloatRange(0.2f, 0.44f), CapRadius = new FloatRange(0.14f, 0.26f), Dome = 0.55f, Cap = V(0.24f, 0.14f, 0.07f), CapTop = V(0.15f, 0.08f, 0.04f), Gills = V(0.32f, 0.27f, 0.2f), Stem = V(0.36f, 0.31f, 0.24f) })
        { Footprint = 0.5f, Scale = new FloatRange(0.7f, 1.4f), Sways = false, Variants = 3 };

        public static readonly PlantSpecies InkCaps = new PlantSpecies("mushroom.ink", WorldAssetKind.GroundCover, new FungusForm { Count = new IntRange(5, 8), Spread = 0.3f, Height = new FloatRange(0.3f, 0.6f), CapRadius = new FloatRange(0.07f, 0.13f), Dome = 1.5f, Flare = 0f, StemWidth = 0.34f, Cap = V(0.5f, 0.44f, 0.4f), CapTop = V(0.3f, 0.24f, 0.22f), Gills = V(0.08f, 0.07f, 0.07f) })
        { Footprint = 0.45f, Scale = new FloatRange(0.7f, 1.4f), Sways = false, Variants = 3 };

        public static readonly PlantSpecies GhostCaps = new PlantSpecies("mushroom.ghost", WorldAssetKind.GroundCover, new FungusForm { Count = new IntRange(4, 7), Height = new FloatRange(0.2f, 0.5f), CapRadius = new FloatRange(0.1f, 0.22f), Cap = V(0.06f, 0.42f, 0.3f), CapTop = V(0.1f, 0.56f, 0.4f), Gills = V(0.06f, 0.36f, 0.28f), Stem = V(0.1f, 0.3f, 0.24f), Glows = true })
        { Footprint = 0.5f, Scale = new FloatRange(0.7f, 1.4f), Sways = false, Variants = 3 };

        // --- Crops: what stands in a field. ---

        public static readonly PlantSpecies Grain = new PlantSpecies("crop.grain", WorldAssetKind.Crop, new TuftForm
        {
            Blades = new IntRange(6, 9), BladeLength = new FloatRange(1f, 1.6f), BladeWidth = 0.03f, BladeArch = new FloatRange(0.7f, 1.4f), Spread = 1.1f,
            Stalks = new IntRange(16, 22), StalkHeight = new FloatRange(2f, 2.9f), Head = SeedHead.Ear, HeadLength = 0.5f, HeadWidth = 0.13f, HeadColour = V(0.72f, 0.54f, 0.2f),
            Colours = new Palette { Shade = V(0.3f, 0.22f, 0.08f), Body = V(0.58f, 0.44f, 0.16f), Sun = V(0.76f, 0.62f, 0.28f), Drift = 0.08f },
        })
        { Footprint = 1.2f, Variants = 3 };

        public static readonly PlantSpecies Cabbage = new PlantSpecies("crop.vegetable", WorldAssetKind.Crop, new RosetteForm
        {
            Card = Hosta, Leaves = new IntRange(11, 15), Length = new FloatRange(0.9f, 1.2f), Aspect = 0.8f, Inner = 0.45f, Lean = new FloatRange(0.15f, 1.3f), Arch = new FloatRange(-0.5f, 0.7f), Fold = 0.1f, Joint = PlantJoint.Leaf, Links = 1, Segments = 3,
            Colours = new Palette { Shade = V(0.1f, 0.2f, 0.07f), Body = V(0.2f, 0.34f, 0.12f), Sun = V(0.36f, 0.48f, 0.2f) },
        })
        { Footprint = 0.9f, Variants = 3 };
    }
}
