using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The old broadleaf country: limes and hornbeams over hazel and wild garlic in the wildwood, and down by the water poplars in rows, pollarded willows along the ditches and flag iris in the
    /// wet. Green in high summer, as these are the woods people cut and graze and fish in.
    /// </summary>
    [FloraCollection]
    public static class Woodlands
    {
        // --- Leaves and cards ---

        // A lime's leaf: broad, wide at its foot, with a short drawn-out point and fine teeth.
        private static LeafForm LindenLeaf() => new LeafForm { Width = 0.47f, Skew = 0.62f, Round = 0.6f, DripTip = 0.55f, Teeth = 15, ToothDepth = 0.035f, Veins = 6, VeinAngle = 0.66f, VeinStrength = 0.4f };

        public static readonly LeafSpray LindenSpray = new LeafSpray { Id = "spray.linden", Leaf = LindenLeaf(), LeafLength = 0.13f, LeafWidth = 0.92f, LeavesPerShoot = new IntRange(7, 10), Shoots = new IntRange(10, 13), ShootLength = 0.5f, LeafAngle = 0.72f, Droop = 0.24f };

        // --- Colours ---

        private static Palette LimeGreen() => new Palette { Shade = V(0.055f, 0.11f, 0.035f), Body = V(0.15f, 0.25f, 0.06f), Sun = V(0.31f, 0.4f, 0.08f), Drift = 0.1f, Patches = 0.1f };
        private static Palette DarkSummer() => new Palette { Shade = V(0.035f, 0.08f, 0.035f), Body = V(0.1f, 0.19f, 0.06f), Sun = V(0.22f, 0.31f, 0.08f), Drift = 0.1f, Patches = 0.08f };
        private static Palette WillowGrey() => new Palette { Shade = V(0.07f, 0.1f, 0.06f), Body = V(0.2f, 0.26f, 0.15f), Sun = V(0.38f, 0.44f, 0.24f), Drift = 0.1f };
        private static Palette PoplarGreen() => new Palette { Shade = V(0.05f, 0.1f, 0.035f), Body = V(0.14f, 0.24f, 0.06f), Sun = V(0.3f, 0.4f, 0.09f), Drift = 0.1f, Patches = 0.12f };
        private static Palette Hawthorn() => new Palette { Shade = V(0.3f, 0.28f, 0.27f), Body = V(0.6f, 0.57f, 0.55f), Sun = V(0.76f, 0.74f, 0.7f), Drift = 0.06f, Patches = 0.1f };
        private static Palette Elderflower() => new Palette { Shade = V(0.3f, 0.29f, 0.22f), Body = V(0.58f, 0.56f, 0.44f), Sun = V(0.72f, 0.7f, 0.56f), Drift = 0.07f, Patches = 0.12f };

        // --- Trees ---

        /// <summary>A lime: tall and domed, dense with heart-shaped leaves, the tree villages plant at their middle and hives are kept under.</summary>
        public static readonly PlantSpecies Linden = new PlantSpecies("linden", WorldAssetKind.Tree, Spreading(21f, 0.44f, 0.44f, 0.04f, Leaves(LindenSpray, LimeGreen(), 3.1f, 330), SmoothBark(V(0.36f, 0.31f, 0.27f), 0.35f, 0.07f), CrownShape.Spherical, 0.52f, forks: 6))
        { Footprint = 8.5f, SolidRadius = 1.2f, Variants = 6 };

        /// <summary>A hornbeam: a grey fluted trunk that looks like muscle under skin, and a close round crown of small pleated leaves.</summary>
        public static readonly PlantSpecies Hornbeam = new PlantSpecies("hornbeam", WorldAssetKind.Tree, With(
            Spreading(16f, 0.5f, 0.38f, 0.04f, Leaves(Broadleaves.BeechSpray, DarkSummer(), 2.8f, 300), SmoothBark(V(0.4f, 0.38f, 0.35f), 0.4f, 0.06f), CrownShape.Spherical, 0.5f, forks: 6, wander: 0.13f),
            tree =>
            {
                tree.Trunk.Lobes = new IntRange(7, 9);
                tree.Trunk.LobeDepth = new FloatRange(0.32f, 0.44f);
            }))
        { Footprint = 7.5f, SolidRadius = 1f, Variants = 6 };

        /// <summary>An alder: dark and narrow, a cone of round dark leaves, standing with its feet by the water.</summary>
        public static readonly PlantSpecies Alder = new PlantSpecies("alder", WorldAssetKind.Tree, Spreading(17f, 0.34f, 0.62f, 0.028f, Leaves(Broadleaves.BeechSpray, DarkSummer(), 2.9f, 330), Furrowed(V(0.3f, 0.27f, 0.25f), 0.45f), CrownShape.Conical, 0.54f, forks: 7, wander: 0.08f))
        { Footprint = 5.5f, SolidRadius = 0.7f, Variants = 6 };

        /// <summary>A hawthorn in flower: low, crooked and thick with white blossom, the hedge tree left to grow.</summary>
        public static readonly PlantSpecies HawthornTree = new PlantSpecies("hawthorn", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(7f, 10f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.26f, 0.38f), Girth = new FloatRange(0.045f, 0.06f), Lean = new FloatRange(0.03f, 0.16f), Curve = new StemCurve { Wander = 0.1f, Waves = 2f }, Flare = new FloatRange(0.3f, 0.5f) },
            Roots = new RootForm { Reach = new FloatRange(0.12f, 0.18f) },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.18f, 0.28f),
            CrownRadius = new FloatRange(0.62f, 0.8f),
            CrownBumps = 0.22f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(4f, 6f), From = 0.55f, To = 1f, Angle = new Ramp(1.15f, 0.65f), AngleJitter = 0.24f,
                    Length = new Ramp(0.5f, 0.72f), Wander = 0.16f, Waves = 3f, Rise = new Ramp(0.02f, 0.05f), RiseFrom = 0.25f, FitRoom = 0.05f,
                },
                new LimbForm
                {
                    Count = new FloatRange(3f, 5f), From = 0.3f, To = 1f, OfParent = true, Angle = new Ramp(0.9f, 0.6f), AngleJitter = 0.22f,
                    Length = new Ramp(0.45f, 0.7f), Wander = 0.16f, Waves = 2f, Sag = 0.03f, Rise = 0.02f, RiseFrom = 0.5f, FitRoom = 0.05f, Shortest = 0.7f, StepLength = 0.55f,
                },
            },
            Buds = new BudForm { Years = new IntRange(11, 13), Apical = new FloatRange(0.47f, 0.49f), Internode = 0.3f, MostNodes = 2400, ClumpSize = 2.4f, Gaps = 0.3f, Hollow = 0.6f },
            Bark = Furrowed(V(0.4f, 0.36f, 0.32f), 0.45f, 1.8f),
            Foliage = new FoliageForm { Card = Broadleaves.CherryBlossom, CardSize = new FloatRange(1.8f, 2.5f), MostCards = 320, Spacing = 0.3f, Droop = 0.14f, Shell = 0.66f, Colours = Hawthorn() },
        })
        { Footprint = 5f, SolidRadius = 0.5f, Variants = 5 };

        /// <summary>A Lombardy poplar: a green flame, every limb climbing close against the stem. They're planted in rows along roads and water, and a land is read by them from far off.</summary>
        public static readonly PlantSpecies Poplar = new PlantSpecies("poplar", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(21f, 27f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.86f, 0.94f), Girth = new FloatRange(0.022f, 0.028f), Lean = new FloatRange(0f, 0.04f), Curve = new StemCurve { Wander = 0.02f, Waves = 2.2f }, Flare = new FloatRange(0.35f, 0.55f), LobeDepth = new FloatRange(0.15f, 0.25f) },
            Crown = CrownShape.Flame,
            CrownBase = new FloatRange(0.04f, 0.12f),
            CrownRadius = new FloatRange(0.11f, 0.16f),
            CrownBumps = 0.16f,
            // Every limb climbs close against the stem, and is crowded with short shoots that all reach up: a column of leaf with no air in it, as nothing here has to fight for light.
            Limbs = new[]
            {
                new LimbForm
                {
                    Spacing = 0.62f, Whorl = 2, From = 0.05f, To = 1f, Jitter = 0.8f, Divergence = 97f, Missing = 0.1f,
                    Angle = new Ramp(0.5f, 0.24f), AngleJitter = 0.1f,
                    Length = new Ramp(0.15f, 0.21f), Profile = new Ramp(0.9f, 1f, 0.45f), Wander = 0.05f, Waves = 2f, Rise = 0.06f, RiseFrom = 0.3f, FitRoom = 0.06f, Shortest = 0.5f, StepLength = 0.5f,
                },
                new LimbForm
                {
                    Spacing = 0.55f, Whorl = 2, From = 0.15f, To = 1f, Jitter = 0.5f, Divergence = 90f,
                    Angle = new Ramp(0.6f, 0.4f), AngleJitter = 0.14f,
                    Length = new Ramp(0.35f, 0.5f), Profile = new Ramp(1f, 0.6f), OfParent = true, Shrink = 0.8f, Fit = false, Shortest = 0.3f,
                    Wander = 0.08f, Waves = 1.5f, Rise = 0.1f, RiseFrom = 0.2f, StepLength = 0.4f,
                },
            },
            Bark = Furrowed(V(0.38f, 0.36f, 0.32f), 0.25f),
            Foliage = new FoliageForm { Card = Broadleaves.BirchSpray, CardSize = new FloatRange(2.2f, 3f), MostCards = 360, Spacing = 0.26f, Pose = CardPose.Upright, Tilt = 0.22f, Fold = 0.24f, Droop = 0.12f, Cross = 0.35f, Shade = 0f, Colours = PoplarGreen() },
        })
        { Footprint = 3.6f, SolidRadius = 0.6f, Variants = 6, Scale = new FloatRange(0.86f, 1.05f) };

        /// <summary>
        /// A pollarded willow: a short thick bole, cut back at a man's height for generations, and a head of straight rods springing from the knuckle. They line the ditches of the water meadows.
        /// </summary>
        public static readonly PlantSpecies PollardWillow = new PlantSpecies("willow.pollard", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(6.5f, 9f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.36f, 0.46f), Girth = new FloatRange(0.075f, 0.095f), Lean = new FloatRange(0.02f, 0.16f), Curve = new StemCurve { Wander = 0.1f, Waves = 2f }, Flare = new FloatRange(0.4f, 0.6f), Lobes = new IntRange(5, 7), LobeDepth = new FloatRange(0.25f, 0.4f) },
            Roots = new RootForm { Reach = new FloatRange(0.12f, 0.18f) },
            Crown = CrownShape.InverseCone,
            CrownBase = new FloatRange(0.36f, 0.46f),
            CrownRadius = new FloatRange(0.4f, 0.52f),
            CrownBumps = 0.18f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(14f, 20f), From = 0.9f, To = 1f, Jitter = 1f,
                    Angle = new Ramp(0.62f, 0.3f), AngleJitter = 0.18f,
                    Length = new Ramp(0.5f, 0.6f), Wander = 0.04f, Waves = 1.5f, Rise = 0.02f, RiseFrom = 0.5f, Fit = false, StepLength = 0.5f,
                },
                // Leafy side shoots up every rod, so that it's leaf from the knuckle to the tip.
                new LimbForm
                {
                    Spacing = 0.5f, Whorl = 2, From = 0.15f, To = 1f, Jitter = 0.5f, Divergence = 90f,
                    Angle = new Ramp(0.7f, 0.45f), AngleJitter = 0.15f,
                    Length = new Ramp(0.2f, 0.32f), Profile = new Ramp(1f, 0.5f), OfParent = true, Shrink = 0.8f, Fit = false, Shortest = 0.25f,
                    Wander = 0.08f, Waves = 1.5f, Rise = 0.05f, RiseFrom = 0.3f, StepLength = 0.4f,
                },
            },
            PipeExponent = 2f,
            TipRadius = 0.02f,
            // The bole is far too big for what it carries, as a pollard's is: its rods stay rods.
            LimbGirth = 0.16f,
            Bark = Furrowed(V(0.46f, 0.42f, 0.36f), 0.5f),
            Foliage = new FoliageForm { Card = Wilds.BambooLeaves, CardSize = new FloatRange(1.5f, 2.1f), Aspect = 0.8f, MostCards = 300, Spacing = 0.28f, Pose = CardPose.Upright, Tilt = 0.35f, Fold = 0.3f, Droop = 0.25f, Cross = 0.3f, Shade = 0f, Colours = WillowGrey() },
        })
        { Footprint = 4.5f, SolidRadius = 0.8f, Variants = 5 };

        // --- Shrubs ---

        /// <summary>Hazel: a fountain of straight stems from one stool, round soft leaves, coppiced for wattle and left to grow back.</summary>
        public static readonly PlantSpecies Hazel = new PlantSpecies("hazel", WorldAssetKind.Shrub, Shrub(4.4f, 0.55f, Broadleaves.BeechSpray, Fresh(), cardSize: 1.7f, cards: 150, crown: CrownShape.InverseCone))
        { Footprint = 2.2f, Scale = new FloatRange(0.8f, 1.2f) };

        /// <summary>An elder in flower, its flat heads of tiny cream flowers held over the leaves.</summary>
        public static readonly PlantSpecies Elder = new PlantSpecies("shrub.elder", WorldAssetKind.Shrub, Shrub(3.4f, 0.64f, Broadleaves.CherryBlossom, Elderflower(), cardSize: 1.5f, cards: 140))
        { Footprint = 1.9f, Scale = new FloatRange(0.8f, 1.2f) };

        /// <summary>Osier: willow kept low for its rods, narrow grey leaves on stems that go straight up.</summary>
        public static readonly PlantSpecies Osier = new PlantSpecies("shrub.osier", WorldAssetKind.Shrub, Shrub(3.2f, 0.48f, Wilds.BambooLeaves, WillowGrey(), cardSize: 1.5f, cards: 140, crown: CrownShape.Flame))
        { Footprint = 1.6f, Scale = new FloatRange(0.8f, 1.25f) };

        // --- Flowers ---

        /// <summary>Wild garlic, which carpets the wildwood's floor in spring with heads of white stars.</summary>
        public static readonly PlantSpecies Ramsons = Flower("flower.ramsons", Undergrowth.Cluster, V(0.72f, 0.72f, 0.66f), height: 0.55f, size: 0.34f, leaves: 2, foliage: Fresh());

        public static readonly PlantSpecies Anemone = Flower("flower.anemone", Undergrowth.Daisy, V(0.74f, 0.68f, 0.68f), height: 0.36f, size: 0.28f, leaves: 2);
        public static readonly PlantSpecies Meadowsweet = Flower("flower.meadowsweet", Undergrowth.Cluster, V(0.68f, 0.64f, 0.5f), height: 1.3f, size: 0.5f);
        public static readonly PlantSpecies YellowIris = Flower("flower.iris", Undergrowth.Poppy, V(0.7f, 0.52f, 0.03f), height: 1.25f, size: 0.42f);
        public static readonly PlantSpecies Loosestrife = Flower("flower.loosestrife", Undergrowth.Lupine, V(0.52f, 0.11f, 0.4f), height: 1.3f, size: 0.24f, carried: Inflorescence.Spike, spike: 0.72f);
        public static readonly PlantSpecies Buttercup = Flower("flower.buttercup", Undergrowth.Poppy, V(0.72f, 0.54f, 0.02f), height: 0.6f, size: 0.24f, leaves: 2);
        public static readonly PlantSpecies Cornflower = Flower("flower.cornflower", Undergrowth.Aster, V(0.11f, 0.19f, 0.64f), height: 0.8f, size: 0.34f);
    }
}
