using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The shapes most species are built on, for the catalogues to share: a broadleaf whose trunk gives way to a few big limbs, tiers of boughs up a straight axis, a shrub of many stems, leafy
    /// stems with a flower on each, a cushion on the ground, a log or a stump in the bark of whatever fell. Each is ranges and nothing else, so a species built on one is still only numbers, and
    /// each is the same as the one the older catalogues keep to themselves, so a plant reads the same whichever file it's written in.
    /// </summary>
    internal static class FloraKit
    {
        public static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);

        /// <summary>A form, changed where a species differs from the shape it's built on.</summary>
        public static T With<T>(T form, Action<T> change)
        {
            change(form);
            return form;
        }

        // --- Colours. Albedos under a sun of about 13, so even the brightest is deep. ---

        public static Palette Summer() => new Palette { Shade = V(0.05f, 0.1f, 0.04f), Body = V(0.13f, 0.23f, 0.06f), Sun = V(0.27f, 0.36f, 0.08f) };
        public static Palette Fresh() => new Palette { Shade = V(0.06f, 0.12f, 0.035f), Body = V(0.16f, 0.27f, 0.06f), Sun = V(0.33f, 0.42f, 0.09f), Drift = 0.12f, Patches = 0.1f };
        public static Palette Evergreen() => new Palette { Shade = V(0.03f, 0.075f, 0.04f), Body = V(0.075f, 0.17f, 0.07f), Sun = V(0.17f, 0.28f, 0.08f), Drift = 0.1f };
        public static Palette Needled() => new Palette { Shade = V(0.022f, 0.052f, 0.042f), Body = V(0.055f, 0.125f, 0.08f), Sun = V(0.13f, 0.22f, 0.095f), Drift = 0.1f, Patches = 0.08f };
        public static Palette BlueNeedled() => new Palette { Shade = V(0.022f, 0.05f, 0.055f), Body = V(0.05f, 0.115f, 0.11f), Sun = V(0.12f, 0.2f, 0.15f), Drift = 0.08f, Patches = 0.07f };
        public static Palette Green() => new Palette { Shade = V(0.04f, 0.09f, 0.04f), Body = V(0.11f, 0.21f, 0.07f), Sun = V(0.24f, 0.34f, 0.09f), Drift = 0.12f };
        public static Palette DeepGreen() => new Palette { Shade = V(0.025f, 0.065f, 0.04f), Body = V(0.07f, 0.16f, 0.08f), Sun = V(0.15f, 0.27f, 0.1f), Drift = 0.1f };
        public static Palette Straw() => new Palette { Shade = V(0.13f, 0.09f, 0.035f), Body = V(0.3f, 0.22f, 0.09f), Sun = V(0.44f, 0.34f, 0.15f), Drift = 0.12f };
        public static Palette Glaucous() => new Palette { Shade = V(0.05f, 0.075f, 0.07f), Body = V(0.13f, 0.18f, 0.16f), Sun = V(0.23f, 0.29f, 0.25f), Drift = 0.08f };

        // --- Bark ---

        public static BarkForm Furrowed(Vector3 colour, float moss = 0.35f, float tile = 2.2f) => new BarkForm { Kind = BarkKind.Furrowed, Colour = colour, YoungColour = colour * 0.72f, Moss = moss, Tile = tile };
        public static BarkForm SmoothBark(Vector3 colour, float moss = 0.3f, float bumps = 0.05f) => new BarkForm { Kind = BarkKind.Smooth, Colour = colour, YoungColour = colour * 0.75f, Moss = moss, Tile = 2f, Bumps = bumps };
        public static BarkForm PlatedBark(Vector3 colour, float moss = 0.25f) => new BarkForm { Kind = BarkKind.Plated, Colour = colour, YoungColour = colour * 0.82f, Moss = moss, Tile = 2f, Bumps = 0.06f };

        // --- A trunk that gives way to a few big limbs, and a crown of what's fought its way out from those. ---

        public static TreeForm Spreading(float height, float width, float bole, float girth, FoliageForm foliage, BarkForm bark, CrownShape crown = CrownShape.Hemispherical, float apical = 0.49f, int forks = 5, float wander = 0.1f) => new TreeForm
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

        public static FoliageForm Leaves(CardForm card, Palette colours, float size = 3.1f, int cards = 280, float shell = 0.72f) => new FoliageForm
        {
            Card = card, Colours = colours, CardSize = new FloatRange(size * 0.84f, size * 1.16f), MostCards = cards, Shell = shell,
        };

        // --- Tiers: a spire of boughs that sag and lift at their ends, with shoots along them, for what has an architecture rigid enough to be written down. ---

        public static LimbForm[] Tiers(float spacing, int whorl, float low, float high, float sag, float lift, float shootSpacing = 0.85f) => new[]
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

        // --- Shrubs: small trees with several stems. ---

        public static TreeForm Shrub(float height, float width, CardForm card, Palette colours, BarkForm? bark = null, float cardSize = 1.5f, int cards = 130, CrownShape crown = CrownShape.Spherical) => new TreeForm
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

        // --- Flowers: leafy stems from a clump, a flower on each or a spike of them. ---

        public static PlantSpecies Flower(string id, CardForm head, Vector3 colour, float height = 0.9f, float size = 0.42f, Inflorescence carried = Inflorescence.Solitary, float spike = 0.55f, int leaves = 3, Palette? foliage = null) => new PlantSpecies(id, WorldAssetKind.GroundCover, new HerbForm
        {
            Height = new FloatRange(height * 0.72f, height * 1.25f), LeafCard = Undergrowth.HerbLeaf, LeavesPerStem = leaves, Flower = head, Carried = carried, FlowerSize = new FloatRange(size * 0.8f, size * 1.2f), SpikeLength = spike, Foliage = foliage ?? Green(),
            Bloom = new Palette { Shade = colour * 0.5f, Body = colour * 0.82f, Sun = Vector3.ComponentMin(colour * 1.16f, Vector3.One), Drift = 0.14f },
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.8f, 1.25f) };

        // --- What only covers the ground: a cushion of something fine, laid over it. ---

        public static PlantSpecies Cushion(string id, Palette colours, float size = 1.6f, float lift = 0.05f) => new PlantSpecies(id, WorldAssetKind.GroundCover, new GroundForm { Card = Undergrowth.Moss, Size = new FloatRange(size * 0.7f, size * 1.3f), Layers = 2, Lift = lift, Colours = colours })
        { Footprint = size * 0.6f, Scale = new FloatRange(0.8f, 1.4f), Sways = false, Variants = 3 };

        // --- What lies on the ground. A log is in the bark of whatever fell. ---

        private static readonly FungusForm ShelfFungi = new FungusForm { Count = new IntRange(3, 5), Spread = 0.24f, Height = new FloatRange(0.05f, 0.12f), CapRadius = new FloatRange(0.22f, 0.36f), Dome = 0.3f, StemWidth = 0.1f, Cap = V(0.4f, 0.26f, 0.12f), CapTop = V(0.56f, 0.42f, 0.22f), Gills = V(0.7f, 0.64f, 0.5f) };

        public static PlantSpecies Log(string id, BarkForm bark, float radius, float length, bool fungi = true) => new PlantSpecies(id, WorldAssetKind.Deadwood, new DeadwoodForm
        {
            Shape = DeadwoodShape.Log, Radius = new FloatRange(radius * 0.8f, radius * 1.2f), Length = new FloatRange(length * 0.75f, length * 1.2f), Bark = bark, Fungi = fungi ? ShelfFungi : null,
        })
        { Footprint = length * 0.55f, SolidRadius = radius, SolidLength = length, Sways = false, Variants = 4, Scale = new FloatRange(0.8f, 1.2f) };

        public static PlantSpecies Stump(string id, BarkForm bark, float radius, bool fungi = true) => new PlantSpecies(id, WorldAssetKind.Deadwood, new DeadwoodForm
        {
            Shape = DeadwoodShape.Stump, Radius = new FloatRange(radius * 0.8f, radius * 1.2f), Length = new FloatRange(0.7f, 1.8f), Bark = bark, Fungi = fungi ? ShelfFungi : null,
        })
        { Footprint = radius * 2f, SolidRadius = radius * 1.05f, Sways = false, Variants = 4, Scale = new FloatRange(0.8f, 1.2f) };
    }
}
