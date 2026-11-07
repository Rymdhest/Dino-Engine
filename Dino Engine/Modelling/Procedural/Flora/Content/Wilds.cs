using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>What grows where nothing else does: bamboo, palms and the jungle, cactus and the desert, and the dead.</summary>
    [FloraCollection]
    public static class Wilds
    {
        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        // --- Cards ---

        public static readonly PinnateFrond PalmFrond = new PinnateFrond { Id = "frond.palm", Pairs = 38, Longest = 0.48f, Skew = 0.82f, Reduce = 0.42f, Slope = 1.0f, Rachis = V(0.34f, 0.3f, 0.16f), Pinna = new LeafForm { Width = 0.055f, Skew = 0.86f, Round = 0.6f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f } };
        public static readonly PinnateFrond TreeFernFrond = new PinnateFrond { Id = "frond.tree", Pairs = 24, Longest = 0.48f, Skew = 0.7f, Reduce = 0.66f, Slope = 1.2f, Pinnules = 11, Pinna = new LeafForm { Width = 0.3f, Skew = 0.8f, Round = 0.7f, Lobes = 2, LobeDepth = 0.3f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f } };
        public static readonly LeafSpray BambooLeaves = new LeafSpray { Id = "spray.bamboo", Leaf = new LeafForm { Width = 0.11f, Skew = 0.72f, Round = 0.66f, Veins = 0, VeinStrength = 0.12f, Mottle = 0.06f }, LeafLength = 0.19f, LeafWidth = 1f, LeavesPerShoot = new IntRange(5, 7), Shoots = new IntRange(9, 12), ShootLength = 0.42f, LeafAngle = 0.5f, Droop = 0.5f, Fold = 0.45f, Twig = new Vector3(0.22f, 0.26f, 0.1f) };

        // --- Bamboo: canes from one root, each thin and jointed, leafy only near the top. ---

        private static TreeForm BambooForm(Vector3 cane, float height, Palette leaves) => new TreeForm
        {
            Height = new FloatRange(height * 0.8f, height * 1.16f),
            Trunk = new TrunkForm { Stems = new IntRange(8, 13), StemSpread = new FloatRange(0.6f, 1.7f), Girth = new FloatRange(0.0085f, 0.0115f), Lean = new FloatRange(0.02f, 0.1f), Curve = new StemCurve { Wander = 0.012f, Waves = 1.4f }, Flare = new FloatRange(0f, 0f), Lobes = new IntRange(0, 0) },
            Crown = CrownShape.Flame,
            CrownBase = new FloatRange(0.3f, 0.42f),
            CrownRadius = new FloatRange(0.2f, 0.26f),
            CrownBumps = 0.1f,
            Limbs = new[]
            {
                new LimbForm { Spacing = 1.6f, Whorl = 2, From = 0.42f, To = 1f, Divergence = 90f, Jitter = 0.3f, Angle = new Ramp(1.15f, 0.7f), AngleJitter = 0.15f, Length = new Ramp(0.1f, 0.17f), Profile = new Ramp(1f, 0.5f), Wander = 0.1f, Waves = 1f, Sag = 0.2f, Rise = -0.1f, RiseFrom = 0.3f, Fit = false, Shortest = 0.5f, StepLength = 0.5f },
            },
            PipeExponent = 3f,
            WoodBudget = 240f,
            MostAxes = 14,
            Bark = new BarkForm { Kind = BarkKind.Cane, Colour = cane, YoungColour = cane, Tile = 3f, Bumps = 0f, NodeSpacing = 1.9f, MostSides = 6 },
            Foliage = new FoliageForm { Card = BambooLeaves, CardSize = new FloatRange(2f, 2.8f), Aspect = 0.8f, MostCards = 220, Spacing = 0.4f, Pose = CardPose.Spreading, Tilt = 0.35f, Droop = 0.3f, Shade = 0f, Colours = leaves },
        };

        public static readonly PlantSpecies GreenBamboo = new PlantSpecies("bamboo", WorldAssetKind.Tree, BambooForm(V(0.36f, 0.5f, 0.2f), 20f, new Palette { Shade = V(0.05f, 0.11f, 0.04f), Body = V(0.14f, 0.27f, 0.07f), Sun = V(0.29f, 0.42f, 0.09f), Drift = 0.1f }))
        { Footprint = 2.8f, SolidRadius = 1.2f, Variants = 6, Scale = new FloatRange(0.8f, 1.15f) };

        public static readonly PlantSpecies BlackBamboo = new PlantSpecies("bamboo.black", WorldAssetKind.Tree, BambooForm(V(0.1f, 0.09f, 0.08f), 15f, new Palette { Shade = V(0.04f, 0.09f, 0.04f), Body = V(0.1f, 0.21f, 0.08f), Sun = V(0.22f, 0.34f, 0.1f), Drift = 0.1f }))
        { Footprint = 2.6f, SolidRadius = 1.1f, Variants = 5, Scale = new FloatRange(0.8f, 1.15f) };

        // --- Palms: one stem and a head of fronds. ---

        private static TreeForm PalmForm(float height, float lean, float frond, int leaves) => new TreeForm
        {
            Height = new FloatRange(height * 0.82f, height * 1.18f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.022f, 0.03f), Lean = new FloatRange(lean * 0.5f, lean), Curve = new StemCurve { Wander = 0.02f, Waves = 1.3f, Rise = 0.02f }, Flare = new FloatRange(0.2f, 0.4f), Lobes = new IntRange(0, 0) },
            CrownBase = new FloatRange(0.8f, 0.9f),
            CrownRadius = new FloatRange(0.16f, 0.2f),
            Bark = new BarkForm { Kind = BarkKind.Ringed, Colour = V(0.52f, 0.44f, 0.34f), YoungColour = V(0.52f, 0.44f, 0.34f), Tile = 1.6f, Bumps = 0.03f, MostSides = 10 },
            Head = new RosetteForm
            {
                Card = PalmFrond, Leaves = new IntRange(leaves, leaves + 5), Length = new FloatRange(frond * 0.86f, frond * 1.1f), Inner = 0.72f, Aspect = 0.32f, Lean = new FloatRange(0.15f, 1.3f), Arch = new FloatRange(1f, 2f), Curl = 1.3f, FootSpread = 0.14f, Segments = 6, Fold = 0.3f, Shell = 0.4f, Joint = PlantJoint.Frond with { Touch = 0f }, Links = 2,
                Colours = new Palette { Shade = V(0.04f, 0.08f, 0.04f), Body = V(0.085f, 0.16f, 0.06f), Sun = V(0.17f, 0.25f, 0.08f), Drift = 0.1f },
            },
        };

        public static readonly PlantSpecies CoconutPalm = new PlantSpecies("palm.coconut", WorldAssetKind.Tree, PalmForm(16f, 0.3f, 6.2f, 13))
        { Footprint = 4f, SolidRadius = 0.55f, Variants = 6 };

        public static readonly PlantSpecies DatePalm = new PlantSpecies("palm.date", WorldAssetKind.Tree, PalmForm(13f, 0.08f, 5.2f, 17))
        { Footprint = 4f, SolidRadius = 0.6f, Variants = 5 };

        public static readonly PlantSpecies TreeFern = new PlantSpecies("fern.tree", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(4.5f, 7.5f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.042f, 0.056f), Lean = new FloatRange(0.02f, 0.14f), Curve = new StemCurve { Wander = 0.05f, Waves = 1.6f }, Flare = new FloatRange(0.1f, 0.3f), Lobes = new IntRange(0, 0) },
            CrownBase = new FloatRange(0.8f, 0.9f),
            CrownRadius = new FloatRange(0.3f, 0.4f),
            Bark = new BarkForm { Kind = BarkKind.Fibrous, Colour = V(0.3f, 0.22f, 0.15f), YoungColour = V(0.3f, 0.22f, 0.15f), Tile = 1.4f, MostSides = 8, Moss = 0.3f },
            Head = new RosetteForm
            {
                Card = TreeFernFrond, Leaves = new IntRange(11, 16), Length = new FloatRange(3f, 4.2f), Aspect = 0.42f, Lean = new FloatRange(0.2f, 1.1f), Arch = new FloatRange(0.8f, 1.5f), Segments = 5, Joint = PlantJoint.Frond with { Touch = 0f }, Links = 2,
                Colours = new Palette { Shade = V(0.03f, 0.08f, 0.04f), Body = V(0.09f, 0.2f, 0.07f), Sun = V(0.2f, 0.33f, 0.09f), Drift = 0.1f },
            },
        })
        { Footprint = 3f, SolidRadius = 0.4f, Variants = 5 };

        public static readonly PlantSpecies Banana = new PlantSpecies("banana", WorldAssetKind.Shrub, new RosetteForm
        {
            Card = Undergrowth.BananaLeaf, Leaves = new IntRange(7, 11), Length = new FloatRange(3.4f, 4.6f), Aspect = 0.32f, Inner = 0.8f, Lean = new FloatRange(0.1f, 0.95f), Arch = new FloatRange(0.7f, 1.6f), Curl = 1.8f, FootSpread = 0.2f, Fold = 0.14f, Segments = 5,
            Colours = new Palette { Shade = V(0.07f, 0.14f, 0.05f), Body = V(0.17f, 0.31f, 0.08f), Sun = V(0.33f, 0.45f, 0.1f), Drift = 0.1f },
        })
        { Footprint = 2.2f, Scale = new FloatRange(0.8f, 1.2f) };

        // --- Cactus and its like. ---

        private static readonly Palette AgaveColours = new Palette { Shade = V(0.09f, 0.15f, 0.12f), Body = V(0.24f, 0.34f, 0.26f), Sun = V(0.42f, 0.5f, 0.36f), Drift = 0.08f };

        public static readonly PlantSpecies Saguaro = new PlantSpecies("cactus.saguaro", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(7f, 11f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.05f, 0.068f), Lean = new FloatRange(0f, 0.03f), Curve = new StemCurve { Wander = 0.008f, Waves = 1f }, Flare = new FloatRange(0f, 0.1f), Lobes = new IntRange(0, 0) },
            Crown = CrownShape.Cylindrical,
            CrownBase = new FloatRange(0.2f, 0.3f),
            CrownRadius = new FloatRange(0.34f, 0.44f),
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(1f, 4f), From = 0.34f, To = 0.72f, Divergence = 130f, Angle = new Ramp(1.55f, 1.4f), Length = new Ramp(0.32f, 0.5f), Wander = 0.02f, Waves = 1f, Rise = 1.2f, RiseFrom = 0.14f, Fit = false, StepLength = 0.3f },
            },
            PipeExponent = 1.6f,
            WoodBudget = 1000f,
            TipRadius = 0.2f,
            Bark = new BarkForm { Kind = BarkKind.Stem, Colour = V(0.2f, 0.32f, 0.14f), YoungColour = V(0.2f, 0.32f, 0.14f), Ribs = 11, RibDepth = 0.1f, Bumps = 0f, MostSides = 22, RoundTips = true },
        })
        { Footprint = 2.4f, SolidRadius = 0.6f, Variants = 5 };

        public static readonly PlantSpecies BarrelCactus = new PlantSpecies("cactus.barrel", WorldAssetKind.GroundCover, new TreeForm
        {
            Height = new FloatRange(0.9f, 1.6f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.36f, 0.48f), Lean = new FloatRange(0f, 0.06f), Curve = new StemCurve(), Flare = new FloatRange(0f, 0f), Lobes = new IntRange(0, 0) },
            Crown = CrownShape.Cylindrical,
            CrownRadius = new FloatRange(0.3f, 0.4f),
            PipeExponent = 1.4f,
            WoodBudget = 1000f,
            TipRadius = 0.3f,
            Bark = new BarkForm { Kind = BarkKind.Stem, Colour = V(0.22f, 0.36f, 0.16f), YoungColour = V(0.22f, 0.36f, 0.16f), Ribs = 13, RibDepth = 0.14f, Bumps = 0f, MostSides = 26, RoundTips = true },
        })
        { Footprint = 0.9f, SolidRadius = 0.4f, Sways = false, Variants = 3, Scale = new FloatRange(0.7f, 1.4f) };

        public static readonly PlantSpecies PricklyPear = new PlantSpecies("cactus.pear", WorldAssetKind.Shrub, new PadForm())
        { Footprint = 1.3f, Sways = false, Variants = 4, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Agave = new PlantSpecies("agave", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Undergrowth.Blade, Leaves = new IntRange(16, 24), Length = new FloatRange(1.3f, 1.9f), Inner = 0.5f, Aspect = 0.2f, Lean = new FloatRange(0.05f, 1.25f), Arch = new FloatRange(0.1f, 0.7f), FootSpread = 0.05f, Fold = 0.34f, Segments = 3, Shell = 0.3f, Joint = PlantJoint.Leaf with { Give = 0.1f, MostBend = 0.2f, Flutter = 0f }, Links = 1, Colours = AgaveColours,
        })
        { Footprint = 1.1f, Scale = new FloatRange(0.7f, 1.4f) };

        // --- The dead. ---

        private static BarkForm SilverWood(float moss = 0.1f) => new BarkForm { Kind = BarkKind.Bare, Colour = V(0.5f, 0.47f, 0.44f), YoungColour = V(0.44f, 0.4f, 0.37f), Moss = moss, Tile = 2.6f, Bumps = 0.12f };
        private static BarkForm Burnt() => new BarkForm { Kind = BarkKind.Charred, Colour = V(0.9f, 0.84f, 0.8f), YoungColour = V(0.7f, 0.65f, 0.6f), Tile = 1.8f, Bumps = 0.1f };
        private static BarkForm BlackBark() => new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.13f, 0.11f, 0.12f), YoungColour = V(0.1f, 0.08f, 0.09f), Moss = 0.25f, MossColour = V(0.09f, 0.14f, 0.09f), Tile = 2f, Bumps = 0.16f };

        // A tangle of limbs with nothing on them. The bare fine wood has to be there for it to read as a tree and not a coat stand.
        private static TreeForm Snag(float height, float girth, BarkForm bark, Palette twigs, float wander = 0.24f, bool beard = false, Decay? decay = null) => new TreeForm
        {
            Height = new FloatRange(height * 0.8f, height * 1.18f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.5f, 0.72f), Girth = new FloatRange(girth * 0.85f, girth * 1.15f), Lean = new FloatRange(0.03f, 0.2f), Curve = new StemCurve { Wander = wander * 0.5f, Waves = 2.5f }, Flare = new FloatRange(0.4f, 0.7f), LobeDepth = new FloatRange(0.2f, 0.34f) },
            Roots = new RootForm { Reach = new FloatRange(0.16f, 0.24f), Girth = new FloatRange(0.3f, 0.42f) },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.3f, 0.4f),
            CrownRadius = new FloatRange(0.42f, 0.56f),
            CrownBumps = 0.3f,
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(5f, 7f), From = 0.45f, To = 1f, Angle = new Ramp(1.25f, 0.6f), AngleJitter = 0.26f, Length = new Ramp(0.4f, 0.6f), Wander = wander, Waves = 3.4f, Rise = 0.03f, RiseFrom = 0.3f, FitRoom = 0.08f },
                new LimbForm { Count = new FloatRange(3f, 5f), From = 0.3f, To = 1f, OfParent = true, Angle = new Ramp(0.95f, 0.6f), AngleJitter = 0.24f, Length = new Ramp(0.4f, 0.65f), Wander = wander * 1.2f, Waves = 2.6f, Rise = 0.01f, RiseFrom = 0.4f, FitRoom = 0.08f, Shortest = 0.8f, StepLength = 0.6f },
            },
            Buds = new BudForm { Years = new IntRange(7, 9), Internode = 0.42f, MostNodes = 1300, Hollow = 0.6f, Gaps = 0.46f, ClumpSize = 3f, Wobble = 0.3f, Tropism = new FloatRange(-0.02f, 0.05f) },
            Bark = bark,
            Foliage = new FoliageForm { Card = Broadleaves.Twigs, CardSize = new FloatRange(2.4f, 3.4f), MostCards = 130, Spacing = 0.5f, Fold = 0.16f, Droop = 0.1f, Cross = 0.4f, Shell = 0.2f, Shade = 0f, Colours = twigs },
            Hangers = beard ? new FoliageForm { Card = Undergrowth.Beard, CardSize = new FloatRange(1.8f, 3.2f), Aspect = 0.5f, MostCards = 70, Spacing = 0.7f, Pose = CardPose.Hanging, Fold = 0.1f, Droop = 0f, Cross = 0.25f, Shell = 0.2f, Shade = 0f, Colours = new Palette { Shade = V(0.14f, 0.17f, 0.13f), Body = V(0.3f, 0.34f, 0.26f), Sun = V(0.46f, 0.48f, 0.38f), Drift = 0.08f } } : null,
            Dead = decay,
        };

        private static readonly Palette Grey = new Palette { Shade = V(0.4f, 0.38f, 0.36f), Body = V(0.56f, 0.53f, 0.5f), Sun = V(0.7f, 0.67f, 0.62f), Drift = 0.05f };
        private static readonly Palette Black = new Palette { Shade = V(0.2f, 0.18f, 0.2f), Body = V(0.28f, 0.25f, 0.27f), Sun = V(0.36f, 0.33f, 0.34f), Drift = 0.05f };
        private static readonly Palette Soot = new Palette { Shade = V(0.1f, 0.09f, 0.09f), Body = V(0.16f, 0.14f, 0.14f), Sun = V(0.2f, 0.18f, 0.18f), Drift = 0.03f };

        /// <summary>The silver ghost of a tree, in a dry place or a drowned one.</summary>
        public static readonly PlantSpecies SilverSnag = new PlantSpecies("snag.silver", WorldAssetKind.Tree, Snag(15f, 0.03f, SilverWood(), Grey))
        { Footprint = 4.5f, SolidRadius = 0.8f, Variants = 5 };

        /// <summary>What the dark woods are made of: black, gnarled, and bearded with lichen.</summary>
        public static readonly PlantSpecies BlackThorn = new PlantSpecies("snag.black", WorldAssetKind.Tree, Snag(17f, 0.042f, BlackBark(), Black, 0.34f, beard: true))
        { Footprint = 6f, SolidRadius = 1.1f, Variants = 6 };

        /// <summary>A wood that's burnt: charcoal, with its top gone.</summary>
        public static readonly PlantSpecies CharredTree = new PlantSpecies("snag.charred", WorldAssetKind.Tree, Snag(14f, 0.034f, Burnt(), Soot, 0.2f, decay: new Decay { Broken = new FloatRange(0.7f, 1f) }))
        { Footprint = 4f, SolidRadius = 0.8f, Variants = 5 };

        /// <summary>A bald cypress: a flared foot standing in the water, hung with moss.</summary>
        public static readonly PlantSpecies SwampCypress = new PlantSpecies("cypress.swamp", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(16f, 22f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.82f, 0.94f), Girth = new FloatRange(0.03f, 0.04f), Lean = new FloatRange(0f, 0.06f), Curve = new StemCurve { Wander = 0.03f, Waves = 2f }, Flare = new FloatRange(1f, 1.5f), FlareHeight = 0.13f, Lobes = new IntRange(7, 9), LobeDepth = new FloatRange(0.3f, 0.42f) },
            Roots = new RootForm { Reach = new FloatRange(0.14f, 0.2f), Rise = 0.9f },
            RootCount = new IntRange(7, 9),
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.44f, 0.54f),
            CrownRadius = new FloatRange(0.32f, 0.42f),
            CrownBumps = 0.24f,
            Limbs = new[]
            {
                new LimbForm { Count = new FloatRange(8f, 11f), From = 0.48f, To = 1f, Angle = new Ramp(1.4f, 0.8f), AngleJitter = 0.2f, Length = new Ramp(0.3f, 0.46f), Wander = 0.1f, Waves = 2.4f, Rise = 0.02f, RiseFrom = 0.4f, FitRoom = 0.06f, StepLength = 0.6f },
            },
            Buds = new BudForm { Years = new IntRange(9, 11), Internode = 0.42f, MostNodes = 1700, ClumpSize = 3.6f, Gaps = 0.44f, Hollow = 0.8f },
            Bark = new BarkForm { Kind = BarkKind.Fibrous, Colour = V(0.5f, 0.42f, 0.36f), YoungColour = V(0.4f, 0.32f, 0.26f), Moss = 0.6f, Tile = 2.4f },
            Foliage = new FoliageForm { Card = Conifers.RedwoodSprigs, CardSize = new FloatRange(2.4f, 3.2f), MostCards = 210, Tilt = 0.3f, Droop = 0.24f, Colours = new Palette { Shade = V(0.04f, 0.09f, 0.04f), Body = V(0.12f, 0.2f, 0.07f), Sun = V(0.25f, 0.33f, 0.09f) } },
            Hangers = new FoliageForm { Card = Undergrowth.Beard, CardSize = new FloatRange(2.2f, 3.6f), Aspect = 0.5f, MostCards = 80, Spacing = 0.7f, Pose = CardPose.Hanging, Fold = 0.1f, Droop = 0f, Cross = 0.25f, Shell = 0.2f, Shade = 0f, Colours = new Palette { Shade = V(0.11f, 0.13f, 0.09f), Body = V(0.25f, 0.28f, 0.2f), Sun = V(0.4f, 0.42f, 0.3f), Drift = 0.08f } },
        })
        { Footprint = 6f, SolidRadius = 1.2f, Variants = 5 };

        // --- What lies on the ground. A log is in the bark of whatever fell. ---

        private static readonly FungusForm ShelfFungi = new FungusForm { Count = new IntRange(3, 5), Spread = 0.24f, Height = new FloatRange(0.05f, 0.12f), CapRadius = new FloatRange(0.22f, 0.36f), Dome = 0.3f, StemWidth = 0.1f, Cap = V(0.4f, 0.26f, 0.12f), CapTop = V(0.56f, 0.42f, 0.22f), Gills = V(0.7f, 0.64f, 0.5f) };

        private static PlantSpecies Log(string id, BarkForm bark, float radius, float length, bool fungi = true) => new PlantSpecies(id, WorldAssetKind.Deadwood, new DeadwoodForm
        {
            Shape = DeadwoodShape.Log, Radius = new FloatRange(radius * 0.8f, radius * 1.2f), Length = new FloatRange(length * 0.75f, length * 1.2f), Bark = bark, Fungi = fungi ? ShelfFungi : null,
        })
        { Footprint = length * 0.55f, SolidRadius = radius, SolidLength = length, Sways = false, Variants = 4, Scale = new FloatRange(0.8f, 1.2f) };

        private static PlantSpecies Stump(string id, BarkForm bark, float radius, bool fungi = true) => new PlantSpecies(id, WorldAssetKind.Deadwood, new DeadwoodForm
        {
            Shape = DeadwoodShape.Stump, Radius = new FloatRange(radius * 0.8f, radius * 1.2f), Length = new FloatRange(0.7f, 1.8f), Bark = bark, Fungi = fungi ? ShelfFungi : null,
        })
        { Footprint = radius * 2f, SolidRadius = radius * 1.05f, Sways = false, Variants = 4, Scale = new FloatRange(0.8f, 1.2f) };

        private static BarkForm Mossy(BarkKind kind, Vector3 colour, float moss = 0.55f) => new BarkForm { Kind = kind, Colour = colour, YoungColour = colour * 0.8f, Moss = moss, Tile = 2.2f };

        public static readonly PlantSpecies PineLog = Log("log.pine", Mossy(BarkKind.Plated, V(0.5f, 0.42f, 0.36f)), 0.62f, 9f);
        public static readonly PlantSpecies OakLog = Log("log.oak", Mossy(BarkKind.Furrowed, V(0.5f, 0.44f, 0.36f)), 0.74f, 8f);
        public static readonly PlantSpecies BirchLog = Log("log.birch", Mossy(BarkKind.Papery, V(0.66f, 0.66f, 0.62f), 0.5f), 0.36f, 7.5f);
        public static readonly PlantSpecies RedwoodLog = Log("log.redwood", Mossy(BarkKind.Fibrous, V(0.8f, 0.46f, 0.3f), 0.6f), 1.5f, 15f);
        public static readonly PlantSpecies SilverLog = Log("log.silver", SilverWood(0f), 0.5f, 7.5f, fungi: false);
        public static readonly PlantSpecies BlackLog = Log("log.black", BlackBark(), 0.6f, 8.5f);
        public static readonly PlantSpecies CharredLog = Log("log.charred", Burnt(), 0.55f, 7.5f, fungi: false);

        public static readonly PlantSpecies PineStump = Stump("stump.pine", Mossy(BarkKind.Plated, V(0.5f, 0.42f, 0.36f), 0.45f), 0.66f);
        public static readonly PlantSpecies OakStump = Stump("stump.oak", Mossy(BarkKind.Furrowed, V(0.5f, 0.44f, 0.36f), 0.45f), 0.8f);
        public static readonly PlantSpecies RedwoodStump = Stump("stump.redwood", Mossy(BarkKind.Fibrous, V(0.8f, 0.46f, 0.3f), 0.5f), 1.9f);
        public static readonly PlantSpecies SilverStump = Stump("stump.silver", SilverWood(0f), 0.55f, fungi: false);
        public static readonly PlantSpecies CharredStump = Stump("stump.charred", Burnt(), 0.6f, fungi: false);
    }
}
