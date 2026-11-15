using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    public enum SiteKind
    {
        Landing,        // Where the player arrives, and where the way out is.
        Village,
        Hamlet,
        Farmstead,
        FishingHamlet,
        LumberCamp,
        Shrine,
        StandingStones,
        Watchtower,
        Ruins,
        BanditCamp,
        Graveyard,
        Keep,           // The stronghold at the far end of the world.
        Haunt,          // Anywhere a pack has settled. Not a place anyone would name, so these aren't on the map.
        Waygate,        // One of a land's old roads: a gate to travel between, and to leave the land by.
        Haven,          // The one village the Reich has never found, which is the whole of the Haven.
        Arena,          // Where a land's boss is fought, every tenth level: the whole of an arena's world.
        Overlook,       // Where the hero stands behind the main menu, the one place in its world, with nothing built.

        // Places with a story of their own, each chosen for a land from what it has ground for (SiteStage.PlaceStories): read PLACES.md.
        Shipwreck,      // One of the Reich's iron ships, or the land's own, broken on a beach, its cargo strewn down the sand.
        Quarry,         // Benches cut into a hillside where the Reich takes the stone for its keeps.
        SnowCastle,     // A castle of snow the north's children raise every winter, which the frost's cult keeps now.
        Tradepost,      // Where the land's merchants traded, which the turncoats hold for the Reich.
        Excavation,     // Where the Ossuary is lifting one of the great beasts out of the ground whole.
        Laboratory,     // An outstation of the Amber Institute, where beasts are grown from what's in the amber.
        Farm,           // A rearing farm, where the Institute's young beasts are raised in paddocks until they're big enough for harness.
        Nest,           // Where what got out of the pens breeds in the deep woods.
        Prison,         // Where the Reich breaks beasts born free, chained in cages round a breaking yard.
    }

    public enum RoadClass
    {
        Footpath,
        Track,
        Highway,
    }

    /// <summary>
    /// What a kind of site wants from the land, in the spirit of Emilien et al.'s building encyclopedia: hard limits that rule a
    /// place out, and preferences that score the rest.
    /// </summary>
    public sealed class SiteDefinition
    {
        public SiteKind Kind;
        public string Label = "";
        public Vector3 MapColour;

        public FloatRange Radius = new FloatRange(20f, 30f);    // Metres of ground the site takes up.
        public float PerSquareKilometre = 1f;
        public IntRange Limits = new IntRange(0, 8);
        public float Spacing = 200f;        // Metres to the next site of the same kind.
        public float Margin = 60f;          // Metres of clear ground between this site's edge and any other's.

        // --- Hard limits ---
        public float MaxSlope = 0.4f;       // Steepest ground allowed within the site.
        public float MinInland = 14f;       // Metres from water, at least.
        public float MaxInland = float.PositiveInfinity;
        public FloatRange Remoteness = new FloatRange(0f, 1f);  // 0 by the landing, 1 the far end of the world.

        // --- Preferences, each about -1..1 ---
        public float Flatness = 1f;
        public float HighGround = 0f;       // Above 0 it wants to look down on its surroundings.
        public float WaterNearby = 0f;      // Above 0 it wants water close by.
        public float Fertility = 0f;        // Above 0 it wants open, farmable land; below 0 it hides in the wild.

        // --- How it joins the world ---
        public RoadClass Road = RoadClass.Track;
        public float RoadChance = 1f;       // Below 1, some are left off the map of roads for the curious to find.
        public float Levelling = 0.8f;      // How flat its ground is made, 0..1.

        // --- What a place with a story needs of the land (PLACES.md), each a hard limit, and nothing for anything else ---
        /// <summary>Weight among the places with a story of their own a land chooses from (SiteStage.PlaceStories), or 0 for a kind that isn't one.</summary>
        public float StoryWeight;
        /// <summary>What the land at its middle has to be, if anything: the snow, for a castle of it.</summary>
        public Func<BiomeDefinition, bool>? Climate;
        /// <summary>How thick the woods round it have to be, 0..1, as a camp that fells them stands at their edge and a nest deep in them.</summary>
        public float LeastWoods;
        /// <summary>Above 0, it wants woods thick round it.</summary>
        public float WoodsNearby;
        /// <summary>Its woods are what it fells, so <see cref="LeastWoods"/> and <see cref="WoodsNearby"/> count only trees worth felling (<see cref="PlaceStory.IsTimber"/>), and never bamboo or palms.</summary>
        public bool Fells;
        /// <summary>Metres the ground has to rise to the north of it, within a stone's throw, as a quarry is cut into a hillside the camera looks at.</summary>
        public float LeastRise;
        /// <summary>Metres above the sea its middle may stand at most, as a wreck lies on a beach.</summary>
        public float MostAboveSea = float.PositiveInfinity;

        /// <summary>The same, looked for less fussily: further from the far end, steeper, nearer the water and at its smallest, for a land too small to hold it otherwise.</summary>
        public SiteDefinition Relaxed()
        {
            var relaxed = (SiteDefinition)MemberwiseClone();
            relaxed.Remoteness = new FloatRange(Remoteness.Min * 0.6f, Remoteness.Max);
            relaxed.MaxSlope = MaxSlope + 0.12f;
            relaxed.MinInland = MinInland * 0.6f;
            relaxed.Radius = new FloatRange(Radius.Min, Radius.Min);
            relaxed.Margin = Margin * 0.6f;
            relaxed.Spacing = Spacing * 0.5f;
            relaxed.LeastWoods = LeastWoods * 0.7f;
            relaxed.LeastRise = LeastRise * 0.75f;
            relaxed.MostAboveSea = MostAboveSea + 1f;
            return relaxed;
        }

        /// <summary>The same, looked for as <see cref="Relaxed"/> looks, at three quarters of its smallest: for a land too crowded to give it the room it would like (<see cref="Squeezes"/>).</summary>
        public SiteDefinition Squeezed()
        {
            SiteDefinition squeezed = Relaxed();
            squeezed.Radius = new FloatRange(Radius.Min * 0.75f, Radius.Min * 0.75f);
            return squeezed;
        }

        /// <summary>
        /// Whether a crowded land puts it down smaller rather than going without one (<see cref="Squeezed"/>): the shrines, rings of stones and watchtowers every land has, which were
        /// given more room for their forecourts, avenues and yards (PLACES.md) than they had before, and whose builders draw what they lay out in to whatever room they're given.
        /// </summary>
        public bool Squeezes => Kind is SiteKind.Shrine or SiteKind.StandingStones or SiteKind.Watchtower;

        public bool IsSettlement => Kind is SiteKind.Village or SiteKind.Hamlet or SiteKind.Farmstead or SiteKind.FishingHamlet or SiteKind.LumberCamp or SiteKind.Haven;
        public bool IsDanger => Kind is SiteKind.BanditCamp or SiteKind.Graveyard or SiteKind.Ruins or SiteKind.Keep or SiteKind.Prison or SiteKind.Laboratory or SiteKind.Excavation or SiteKind.Nest;

        /// <summary>Whether it's one of the places with a story of its own (PLACES.md), laid out by a builder of its own and drawn from ids of its own: those a land chooses, its ruins, and its shrines, rings of stones, watchtowers and pens.</summary>
        public bool IsStory => StoryWeight > 0f || Kind is SiteKind.Ruins or SiteKind.Shrine or SiteKind.StandingStones or SiteKind.Watchtower or SiteKind.BanditCamp;
    }

    /// <summary>Every kind of site, in the order they claim land: the ones that shape a world pick first.</summary>
    public static class SiteCatalog
    {
        public static readonly SiteDefinition Landing = new SiteDefinition
        {
            Kind = SiteKind.Landing,
            Label = "Landing",
            MapColour = new Vector3(0.45f, 0.85f, 1f),
            Radius = new FloatRange(20f, 24f),
            Limits = new IntRange(1, 1),
            MaxSlope = 0.32f,
            MinInland = 18f,
            Road = RoadClass.Highway,
            Levelling = 0.9f,
        };

        public static readonly SiteDefinition Keep = new SiteDefinition
        {
            Kind = SiteKind.Keep,
            Label = "Stronghold",
            MapColour = new Vector3(0.75f, 0.1f, 0.12f),
            Radius = new FloatRange(56f, 64f),     // Room inside its walls for an open yard its tyrant can be led round, and the camp along them.
            Limits = new IntRange(1, 1),
            MaxSlope = 0.42f,
            MinInland = 40f,
            Remoteness = new FloatRange(0.72f, 1f),
            HighGround = 1f,
            Road = RoadClass.Highway,
            Levelling = 0.92f,
        };

        public static readonly SiteDefinition Village = new SiteDefinition
        {
            Kind = SiteKind.Village,
            Label = "Village",
            MapColour = new Vector3(1f, 0.87f, 0.5f),
            Radius = new FloatRange(72f, 104f),
            PerSquareKilometre = 1.15f,
            Limits = new IntRange(1, 4),
            Spacing = 420f,
            Margin = 90f,
            MaxSlope = 0.3f,
            MinInland = 30f,
            Remoteness = new FloatRange(0.1f, 0.75f),
            Flatness = 1.3f,
            WaterNearby = 0.5f,
            Fertility = 1f,
            Road = RoadClass.Highway,
            Levelling = 0.55f,
        };

        // A fishing village, laid out along its shore with its jetties out over the water (PLACES.md). A land has one only if it's chosen from those with a story (SiteStage.PlaceStories).
        public static readonly SiteDefinition FishingHamlet = new SiteDefinition
        {
            Kind = SiteKind.FishingHamlet,
            Label = "Fishing hamlet",
            MapColour = new Vector3(0.6f, 0.85f, 0.85f),
            Radius = new FloatRange(38f, 46f),
            PerSquareKilometre = 0.8f,
            Limits = new IntRange(0, 1),
            Spacing = 340f,
            Margin = 44f,
            MaxSlope = 0.3f,
            MinInland = 12f,
            MaxInland = 38f,
            Remoteness = new FloatRange(0.05f, 0.9f),
            WaterNearby = 1f,
            Levelling = 0.45f,
            StoryWeight = 1f,
        };

        public static readonly SiteDefinition Hamlet = new SiteDefinition
        {
            Kind = SiteKind.Hamlet,
            Label = "Hamlet",
            MapColour = new Vector3(0.95f, 0.76f, 0.42f),
            Radius = new FloatRange(42f, 58f),
            PerSquareKilometre = 1.5f,
            Limits = new IntRange(1, 5),
            Spacing = 300f,
            MaxSlope = 0.32f,
            MinInland = 26f,
            Remoteness = new FloatRange(0.08f, 0.85f),
            WaterNearby = 0.3f,
            Fertility = 0.8f,
            Levelling = 0.6f,
        };

        public static readonly SiteDefinition Farmstead = new SiteDefinition
        {
            Kind = SiteKind.Farmstead,
            Label = "Farmstead",
            MapColour = new Vector3(0.88f, 0.68f, 0.32f),
            Radius = new FloatRange(30f, 40f),
            PerSquareKilometre = 2.2f,
            Limits = new IntRange(2, 8),
            Spacing = 190f,
            Margin = 40f,
            MaxSlope = 0.28f,
            MinInland = 24f,
            Remoteness = new FloatRange(0.03f, 0.9f),
            Fertility = 1.2f,
            Levelling = 0.75f,
        };

        // A logging camp at the edge of a wood it's felling (PLACES.md). A land has one only if it's chosen from those with a story (SiteStage.PlaceStories).
        public static readonly SiteDefinition LumberCamp = new SiteDefinition
        {
            Kind = SiteKind.LumberCamp,
            Label = "Lumber camp",
            MapColour = new Vector3(0.62f, 0.45f, 0.26f),
            Radius = new FloatRange(30f, 36f),
            PerSquareKilometre = 0.7f,
            Limits = new IntRange(0, 1),
            Spacing = 380f,
            Margin = 40f,
            MaxSlope = 0.32f,
            MinInland = 28f,
            Fertility = -0.4f,
            WoodsNearby = 1.4f,
            LeastWoods = 0.32f,
            Fells = true,
            Levelling = 0.7f,
            StoryWeight = 1f,
        };

        public static readonly SiteDefinition BanditCamp = new SiteDefinition
        {
            Kind = SiteKind.BanditCamp,
            Label = "Bandit camp",
            MapColour = new Vector3(0.9f, 0.3f, 0.2f),
            Radius = new FloatRange(28f, 38f),
            PerSquareKilometre = 2.1f,
            Limits = new IntRange(2, 8),
            Spacing = 240f,
            Margin = 70f,
            MaxSlope = 0.38f,
            MinInland = 20f,
            Remoteness = new FloatRange(0.2f, 1f),
            Fertility = -0.6f,
            HighGround = 0.25f,
            RoadChance = 0.55f,
            Levelling = 0.75f,
        };

        public static readonly SiteDefinition Graveyard = new SiteDefinition
        {
            Kind = SiteKind.Graveyard,
            Label = "Barrow",   // Older than the church that now stands there, and a label seeds its kind's fork: renaming one moves every site of it.
            MapColour = new Vector3(0.64f, 0.56f, 0.82f),
            Radius = new FloatRange(36f, 45f),  // A churchyard sprawls: its plots of graves round a church of forty metres and more (CHURCHYARD.md).
            PerSquareKilometre = 1.1f,
            Limits = new IntRange(1, 4),
            Spacing = 340f,
            Margin = 40f,       // Walled, so it can stand nearer its neighbours than an open site.
            MaxSlope = 0.36f,
            MinInland = 30f,    // Its wall comes in from the water where it has to (ChurchyardBuilder.PlanOf).
            Remoteness = new FloatRange(0.3f, 1f),
            HighGround = 0.5f,
            Road = RoadClass.Footpath,
            RoadChance = 0.6f,
            Levelling = 0.75f,
        };

        public static readonly SiteDefinition Ruins = new SiteDefinition
        {
            Kind = SiteKind.Ruins,
            Label = "Ruins",
            MapColour = new Vector3(0.7f, 0.68f, 0.6f),
            Radius = new FloatRange(26f, 36f),
            PerSquareKilometre = 1.4f,
            Limits = new IntRange(1, 6),
            Spacing = 260f,
            MaxSlope = 0.4f,
            MinInland = 18f,
            Remoteness = new FloatRange(0.15f, 1f),
            HighGround = 0.3f,
            Fertility = -0.4f,
            Road = RoadClass.Footpath,
            RoadChance = 0.4f,
            Levelling = 0.55f,
        };

        public static readonly SiteDefinition Watchtower = new SiteDefinition
        {
            Kind = SiteKind.Watchtower,
            Label = "Watchtower",
            MapColour = new Vector3(0.82f, 0.82f, 0.86f),
            Radius = new FloatRange(15f, 18f),     // A beacon tower on the north of a yard, the keepers' cottage, the beacon's wood and the sighting stone (PLACES.md).
            PerSquareKilometre = 1.2f,
            Limits = new IntRange(1, 5),
            Spacing = 340f,
            Margin = 40f,
            MaxSlope = 0.44f,
            MinInland = 16f,
            HighGround = 1.4f,
            Flatness = 0.3f,
            Road = RoadClass.Footpath,
            RoadChance = 0.7f,
            Levelling = 0.9f,
        };

        public static readonly SiteDefinition Shrine = new SiteDefinition
        {
            Kind = SiteKind.Shrine,
            Label = "Shrine",
            MapColour = new Vector3(0.55f, 0.95f, 0.7f),
            Radius = new FloatRange(16f, 20f),     // Its hall, forecourt, gateway and the lanterns along its path (PLACES.md).
            PerSquareKilometre = 2.3f,
            Limits = new IntRange(3, 9),
            Spacing = 230f,
            Margin = 30f,
            MaxSlope = 0.42f,
            MinInland = 14f,
            HighGround = 0.8f,
            WaterNearby = 0.3f,
            Flatness = 0.4f,
            Road = RoadClass.Footpath,
            RoadChance = 0.75f,
            Levelling = 0.9f,
        };

        public static readonly SiteDefinition StandingStones = new SiteDefinition
        {
            Kind = SiteKind.StandingStones,
            Label = "Standing stones",
            MapColour = new Vector3(0.5f, 0.75f, 0.95f),
            Radius = new FloatRange(20f, 25f),     // A ring of stones, its avenue and the barrow beyond it (PLACES.md).
            PerSquareKilometre = 0.9f,
            Limits = new IntRange(1, 4),
            Spacing = 380f,
            Margin = 30f,
            MaxSlope = 0.42f,
            MinInland = 20f,
            HighGround = 0.7f,
            Flatness = 0.3f,
            Road = RoadClass.Footpath,
            RoadChance = 0.25f,
            Levelling = 0.6f,
        };

        /// <summary>
        /// A gate of the land's old roads, standing on its own beside a road: somewhere to travel to once it's been found, and to leave the land by. They're spread out along the roads, as a gate is
        /// worth most where none is near, and the landing and every village have one of their own as well.
        /// </summary>
        public static readonly SiteDefinition Waygate = new SiteDefinition
        {
            Kind = SiteKind.Waygate,
            Label = "Waygate",
            MapColour = new Vector3(0.38f, 0.78f, 1f),
            Radius = new FloatRange(11f, 13f),
            PerSquareKilometre = 2f,
            Limits = new IntRange(3, 6),
            Spacing = 280f,     // Far enough apart to be worth travelling between, near enough that three fit on a small land.
            Margin = 30f,
            MaxSlope = 0.36f,
            MinInland = 16f,
            Remoteness = new FloatRange(0.2f, 1f),
            Flatness = 0.8f,
            Road = RoadClass.Track,
            Levelling = 0.85f,
        };

        // ---------------------------------------------------------------- Places with a story of their own (PLACES.md)
        //
        // None of these is in All: a land chooses a few of them from those it has the ground for (SiteStage.PlaceStories), so no two lands have the same. Each label seeds its kind's
        // fork, as any site's does, so renaming one moves every one of them.

        /// <summary>A ship broken on a beach: a strand of sand, low, by the sea, with room along the shore for its hull and what came out of it.</summary>
        public static readonly SiteDefinition Shipwreck = new SiteDefinition
        {
            Kind = SiteKind.Shipwreck,
            Label = "Wreck",
            MapColour = new Vector3(0.52f, 0.6f, 0.66f),
            Radius = new FloatRange(28f, 34f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 36f,
            MaxSlope = 0.3f,
            MinInland = 6f,
            MaxInland = 26f,
            MostAboveSea = 3.4f,
            Remoteness = new FloatRange(0.08f, 1f),
            Flatness = 0.6f,
            WaterNearby = 1.2f,
            Road = RoadClass.Footpath,
            RoadChance = 0.6f,
            Levelling = 0.12f,         // The beach is what it lies on: it keeps its fall to the sea.
            StoryWeight = 1.1f,
        };

        /// <summary>A quarry is cut into ground that rises to its north, so that the camera, which looks north, sees its faces.</summary>
        public static readonly SiteDefinition Quarry = new SiteDefinition
        {
            Kind = SiteKind.Quarry,
            Label = "Quarry",
            MapColour = new Vector3(0.78f, 0.74f, 0.66f),
            Radius = new FloatRange(30f, 35f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 40f,
            MaxSlope = 0.5f,
            MinInland = 22f,
            Remoteness = new FloatRange(0.15f, 1f),
            Flatness = 0.2f,
            LeastRise = 5f,
            Fertility = -0.3f,
            Road = RoadClass.Track,
            Levelling = 0.35f,         // Its floor is levelled where it's cut (QuarryBuilder), and the hill behind it has to stay a hill.
            StoryWeight = 0.85f,     // It wants little but a hillside, so it would turn up in most lands.
        };

        /// <summary>A castle of snow, in the snow and nowhere else.</summary>
        public static readonly SiteDefinition SnowCastle = new SiteDefinition
        {
            Kind = SiteKind.SnowCastle,
            Label = "Snow castle",
            MapColour = new Vector3(0.86f, 0.92f, 0.98f),
            Radius = new FloatRange(22f, 27f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 36f,
            MaxSlope = 0.24f,
            MinInland = 20f,
            Remoteness = new FloatRange(0.12f, 1f),
            Flatness = 1.2f,
            Climate = biome => biome.Ground == SurfaceMaterial.Snow,
            Road = RoadClass.Footpath,
            RoadChance = 0.7f,
            Levelling = 0.85f,
            StoryWeight = 1.6f,         // A land has snow far less often than it has a coast or a wood, so where it has, the castle is likelier.
        };

        /// <summary>A trading post on the high road, on open ground between the land's places.</summary>
        public static readonly SiteDefinition Tradepost = new SiteDefinition
        {
            Kind = SiteKind.Tradepost,
            Label = "Trading post",
            MapColour = new Vector3(0.92f, 0.74f, 0.48f),
            Radius = new FloatRange(34f, 39f),     // Its house and store with room left in its yard for the stalls, the scales and the wagons (PLACES.md).
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 44f,
            MaxSlope = 0.28f,
            MinInland = 24f,
            Remoteness = new FloatRange(0.15f, 0.8f),
            Flatness = 1.1f,
            Fertility = 0.3f,
            Road = RoadClass.Highway,
            Levelling = 0.8f,
            StoryWeight = 0.9f,
        };

        /// <summary>The Ossuary's excavation: dry, open ground away from the landing, where the bones it's lifting came up.</summary>
        public static readonly SiteDefinition Excavation = new SiteDefinition
        {
            Kind = SiteKind.Excavation,
            Label = "Excavation",
            MapColour = new Vector3(0.7f, 0.62f, 0.5f),
            Radius = new FloatRange(26f, 31f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 40f,
            MaxSlope = 0.3f,
            MinInland = 24f,
            Remoteness = new FloatRange(0.25f, 1f),
            Flatness = 0.9f,
            HighGround = 0.2f,
            Fertility = -0.3f,
            Road = RoadClass.Track,
            RoadChance = 0.85f,
            Levelling = 0.8f,
            StoryWeight = 0.8f,      // It wants little but flat ground, so it would turn up in most lands.
        };

        /// <summary>The Amber Institute's outstation, deep in the land, on flat ground near water.</summary>
        public static readonly SiteDefinition Laboratory = new SiteDefinition
        {
            Kind = SiteKind.Laboratory,
            Label = "Laboratory",
            MapColour = new Vector3(0.82f, 0.5f, 0.18f),
            Radius = new FloatRange(32f, 37f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 46f,
            MaxSlope = 0.26f,
            MinInland = 26f,
            Remoteness = new FloatRange(0.4f, 1f),
            Flatness = 1.2f,
            WaterNearby = 0.3f,
            Road = RoadClass.Highway,
            Levelling = 0.88f,
            StoryWeight = 0.9f,
        };

        /// <summary>A rearing farm, on good flat land with room for its paddocks.</summary>
        public static readonly SiteDefinition Farm = new SiteDefinition
        {
            Kind = SiteKind.Farm,
            Label = "Rearing farm",
            MapColour = new Vector3(0.74f, 0.66f, 0.36f),
            Radius = new FloatRange(34f, 40f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 44f,
            MaxSlope = 0.24f,
            MinInland = 26f,
            Remoteness = new FloatRange(0.15f, 0.85f),
            Flatness = 1.3f,
            Fertility = 1f,
            Road = RoadClass.Track,
            Levelling = 0.75f,
            StoryWeight = 0.9f,
        };

        /// <summary>A nesting ground deep in the woods, far from anyone.</summary>
        public static readonly SiteDefinition Nest = new SiteDefinition
        {
            Kind = SiteKind.Nest,
            Label = "Nest",
            MapColour = new Vector3(0.62f, 0.5f, 0.3f),
            Radius = new FloatRange(22f, 27f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 40f,
            MaxSlope = 0.34f,
            MinInland = 20f,
            Remoteness = new FloatRange(0.3f, 1f),
            Flatness = 0.5f,
            Fertility = -1f,
            WoodsNearby = 1.4f,
            LeastWoods = 0.38f,
            Road = RoadClass.Footpath,
            RoadChance = 0.25f,
            Levelling = 0.45f,
            StoryWeight = 1f,
        };

        /// <summary>The Reich's prison for beasts, on flat ground away from the landing.</summary>
        public static readonly SiteDefinition Prison = new SiteDefinition
        {
            Kind = SiteKind.Prison,
            Label = "Prison",
            MapColour = new Vector3(0.7f, 0.18f, 0.16f),
            Radius = new FloatRange(30f, 35f),
            Limits = new IntRange(0, 1),
            Spacing = 300f,
            Margin = 44f,
            MaxSlope = 0.26f,
            MinInland = 24f,
            Remoteness = new FloatRange(0.3f, 1f),
            Flatness = 1.1f,
            Road = RoadClass.Track,
            Levelling = 0.85f,
            StoryWeight = 1f,
        };

        /// <summary>Every kind a land can choose its places with a story from, in the order they're considered when two would want the same ground.</summary>
        public static readonly IReadOnlyList<SiteDefinition> Stories = new[]
        {
            Shipwreck, FishingHamlet, Quarry, LumberCamp, Nest, SnowCastle, Prison, Laboratory, Excavation, Tradepost, Farm,
        };

        /// <summary>The Haven, which is only ever placed in the Haven's own world, by itself. It isn't in <see cref="All"/>.</summary>
        public static readonly SiteDefinition Haven = new SiteDefinition
        {
            Kind = SiteKind.Haven,
            Label = "Haven",
            MapColour = new Vector3(1f, 0.9f, 0.6f),
            Radius = new FloatRange(92f, 92f),
            Limits = new IntRange(0, 0),
            MaxSlope = 0.3f,
            Road = RoadClass.Track,
            Levelling = 0.75f,
        };

        /// <summary>An arena, which is only ever placed in an arena's own world, by itself. It isn't in <see cref="All"/>.</summary>
        public static readonly SiteDefinition Arena = new SiteDefinition
        {
            Kind = SiteKind.Arena,
            Label = "Arena",
            MapColour = new Vector3(0.75f, 0.2f, 0.16f),
            Radius = new FloatRange(46f, 46f),
            Limits = new IntRange(0, 0),
            MaxSlope = 0.35f,
            Road = RoadClass.Track,
            Levelling = 0.8f,
        };

        /// <summary>Where the hero stands in the picture behind the main menu (<see cref="Vista"/>), which is only ever placed there, by itself. Nothing is built on it. It isn't in <see cref="All"/>.</summary>
        public static readonly SiteDefinition Overlook = new SiteDefinition
        {
            Kind = SiteKind.Overlook,
            Label = "Overlook",
            MapColour = new Vector3(0.6f, 0.8f, 0.5f),
            Radius = new FloatRange(10f, 10f),
            Limits = new IntRange(0, 0),
            Road = RoadClass.Footpath,
            Levelling = 0f,             // The meadow falls away from the hero's feet, which is the whole point of standing there.
        };

        /// <summary>What every <see cref="WorldData.Haunts"/> is. It isn't in <see cref="All"/>, as these don't claim land the way places do.</summary>
        public static readonly SiteDefinition Haunt = new SiteDefinition
        {
            Kind = SiteKind.Haunt,
            Label = "Haunt",
            MapColour = new Vector3(0.8f, 0.35f, 0.3f),
            Limits = new IntRange(0, 0),
        };

        public static readonly IReadOnlyList<SiteDefinition> All = new[]
        {
            Landing, Keep, Village, FishingHamlet, Hamlet, Farmstead, LumberCamp, BanditCamp, Graveyard, Ruins, Watchtower, Shrine, StandingStones, Waygate,
        };

        public static SiteDefinition Get(SiteKind kind)
        {
            foreach (SiteDefinition definition in All)
            {
                if (definition.Kind == kind) return definition;
            }
            foreach (SiteDefinition definition in Stories)
            {
                if (definition.Kind == kind) return definition;
            }
            if (kind == SiteKind.Haven) return Haven;
            if (kind == SiteKind.Arena) return Arena;
            if (kind == SiteKind.Overlook) return Overlook;
            if (kind == SiteKind.Haunt) return Haunt;
            throw new ArgumentException($"No site definition for {kind}.");
        }
    }
}
