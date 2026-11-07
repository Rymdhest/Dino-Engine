using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Trees with leaves. A crown is a scaffold of a few big limbs and fine wood that's fought for the light, so what differs from one species to the next is mostly how the limbs go.
    /// Most colours are a tree in autumn, as that's what an autumn wood is for.
    /// </summary>
    [FloraCollection]
    public static class Broadleaves
    {
        // --- Leaves ---

        private static LeafForm OakLeaf() => new LeafForm { Width = 0.32f, Skew = 1.05f, Round = 0.75f, Lobes = 4, LobeDepth = 0.62f, LobeSharp = 1.5f, Veins = 5, VeinAngle = 0.7f };
        private static LeafForm MapleLeaf() => new LeafForm { Blade = LeafBlade.Palmate, Lobes = 5, LobeDepth = 0.62f, LobeSpread = 2.1f, Teeth = 9, ToothDepth = 0.09f, VeinStrength = 0.45f };
        private static LeafForm JapaneseMapleLeaf() => new LeafForm { Blade = LeafBlade.Palmate, Lobes = 7, LobeDepth = 0.9f, LobeSpread = 2.55f, Teeth = 16, ToothDepth = 0.1f, VeinStrength = 0.35f };
        private static LeafForm GinkgoLeaf() => new LeafForm { Blade = LeafBlade.Fan, LobeSpread = 0.95f, Notch = 0.34f, VeinStrength = 0.3f, Mottle = 0.06f };
        private static LeafForm BeechLeaf() => new LeafForm { Width = 0.3f, Skew = 0.86f, Round = 0.72f, DripTip = 0.4f, Teeth = 7, ToothDepth = 0.04f, Veins = 8, VeinAngle = 0.72f };
        private static LeafForm BirchLeaf() => new LeafForm { Width = 0.36f, Skew = 0.72f, Round = 0.84f, DripTip = 0.6f, Teeth = 13, ToothDepth = 0.075f, Veins = 7, VeinAngle = 0.65f };
        private static LeafForm WillowLeaf() => new LeafForm { Width = 0.1f, Skew = 0.9f, Round = 0.72f, DripTip = 0.3f, Veins = 0, VeinStrength = 0.2f };
        private static LeafForm LaurelLeaf() => new LeafForm { Width = 0.27f, Skew = 0.92f, Round = 0.68f, DripTip = 0.3f, Veins = 6, VeinAngle = 0.8f, VeinStrength = 0.3f, Mottle = 0.08f };
        private static LeafForm SmallLeaf() => new LeafForm { Width = 0.3f, Skew = 0.9f, Round = 0.72f, Veins = 0, VeinStrength = 0.15f, Mottle = 0.08f };

        // --- Cards ---

        public static readonly LeafSpray OakSpray = new LeafSpray { Id = "spray.oak", Leaf = OakLeaf(), LeafLength = 0.15f, LeavesPerShoot = new IntRange(7, 9), Shoots = new IntRange(10, 13), ShootLength = 0.5f };
        public static readonly LeafSpray MapleSpray = new LeafSpray { Id = "spray.maple", Leaf = MapleLeaf(), LeafLength = 0.125f, LeafWidth = 0.82f, LeavesPerShoot = new IntRange(6, 8), Shoots = new IntRange(9, 12), ShootLength = 0.5f, Opposite = true, Droop = 0.32f };
        public static readonly LeafSpray JapaneseMapleSpray = new LeafSpray { Id = "spray.momiji", Leaf = JapaneseMapleLeaf(), LeafLength = 0.085f, LeafWidth = 0.9f, LeavesPerShoot = new IntRange(8, 11), Shoots = new IntRange(11, 14), ShootLength = 0.46f, Opposite = true, Droop = 0.2f };
        public static readonly LeafSpray GinkgoSpray = new LeafSpray { Id = "spray.ginkgo", Leaf = GinkgoLeaf(), LeafLength = 0.11f, LeafWidth = 0.85f, LeavesPerShoot = new IntRange(9, 12), Shoots = new IntRange(10, 13), ShootLength = 0.48f, LeafAngle = 0.85f, Droop = 0.1f };
        public static readonly LeafSpray BeechSpray = new LeafSpray { Id = "spray.beech", Leaf = BeechLeaf(), LeafLength = 0.12f, LeavesPerShoot = new IntRange(8, 11), Shoots = new IntRange(10, 13), ShootLength = 0.5f, LeafAngle = 0.75f, Droop = 0.15f };
        public static readonly LeafSpray BirchSpray = new LeafSpray { Id = "spray.birch", Leaf = BirchLeaf(), LeafLength = 0.085f, LeavesPerShoot = new IntRange(10, 14), Shoots = new IntRange(12, 15), ShootLength = 0.5f, Droop = 0.34f, Twig = new Vector3(0.1f, 0.06f, 0.05f) };
        public static readonly LeafSpray WillowStrands = new LeafSpray { Id = "spray.willow", Leaf = WillowLeaf(), LeafLength = 0.13f, LeavesPerShoot = new IntRange(18, 24), Shoots = new IntRange(6, 8), ShootLength = 0.78f, ShootAngle = 0.25f, LeafAngle = 0.5f, Droop = 0.5f, Hanging = true };
        public static readonly LeafSpray LaurelSpray = new LeafSpray { Id = "spray.laurel", Leaf = LaurelLeaf(), LeafLength = 0.165f, LeavesPerShoot = new IntRange(6, 8), Shoots = new IntRange(9, 12), ShootLength = 0.46f, Roughness = 0.5f };
        public static readonly LeafSpray SmallLeaves = new LeafSpray { Id = "spray.small", Leaf = SmallLeaf(), LeafLength = 0.055f, LeavesPerShoot = new IntRange(15, 20), Shoots = new IntRange(13, 16), ShootLength = 0.46f, LeafAngle = 0.8f, Droop = 0.05f };
        public static readonly BlossomSpray CherryBlossom = new BlossomSpray { Id = "blossom.cherry", YoungLeaf = BeechLeaf(), YoungLeaves = 0.1f };
        public static readonly FlowerSpike Wisteria = new FlowerSpike { Id = "raceme.wisteria", Florets = 150, Size = 0.075f, Width = 0.1f, Strands = 3 };
        public static readonly BareTwigs Twigs = new BareTwigs { Id = "twigs.bare" };

        public static readonly LeafScatter OakLitter = new LeafScatter { Id = "litter.oak", Leaf = OakLeaf(), LeafLength = 0.11f, Leaves = 110 };
        public static readonly LeafScatter MapleLitter = new LeafScatter { Id = "litter.maple", Leaf = MapleLeaf(), LeafLength = 0.09f, Leaves = 150 };
        public static readonly LeafScatter GinkgoLitter = new LeafScatter { Id = "litter.ginkgo", Leaf = GinkgoLeaf(), LeafLength = 0.075f, Leaves = 200 };
        public static readonly LeafScatter PetalLitter = new LeafScatter { Id = "litter.petals", Leaf = new LeafForm { Blade = LeafBlade.Petal, Width = 0.42f, Skew = 1.4f, Round = 0.7f, Notch = 0.2f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f, Tone = new Vector3(1f, 0.98f, 0.98f) }, LeafLength = 0.04f, Leaves = 340 };

        // --- Colours ---

        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);
        private static Palette Summer() => new Palette { Shade = V(0.05f, 0.1f, 0.04f), Body = V(0.13f, 0.23f, 0.06f), Sun = V(0.27f, 0.36f, 0.08f) };
        private static Palette Crimson() => new Palette { Shade = V(0.12f, 0.012f, 0.016f), Body = V(0.33f, 0.028f, 0.02f), Sun = V(0.52f, 0.09f, 0.025f), Drift = 0.18f, Patches = 0.16f };
        private static Palette Amber() => new Palette { Shade = V(0.15f, 0.05f, 0.01f), Body = V(0.36f, 0.15f, 0.016f), Sun = V(0.5f, 0.28f, 0.03f), Drift = 0.16f, Patches = 0.15f };
        private static Palette Gold() => new Palette { Shade = V(0.16f, 0.08f, 0.01f), Body = V(0.4f, 0.24f, 0.02f), Sun = V(0.55f, 0.4f, 0.04f), Drift = 0.1f, Patches = 0.1f };
        private static Palette SakuraPink() => new Palette { Shade = V(0.3f, 0.12f, 0.17f), Body = V(0.62f, 0.3f, 0.37f), Sun = V(0.78f, 0.48f, 0.53f), Drift = 0.1f, Patches = 0.12f };
        private static Palette WhiteBlossom() => new Palette { Shade = V(0.3f, 0.27f, 0.28f), Body = V(0.6f, 0.56f, 0.55f), Sun = V(0.78f, 0.75f, 0.71f), Drift = 0.05f, Patches = 0.08f };
        private static Palette WisteriaColours() => new Palette { Shade = V(0.15f, 0.09f, 0.3f), Body = V(0.34f, 0.22f, 0.62f), Sun = V(0.56f, 0.44f, 0.82f), Drift = 0.1f, Patches = 0.12f };
        private static Palette Evergreen() => new Palette { Shade = V(0.03f, 0.075f, 0.04f), Body = V(0.075f, 0.17f, 0.07f), Sun = V(0.17f, 0.28f, 0.08f), Drift = 0.1f };
        private static Palette Sage() => new Palette { Shade = V(0.07f, 0.1f, 0.07f), Body = V(0.2f, 0.26f, 0.15f), Sun = V(0.36f, 0.42f, 0.24f), Drift = 0.08f };
        private static Palette DryGreen() => new Palette { Shade = V(0.08f, 0.1f, 0.03f), Body = V(0.2f, 0.25f, 0.06f), Sun = V(0.38f, 0.4f, 0.1f), Drift = 0.12f };

        // --- Bark ---

        private static BarkForm OakBark() => new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.46f, 0.4f, 0.33f), YoungColour = V(0.3f, 0.24f, 0.2f), Moss = 0.45f, Tile = 2.2f };
        private static BarkForm GreyBark(float moss = 0.3f) => new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.27f, 0.22f, 0.19f), YoungColour = V(0.19f, 0.14f, 0.12f), Moss = moss, Tile = 2f, Bumps = 0.05f };
        private static BarkForm CherryBark() => new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.44f, 0.28f, 0.27f), YoungColour = V(0.3f, 0.18f, 0.17f), Moss = 0.4f, Tile = 1.6f, Bumps = 0.08f };

        // --- A shape most of them share: a trunk that gives way to a few big limbs, and a crown of what's fought its way out from those. ---

        private static TreeForm Spreading(float height, float width, float bole, float girth, FoliageForm foliage, BarkForm bark, CrownShape crown = CrownShape.Hemispherical, float apical = 0.49f, int forks = 5, float wander = 0.1f) => new TreeForm
        {
            Height = new FloatRange(height * 0.82f, height * 1.14f),
            Trunk = new TrunkForm
            {
                Reach = new FloatRange(bole * 0.85f, bole * 1.15f),
                Girth = new FloatRange(girth * 0.88f, girth * 1.12f),
                Lean = new FloatRange(0f, 0.09f),
                Curve = new StemCurve { Wander = wander * 0.5f, Waves = 2f },
                Flare = new FloatRange(0.5f, 0.8f),
                LobeDepth = new FloatRange(0.2f, 0.3f),
            },
            Roots = new RootForm { Reach = new FloatRange(0.14f, 0.2f), Girth = new FloatRange(0.32f, 0.45f) },
            Crown = crown,
            CrownBase = new FloatRange(bole * 0.6f, bole * 0.8f),
            CrownRadius = new FloatRange(width * 0.88f, width * 1.1f),
            CrownBumps = 0.2f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(forks - 1, forks + 1), From = 0.62f, To = 1f,
                    Angle = new Ramp(1.15f, 0.55f), AngleJitter = 0.2f,
                    Length = new Ramp(0.5f, 0.68f), Wander = wander, Waves = 2.5f,
                    Rise = new Ramp(0.03f, 0.05f), RiseFrom = 0.2f, FitRoom = 0.05f,
                },
                new LimbForm
                {
                    Count = new FloatRange(3f, 5f), From = 0.3f, To = 0.95f, OfParent = true,
                    Angle = new Ramp(0.85f, 0.55f), AngleJitter = 0.2f,
                    Length = new Ramp(0.5f, 0.75f), Wander = wander * 1.2f, Waves = 2f,
                    Rise = 0.03f, RiseFrom = 0.3f, FitRoom = 0.05f, Shortest = 1f, StepLength = 0.7f,
                },
            },
            Buds = new BudForm { Years = new IntRange(12, 14), Apical = new FloatRange(apical - 0.01f, apical + 0.01f), Internode = 0.42f, MostNodes = 3200 },
            Bark = bark,
            Foliage = foliage,
        };

        private static FoliageForm Leaves(CardForm card, Palette colours, float size = 3.1f, int cards = 280, float shell = 0.72f) => new FoliageForm
        {
            Card = card, Colours = colours, CardSize = new FloatRange(size * 0.84f, size * 1.16f), MostCards = cards, Shell = shell,
        };

        // --- Species ---

        public static readonly PlantSpecies Oak = new PlantSpecies("oak", WorldAssetKind.Tree, Spreading(18f, 0.6f, 0.45f, 0.045f, Leaves(OakSpray, Summer()), OakBark()))
        { Footprint = 9f, SolidRadius = 1.4f, Variants = 6 };

        /// <summary>An oak that's stood for centuries: as wide as it's tall, low-limbed and heavy.</summary>
        public static readonly PlantSpecies AncientOak = new PlantSpecies("oak.ancient", WorldAssetKind.Tree, Spreading(19f, 0.78f, 0.3f, 0.075f, Leaves(OakSpray, Summer(), 3.3f, 340), OakBark(), forks: 6, wander: 0.15f))
        { Footprint = 12f, SolidRadius = 2.1f, Variants = 4 };

        public static readonly PlantSpecies CrimsonMaple = new PlantSpecies("maple.crimson", WorldAssetKind.Tree, Spreading(16f, 0.52f, 0.36f, 0.036f, Leaves(MapleSpray, Crimson(), 3f, 300), GreyBark(0.25f), apical: 0.5f))
        { Footprint = 8f, SolidRadius = 1f, Variants = 6 };

        public static readonly PlantSpecies GoldMaple = new PlantSpecies("maple.gold", WorldAssetKind.Tree, Spreading(16f, 0.52f, 0.36f, 0.036f, Leaves(MapleSpray, Amber(), 3f, 300), GreyBark(0.25f), apical: 0.5f))
        { Footprint = 8f, SolidRadius = 1f, Variants = 6 };

        /// <summary>A momiji: small and fine, with its limbs fanning out flat in layers. It's the tree of temple gardens.</summary>
        public static readonly PlantSpecies JapaneseMaple = new PlantSpecies("maple.momiji", WorldAssetKind.Tree, Spreading(9.5f, 0.74f, 0.26f, 0.03f, Leaves(JapaneseMapleSpray, Crimson(), 2.4f, 240), GreyBark(0.3f), CrownShape.Umbrella, 0.47f, forks: 6, wander: 0.14f))
        { Footprint = 6f, SolidRadius = 0.6f, Variants = 6 };

        public static readonly PlantSpecies Beech = new PlantSpecies("beech", WorldAssetKind.Tree, Spreading(21f, 0.44f, 0.5f, 0.036f, Leaves(BeechSpray, Summer(), 3.2f, 320), GreyBark(0.35f), CrownShape.Spherical, 0.51f))
        { Footprint = 8f, SolidRadius = 1.1f, Variants = 6 };

        public static readonly PlantSpecies CopperBeech = new PlantSpecies("beech.copper", WorldAssetKind.Tree, Spreading(21f, 0.44f, 0.5f, 0.036f, Leaves(BeechSpray, new Palette { Shade = V(0.1f, 0.03f, 0.03f), Body = V(0.3f, 0.09f, 0.05f), Sun = V(0.52f, 0.24f, 0.08f), Drift = 0.12f }, 3.2f, 320), GreyBark(0.35f), CrownShape.Spherical, 0.51f))
        { Footprint = 8f, SolidRadius = 1.1f, Variants = 5 };

        /// <summary>Ginkgo, which goes a single clear yellow all at once and lets it all fall in a week.</summary>
        public static readonly PlantSpecies Ginkgo = new PlantSpecies("ginkgo", WorldAssetKind.Tree, Spreading(19f, 0.38f, 0.42f, 0.03f, Leaves(GinkgoSpray, Gold(), 3f, 300), new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.5f, 0.44f, 0.38f), YoungColour = V(0.34f, 0.28f, 0.24f), Moss = 0.15f }, CrownShape.Flame, 0.53f, forks: 7))
        { Footprint = 7f, SolidRadius = 0.9f, Variants = 6 };

        private static TreeForm BirchForm(Palette colours) => new TreeForm
        {
            Height = new FloatRange(16f, 23f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.86f, 0.96f), Girth = new FloatRange(0.017f, 0.023f), Lean = new FloatRange(0.01f, 0.1f), Curve = new StemCurve { Wander = 0.035f, Waves = 2.4f }, Flare = new FloatRange(0.25f, 0.45f), LobeDepth = new FloatRange(0.1f, 0.18f) },
            Crown = CrownShape.Flame,
            CrownBase = new FloatRange(0.3f, 0.4f),
            CrownRadius = new FloatRange(0.28f, 0.36f),
            CrownBumps = 0.2f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(11f, 15f), From = 0.35f, To = 1f, Jitter = 0.8f,
                    Angle = new Ramp(0.95f, 0.45f), AngleJitter = 0.16f,
                    Length = new Ramp(0.22f, 0.34f), Wander = 0.1f, Waves = 2f, Sag = 0.05f, Rise = -0.04f, RiseFrom = 0.45f, FitRoom = 0.08f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(12, 14), Apical = new FloatRange(0.52f, 0.54f), Internode = 0.36f, MostNodes = 2600, Tropism = new FloatRange(-0.02f, 0.02f), TropismByOrder = -0.03f, MarkerDensity = 1.3f, Occupancy = 0.66f, Gaps = 0.34f },
            Bark = new BarkForm { Kind = BarkKind.Papery, Colour = V(0.72f, 0.72f, 0.68f), YoungColour = V(0.28f, 0.16f, 0.12f), Moss = 0.1f, Tile = 2.2f, Bumps = 0.05f },
            Foliage = new FoliageForm { Card = BirchSpray, CardSize = new FloatRange(2.6f, 3.5f), MostCards = 320, Spacing = 0.32f, Droop = 0.34f, Colours = colours },
        };

        public static readonly PlantSpecies Birch = new PlantSpecies("birch", WorldAssetKind.Tree, BirchForm(new Palette { Shade = V(0.07f, 0.13f, 0.04f), Body = V(0.18f, 0.29f, 0.07f), Sun = V(0.36f, 0.45f, 0.1f) }))
        { Footprint = 5.5f, SolidRadius = 0.6f, Variants = 6 };

        public static readonly PlantSpecies GoldBirch = new PlantSpecies("birch.gold", WorldAssetKind.Tree, BirchForm(Gold()))
        { Footprint = 5.5f, SolidRadius = 0.6f, Variants = 6 };

        private static TreeForm CherryForm(Palette colours) => new TreeForm
        {
            Height = new FloatRange(10f, 14f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.28f, 0.4f), Girth = new FloatRange(0.042f, 0.056f), Lean = new FloatRange(0.02f, 0.2f), Curve = new StemCurve { Wander = 0.09f, Waves = 2f }, Flare = new FloatRange(0.3f, 0.5f) },
            Roots = new RootForm { Reach = new FloatRange(0.12f, 0.18f) },
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.2f, 0.28f),
            CrownRadius = new FloatRange(0.72f, 0.92f),
            CrownBumps = 0.22f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(4f, 6f), From = 0.55f, To = 1f, Angle = new Ramp(1.2f, 0.7f), AngleJitter = 0.22f,
                    Length = new Ramp(0.5f, 0.74f), Wander = 0.16f, Waves = 3f, Rise = new Ramp(0.02f, 0.05f), RiseFrom = 0.25f, FitRoom = 0.05f,
                },
                new LimbForm
                {
                    Count = new FloatRange(3f, 5f), From = 0.3f, To = 1f, OfParent = true, Angle = new Ramp(0.9f, 0.6f), AngleJitter = 0.2f,
                    Length = new Ramp(0.45f, 0.7f), Wander = 0.16f, Waves = 2f, Sag = 0.04f, Rise = 0.02f, RiseFrom = 0.5f, FitRoom = 0.05f, Shortest = 0.8f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(11, 13), Apical = new FloatRange(0.48f, 0.5f), Internode = 0.36f, MostNodes = 2600, ClumpSize = 3f, Gaps = 0.4f },
            Bark = CherryBark(),
            Foliage = new FoliageForm { Card = CherryBlossom, CardSize = new FloatRange(2.2f, 3f), MostCards = 320, Spacing = 0.36f, Droop = 0.14f, Shell = 0.66f, Colours = colours },
        };

        public static readonly PlantSpecies Sakura = new PlantSpecies("cherry.pink", WorldAssetKind.Tree, CherryForm(SakuraPink()))
        { Footprint = 7.5f, SolidRadius = 0.8f, Variants = 6 };

        public static readonly PlantSpecies WhiteCherry = new PlantSpecies("cherry.white", WorldAssetKind.Tree, CherryForm(WhiteBlossom()))
        { Footprint = 7.5f, SolidRadius = 0.8f, Variants = 5 };

        /// <summary>A weeping willow: a low trunk and a curtain, all the way to the ground.</summary>
        public static readonly PlantSpecies Willow = new PlantSpecies("willow", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(13f, 17f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.3f, 0.42f), Girth = new FloatRange(0.05f, 0.065f), Lean = new FloatRange(0.03f, 0.14f), Curve = new StemCurve { Wander = 0.08f, Waves = 2f }, Flare = new FloatRange(0.5f, 0.8f) },
            Roots = new RootForm { Reach = new FloatRange(0.14f, 0.2f) },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.14f, 0.22f),
            CrownRadius = new FloatRange(0.62f, 0.76f),
            CrownBumps = 0.14f,
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(5f, 7f), From = 0.6f, To = 1f, Angle = new Ramp(1f, 0.5f), AngleJitter = 0.2f, Length = new Ramp(0.5f, 0.72f), Wander = 0.1f, Waves = 2.5f, Rise = 0.03f, RiseFrom = 0.1f, FitRoom = 0.06f },
            },
            Buds = new BudForm { Years = new IntRange(9, 11), Internode = 0.46f, MostNodes = 1900, Apical = new FloatRange(0.49f, 0.51f), Tropism = new FloatRange(-0.06f, 0f), TropismByOrder = -0.14f, Hollow = 0.85f, Gaps = 0.3f },
            Bark = new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.5f, 0.46f, 0.4f), YoungColour = V(0.4f, 0.32f, 0.16f), Moss = 0.5f },
            Foliage = new FoliageForm { Card = WillowStrands, CardSize = new FloatRange(4.4f, 6.4f), Aspect = 0.6f, MostCards = 300, Spacing = 0.26f, Pose = CardPose.Hanging, Fold = 0.2f, Droop = 0f, Cross = 0.3f, Shade = 0f, Shell = 0.6f, Colours = new Palette { Shade = V(0.07f, 0.11f, 0.03f), Body = V(0.2f, 0.28f, 0.06f), Sun = V(0.4f, 0.46f, 0.1f) } },
        })
        { Footprint = 9f, SolidRadius = 1.2f, Variants = 5 };

        /// <summary>A wisteria grown out into a tree: a tangle of old wood with a rain of flowers from it.</summary>
        public static readonly PlantSpecies WisteriaTree = new PlantSpecies("wisteria", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(8f, 11f),
            Trunk = new TrunkForm { Stems = new IntRange(2, 3), StemSpread = new FloatRange(0.2f, 0.4f), Reach = new FloatRange(0.5f, 0.66f), Girth = new FloatRange(0.03f, 0.04f), Lean = new FloatRange(0.05f, 0.22f), Curve = new StemCurve { Wander = 0.2f, Waves = 3.5f }, Flare = new FloatRange(0.2f, 0.4f) },
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.42f, 0.52f),
            CrownRadius = new FloatRange(0.66f, 0.84f),
            CrownBumps = 0.2f,
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(4f, 6f), From = 0.5f, To = 1f, Angle = new Ramp(1.2f, 0.8f), AngleJitter = 0.24f, Length = new Ramp(0.5f, 0.7f), Wander = 0.24f, Waves = 3.5f, Rise = 0.03f, RiseFrom = 0.3f, FitRoom = 0.05f },
            },
            Buds = new BudForm { Years = new IntRange(10, 12), Internode = 0.36f, MostNodes = 1800, Tropism = new FloatRange(-0.04f, 0.02f), Gaps = 0.34f, Wobble = 0.22f },
            Bark = new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.46f, 0.42f, 0.38f), YoungColour = V(0.36f, 0.3f, 0.24f), Moss = 0.2f, Bumps = 0.12f },
            Foliage = new FoliageForm { Card = Wisteria, CardSize = new FloatRange(2.6f, 3.8f), Aspect = 0.62f, MostCards = 280, Spacing = 0.3f, Pose = CardPose.Hanging, Fold = 0.2f, Droop = 0f, Cross = 0.35f, Shade = 0f, Shell = 0.55f, Colours = WisteriaColours() },
        })
        { Footprint = 7f, SolidRadius = 0.6f, Variants = 5 };

        /// <summary>A camphor: evergreen, broad and dark, and old before anything around it was planted.</summary>
        public static readonly PlantSpecies Camphor = new PlantSpecies("camphor", WorldAssetKind.Tree, Spreading(20f, 0.62f, 0.34f, 0.055f, Leaves(LaurelSpray, Evergreen(), 3.2f, 340), new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.46f, 0.42f, 0.34f), YoungColour = V(0.3f, 0.3f, 0.2f), Moss = 0.6f, Tile = 2f }, forks: 6, wander: 0.12f))
        { Footprint = 10f, SolidRadius = 1.6f, Variants = 5 };

        /// <summary>An olive: small, silver, and twisted round itself.</summary>
        public static readonly PlantSpecies Olive = new PlantSpecies("olive", WorldAssetKind.Tree, Spreading(8.5f, 0.66f, 0.34f, 0.052f, Leaves(SmallLeaves, Sage(), 2.4f, 300), new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.52f, 0.48f, 0.42f), YoungColour = V(0.4f, 0.36f, 0.3f), Moss = 0.05f, Bumps = 0.2f }, forks: 4, wander: 0.26f))
        { Footprint = 5.5f, SolidRadius = 0.8f, Variants = 5 };

        /// <summary>An acacia: a flat green roof on thin limbs, the shape of a dry country.</summary>
        public static readonly PlantSpecies Acacia = new PlantSpecies("acacia", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(9f, 13f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.34f, 0.48f), Girth = new FloatRange(0.03f, 0.04f), Lean = new FloatRange(0.02f, 0.12f), Curve = new StemCurve { Wander = 0.08f, Waves = 2f }, Flare = new FloatRange(0.2f, 0.4f) },
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.72f, 0.8f),
            CrownRadius = new FloatRange(0.82f, 1.05f),
            CrownBumps = 0.12f,
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(4f, 6f), From = 0.7f, To = 1f, Angle = new Ramp(0.95f, 0.5f), AngleJitter = 0.16f, Length = new Ramp(0.5f, 0.78f), Wander = 0.1f, Waves = 2f, Rise = -0.01f, RiseFrom = 0.6f, Fit = false },
            },
            Buds = new BudForm { Years = new IntRange(11, 13), Internode = 0.36f, MostNodes = 2200, Hollow = 0.1f, Gaps = 0.24f, ClumpSize = 3f, Tropism = new FloatRange(-0.02f, 0.02f), MarkerDensity = 2.2f, Occupancy = 0.6f },
            Bark = new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.48f, 0.4f, 0.32f), YoungColour = V(0.36f, 0.28f, 0.2f), Moss = 0f },
            Foliage = new FoliageForm { Card = SmallLeaves, CardSize = new FloatRange(2.8f, 3.7f), MostCards = 300, Spacing = 0.3f, Tilt = 0.2f, Droop = 0.06f, Cross = 0.06f, Colours = DryGreen() },
        })
        { Footprint = 8f, SolidRadius = 0.7f, Variants = 5 };

        /// <summary>A baobab: a bottle with roots for a crown.</summary>
        public static readonly PlantSpecies Baobab = new PlantSpecies("baobab", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(13f, 17f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.56f, 0.66f), Girth = new FloatRange(0.15f, 0.2f), Lean = new FloatRange(0f, 0.04f), Curve = new StemCurve { Wander = 0.01f, Waves = 1f }, Flare = new FloatRange(0.1f, 0.25f), FlareHeight = 0.2f, Lobes = new IntRange(5, 7), LobeDepth = new FloatRange(0.18f, 0.28f) },
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.58f, 0.66f),
            CrownRadius = new FloatRange(0.6f, 0.74f),
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(6f, 8f), From = 0.9f, To = 1f, Angle = new Ramp(1.2f, 0.7f), AngleJitter = 0.2f, Length = new Ramp(0.32f, 0.5f), Wander = 0.1f, Waves = 2f, Rise = 0.05f, RiseFrom = 0.2f, Fit = false },
            },
            Buds = new BudForm { Years = new IntRange(9, 11), Internode = 0.38f, MostNodes = 1400, Gaps = 0.34f, ClumpSize = 2.6f, MarkerDensity = 1.6f, Occupancy = 0.62f },
            PipeExponent = 1.8f,
            Bark = new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.62f, 0.54f, 0.46f), YoungColour = V(0.5f, 0.42f, 0.34f), Tile = 3f, Bumps = 0.09f },
            Foliage = new FoliageForm { Card = SmallLeaves, CardSize = new FloatRange(2.1f, 2.8f), MostCards = 190, Spacing = 0.3f, Tilt = 0.4f, Colours = DryGreen() },
        })
        { Footprint = 7.5f, SolidRadius = 2.5f, Variants = 4 };

        // --- What falls from them ---

        private static PlantSpecies Litter(string id, CardForm card, Palette colours) => new PlantSpecies(id, WorldAssetKind.GroundCover, new GroundForm { Card = card, Colours = colours, Size = new FloatRange(2.4f, 3.6f) })
        { Footprint = 1.2f, Scale = new FloatRange(0.8f, 1.3f), Sways = false, Variants = 3 };

        public static readonly PlantSpecies OakLeaves = Litter("litter.oak", OakLitter, new Palette { Shade = V(0.1f, 0.06f, 0.02f), Body = V(0.3f, 0.19f, 0.06f), Sun = V(0.44f, 0.3f, 0.1f) });
        public static readonly PlantSpecies CrimsonLeaves = Litter("litter.crimson", MapleLitter, Crimson());
        public static readonly PlantSpecies AmberLeaves = Litter("litter.amber", MapleLitter, Amber());
        public static readonly PlantSpecies GinkgoLeaves = Litter("litter.ginkgo", GinkgoLitter, Gold());
        public static readonly PlantSpecies FallenPetals = Litter("litter.petals", PetalLitter, SakuraPink());
    }
}
