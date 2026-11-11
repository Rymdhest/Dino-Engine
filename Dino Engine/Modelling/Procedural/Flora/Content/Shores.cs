using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Where the land meets the sea. In the hot wet south, mangroves standing on their stilts in the tidal mud, nipa palms along the channels and cord grass on the flats; on the windward coasts of
    /// the north, pines the gales have bent flat, tamarisk, and thrift and sea kale on the shingle.
    /// </summary>
    [FloraCollection]
    public static class Shores
    {
        // --- Colours ---

        private static Palette MangroveGreen() => new Palette { Shade = V(0.025f, 0.065f, 0.035f), Body = V(0.07f, 0.16f, 0.06f), Sun = V(0.16f, 0.27f, 0.08f), Drift = 0.09f, Patches = 0.09f };
        private static Palette NipaGreen() => new Palette { Shade = V(0.04f, 0.09f, 0.035f), Body = V(0.1f, 0.2f, 0.06f), Sun = V(0.22f, 0.32f, 0.09f), Drift = 0.1f };
        private static Palette Tamarisk() => new Palette { Shade = V(0.17f, 0.09f, 0.12f), Body = V(0.38f, 0.22f, 0.28f), Sun = V(0.52f, 0.34f, 0.4f), Drift = 0.1f, Patches = 0.14f };
        private static Palette CordGrass() => new Palette { Shade = V(0.07f, 0.1f, 0.04f), Body = V(0.18f, 0.25f, 0.08f), Sun = V(0.34f, 0.37f, 0.14f), Drift = 0.14f };

        /// <summary>
        /// A mangrove: a dark glossy crown on a crooked trunk that stands on a tangle of stilts, each arching out from high up and down into the tidal mud. Where they grow, the land and the
        /// sea can't be told apart.
        /// </summary>
        public static readonly PlantSpecies Mangrove = new PlantSpecies("mangrove", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(9f, 13.5f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.4f, 0.56f), Girth = new FloatRange(0.034f, 0.044f), Lean = new FloatRange(0.03f, 0.2f), Curve = new StemCurve { Wander = 0.12f, Waves = 2.2f }, Flare = new FloatRange(0.1f, 0.2f), LobeDepth = new FloatRange(0.1f, 0.18f) },
            Roots = new RootForm { Reach = new FloatRange(0.2f, 0.3f), Girth = new FloatRange(0.22f, 0.3f), Rise = 6f, Props = true },
            RootCount = new IntRange(9, 12),
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.32f, 0.42f),
            CrownRadius = new FloatRange(0.5f, 0.62f),
            CrownBumps = 0.22f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(4f, 6f), From = 0.6f, To = 1f, Angle = new Ramp(1.15f, 0.6f), AngleJitter = 0.22f,
                    Length = new Ramp(0.5f, 0.66f), Wander = 0.18f, Waves = 2.6f, Rise = new Ramp(0.02f, 0.04f), RiseFrom = 0.2f, FitRoom = 0.05f,
                },
                new LimbForm
                {
                    Count = new FloatRange(3f, 5f), From = 0.3f, To = 0.95f, OfParent = true, Angle = new Ramp(0.85f, 0.55f), AngleJitter = 0.22f,
                    Length = new Ramp(0.5f, 0.72f), Wander = 0.2f, Waves = 2f, Rise = 0.02f, RiseFrom = 0.3f, FitRoom = 0.05f, Shortest = 0.8f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(11, 13), Apical = new FloatRange(0.47f, 0.49f), Internode = 0.38f, MostNodes = 2600, ClumpSize = 2.8f, Gaps = 0.32f, Hollow = 0.7f },
            Bark = SmoothBark(V(0.42f, 0.38f, 0.32f), 0.2f, 0.08f),
            Foliage = new FoliageForm { Card = Broadleaves.LaurelSpray, CardSize = new FloatRange(2.4f, 3.2f), MostCards = 300, Spacing = 0.32f, Tilt = 0.35f, Droop = 0.12f, Cross = 0.2f, Colours = MangroveGreen() },
        })
        { Footprint = 6.5f, SolidRadius = 1.6f, Variants = 6 };

        /// <summary>A nipa palm: no stem at all, only great fronds springing straight from the mud of a tidal channel.</summary>
        public static readonly PlantSpecies NipaPalm = new PlantSpecies("palm.nipa", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Wilds.PalmFrond, Leaves = new IntRange(7, 11), Length = new FloatRange(3.6f, 5.2f), Inner = 0.72f, Aspect = 0.28f, Lean = new FloatRange(0.15f, 0.75f), Arch = new FloatRange(0.6f, 1.2f),
            Curl = 1.45f, FootSpread = 0.2f, Segments = 5, Fold = 0.3f, Shell = 0.45f, Joint = PlantJoint.Frond with { Touch = 0.5f }, Links = 2, Colours = NipaGreen(),
        })
        { Footprint = 2.4f, Scale = new FloatRange(0.8f, 1.2f) };

        /// <summary>Cord grass: the first thing to hold the tidal mud, tall green blades and thin seed spikes.</summary>
        public static readonly PlantSpecies CordGrassTuft = new PlantSpecies("grass.cord", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(18, 28), BladeLength = new FloatRange(1.2f, 2f), BladeWidth = 0.035f, BladeArch = new FloatRange(0.6f, 1.5f), Spread = 0.42f,
            Stalks = new IntRange(3, 6), StalkHeight = new FloatRange(1.8f, 2.5f), Head = SeedHead.Ear, HeadLength = 0.4f, HeadWidth = 0.06f, HeadColour = V(0.44f, 0.38f, 0.22f), Colours = CordGrass(),
        })
        { Footprint = 1f, Scale = new FloatRange(0.85f, 1.3f) };

        /// <summary>A pine on a windward headland, bent from its foot and flattened on top by every gale it's stood through.</summary>
        public static readonly PlantSpecies WindsweptPine = new PlantSpecies("pine.windswept", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(10f, 15f),
            Trunk = new TrunkForm
            {
                Reach = new FloatRange(0.7f, 0.86f), Girth = new FloatRange(0.034f, 0.044f), Lean = new FloatRange(0.2f, 0.34f), Curve = new StemCurve { Wander = 0.1f, Waves = 2f, Rise = 0.03f },
                Flare = new FloatRange(0.35f, 0.6f),
            },
            Roots = new RootForm(),
            Crown = CrownShape.Umbrella,
            CrownBase = new FloatRange(0.46f, 0.58f),
            CrownRadius = new FloatRange(0.5f, 0.66f),
            CrownBumps = 0.24f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(5f, 8f), From = 0.45f, To = 1f, Jitter = 1f,
                    Angle = new Ramp(1.5f, 1.05f), AngleJitter = 0.2f,
                    Length = new Ramp(0.32f, 0.48f), Wander = 0.16f, Waves = 2.8f, Rise = new Ramp(0.01f, 0.03f), RiseFrom = 0.5f, FitRoom = 0.04f, StepLength = 0.7f,
                },
            },
            Buds = new BudForm
            {
                Years = new IntRange(12, 14), Apical = new FloatRange(0.49f, 0.51f), Internode = 0.4f, MostNodes = 2400,
                Tropism = new FloatRange(0f, 0.02f), BranchAngle = new FloatRange(0.75f, 1f), Hollow = 0.75f, ClumpSize = 4f, Gaps = 0.42f, MarkerDensity = 1.1f, Occupancy = 0.7f,
            },
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = V(0.44f, 0.36f, 0.3f), YoungColour = V(0.38f, 0.26f, 0.18f), Moss = 0.25f, Tile = 1.8f, Bumps = 0.08f },
            Foliage = new FoliageForm { Card = Conifers.LongNeedles, CardSize = new FloatRange(2.7f, 3.6f), MostCards = 300, Spacing = 0.32f, Tilt = 0.2f, Fold = 0.2f, Droop = 0.08f, Cross = 0.1f, Colours = Needled() },
        })
        { Footprint = 7f, SolidRadius = 0.9f, Variants = 6 };

        /// <summary>Tamarisk: a feathery haze of grey-pink, salt-proof, on the sand behind the beach.</summary>
        public static readonly PlantSpecies TamariskShrub = new PlantSpecies("shrub.tamarisk", WorldAssetKind.Shrub, Shrub(3.4f, 0.6f, Conifers.Plumes, Tamarisk(), cardSize: 1.6f, cards: 150))
        { Footprint = 1.8f, Scale = new FloatRange(0.8f, 1.25f) };

        public static readonly PlantSpecies Thrift = Flower("flower.thrift", Undergrowth.Cluster, V(0.66f, 0.24f, 0.42f), height: 0.45f, size: 0.34f, leaves: 2);

        /// <summary>Sea kale, broad blue-grey leaves in a heap on the shingle.</summary>
        public static readonly PlantSpecies SeaKale = new PlantSpecies("seakale", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Undergrowth.Hosta, Leaves = new IntRange(8, 12), Length = new FloatRange(0.7f, 1.1f), Aspect = 0.76f, Inner = 0.55f, Lean = new FloatRange(0.5f, 1.3f), Arch = new FloatRange(0.2f, 0.6f),
            Fold = 0.2f, Joint = PlantJoint.Leaf, Links = 2, Segments = 3, Colours = Glaucous(),
        })
        { Footprint = 1f, Scale = new FloatRange(0.8f, 1.3f) };
    }
}
