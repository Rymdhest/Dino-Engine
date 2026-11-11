using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The cold country: larches gone gold over a floor of lichen and bilberry, silver firs, the stone pines and the twisted mountain pines of the high pastures with their gentians and edelweiss,
    /// and the bogs, where cotton grass nods over red sphagnum.
    /// </summary>
    [FloraCollection]
    public static class Northlands
    {
        // --- Colours ---

        private static Palette LarchGold() => new Palette { Shade = V(0.15f, 0.085f, 0.02f), Body = V(0.4f, 0.25f, 0.035f), Sun = V(0.55f, 0.4f, 0.07f), Drift = 0.12f, Patches = 0.14f };
        private static Palette FirBlue() => new Palette { Shade = V(0.02f, 0.045f, 0.05f), Body = V(0.045f, 0.105f, 0.1f), Sun = V(0.11f, 0.19f, 0.15f), Drift = 0.07f, Patches = 0.06f };
        private static Palette StonePine() => new Palette { Shade = V(0.022f, 0.05f, 0.045f), Body = V(0.055f, 0.12f, 0.09f), Sun = V(0.14f, 0.23f, 0.12f), Drift = 0.1f, Patches = 0.08f };
        private static Palette Alpenrose() => new Palette { Shade = V(0.26f, 0.03f, 0.1f), Body = V(0.52f, 0.07f, 0.2f), Sun = V(0.7f, 0.18f, 0.32f), Drift = 0.12f };
        private static Palette Bilberry() => new Palette { Shade = V(0.04f, 0.08f, 0.03f), Body = V(0.11f, 0.2f, 0.06f), Sun = V(0.22f, 0.32f, 0.08f), Drift = 0.14f };
        private static Palette DwarfBirch() => new Palette { Shade = V(0.1f, 0.06f, 0.02f), Body = V(0.26f, 0.16f, 0.05f), Sun = V(0.42f, 0.26f, 0.08f), Drift = 0.16f, Patches = 0.14f };

        // --- Trees ---

        // Tiers whose boughs dip and then lift at their tips, with short shoots along them: a larch's are fewer and airier than a spruce's.
        private static TreeForm LarchForm(Palette colours) => new TreeForm
        {
            Height = new FloatRange(19f, 26f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.021f, 0.027f), Lean = new FloatRange(0f, 0.05f), Curve = new StemCurve { Wander = 0.025f, Waves = 2.2f }, Flare = new FloatRange(0.3f, 0.5f) },
            Crown = CrownShape.Conical,
            CrownBase = new FloatRange(0.14f, 0.3f),
            CrownRadius = new FloatRange(0.17f, 0.24f),
            CrownBumps = 0.18f,
            Limbs = Tiers(1.25f, 5, 1.62f, 1.12f, 0.16f, 0.26f, 0.8f),
            Bark = new BarkForm { Kind = BarkKind.Plated, Colour = V(0.5f, 0.36f, 0.3f), YoungColour = V(0.44f, 0.3f, 0.2f), Moss = 0.3f, Tile = 2f, Bumps = 0.06f },
            Foliage = new FoliageForm { Card = Conifers.ShortNeedles, CardSize = new FloatRange(2.5f, 3.3f), MostCards = 370, Spacing = 0.31f, Tilt = 0.32f, Fold = 0.24f, Droop = 0.22f, Cross = 0.16f, Shade = 0f, Colours = colours },
        };

        /// <summary>A larch in its season, the one conifer that turns: a spire of soft gold needles that lights a dark wood from inside, and lets them all fall.</summary>
        public static readonly PlantSpecies Larch = new PlantSpecies("larch", WorldAssetKind.Tree, LarchForm(LarchGold()))
        { Footprint = 5.5f, SolidRadius = 0.7f, Variants = 6, Scale = new FloatRange(0.82f, 1.05f) };

        /// <summary>A silver fir: grey smooth bark and level tiers of dark blue-green, flat-topped when old.</summary>
        public static readonly PlantSpecies SilverFir = new PlantSpecies("fir", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(19f, 25f),
            Trunk = new TrunkForm { Girth = new FloatRange(0.026f, 0.032f), Lean = new FloatRange(0f, 0.025f), Curve = new StemCurve { Wander = 0.01f, Waves = 2f }, Flare = new FloatRange(0.35f, 0.55f) },
            Crown = CrownShape.TaperedColumn,
            CrownBase = new FloatRange(0.06f, 0.16f),
            CrownRadius = new FloatRange(0.17f, 0.22f),
            CrownBumps = 0.1f,
            Limbs = Tiers(1.4f, 5, 1.6f, 1.32f, 0.05f, 0.14f),
            Bark = SmoothBark(V(0.46f, 0.45f, 0.43f), 0.3f, 0.04f),
            Foliage = new FoliageForm { Card = Conifers.ShortNeedles, CardSize = new FloatRange(2.6f, 3.4f), MostCards = 380, Spacing = 0.3f, Tilt = 0.18f, Fold = 0.2f, Droop = 0.1f, Cross = 0.1f, Shade = 0f, Colours = FirBlue() },
        })
        { Footprint = 5.5f, SolidRadius = 0.8f, Variants = 6, Scale = new FloatRange(0.8f, 1.02f) };

        /// <summary>A stone pine of the high pastures: dense, dark and rounded, lower limbs to the ground, the last tree before the rock.</summary>
        public static readonly PlantSpecies StonePineTree = new PlantSpecies("pine.stone", WorldAssetKind.Tree, new TreeForm
        {
            Height = new FloatRange(12f, 17f),
            Trunk = new TrunkForm { Reach = new FloatRange(0.74f, 0.88f), Girth = new FloatRange(0.036f, 0.046f), Lean = new FloatRange(0f, 0.1f), Curve = new StemCurve { Wander = 0.06f, Waves = 2.2f }, Flare = new FloatRange(0.4f, 0.6f) },
            Crown = CrownShape.Spherical,
            CrownBase = new FloatRange(0.1f, 0.22f),
            CrownRadius = new FloatRange(0.3f, 0.38f),
            CrownBumps = 0.22f,
            Limbs = new[]
            {
                new LimbForm
                {
                    Count = new FloatRange(10f, 14f), From = 0.15f, To = 1f, Jitter = 0.9f,
                    Angle = new Ramp(1.35f, 0.75f), AngleJitter = 0.18f,
                    Length = new Ramp(0.32f, 0.22f), Wander = 0.1f, Waves = 2.4f, Rise = new Ramp(0.03f, 0.06f), RiseFrom = 0.4f, FitRoom = 0.05f, StepLength = 0.6f,
                },
            },
            Buds = new BudForm { Years = new IntRange(12, 14), Apical = new FloatRange(0.5f, 0.52f), Internode = 0.36f, MostNodes = 2400, Tropism = new FloatRange(0.03f, 0.06f), Hollow = 0.62f, ClumpSize = 3f, Gaps = 0.3f, MarkerDensity = 1.2f, Occupancy = 0.7f },
            Bark = PlatedBark(V(0.44f, 0.4f, 0.37f), 0.35f),
            Foliage = new FoliageForm { Card = Conifers.LongNeedles, CardSize = new FloatRange(2.6f, 3.5f), MostCards = 340, Spacing = 0.3f, Tilt = 0.3f, Fold = 0.2f, Droop = 0.1f, Cross = 0.14f, Colours = StonePine() },
        })
        { Footprint = 6f, SolidRadius = 0.9f, Variants = 6 };

        // --- Shrubs ---

        /// <summary>Mountain pine as the wind leaves it up high: a low dark tangle of needled limbs crawling over the rock.</summary>
        public static readonly PlantSpecies MountainPine = new PlantSpecies("pine.mountain", WorldAssetKind.Shrub, Shrub(2.2f, 0.95f, Conifers.LongNeedles, StonePine(), PlatedBark(V(0.4f, 0.34f, 0.3f), 0.2f), cardSize: 1.5f, cards: 140, crown: CrownShape.Hemispherical))
        { Footprint = 2.2f, Scale = new FloatRange(0.8f, 1.35f) };

        /// <summary>Alpenrose: a low evergreen shrub that goes crimson with flowers along the edge of the snow.</summary>
        public static readonly PlantSpecies AlpenroseShrub = new PlantSpecies("shrub.alpenrose", WorldAssetKind.Shrub, Shrub(1.3f, 0.82f, Undergrowth.AzaleaBloom, Alpenrose(), cardSize: 1.05f, cards: 120))
        { Footprint = 1.4f, Scale = new FloatRange(0.8f, 1.3f) };

        /// <summary>Dwarf birch: knee-high, round-leaved and coppery, on the bogs and the tundra.</summary>
        public static readonly PlantSpecies DwarfBirchShrub = new PlantSpecies("shrub.dwarfbirch", WorldAssetKind.Shrub, Shrub(1.1f, 0.88f, Broadleaves.SmallLeaves, DwarfBirch(), cardSize: 0.95f, cards: 140))
        { Footprint = 1.2f, Scale = new FloatRange(0.8f, 1.35f) };

        // --- What covers the ground ---

        /// <summary>Bilberry, a low green carpet of tiny leaves under the larches.</summary>
        public static readonly PlantSpecies BilberryPatch = new PlantSpecies("bilberry", WorldAssetKind.GroundCover, Shrub(0.62f, 0.95f, Broadleaves.SmallLeaves, Bilberry(), cardSize: 0.62f, cards: 46))
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.3f) };

        public static readonly PlantSpecies Lichen = Cushion("lichen", new Palette { Shade = V(0.15f, 0.17f, 0.13f), Body = V(0.34f, 0.38f, 0.29f), Sun = V(0.48f, 0.52f, 0.4f), Drift = 0.08f }, size: 1.5f, lift: 0.04f);
        public static readonly PlantSpecies Sphagnum = Cushion("sphagnum", new Palette { Shade = V(0.15f, 0.05f, 0.035f), Body = V(0.34f, 0.15f, 0.07f), Sun = V(0.48f, 0.31f, 0.09f), Drift = 0.14f }, size: 1.8f, lift: 0.06f);
        public static readonly PlantSpecies MossCampion = Cushion("cushion.campion", new Palette { Shade = V(0.2f, 0.05f, 0.11f), Body = V(0.42f, 0.12f, 0.24f), Sun = V(0.6f, 0.24f, 0.36f), Drift = 0.12f }, size: 1.0f, lift: 0.07f);

        /// <summary>Cotton grass, white tufts nodding on thin stalks over the wet ground of a bog.</summary>
        public static readonly PlantSpecies CottonGrass = new PlantSpecies("cottongrass", WorldAssetKind.GroundCover, new TuftForm
        {
            Blades = new IntRange(10, 16), BladeLength = new FloatRange(0.6f, 1.05f), BladeWidth = 0.02f, BladeArch = new FloatRange(0.8f, 1.8f), Spread = 0.3f,
            Stalks = new IntRange(6, 11), StalkHeight = new FloatRange(1f, 1.55f), Head = SeedHead.Plume, HeadCard = Undergrowth.ReedHead, HeadLength = 0.34f, HeadWidth = 0.32f, HeadColour = V(0.58f, 0.57f, 0.53f),
            Colours = new Palette { Shade = V(0.08f, 0.1f, 0.04f), Body = V(0.2f, 0.24f, 0.08f), Sun = V(0.34f, 0.34f, 0.12f), Drift = 0.12f },
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.8f, 1.3f) };

        /// <summary>Ceps: fat brown caps on fatter pale stems, where the larch and the birch drop their needles.</summary>
        public static readonly PlantSpecies Ceps = new PlantSpecies("mushroom.cep", WorldAssetKind.GroundCover, new FungusForm { Count = new IntRange(2, 4), Spread = 0.32f, Height = new FloatRange(0.2f, 0.4f), CapRadius = new FloatRange(0.16f, 0.3f), Dome = 0.62f, StemWidth = 0.44f, Flare = 0.04f, Cap = V(0.3f, 0.17f, 0.08f), CapTop = V(0.22f, 0.12f, 0.05f), Gills = V(0.4f, 0.38f, 0.24f), Stem = V(0.5f, 0.46f, 0.38f) })
        { Footprint = 0.5f, Scale = new FloatRange(0.7f, 1.4f), Sways = false, Variants = 3 };

        public static readonly PlantSpecies Gentian = Flower("flower.gentian", Undergrowth.Poppy, V(0.06f, 0.11f, 0.6f), height: 0.4f, size: 0.32f, leaves: 2, foliage: DeepGreen());
        public static readonly PlantSpecies Edelweiss = Flower("flower.edelweiss", Undergrowth.Aster, V(0.55f, 0.55f, 0.51f), height: 0.36f, size: 0.3f, leaves: 2, foliage: Glaucous());
    }
}
