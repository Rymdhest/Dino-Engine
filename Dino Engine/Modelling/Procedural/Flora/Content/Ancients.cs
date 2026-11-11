using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// What grew before the flowers: monkey puzzles, cycads, horsetails and tree ferns, which the Fernlands and the Elder Pines are made of and which make a land look as old as the beasts the Reich
    /// grows. And the cloud forest, where everything wears moss and the mist never lifts.
    /// </summary>
    [FloraCollection]
    public static class Ancients
    {
        // --- Cards ---

        /// <summary>A monkey puzzle's shoots: ropes covered all round in stiff overlapping scales, forking only a little.</summary>
        public static readonly ScaleSpray AraucariaRopes = new ScaleSpray { Id = "scales.araucaria", Depth = 1, Length = 0.9f, Angle = 0.62f, Ratio = 0.58f, Branchlets = 4, Thickness = 0.028f, Barbs = 15, BarbLength = 0.028f, BarbedThroughout = true, Translucency = 0.25f, Roughness = 0.55f };

        // --- Colours ---

        private static Palette Glossy() => new Palette { Shade = V(0.02f, 0.06f, 0.035f), Body = V(0.05f, 0.14f, 0.06f), Sun = V(0.13f, 0.25f, 0.08f), Drift = 0.09f, Patches = 0.07f };
        private static Palette CycadGreen() => new Palette { Shade = V(0.03f, 0.07f, 0.03f), Body = V(0.08f, 0.17f, 0.05f), Sun = V(0.2f, 0.3f, 0.07f), Drift = 0.1f };
        private static Palette HorsetailGreen() => new Palette { Shade = V(0.05f, 0.11f, 0.04f), Body = V(0.13f, 0.25f, 0.07f), Sun = V(0.26f, 0.38f, 0.1f), Drift = 0.12f };
        private static Palette CloudLeaves() => new Palette { Shade = V(0.025f, 0.07f, 0.04f), Body = V(0.07f, 0.17f, 0.07f), Sun = V(0.17f, 0.3f, 0.09f), Drift = 0.1f, Patches = 0.12f };
        private static Palette HangingMoss() => new Palette { Shade = V(0.07f, 0.12f, 0.04f), Body = V(0.17f, 0.27f, 0.08f), Sun = V(0.3f, 0.4f, 0.12f), Drift = 0.1f };

        // --- The monkey puzzle ---

        /// <summary>
        /// A monkey puzzle as it is when old: a straight grey column with its lower boughs long shed, and at the top a dome of whorled boughs turned up at their ends, each a rope of scales. It's the
        /// tree the oldest woods were made of, and nothing else in a land has its outline.
        /// </summary>
        public static readonly PlantSpecies Araucaria = new PlantSpecies("araucaria", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(19f, 26f),
            Trunk = new TrunkForm
            {
                Girth = new FloatRange(0.026f, 0.034f), Lean = new FloatRange(0f, 0.035f), Curve = new StemCurve { Wander = 0.012f, Waves = 2f },
                Flare = new FloatRange(0.35f, 0.55f), LobeDepth = new FloatRange(0.1f, 0.18f),
            },
            Roots = new RootForm { Reach = new FloatRange(0.08f, 0.12f) },
            Crown = CrownShape.Hemispherical,
            CrownBase = new FloatRange(0.5f, 0.62f),
            CrownRadius = new FloatRange(0.3f, 0.4f),
            CrownBumps = 0.12f,
            Limbs = new[]
            {
                // Whorls of five, near level low down and climbing higher up, longest at the bottom of the crown, each lifting at its tip as a candelabrum's arms do. They run as long as
                // they are and aren't stopped at the crown's edge, as there's no fine wood to fill it.
                new LimbForm
                {
                    Spacing = 1.35f, Whorl = 5, From = 0.5f, To = 0.98f, Jitter = 0.5f, Divergence = 63f, Missing = 0.15f,
                    Angle = new Ramp(1.5f, 1.1f), AngleJitter = 0.1f,
                    Length = new Ramp(0.28f, 0.36f), Profile = new Ramp(1f, 0.5f), Fit = false, Wander = 0.04f, Waves = 1.5f,
                    Sag = 0.06f, Rise = new Ramp(0.28f, 0.36f), RiseFrom = 0.5f, Shortest = 0.6f, StepLength = 0.55f,
                },
                new LimbForm
                {
                    Spacing = 0.95f, Whorl = 2, From = 0.25f, To = 1f, Jitter = 0.5f, Divergence = 90f, DivergenceJitter = 0.12f,
                    Angle = new Ramp(1.0f, 0.65f), AngleJitter = 0.12f,
                    Length = new Ramp(0.3f, 0.4f), Profile = new Ramp(1f, 0.6f), OfParent = true, Shrink = 0.8f, Fit = false, Shortest = 0.4f,
                    Wander = 0.06f, Waves = 1.5f, Rise = 0.18f, RiseFrom = 0.4f, StepLength = 0.5f,
                },
            },
            LimbGirth = 0.8f,
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = V(0.48f, 0.44f, 0.4f), YoungColour = V(0.3f, 0.36f, 0.22f), Moss = 0.2f, Tile = 2.2f, Bumps = 0.04f },
            Foliage = new FoliageForm { Card = AraucariaRopes, CardSize = new FloatRange(2.1f, 2.9f), MostCards = 320, Spacing = 0.3f, Tilt = 0.3f, Fold = 0.18f, Droop = 0.06f, Cross = 0.18f, Shade = 0f, Colours = Glossy() },
        })
        { Footprint = 6.5f, SolidRadius = 1f, Variants = 6, Scale = new FloatRange(0.82f, 1f) };

        /// <summary>A young monkey puzzle, still a cone of whorls from the ground up, before it sheds its skirts.</summary>
        public static readonly PlantSpecies YoungAraucaria = new PlantSpecies("araucaria.young", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(8f, 13f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.026f, 0.032f), Lean = new FloatRange(0f, 0.04f), Curve = new StemCurve { Wander = 0.015f, Waves = 2f }, Flare = new FloatRange(0.2f, 0.4f) },
            Crown = CrownShape.Conical,
            CrownBase = new FloatRange(0.04f, 0.12f),
            CrownRadius = new FloatRange(0.28f, 0.36f),
            CrownBumps = 0.08f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Spacing = 0.95f, Whorl = 5, From = 0.06f, To = 0.97f, Jitter = 0.4f, Divergence = 63f, Missing = 0.1f,
                    Angle = new Ramp(1.55f, 1.0f), AngleJitter = 0.1f,
                    Length = new Ramp(0.42f, 0.5f), Wander = 0.03f, Waves = 1.5f,
                    Sag = 0.04f, Rise = new Ramp(0.16f, 0.24f), RiseFrom = 0.5f, FitRoom = 0.03f, Shortest = 0.5f, StepLength = 0.5f,
                },
                new LimbForm
                {
                    Spacing = 0.9f, Whorl = 2, From = 0.3f, To = 1f, Jitter = 0.5f, Divergence = 90f,
                    Angle = new Ramp(1.0f, 0.65f), AngleJitter = 0.12f,
                    Length = new Ramp(0.28f, 0.38f), Profile = new Ramp(1f, 0.6f), OfParent = true, Shrink = 0.8f, Fit = false, Shortest = 0.35f,
                    Wander = 0.06f, Waves = 1.5f, Rise = 0.14f, RiseFrom = 0.4f, StepLength = 0.45f,
                },
            },
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = V(0.46f, 0.42f, 0.36f), YoungColour = V(0.3f, 0.36f, 0.22f), Moss = 0.1f, Tile = 2f, Bumps = 0.03f },
            Foliage = new FoliageForm { Card = AraucariaRopes, CardSize = new FloatRange(2f, 2.7f), MostCards = 320, Spacing = 0.28f, Tilt = 0.32f, Fold = 0.18f, Droop = 0.08f, Cross = 0.2f, Shade = 0f, Colours = Glossy() },
        })
        { Footprint = 4.6f, SolidRadius = 0.6f, Variants = 5 };

        // --- Cycads: a thick scaled stem and a crown of stiff fronds, older than any palm and only looking like one. ---

        private static RosetteForm CycadCrown(int leaves, float length, float lean, float arch) => new RosetteForm
        {
            Card = Wilds.PalmFrond, Leaves = new IntRange(leaves, leaves + 6), Length = new FloatRange(length * 0.86f, length * 1.12f), Inner = 0.66f, Aspect = 0.3f,
            Lean = new FloatRange(lean, lean + 0.9f), Arch = new FloatRange(arch, arch + 0.5f), Curl = 1.05f, FootSpread = 0.12f, Segments = 4, Fold = 0.3f, Shell = 0.45f,
            Joint = PlantJoint.Frond with { Touch = 0f }, Links = 2, Colours = CycadGreen(),
        };

        /// <summary>A cycad grown up on its stem, a man's height and more, the stem armoured in the bases of the fronds it's shed.</summary>
        public static readonly PlantSpecies Cycad = new PlantSpecies("cycad", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(1.6f, 3.0f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.12f, 0.16f), Lean = new FloatRange(0f, 0.12f), Curve = new StemCurve { Wander = 0.03f, Waves = 1.2f }, Flare = new FloatRange(0.08f, 0.18f), Lobes = new IntRange(0, 0) },
            CrownBase = new FloatRange(0.86f, 0.92f),
            CrownRadius = new FloatRange(0.4f, 0.5f),
            Bark = new BarkForm { Kind = BarkKind.Ringed, Colour = V(0.42f, 0.38f, 0.3f), YoungColour = V(0.42f, 0.38f, 0.3f), Tile = 1.2f, Bumps = 0.06f, MostSides = 10 },
            Head = CycadCrown(18, 2.8f, 0.3f, 0.25f),
        })
        { Footprint = 2.6f, SolidRadius = 0.5f, Variants = 5, Scale = new FloatRange(0.85f, 1.2f) };

        /// <summary>A cycad with no stem to speak of yet: a stiff green fountain from the ground.</summary>
        public static readonly PlantSpecies DwarfCycad = new PlantSpecies("cycad.dwarf", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Wilds.PalmFrond, Leaves = new IntRange(13, 19), Length = new FloatRange(1.5f, 2.3f), Inner = 0.6f, Aspect = 0.3f, Lean = new FloatRange(0.35f, 1.25f), Arch = new FloatRange(0.25f, 0.7f),
            Curl = 1.1f, FootSpread = 0.1f, Segments = 4, Fold = 0.28f, Shell = 0.5f, Colours = CycadGreen(),
        })
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.3f) };

        /// <summary>Horsetails: a stand of jointed green rods, a plant older than the beasts.</summary>
        public static readonly PlantSpecies Horsetail = new PlantSpecies("horsetail", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(6, 10), BladeLength = new FloatRange(0.5f, 0.9f), BladeWidth = 0.014f, BladeArch = new FloatRange(0.6f, 1.4f), Spread = 0.4f,
            Stalks = new IntRange(14, 22), StalkHeight = new FloatRange(1.3f, 2.4f), Head = SeedHead.None, Colours = HorsetailGreen(),
        })
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.3f) };

        // --- The cloud forest ---

        /// <summary>
        /// A cloud oak: an evergreen bent and knotted by the wind on the high slopes, its limbs furred in moss and hung with it, its crown a dark mass in the mist. What a cloud forest is made of.
        /// </summary>
        public static readonly PlantSpecies CloudOak = new PlantSpecies("oak.cloud", WorldAssetKind.Tree, With(
            Spreading(15f, 0.6f, 0.3f, 0.052f, Leaves(Broadleaves.LaurelSpray, CloudLeaves(), 2.9f, 300), new BarkForm { Kind = BarkKind.Furrowed, Colour = V(0.42f, 0.38f, 0.32f), YoungColour = V(0.3f, 0.28f, 0.2f), Moss = 0.95f, MossColour = V(0.18f, 0.3f, 0.06f), Tile = 2f, Bumps = 0.14f }, forks: 6, wander: 0.24f),
            tree => tree.Hangers = new FoliageForm { Card = Undergrowth.Beard, CardSize = new FloatRange(2f, 3.4f), Aspect = 0.5f, MostCards = 110, Spacing = 0.6f, Pose = CardPose.Hanging, Fold = 0.1f, Droop = 0f, Cross = 0.25f, Shell = 0.2f, Shade = 0f, Colours = HangingMoss() }))
        { Footprint = 9f, SolidRadius = 1.4f, Variants = 6 };

        /// <summary>Taro's giant cousin, elephant ears: a few vast heart-shaped leaves on long stalks, out of the wet ground under the trees.</summary>
        public static readonly PlantSpecies ElephantEar = new PlantSpecies("elephantear", WorldAssetKind.Shrub, new RosetteForm
        {
            Card = Undergrowth.Hosta, Leaves = new IntRange(5, 8), Length = new FloatRange(1.9f, 2.7f), Aspect = 0.72f, Inner = 0.7f, Lean = new FloatRange(0.45f, 1.2f), Arch = new FloatRange(0.45f, 0.95f),
            Curl = 1.6f, FootSpread = 0.12f, Fold = 0.16f, Segments = 4, Joint = PlantJoint.Leaf, Links = 2, Colours = DeepGreen(),
        })
        { Footprint = 1.7f, Scale = new FloatRange(0.8f, 1.25f) };

        /// <summary>A bromeliad on the forest floor: a vase of stiff leaves, green outside and red at its heart.</summary>
        public static readonly PlantSpecies Bromeliad = new PlantSpecies("bromeliad", WorldAssetKind.GroundCover, new RosetteForm
        {
            Card = Undergrowth.Blade, Leaves = new IntRange(14, 20), Length = new FloatRange(0.7f, 1.1f), Inner = 0.5f, Aspect = 0.2f, Lean = new FloatRange(0.2f, 1.15f), Arch = new FloatRange(0.4f, 1f),
            FootSpread = 0.05f, Fold = 0.3f, Segments = 3, Shell = 0.35f, Joint = PlantJoint.Leaf with { Give = 0.2f, MostBend = 0.3f }, Links = 1,
            Colours = new Palette { Shade = V(0.04f, 0.09f, 0.04f), Body = V(0.15f, 0.22f, 0.07f), Sun = V(0.5f, 0.07f, 0.06f), Drift = 0.12f },
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.75f, 1.3f) };

        public static readonly PlantSpecies Orchid = Flower("flower.orchid", Undergrowth.Poppy, V(0.58f, 0.15f, 0.44f), height: 0.65f, size: 0.36f, leaves: 2, foliage: DeepGreen());
    }
}
