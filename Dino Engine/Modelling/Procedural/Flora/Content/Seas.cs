using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Flora.FloraKit;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// What lives under the water and on it (SEA.md). In cold and temperate seas, meadows of eelgrass on the sand, kelp on the rock, wrack and sea lettuce where the waves wash, purple
    /// urchins and starfish; in warm seas, seagrass whose old blades are crusted pale, and weedy rock; in the hot south, reefs of staghorn, tables, brain and boulder corals, soft corals,
    /// sea fans and sponges, with black urchins and cushion stars on the sand; and on still fresh water, lilies. Nothing here is a biome's own: <see cref="SeaLife"/> says which coasts
    /// grow what, and <see cref="WorldSeaFloor"/> puts it down.
    /// </summary>
    [FloraCollection]
    public static class Seas
    {
        // --- Cards ---

        public static readonly LilyPad LilyPads = new LilyPad();

        public static readonly FlowerHead WaterLily = new FlowerHead
        {
            Petal = new LeafForm { Blade = LeafBlade.Simple, Width = 0.24f, Skew = 1f, Round = 1.25f, Veins = 0, VeinStrength = 0f, Mottle = 0.06f, Tone = new Vector3(1f, 0.99f, 0.97f) },
            Petals = 12, Whorls = 3, Length = 0.46f, Heart = 0.15f, HeartColour = new Vector3(0.78f, 0.55f, 0.08f),
        };

        public static readonly SeaFan SeaFans = new SeaFan();

        // --- Colours ---

        private static Palette EelgrassGreen() => new Palette { Shade = V(0.02f, 0.05f, 0.018f), Body = V(0.045f, 0.1f, 0.028f), Sun = V(0.085f, 0.16f, 0.04f), Drift = 0.12f, Patches = 0.1f };
        private static Palette TurtleGrass() => new Palette { Shade = V(0.025f, 0.06f, 0.02f), Body = V(0.055f, 0.12f, 0.032f), Sun = V(0.1f, 0.19f, 0.045f), Drift = 0.12f, Patches = 0.1f };
        private static Palette Pondweed() => new Palette { Shade = V(0.04f, 0.06f, 0.02f), Body = V(0.08f, 0.12f, 0.035f), Sun = V(0.14f, 0.18f, 0.05f), Drift = 0.14f };
        private static Palette Wrack() => new Palette { Shade = V(0.05f, 0.04f, 0.015f), Body = V(0.11f, 0.09f, 0.03f), Sun = V(0.2f, 0.16f, 0.05f), Drift = 0.12f };
        private static Palette Lettuce() => new Palette { Shade = V(0.04f, 0.1f, 0.02f), Body = V(0.09f, 0.2f, 0.035f), Sun = V(0.16f, 0.3f, 0.05f), Drift = 0.1f };
        // Wet kelp is dark olive-brown, darker than the sand and no lighter than the seagrass beside it: lighter, it read as pale copper stars through the water.
        private static Palette KelpBrown() => new Palette { Shade = V(0.05f, 0.04f, 0.014f), Body = V(0.11f, 0.085f, 0.03f), Sun = V(0.2f, 0.155f, 0.05f), Drift = 0.1f };
        private static Palette SugarKelp() => new Palette { Shade = V(0.07f, 0.055f, 0.02f), Body = V(0.15f, 0.115f, 0.04f), Sun = V(0.25f, 0.19f, 0.06f), Drift = 0.1f };
        private static Palette GiantKelp() => new Palette { Shade = V(0.06f, 0.048f, 0.016f), Body = V(0.13f, 0.1f, 0.035f), Sun = V(0.23f, 0.18f, 0.06f), Drift = 0.1f };

        // --- Fresh water ---

        /// <summary>Water lilies: pads crowding on still water, a young one bronze, with a white flower or a pink one open among them.</summary>
        public static readonly PlantSpecies Lily = new PlantSpecies("lily.water", WorldAssetKind.GroundCover, new LilyForm { Pad = LilyPads, Flower = WaterLily })
        { Footprint = 1.3f, Scale = new FloatRange(0.8f, 1.2f), Variants = 5 };

        /// <summary>Pondweed, under still water: olive ribbons rising from the mud. There's no swell to lean them, so they hardly move.</summary>
        public static readonly PlantSpecies PondweedBed = new PlantSpecies("pondweed", WorldAssetKind.GroundCover, new RibbonForm
        {
            Shoots = new IntRange(4, 7), BladesPerShoot = new IntRange(2, 4), BladeLength = new FloatRange(0.35f, 0.7f), BladeWidth = 0.02f, Lean = new FloatRange(0.1f, 0.4f),
            Arch = new FloatRange(0.4f, 1.1f), Spread = 0.5f, Colours = Pondweed(), Tips = V(0.1f, 0.09f, 0.04f), TipShare = 0.2f, Joint = SeaJoints.Ribbon with { Give = 0.15f },
            Segments = 3,
        })
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.2f), Variants = 3, CastsShadow = false };

        // --- Cold and temperate seas ---

        // A meadow is the commonest thing under the water, so its blades have three segments, all a ribbon a pixel or two wide needs from up here, and cast no shadow. Its shoots
        // stay as many: two thirds as many, even half as wide again, read as sprigs on sand rather than a carpet.
        /// <summary>Eelgrass: long dark ribbons in rows from runners in the sand, the old ones furred brown at their ends. A meadow of it is the darkest thing in the shallows.</summary>
        public static readonly PlantSpecies Eelgrass = new PlantSpecies("seagrass.eel", WorldAssetKind.GroundCover, new RibbonForm
        {
            Shoots = new IntRange(12, 16), BladesPerShoot = new IntRange(3, 5), BladeLength = new FloatRange(0.55f, 1f), BladeWidth = 0.024f, Taper = 0.15f, Lean = new FloatRange(0.18f, 0.55f),
            Arch = new FloatRange(0.5f, 1.4f), Spread = 0.95f, Colours = EelgrassGreen(), Tips = V(0.12f, 0.11f, 0.05f), TipShare = 0.35f, Dead = 0.08f,
            Segments = 3,
        })
        { Footprint = 1.4f, Scale = new FloatRange(0.85f, 1.25f), Variants = 5, CastsShadow = false };

        /// <summary>Bladderwrack: olive fronds forking off a short stalk, slumped over the rock where the waves wash, their tips swollen yellow.</summary>
        public static readonly PlantSpecies BladderWrack = new PlantSpecies("weed.wrack", WorldAssetKind.GroundCover, new RibbonForm
        {
            Shoots = new IntRange(6, 9), BladesPerShoot = new IntRange(3, 5), BladeLength = new FloatRange(0.22f, 0.45f), BladeWidth = 0.03f, Taper = 0.35f, Lean = new FloatRange(0.5f, 1.1f),
            Arch = new FloatRange(0.6f, 1.4f), Curl = 1.3f, Spread = 0.42f, Runners = 1, Segments = 3, Colours = Wrack(), Tips = V(0.26f, 0.2f, 0.06f), TipShare = 0.25f, Dead = 0.04f,
        })
        { Footprint = 0.8f, Scale = new FloatRange(0.8f, 1.3f), Variants = 4, CastsShadow = false };

        /// <summary>Sea lettuce: sheets of bright green, thin as paper, lying over the rock and the sand in the shallowest water.</summary>
        public static readonly PlantSpecies SeaLettuce = new PlantSpecies("weed.lettuce", WorldAssetKind.GroundCover, new RibbonForm
        {
            Shoots = new IntRange(3, 5), BladesPerShoot = new IntRange(2, 4), BladeLength = new FloatRange(0.18f, 0.32f), BladeWidth = 0.07f, Taper = 0.1f, Lean = new FloatRange(0.7f, 1.3f),
            Arch = new FloatRange(0.3f, 0.8f), Spread = 0.3f, Runners = 1, Segments = 3, Colours = Lettuce(), Tips = V(0.12f, 0.22f, 0.04f), TipShare = 0.1f, Dead = 0f,
            // A sheet as wide as it's long can hardly bend without its edges stretching (PlantRig's BendThatHolds): it lifts and settles rather than streams.
            Joint = SeaJoints.Ribbon with { Give = 0.5f, MostBend = 0.4f }, Links = 1,
        })
        { Footprint = 0.6f, Scale = new FloatRange(0.8f, 1.3f), Variants = 3, CastsShadow = false };

        /// <summary>Oarweed: a stiff stalk off the rock and a leathery blade split into straps that fan out from it and stream with the swell.</summary>
        public static readonly PlantSpecies Oarweed = new PlantSpecies("kelp.oarweed", WorldAssetKind.GroundCover, new KelpForm
        {
            Stipes = new IntRange(1, 2), StipeLength = new FloatRange(0.45f, 1.1f), StipeRadius = 0.03f, StipeLean = new FloatRange(0.05f, 0.3f), Fronds = new IntRange(7, 11),
            FrondLength = new FloatRange(0.8f, 1.5f), FrondWidth = 0.045f, Splay = 0.5f, FrondLean = new FloatRange(0.7f, 1.2f), FrondArch = new FloatRange(0.4f, 0.9f), Colours = KelpBrown(),
        })
        { Footprint = 1.3f, Scale = new FloatRange(0.8f, 1.25f), Variants = 5, CastsShadow = false };

        /// <summary>Sugar kelp: a short stalk and one long golden strap, crinkled down its middle, lying out with the water.</summary>
        public static readonly PlantSpecies SugarKelpPlant = new PlantSpecies("kelp.sugar", WorldAssetKind.GroundCover, new KelpForm
        {
            Stipes = new IntRange(1, 2), StipeLength = new FloatRange(0.18f, 0.4f), StipeRadius = 0.022f, Fronds = new IntRange(1, 2), FrondLength = new FloatRange(1.3f, 2.2f), FrondWidth = 0.12f,
            Splay = 1.5f, FrondLean = new FloatRange(0.5f, 1f), FrondArch = new FloatRange(0.6f, 1.3f), Ruffle = 0.025f, Colours = SugarKelp(),
        })
        { Footprint = 1.5f, Scale = new FloatRange(0.8f, 1.2f), Variants = 4, CastsShadow = false };

        /// <summary>
        /// Giant kelp, in deeper water: ropes up to the surface on their floats, fronds all up their tops, and their crowns lying out on the water, which is what a kelp forest is from
        /// above. It's made for water about 3.7 m deep, and the world scales it to the depth it stands in (WorldSeaFloor).
        /// </summary>
        public static readonly PlantSpecies GiantKelpPlant = new PlantSpecies("kelp.giant", WorldAssetKind.GroundCover, new KelpForm
        {
            Floats = true, Stipes = new IntRange(2, 3), StipeLength = new FloatRange(3.65f, 3.75f), StipeRadius = 0.02f, StipeLean = new FloatRange(0f, 0.12f), Fronds = new IntRange(5, 8),
            FrondLength = new FloatRange(0.8f, 1.3f), FrondWidth = 0.06f, SideFronds = new IntRange(6, 9), Colours = GiantKelp(),
        })
        { Footprint = 1.6f, Scale = new FloatRange(0.6f, 2f), Variants = 4 };

        /// <summary>
        /// The height a giant kelp is made to reach, at a scale of one: the world scales one by the depth of water over it over this. The top of its rope at the middle of its
        /// lengths, which are kept close, as one scaled seven metres down is that much more or less than the surface for each part in a hundred.
        /// </summary>
        public const float GiantKelpReach = 3.74f;

        /// <summary>A purple urchin, among the kelp, on the rock it grazes.</summary>
        public static readonly PlantSpecies PurpleUrchin = new PlantSpecies("urchin.purple", WorldAssetKind.GroundCover, new UrchinForm
        {
            Radius = new FloatRange(0.06f, 0.085f), Spine = new FloatRange(0.06f, 0.1f), Spines = new IntRange(11, 14), Body = V(0.13f, 0.04f, 0.12f), SpineColour = V(0.16f, 0.05f, 0.16f),
        })
        { Footprint = 0.3f, Scale = new FloatRange(0.85f, 1.2f), Variants = 3, Sways = false, CastsShadow = false };

        /// <summary>A green urchin, of the coldest seas.</summary>
        public static readonly PlantSpecies GreenUrchin = new PlantSpecies("urchin.green", WorldAssetKind.GroundCover, new UrchinForm
        {
            Radius = new FloatRange(0.055f, 0.08f), Spine = new FloatRange(0.04f, 0.07f), Spines = new IntRange(12, 14), Body = V(0.1f, 0.13f, 0.05f), SpineColour = V(0.12f, 0.15f, 0.06f),
        })
        { Footprint = 0.3f, Scale = new FloatRange(0.85f, 1.2f), Variants = 3, Sways = false, CastsShadow = false };

        /// <summary>A starfish of the rocky shore, orange, purple or ochre.</summary>
        public static readonly PlantSpecies OchreStar = new PlantSpecies("star.ochre", WorldAssetKind.GroundCover, new StarForm
        {
            Colours = new[] { V(0.42f, 0.14f, 0.05f), V(0.22f, 0.07f, 0.17f), V(0.36f, 0.22f, 0.06f) },
        })
        { Footprint = 0.4f, Scale = new FloatRange(0.85f, 1.2f), Variants = 4, Sways = false, CastsShadow = false };

        // --- Warm and hot seas ---

        /// <summary>Turtle grass: broader, brighter ribbons than eelgrass, the oldest crusted pale at their tips with what's grown on them.</summary>
        public static readonly PlantSpecies TurtleGrassBed = new PlantSpecies("seagrass.turtle", WorldAssetKind.GroundCover, new RibbonForm
        {
            Shoots = new IntRange(12, 16), BladesPerShoot = new IntRange(3, 5), BladeLength = new FloatRange(0.35f, 0.65f), BladeWidth = 0.032f, Taper = 0.1f, Lean = new FloatRange(0.15f, 0.45f),
            Arch = new FloatRange(0.4f, 1.1f), Spread = 0.95f, Colours = TurtleGrass(), Tips = V(0.3f, 0.29f, 0.22f), TipShare = 0.22f, Dead = 0.06f,
            Segments = 3,
        })
        { Footprint = 1.4f, Scale = new FloatRange(0.85f, 1.25f), Variants = 5, CastsShadow = false };

        // Living coral gives back a tenth of the light or so, brown, olive and purple, and sand two fifths: a reef reads from the air as dark heads on pale sand. As bright as the
        // sand, the colonies read as pale discs lying on it, like lily pads. Fans and sponges are brighter, as pigment and not coral is what colours them.
        /// <summary>Staghorn: a thicket of antlers that fork as they climb, up and out, their growing tips pale.</summary>
        public static readonly PlantSpecies Staghorn = new PlantSpecies("coral.staghorn", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Branching, Size = new FloatRange(0.8f, 1.4f), Height = new FloatRange(0.4f, 0.75f), Bases = new IntRange(4, 6), Generations = 2, BranchRadius = 0.05f, BranchAngle = 0.55f,
            Colours = new[] { V(0.17f, 0.12f, 0.055f), V(0.14f, 0.095f, 0.042f), V(0.11f, 0.1f, 0.046f), V(0.13f, 0.065f, 0.14f) }, Tips = V(0.3f, 0.29f, 0.27f), TipShare = 0.45f,
        })
        { Footprint = 1.2f, Scale = new FloatRange(0.8f, 1.3f), Variants = 5, Sways = false };

        /// <summary>A table coral: a broad flat plate on a stalk, its lobed rim pale where it grows and its underside in its own shade.</summary>
        public static readonly PlantSpecies TableCoral = new PlantSpecies("coral.table", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Table, Size = new FloatRange(1f, 1.9f), Height = new FloatRange(0.15f, 0.4f),
            Colours = new[] { V(0.17f, 0.13f, 0.066f), V(0.11f, 0.11f, 0.055f), V(0.14f, 0.083f, 0.1f) }, Tips = V(0.28f, 0.27f, 0.25f), TipShare = 0.55f,
        })
        { Footprint = 1.3f, Scale = new FloatRange(0.8f, 1.2f), Variants = 4, Sways = false };

        /// <summary>A brain coral: a round dome, ridged in meanders.</summary>
        public static readonly PlantSpecies BrainCoral = new PlantSpecies("coral.brain", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Massive, Size = new FloatRange(0.5f, 1.1f), Squat = new FloatRange(0.6f, 0.85f), Lumps = 0.03f, Grooves = 1f,
            Colours = new[] { V(0.18f, 0.135f, 0.058f), V(0.12f, 0.13f, 0.074f), V(0.19f, 0.126f, 0.1f) },
        })
        { Footprint = 0.7f, Scale = new FloatRange(0.8f, 1.3f), Variants = 4, Sways = false };

        /// <summary>A boulder coral: a broad lumpy mound, as old as anything on the reef.</summary>
        public static readonly PlantSpecies BoulderCoral = new PlantSpecies("coral.boulder", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Massive, Size = new FloatRange(0.7f, 1.6f), Squat = new FloatRange(0.5f, 0.85f), Lumps = 0.14f, Mottle = 0.15f,
            Colours = new[] { V(0.13f, 0.092f, 0.046f), V(0.11f, 0.105f, 0.05f), V(0.14f, 0.092f, 0.106f), V(0.165f, 0.133f, 0.046f) },
        })
        { Footprint = 0.9f, Scale = new FloatRange(0.8f, 1.3f), Variants = 4, Sways = false };

        /// <summary>A soft coral: fat fingers from a mound, olive, yellow, pink or purple, giving a little to the water.</summary>
        public static readonly PlantSpecies SoftCoral = new PlantSpecies("coral.fingers", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Fingers, Size = new FloatRange(0.45f, 0.85f), Height = new FloatRange(0.14f, 0.34f), Count = new IntRange(12, 18), Thickness = 0.045f,
            Colours = new[] { V(0.12f, 0.12f, 0.046f), V(0.25f, 0.2f, 0.045f), V(0.25f, 0.1f, 0.14f), V(0.15f, 0.065f, 0.16f) }, Tips = V(0.3f, 0.27f, 0.24f), TipShare = 0.4f,
        })
        { Footprint = 0.6f, Scale = new FloatRange(0.85f, 1.25f), Variants = 4 };

        /// <summary>A sea fan: a lattice on a stalk, purple, red, yellow or ochre, broadside to the waves and rocking with them.</summary>
        public static readonly PlantSpecies SeaFanPlant = new PlantSpecies("coral.fan", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Fan, Card = SeaFans, Size = new FloatRange(0.5f, 1f), Height = new FloatRange(0.4f, 0.6f),
            Colours = new[] { V(0.19f, 0.06f, 0.18f), V(0.28f, 0.085f, 0.042f), V(0.27f, 0.21f, 0.043f), V(0.24f, 0.15f, 0.06f) },
        })
        { Footprint = 0.6f, Scale = new FloatRange(0.85f, 1.25f), Variants = 4 };

        /// <summary>A barrel sponge: a great vase open at the top, dark inside.</summary>
        public static readonly PlantSpecies BarrelSponge = new PlantSpecies("sponge.barrel", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Tubes, Count = new IntRange(1, 1), Size = new FloatRange(0.45f, 0.9f), Height = new FloatRange(0.4f, 0.85f),
            Colours = new[] { V(0.19f, 0.09f, 0.048f), V(0.21f, 0.15f, 0.08f), V(0.14f, 0.07f, 0.14f) },
        })
        { Footprint = 0.6f, Scale = new FloatRange(0.85f, 1.2f), Variants = 3, Sways = false };

        /// <summary>Tube sponges: a stand of tubes, yellow, purple, orange or grey-blue, each open at its top.</summary>
        public static readonly PlantSpecies TubeSponge = new PlantSpecies("sponge.tube", WorldAssetKind.GroundCover, new CoralForm
        {
            Growth = CoralGrowth.Tubes, Count = new IntRange(3, 6), Size = new FloatRange(0.3f, 0.6f), Height = new FloatRange(0.28f, 0.6f), Thickness = 0.05f,
            Colours = new[] { V(0.28f, 0.22f, 0.05f), V(0.17f, 0.06f, 0.18f), V(0.29f, 0.12f, 0.03f), V(0.12f, 0.145f, 0.18f) },
        })
        { Footprint = 0.5f, Scale = new FloatRange(0.85f, 1.2f), Variants = 4, Sways = false };

        /// <summary>A long-spined black urchin, of the reef and the sand round it.</summary>
        public static readonly PlantSpecies BlackUrchin = new PlantSpecies("urchin.diadem", WorldAssetKind.GroundCover, new UrchinForm
        {
            Radius = new FloatRange(0.05f, 0.07f), Spine = new FloatRange(0.12f, 0.2f), Spines = new IntRange(12, 16), Body = V(0.03f, 0.02f, 0.03f), SpineColour = V(0.035f, 0.025f, 0.04f),
        })
        { Footprint = 0.35f, Scale = new FloatRange(0.85f, 1.2f), Variants = 3, Sways = false, CastsShadow = false };

        /// <summary>A cushion star: a fat star, orange, red or tan, out on the sand.</summary>
        public static readonly PlantSpecies CushionStar = new PlantSpecies("star.cushion", WorldAssetKind.GroundCover, new StarForm
        {
            Radius = new FloatRange(0.14f, 0.2f), Plump = 0.55f, Thick = 0.05f, Colours = new[] { V(0.5f, 0.18f, 0.04f), V(0.45f, 0.07f, 0.05f), V(0.42f, 0.3f, 0.12f) },
        })
        { Footprint = 0.45f, Scale = new FloatRange(0.85f, 1.2f), Variants = 4, Sways = false, CastsShadow = false };
    }
}
