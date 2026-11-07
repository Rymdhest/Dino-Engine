using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Trees with needles. A conifer's architecture is rigid enough to be written down, so most are a scaffold alone: a straight axis, tiers of boughs, and shoots along them.
    /// Pines are the exception, as an old one has lost its tiers and comes in flat pads that fight for the light like a broadleaf's crown.
    /// </summary>
    [FloraCollection]
    public static class Conifers
    {
        // --- Cards ---

        public static readonly NeedleSpray LongNeedles = new NeedleSpray { Id = "needles.long", Needle = 0.17f, NeedleRadius = 0.0036f, NeedlesPerShoot = 230, Spread = 1.35f, Tufted = 0.92f, Shoots = new IntRange(9, 12), ShootLength = 0.46f, ShootAngle = 0.72f };
        public static readonly NeedleSpray ShortNeedles = new NeedleSpray { Id = "needles.short", Needle = 0.085f, NeedleRadius = 0.0034f, NeedlesPerShoot = 240, Spread = 1.1f, Tufted = 0.1f, Shoots = new IntRange(9, 12), ShootAngle = 0.72f, ShootLength = 0.5f };
        public static readonly ScaleSpray Plumes = new ScaleSpray { Id = "scales.plume", Depth = 3, Angle = 0.5f, Ratio = 0.52f, Branchlets = 8, Thickness = 0.0125f };
        public static readonly ScaleSpray RedwoodSprigs = new ScaleSpray { Id = "scales.redwood", Depth = 1, Angle = 0.78f, Ratio = 0.5f, Branchlets = 11, Thickness = 0.0052f, Barbs = 7, BarbLength = 0.05f };

        // --- What they're coloured. Conifers are the darkest thing in a forest, and bluer than it. ---

        private static Palette Evergreen() => new Palette { Shade = new Vector3(0.022f, 0.052f, 0.042f), Body = new Vector3(0.055f, 0.125f, 0.08f), Sun = new Vector3(0.13f, 0.22f, 0.095f), Drift = 0.1f, Patches = 0.08f };
        private static Palette BlueGreen() => new Palette { Shade = new Vector3(0.022f, 0.05f, 0.055f), Body = new Vector3(0.05f, 0.115f, 0.11f), Sun = new Vector3(0.12f, 0.2f, 0.15f), Drift = 0.08f, Patches = 0.07f };

        private static BarkForm PineBark(float moss = 0.25f) => new BarkForm { Kind = BarkKind.Plated, Colour = new Vector3(0.44f, 0.36f, 0.3f), YoungColour = new Vector3(0.38f, 0.26f, 0.18f), Moss = moss, Tile = 1.8f, Bumps = 0.07f };

        // --- A shape shared by everything with tiers: a spire of boughs that sag and lift at their ends. ---

        private static LimbForm[] Tiers(float spacing, int whorl, float low, float high, float sag, float lift, float shootSpacing = 0.85f) => new[]
        {
            new LimbForm
            {
                Spacing = spacing, Whorl = whorl, From = 0.04f, To = 0.97f, Jitter = 0.7f, Divergence = 180f / whorl + 27f, Missing = 0.14f,
                Angle = new Ramp(low, high), AngleJitter = 0.12f,
                Length = new Ramp(0.3f, 0.5f), Wander = 0.035f, Waves = 1.6f,
                Sag = sag, Rise = lift, RiseFrom = 0.55f, FitRoom = 0.02f, Shortest = 0.7f, StepLength = 0.6f,
            },
            new LimbForm
            {
                Spacing = shootSpacing, Whorl = 2, From = 0.14f, To = 1f, Jitter = 0.5f, Divergence = 90f, DivergenceJitter = 0.1f,
                Angle = new Ramp(0.95f, 0.5f), AngleJitter = 0.14f,
                Length = new Ramp(0.32f, 0.46f), Profile = new Ramp(1f, 0.55f), OfParent = true, Shrink = 0.8f, Fit = false, Shortest = 0.35f,
                Wander = 0.1f, Waves = 1.5f, Sag = sag * 0.8f, RiseFrom = 1f, StepLength = 0.5f,
            },
        };

        // --- Species ---

        /// <summary>A Japanese black pine: an old, leaning, wind-shaped thing, in flat pads of dark needles.</summary>
        public static readonly PlantSpecies BlackPine = new PlantSpecies("pine.black", WorldAssetKind.Tree, BlackPineForm())
        { Footprint = 7f, SolidRadius = 0.9f, Variants = 6 };

        private static TreeForm BlackPineForm(bool dead = false) => new TreeForm
        {
            Height = new FloatRange(14f, 21f),
            Trunk = new TrunkForm
            {
                Reach = new FloatRange(0.72f, 0.9f),
                Girth = new FloatRange(0.03f, 0.04f),
                Lean = new FloatRange(0.08f, 0.24f),
                Curve = new StemCurve { Wander = 0.1f, Waves = 2.2f },
                Flare = new FloatRange(0.3f, 0.55f),
            },
            Roots = new RootForm(),
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.42f, 0.55f),
            CrownRadius = new FloatRange(0.42f, 0.56f),
            CrownBumps = 0.22f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(6f, 9f), From = 0.45f, To = 1f, Jitter = 1f,
                    Angle = new Ramp(1.45f, 0.9f), AngleJitter = 0.18f,
                    Length = new Ramp(0.3f, 0.5f), Wander = 0.13f, Waves = 2.8f, Rise = new Ramp(0.015f, 0.05f), RiseFrom = 0.45f, FitRoom = 0.04f, StepLength = 0.7f,
                },
            },
            Buds = new BudForm
            {
                Years = new IntRange(13, 15), Apical = new FloatRange(0.5f, 0.52f), Internode = 0.4f, MostNodes = 2800,
                Tropism = new FloatRange(0.02f, 0.05f), BranchAngle = new FloatRange(0.7f, 0.95f), Hollow = 0.72f, ClumpSize = 4.4f, Gaps = 0.4f, MarkerDensity = 1.1f, Occupancy = 0.7f,
            },
            Bark = PineBark(dead ? 0.1f : 0.3f),
            Foliage = new FoliageForm { Card = LongNeedles, CardSize = new FloatRange(2.9f, 3.9f), MostCards = 340, Spacing = 0.32f, Tilt = 0.28f, Fold = 0.2f, Droop = 0.1f, Cross = 0.12f, Colours = Evergreen() },
            Dead = dead ? new Decay() : null,
        };

        /// <summary>A Scots pine: tall and bare, orange high up, with a small flat crown at the top. What northern pinewoods are made of.</summary>
        public static readonly PlantSpecies ScotsPine = new PlantSpecies("pine.scots", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(20f, 27f),
            Trunk = new TrunkForm
            {
                Reach = new FloatRange(0.88f, 0.96f),
                Girth = new FloatRange(0.02f, 0.026f),
                Lean = new FloatRange(0f, 0.05f),
                Curve = new StemCurve { Wander = 0.03f, Waves = 2.5f },
                Flare = new FloatRange(0.25f, 0.4f),
            },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.58f, 0.68f),
            CrownRadius = new FloatRange(0.26f, 0.34f),
            CrownBumps = 0.25f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(8f, 12f), From = 0.62f, To = 1f, Jitter = 0.9f,
                    Angle = new Ramp(1.3f, 0.7f), AngleJitter = 0.16f,
                    Length = new Ramp(0.24f, 0.34f), Wander = 0.1f, Waves = 2.2f, Rise = 0.05f, RiseFrom = 0.4f, FitRoom = 0.05f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(12, 14), Internode = 0.38f, MostNodes = 2000, Hollow = 0.7f, ClumpSize = 3.4f, Gaps = 0.36f, MarkerDensity = 1.2f, Occupancy = 0.7f },
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = new Vector3(0.48f, 0.4f, 0.36f), UpperColour = new Vector3(0.62f, 0.34f, 0.2f), UpperFrom = 0.42f, YoungColour = new Vector3(0.55f, 0.32f, 0.2f), Moss = 0.3f, Tile = 2.4f, Bumps = 0.05f },
            Foliage = new FoliageForm { Card = LongNeedles, CardSize = new FloatRange(2.8f, 3.7f), MostCards = 280, Spacing = 0.3f, Tilt = 0.34f, Fold = 0.2f, Droop = 0.12f, Cross = 0.15f, Colours = BlueGreen() },
        })
        { Footprint = 5.5f, SolidRadius = 0.8f, Variants = 6, Scale = new FloatRange(0.8f, 1f) };

        /// <summary>A spruce: a dark spire, skirts to the ground, boughs that hang and lift at their tips.</summary>
        public static readonly PlantSpecies Spruce = new PlantSpecies("spruce", WorldAssetKind.Tree, SpruceForm(Evergreen()))
        { Footprint = 5.5f, SolidRadius = 0.75f, Variants = 6, Scale = new FloatRange(0.8f, 1.04f) };

        /// <summary>A spruce in winter. The white on it is the same card, in a different light.</summary>
        public static readonly PlantSpecies FrostedSpruce = new PlantSpecies("spruce.frosted", WorldAssetKind.Tree, SpruceForm(new Palette { Shade = new Vector3(0.03f, 0.05f, 0.06f), Body = new Vector3(0.2f, 0.26f, 0.28f), Sun = new Vector3(0.62f, 0.66f, 0.68f), Drift = 0.05f, Patches = 0.09f }))
        { Footprint = 5.5f, SolidRadius = 0.75f, Variants = 5, Scale = new FloatRange(0.8f, 1.04f) };

        private static TreeForm SpruceForm(Palette colours) => new TreeForm
        {
            Height = new FloatRange(17f, 25f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.024f, 0.03f), Lean = new FloatRange(0f, 0.03f), Curve = new StemCurve { Wander = 0.012f, Waves = 2f }, Flare = new FloatRange(0.3f, 0.5f) },
            Crown = CrownShape.Conical,
            CrownBase = new FloatRange(0.03f, 0.1f),
            CrownRadius = new FloatRange(0.2f, 0.26f),
            CrownBumps = 0.1f,
            Limbs = Tiers(1.5f, 5, 1.72f, 1.05f, 0.12f, 0.28f),
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = new Vector3(0.5f, 0.42f, 0.36f), YoungColour = new Vector3(0.42f, 0.3f, 0.2f), Moss = 0.3f, Tile = 2f, Bumps = 0.05f },
            Foliage = new FoliageForm { Card = ShortNeedles, CardSize = new FloatRange(2.6f, 3.5f), MostCards = 380, Spacing = 0.3f, Tilt = 0.22f, Fold = 0.26f, Droop = 0.18f, Cross = 0.1f, Shade = 0f, Colours = colours },
        };

        /// <summary>A sugi, or Japanese cedar: straight as a mast, in rising plumes of dark green, and the tree of old shrines.</summary>
        public static readonly PlantSpecies Cedar = new PlantSpecies("cedar", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(21f, 28f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.03f, 0.038f), Lean = new FloatRange(0f, 0.025f), Curve = new StemCurve { Wander = 0.01f, Waves = 2f }, Flare = new FloatRange(0.45f, 0.7f), LobeDepth = new FloatRange(0.2f, 0.3f) },
            Roots = new RootForm { Reach = new FloatRange(0.08f, 0.12f) },
            Crown = CrownShape.Flame,
            CrownBase = new FloatRange(0.2f, 0.32f),
            CrownRadius = new FloatRange(0.17f, 0.22f),
            CrownBumps = 0.16f,
            Limbs = Tiers(1.15f, 3, 1.25f, 0.7f, 0f, 0.14f, 0.7f),
            Bark = new BarkForm { Kind = BarkKind.Fibrous, Colour = new Vector3(0.52f, 0.34f, 0.24f), YoungColour = new Vector3(0.36f, 0.22f, 0.15f), Moss = 0.5f, Tile = 2.4f, Bumps = 0.04f },
            Foliage = new FoliageForm { Card = Plumes, CardSize = new FloatRange(2.4f, 3.2f), MostCards = 320, Spacing = 0.34f, Pose = CardPose.Upright, Tilt = 0.28f, Fold = 0.3f, Droop = 0.12f, Cross = 0.2f, Shade = 0f, Colours = Evergreen() },
        })
        { Footprint = 5.5f, SolidRadius = 1f, Variants = 6, Scale = new FloatRange(0.8f, 0.98f) };

        /// <summary>A redwood: a column of red bark as wide as a house, with its crown a long way up. Limits on the camera keep it short of what it should be, so it's all in the girth.</summary>
        public static readonly PlantSpecies Redwood = new PlantSpecies("redwood", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(24.5f, 27.5f),
            Trunk = new TrunkForm
            {
                Girth = new FloatRange(0.06f, 0.082f), Lean = new FloatRange(0f, 0.02f), Curve = new StemCurve { Wander = 0.008f, Waves = 2f },
                Flare = new FloatRange(0.55f, 0.85f), FlareHeight = 0.1f, Lobes = new IntRange(5, 7), LobeDepth = new FloatRange(0.3f, 0.45f),
            },
            Roots = new RootForm { Reach = new FloatRange(0.2f, 0.28f), Girth = new FloatRange(0.32f, 0.42f), Rise = 0.8f },
            RootCount = new IntRange(6, 8),
            Crown = CrownShape.Conical,
            CrownBase = new FloatRange(0.36f, 0.46f),
            CrownRadius = new FloatRange(0.32f, 0.4f),
            CrownBumps = 0.14f,
            Limbs = Tiers(1.15f, 3, 1.72f, 1.1f, 0.1f, 0.16f, 0.7f),
            LimbGirth = 0.42f,
            MostAxes = 100,
            PipeExponent = 2.2f,
            Bark = new BarkForm { Kind = BarkKind.Fibrous, Colour = new Vector3(0.68f, 0.36f, 0.22f), YoungColour = new Vector3(0.42f, 0.24f, 0.15f), Moss = 0.35f, Tile = 3.2f, Bumps = 0.04f, MostSides = 18 },
            Foliage = new FoliageForm { Card = RedwoodSprigs, CardSize = new FloatRange(3f, 4.1f), MostCards = 330, Spacing = 0.28f, Tilt = 0.25f, Fold = 0.24f, Droop = 0.2f, Cross = 0.1f, Shade = 0f, Colours = new Palette { Shade = new Vector3(0.028f, 0.06f, 0.04f), Body = new Vector3(0.07f, 0.15f, 0.075f), Sun = new Vector3(0.16f, 0.26f, 0.09f), Drift = 0.08f, Patches = 0.09f } },
        })
        { Footprint = 8.5f, SolidRadius = 2.2f, Variants = 5, Scale = new FloatRange(0.84f, 0.96f) };

        /// <summary>A dead pine. The world's sites stand these about by name.</summary>
        public static readonly PlantSpecies DeadPine = new PlantSpecies("pine.dead", WorldAssetKind.Tree, BlackPineForm(dead: true))
        { Footprint = 4f, SolidRadius = 0.8f, Variants = 5 };
    }
}
