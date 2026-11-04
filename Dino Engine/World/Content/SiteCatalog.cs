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

        public bool IsSettlement => Kind is SiteKind.Village or SiteKind.Hamlet or SiteKind.Farmstead or SiteKind.FishingHamlet or SiteKind.LumberCamp;
        public bool IsDanger => Kind is SiteKind.BanditCamp or SiteKind.Graveyard or SiteKind.Ruins or SiteKind.Keep;
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
            Radius = new FloatRange(46f, 56f),
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

        public static readonly SiteDefinition FishingHamlet = new SiteDefinition
        {
            Kind = SiteKind.FishingHamlet,
            Label = "Fishing hamlet",
            MapColour = new Vector3(0.6f, 0.85f, 0.85f),
            Radius = new FloatRange(38f, 50f),
            PerSquareKilometre = 0.8f,
            Limits = new IntRange(0, 3),
            Spacing = 340f,
            MaxSlope = 0.3f,
            MinInland = 14f,
            MaxInland = 48f,
            Remoteness = new FloatRange(0.05f, 0.9f),
            WaterNearby = 1f,
            Levelling = 0.6f,
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

        public static readonly SiteDefinition LumberCamp = new SiteDefinition
        {
            Kind = SiteKind.LumberCamp,
            Label = "Lumber camp",
            MapColour = new Vector3(0.62f, 0.45f, 0.26f),
            Radius = new FloatRange(28f, 36f),
            PerSquareKilometre = 0.7f,
            Limits = new IntRange(0, 3),
            Spacing = 380f,
            MaxSlope = 0.36f,
            MinInland = 30f,
            Fertility = -0.9f,
            Levelling = 0.7f,
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
            Radius = new FloatRange(24f, 32f),
            PerSquareKilometre = 1.1f,
            Limits = new IntRange(1, 4),
            Spacing = 340f,
            MaxSlope = 0.36f,
            MinInland = 22f,
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
            Radius = new FloatRange(11f, 14f),
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
            Radius = new FloatRange(12f, 16f),
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
            Radius = new FloatRange(14f, 20f),
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

        public static readonly IReadOnlyList<SiteDefinition> All = new[]
        {
            Landing, Keep, Village, FishingHamlet, Hamlet, Farmstead, LumberCamp, BanditCamp, Graveyard, Ruins, Watchtower, Shrine, StandingStones,
        };

        public static SiteDefinition Get(SiteKind kind)
        {
            foreach (SiteDefinition definition in All)
            {
                if (definition.Kind == kind) return definition;
            }
            throw new ArgumentException($"No site definition for {kind}.");
        }
    }
}
