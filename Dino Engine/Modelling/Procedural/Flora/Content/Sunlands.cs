using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The sun's country. The cypress hills: dark spires of cypress over umbrella pines and holm oaks, and between them the scrub that smells of resin in the heat, rosemary, rock rose, broom and
    /// thyme. And the red scrub, further south: pale gum trees over hummocks of spinifex, and grass trees black from the last fire.
    /// </summary>
    [FloraCollection]
    public static class Sunlands
    {
        // --- Leaves and cards ---

        // A gum leaf: long, narrow and curved like a blade, hanging edge on to the sun.
        private static LeafForm GumLeaf() => new LeafForm { Width = 0.11f, Skew = 0.82f, Round = 0.78f, DripTip = 0.7f, Veins = 0, VeinStrength = 0.15f, Mottle = 0.07f };

        public static readonly LeafSpray GumSpray = new LeafSpray { Id = "spray.gum", Leaf = GumLeaf(), LeafLength = 0.16f, LeavesPerShoot = new IntRange(9, 12), Shoots = new IntRange(8, 11), ShootLength = 0.5f, ShootAngle = 0.62f, LeafAngle = 0.35f, Droop = 0.68f, Roughness = 0.6f, Twig = new Vector3(0.24f, 0.13f, 0.08f) };

        // --- Colours ---

        private static Palette CypressGreen() => new Palette { Shade = V(0.018f, 0.045f, 0.03f), Body = V(0.045f, 0.105f, 0.055f), Sun = V(0.1f, 0.18f, 0.07f), Drift = 0.08f, Patches = 0.06f };
        private static Palette UmbrellaGreen() => new Palette { Shade = V(0.03f, 0.07f, 0.04f), Body = V(0.08f, 0.17f, 0.07f), Sun = V(0.18f, 0.29f, 0.09f), Drift = 0.1f, Patches = 0.08f };
        private static Palette HolmOak() => new Palette { Shade = V(0.03f, 0.06f, 0.035f), Body = V(0.08f, 0.14f, 0.07f), Sun = V(0.17f, 0.24f, 0.1f), Drift = 0.08f, Patches = 0.07f };
        private static Palette GumGreen() => new Palette { Shade = V(0.07f, 0.1f, 0.075f), Body = V(0.18f, 0.25f, 0.18f), Sun = V(0.33f, 0.4f, 0.28f), Drift = 0.1f, Patches = 0.12f };
        private static Palette Rosemary() => new Palette { Shade = V(0.04f, 0.07f, 0.06f), Body = V(0.12f, 0.19f, 0.15f), Sun = V(0.24f, 0.32f, 0.26f), Drift = 0.08f };
        private static Palette RockRose() => new Palette { Shade = V(0.3f, 0.18f, 0.22f), Body = V(0.6f, 0.4f, 0.46f), Sun = V(0.74f, 0.58f, 0.6f), Drift = 0.12f };
        private static Palette Broom() => new Palette { Shade = V(0.18f, 0.12f, 0.02f), Body = V(0.46f, 0.34f, 0.03f), Sun = V(0.58f, 0.46f, 0.05f), Drift = 0.1f };
        private static Palette Thyme() => new Palette { Shade = V(0.12f, 0.07f, 0.13f), Body = V(0.26f, 0.15f, 0.28f), Sun = V(0.4f, 0.26f, 0.4f), Drift = 0.12f };
        private static Palette Spinifex() => new Palette { Shade = V(0.14f, 0.12f, 0.05f), Body = V(0.32f, 0.29f, 0.12f), Sun = V(0.46f, 0.42f, 0.2f), Drift = 0.12f };
        private static Palette GrassTree() => new Palette { Shade = V(0.05f, 0.08f, 0.04f), Body = V(0.14f, 0.2f, 0.09f), Sun = V(0.28f, 0.34f, 0.14f), Drift = 0.1f };

        // --- The cypress hills ---

        /// <summary>An Italian cypress: a dark green flame, every bough climbing close to the stem. The hills are read by them, standing in ones and rows along the tracks.</summary>
        public static readonly PlantSpecies Cypress = new PlantSpecies("cypress", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(15f, 24f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.017f, 0.024f), Lean = new FloatRange(0f, 0.07f), Curve = new StemCurve { Wander = 0.03f, Waves = 2.4f }, Flare = new FloatRange(0.3f, 0.5f) },
            Crown = CrownShape.TaperedColumn,
            CrownBase = new FloatRange(0.02f, 0.08f),
            CrownRadius = new FloatRange(0.055f, 0.1f),
            CrownBumps = 0.16f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Spacing = 0.5f, Whorl = 4, From = 0.03f, To = 0.98f, Jitter = 0.8f, Divergence = 67f, Missing = 0.08f,
                    Angle = new Ramp(0.5f, 0.3f), AngleJitter = 0.12f,
                    Length = new Ramp(0.13f, 0.2f), Profile = new Ramp(0.85f, 1f, 0.5f), Wander = 0.04f, Waves = 1.6f, Rise = 0.06f, RiseFrom = 0.3f, FitRoom = 0.04f, Shortest = 0.4f, StepLength = 0.45f,
                },
            },
            // Its limbs are all alike and hidden in its own foliage, so none of them is worth skinning: only the stem is.
            LimbGirth = 0.55f,
            ThinnestWood = 0.05f,
            Bark = new BarkForm { Kind = BarkKind.Fibrous, Colour = V(0.46f, 0.38f, 0.32f), YoungColour = V(0.34f, 0.27f, 0.2f), Moss = 0.15f, Tile = 2f, Bumps = 0.04f },
            Foliage = new FoliageForm { Card = Conifers.Plumes, CardSize = new FloatRange(2.1f, 2.9f), MostCards = 380, Spacing = 0.22f, Pose = CardPose.Upright, Tilt = 0.16f, Fold = 0.3f, Droop = 0.04f, Cross = 0.4f, Shade = 0f, Colours = CypressGreen() },
        })
        { Footprint = 2.6f, SolidRadius = 0.5f, Variants = 6, Scale = new FloatRange(0.82f, 1.04f) };

        /// <summary>An umbrella pine: a tall bare stem that forks high up into a flat broad canopy, the shape of a southern hillside at noon.</summary>
        public static readonly PlantSpecies UmbrellaPine = new PlantSpecies("pine.umbrella", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(14f, 19f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.6f, 0.74f), Girth = new FloatRange(0.03f, 0.038f), Lean = new FloatRange(0.03f, 0.14f), Curve = new StemCurve { Wander = 0.06f, Waves = 2f }, Flare = new FloatRange(0.35f, 0.55f) },
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.62f, 0.72f),
            CrownRadius = new FloatRange(0.48f, 0.62f),
            CrownBumps = 0.14f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(4f, 6f), From = 0.72f, To = 1f, Angle = new Ramp(0.95f, 0.55f), AngleJitter = 0.2f,
                    Length = new Ramp(0.4f, 0.54f), Wander = 0.08f, Waves = 2f, Rise = new Ramp(0.02f, 0.04f), RiseFrom = 0.3f, Fit = false,
                },
                new LimbForm
                {
                    Count = new FloatRange(3f, 5f), From = 0.3f, To = 0.95f, OfParent = true, Angle = new Ramp(0.85f, 0.55f), AngleJitter = 0.2f,
                    Length = new Ramp(0.45f, 0.7f), Wander = 0.1f, Waves = 2f, Rise = 0.02f, RiseFrom = 0.4f, FitRoom = 0.05f, Shortest = 0.9f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(12, 14), Apical = new FloatRange(0.46f, 0.48f), Internode = 0.4f, MostNodes = 2800, Tropism = new FloatRange(0.02f, 0.04f), Hollow = 0.82f, ClumpSize = 3.6f, Gaps = 0.3f, MarkerDensity = 1.3f, Occupancy = 0.7f },
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = V(0.5f, 0.38f, 0.3f), UpperColour = V(0.58f, 0.36f, 0.24f), UpperFrom = 0.45f, YoungColour = V(0.48f, 0.3f, 0.2f), Moss = 0.08f, Tile = 2.2f, Bumps = 0.05f },
            Foliage = new FoliageForm { Card = Conifers.LongNeedles, CardSize = new FloatRange(3f, 4f), MostCards = 340, Spacing = 0.3f, Tilt = 0.24f, Fold = 0.2f, Droop = 0.08f, Cross = 0.1f, Colours = UmbrellaGreen() },
        })
        { Footprint = 8.5f, SolidRadius = 0.9f, Variants = 6 };

        /// <summary>A holm oak: an evergreen oak, round, dense and nearly black at noon, with small hard leaves silver underneath.</summary>
        public static readonly PlantSpecies HolmOakTree = new PlantSpecies("oak.holm", WorldAssetKind.Tree, Spreading(13f, 0.6f, 0.34f, 0.05f, Leaves(Broadleaves.SmallLeaves, HolmOak(), 2.7f, 320), Furrowed(V(0.3f, 0.28f, 0.27f), 0.2f, 1.8f), CrownShape.Spherical, 0.5f, forks: 5, wander: 0.16f))
        { Footprint = 7f, SolidRadius = 1f, Variants = 6 };

        public static readonly PlantSpecies RosemaryShrub = new PlantSpecies("shrub.rosemary", WorldAssetKind.Shrub, Shrub(1.6f, 0.62f, Conifers.ShortNeedles, Rosemary(), cardSize: 1.2f, cards: 170))
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.3f) };

        /// <summary>Rock rose, the hills' commonest shrub, scattered all summer with crumpled pink and white flowers that last a day.</summary>
        public static readonly PlantSpecies Cistus = new PlantSpecies("shrub.cistus", WorldAssetKind.Shrub, Shrub(1.4f, 0.72f, Undergrowth.AzaleaBloom, RockRose(), cardSize: 1.05f, cards: 120))
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies BroomShrub = new PlantSpecies("shrub.broom", WorldAssetKind.Shrub, Shrub(2.3f, 0.58f, Undergrowth.HeatherSprig, Broom(), cardSize: 1.35f, cards: 180, crown: CrownShape.Flame))
        { Footprint = 1.5f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies ThymeCushion = Cushion("thyme", Thyme(), size: 1.3f, lift: 0.06f);
        public static readonly PlantSpecies Asphodel = Flower("flower.asphodel", Undergrowth.Lupine, V(0.72f, 0.7f, 0.64f), height: 1.2f, size: 0.26f, carried: Inflorescence.Spike, spike: 0.6f, leaves: 2, foliage: Glaucous());

        // --- The red scrub ---

        /// <summary>A ghost gum: a smooth bone-pale trunk, a few crooked limbs, and an open crown of grey-green leaves that hang edge on to the sun and cast hardly any shade.</summary>
        public static readonly PlantSpecies Gum = new PlantSpecies("gum", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(16f, 23f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.52f, 0.68f), Girth = new FloatRange(0.024f, 0.03f), Lean = new FloatRange(0.03f, 0.18f), Curve = new StemCurve { Wander = 0.08f, Waves = 2.2f }, Flare = new FloatRange(0.3f, 0.5f), LobeDepth = new FloatRange(0.1f, 0.2f) },
            Roots = new RootForm { Reach = new FloatRange(0.1f, 0.15f) },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.45f, 0.6f),
            CrownRadius = new FloatRange(0.3f, 0.42f),
            CrownBumps = 0.3f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(4f, 6f), From = 0.55f, To = 1f, Angle = new Ramp(0.85f, 0.45f), AngleJitter = 0.24f,
                    Length = new Ramp(0.42f, 0.56f), Wander = 0.15f, Waves = 2.6f, Rise = new Ramp(0.03f, 0.05f), RiseFrom = 0.25f, FitRoom = 0.06f,
                },
                new LimbForm
                {
                    Count = new FloatRange(2f, 4f), From = 0.35f, To = 0.95f, OfParent = true, Angle = new Ramp(0.8f, 0.5f), AngleJitter = 0.24f,
                    Length = new Ramp(0.45f, 0.65f), Wander = 0.16f, Waves = 2f, Rise = 0.03f, RiseFrom = 0.3f, FitRoom = 0.06f, Shortest = 0.9f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(11, 13), Apical = new FloatRange(0.5f, 0.52f), Internode = 0.4f, MostNodes = 2600, ClumpSize = 3.2f, Gaps = 0.4f, Hollow = 0.72f, Tropism = new FloatRange(0.02f, 0.06f) },
            Bark = new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.74f, 0.71f, 0.65f), YoungColour = V(0.6f, 0.52f, 0.44f), Moss = 0f, Tile = 2.4f, Bumps = 0.07f },
            Foliage = new FoliageForm { Card = GumSpray, CardSize = new FloatRange(2.4f, 3.3f), MostCards = 290, Spacing = 0.33f, Tilt = 0.4f, Fold = 0.16f, Droop = 0.3f, Cross = 0.35f, Colours = GumGreen() },
        })
        { Footprint = 7f, SolidRadius = 0.8f, Variants = 6, Scale = new FloatRange(0.85f, 1.1f) };

        /// <summary>A grass tree: a stout black stem, burnt and burnt again, and a great round head of fine leaves like a frozen fountain.</summary>
        public static readonly PlantSpecies GrassTreePlant = new PlantSpecies("grasstree", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(2.2f, 4.2f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.06f, 0.085f), Lean = new FloatRange(0f, 0.14f), Curve = new StemCurve { Wander = 0.04f, Waves = 1.4f }, Flare = new FloatRange(0.1f, 0.25f), Lobes = new IntRange(0, 0) },
            CrownBase = new FloatRange(0.86f, 0.92f),
            CrownRadius = new FloatRange(0.32f, 0.42f),
            Bark = new BarkForm { Kind = BarkKind.Charred, Colour = V(0.5f, 0.46f, 0.42f), YoungColour = V(0.5f, 0.46f, 0.42f), Tile = 1.4f, Bumps = 0.1f, MostSides = 9 },
            Head = new RosetteForm
            {
                Card = Undergrowth.Blade, Leaves = new IntRange(40, 54), Length = new FloatRange(1.4f, 2f), Inner = 0.62f, Aspect = 0.05f, Lean = new FloatRange(0.1f, 1.7f), Arch = new FloatRange(0.7f, 1.5f),
                Curl = 1.2f, FootSpread = 0.06f, Segments = 4, Fold = 0.1f, Shell = 0.5f, Joint = PlantJoint.Frond with { Touch = 0f }, Links = 2, Colours = GrassTree(),
            },
        })
        { Footprint = 2.4f, SolidRadius = 0.4f, Variants = 5, Scale = new FloatRange(0.85f, 1.25f) };

        /// <summary>Spinifex: a hummock of needle-fine grass, domed and spiny, the commonest thing on the red ground.</summary>
        public static readonly PlantSpecies SpinifexHummock = new PlantSpecies("grass.spinifex", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(40, 58), BladeLength = new FloatRange(0.8f, 1.3f), BladeWidth = 0.02f, BladeArch = new FloatRange(1.3f, 2.4f), Spread = 0.5f,
            Stalks = new IntRange(0, 3), StalkHeight = new FloatRange(1.3f, 1.8f), Head = SeedHead.Ear, HeadLength = 0.26f, HeadWidth = 0.05f, HeadColour = V(0.5f, 0.42f, 0.26f), Colours = Spinifex(),
        })
        { Footprint = 1.1f, Scale = new FloatRange(0.8f, 1.35f) };

        public static readonly PlantSpecies Everlasting = Flower("flower.everlasting", Undergrowth.Aster, V(0.62f, 0.24f, 0.36f), height: 0.55f, size: 0.3f, leaves: 2, foliage: Glaucous());

        // --- What lies on the red ground ---

        private static BarkForm PaleGum() => new BarkForm { Kind = BarkKind.Smooth, Colour = V(0.7f, 0.66f, 0.6f), YoungColour = V(0.58f, 0.5f, 0.42f), Moss = 0f, Tile = 2.4f, Bumps = 0.08f };

        public static readonly PlantSpecies GumLog = Log("log.gum", PaleGum(), 0.48f, 8f, fungi: false);
        public static readonly PlantSpecies GumStump = Stump("stump.gum", PaleGum(), 0.6f, fungi: false);
    }
}
