using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    /// <summary>How a culture builds. Picked per world, so all the buildings of a world belong together.</summary>
    public enum BuildingStyle
    {
        Timber,     // Half-timbered walls under steep thatch.
        Eastern,    // Dark wood and pale panels under wide, upswept shingle roofs.
        Stone,      // Rough stone under slate.
    }

    /// <summary>
    /// A building or prop. Its size is fixed here, and <see cref="Modelling.Procedural.Architecture"/> builds its model to these
    /// numbers, so worlds can be laid out without any geometry. Local +z is the front; a door opens that way.
    /// </summary>
    public sealed class StructureDefinition
    {
        public readonly string Id;
        public readonly Vector2 HalfExtents;    // Metres along local x and z. Y of zero means a circle of radius X.
        public readonly bool Solid;
        public readonly bool Styled;            // Comes in a version per building style.
        public readonly bool OnPad;             // The ground under it is levelled.
        public readonly float ImposterDistance;
        public readonly int Variants;
        public readonly bool SpansRoad;         // A gate: the road is meant to run through it.

        /// <summary>Metres up that it stops what flies, which goes at 3.2 m. What's thin, as a post is, stops nothing in the air however tall it is.</summary>
        public readonly float Height;

        /// <summary>
        /// Clutter that's in nobody's way: nothing walks round it, paths round it or stops on it, and a blast or a body going through breaks it, on each
        /// client and for show alone (<see cref="ECS.Systems.BreakageSystem"/>).
        /// </summary>
        public readonly bool Loose;

        public StructureDefinition(string id, Vector2 halfExtents, bool solid = true, bool styled = false, bool onPad = false, float imposterDistance = 0f, int variants = 1, bool spansRoad = false, float height = PlacementIndex.Tall, bool loose = false)
        {
            Id = id;
            HalfExtents = halfExtents;
            Solid = solid;
            Styled = styled;
            OnPad = onPad;
            ImposterDistance = imposterDistance;
            Variants = variants;
            SpansRoad = spansRoad;
            Height = height;
            Loose = loose;
        }

        public bool IsRound => HalfExtents.Y == 0f;
        public float Width => HalfExtents.X * 2f;
        public float Depth => (IsRound ? HalfExtents.X : HalfExtents.Y) * 2f;

        public Footprint FootprintAt(Vector2 position, float yaw, float grow = 0f)
        {
            return IsRound
                ? Footprint.Circle(position, HalfExtents.X + grow)
                : Footprint.Box(position, HalfExtents + new Vector2(grow), yaw);
        }
    }

    /// <summary>Every structure sites can be built from. A character stands about 4.6 m tall, so a door is 6 m high.</summary>
    public static class StructureCatalog
    {
        private static StructureDefinition Building(string id, float halfWidth, float halfDepth, int variants = 3)
        {
            return new StructureDefinition(id, new Vector2(halfWidth, halfDepth), solid: true, styled: true, onPad: true, variants: variants);
        }

        // A person stands 4.7 m tall and what's thrown goes at 3.2 m, so most of these are passed over, as anything waist-high on the way would be.
        private static StructureDefinition Prop(string id, float halfWidth, float halfDepth, float height, bool solid = true, bool loose = false)
        {
            return new StructureDefinition(id, new Vector2(halfWidth, halfDepth), solid: solid && !loose, height: height, loose: loose);
        }

        private static StructureDefinition Round(string id, float radius, float height, bool solid = true, bool loose = false)
        {
            return new StructureDefinition(id, new Vector2(radius, 0f), solid: solid && !loose, height: height, loose: loose);
        }

        private const float Thin = 0f;      // Posts and poles: walked round, and never in the way of what's thrown.

        public static readonly StructureDefinition Hut = Building("hut", 4.5f, 4f);
        public static readonly StructureDefinition Cottage = Building("cottage", 6.5f, 5f);
        public static readonly StructureDefinition House = Building("house", 8f, 6f);
        public static readonly StructureDefinition Longhouse = Building("longhouse", 12f, 6f);
        public static readonly StructureDefinition Barn = Building("barn", 9.5f, 7f, variants: 2);
        public static readonly StructureDefinition Inn = Building("inn", 10f, 7.5f, variants: 2);
        public static readonly StructureDefinition Hall = Building("hall", 13f, 8.5f, variants: 2);
        public static readonly StructureDefinition Temple = Building("temple", 8.5f, 8.5f, variants: 1);
        public static readonly StructureDefinition Shed = Building("shed", 3.5f, 3f, variants: 2);

        public static readonly StructureDefinition Well = Round("well", 2f, 1.3f);
        public static readonly StructureDefinition MarketStall = Prop("stall", 3.5f, 2.4f, 1.8f);
        public static readonly StructureDefinition Cart = Prop("cart", 3.4f, 1.8f, 2.6f);
        public static readonly StructureDefinition Haystack = Round("haystack", 2.3f, 4f);
        public static readonly StructureDefinition Woodpile = Prop("woodpile", 2.6f, 1.1f, 1.1f);
        public static readonly StructureDefinition Barrels = Round("barrels", 1.4f, 2f, loose: true);
        public static readonly StructureDefinition Crates = Prop("crates", 1.5f, 1.5f, 2.3f, loose: true);
        public static readonly StructureDefinition Lantern = Round("lantern", 0.5f, Thin);
        public static readonly StructureDefinition StoneLantern = Round("lantern.stone", 0.9f, Thin);
        public static readonly StructureDefinition Signpost = Round("signpost", 0.4f, Thin);
        public static readonly StructureDefinition Scarecrow = Round("scarecrow", 0.6f, Thin, solid: false);
        public static readonly StructureDefinition DryingRack = Prop("rack", 3f, 0.7f, Thin);
        public static readonly StructureDefinition Bench = Prop("bench", 1.8f, 0.6f, 1.2f);
        public static readonly StructureDefinition Tent = Prop("tent", 4.2f, 3.4f, 4.5f);
        public static readonly StructureDefinition LargeTent = Prop("tent.large", 6f, 4.5f, 6f);
        public static readonly StructureDefinition Campfire = Round("campfire", 1.6f, 0.9f);
        public static readonly StructureDefinition LogStack = Prop("logs", 4f, 1.8f, 2.6f);
        public static readonly StructureDefinition Boat = Prop("boat", 4.2f, 1.6f, 1.3f);
        public static readonly StructureDefinition Jetty = Prop("jetty", 2.2f, 9f, Thin, solid: false);

        public static readonly StructureDefinition WatchTower = new StructureDefinition("tower.watch", new Vector2(4.5f, 4.5f), solid: true, styled: true, onPad: true);
        public static readonly StructureDefinition GuardTower = new StructureDefinition("tower.guard", new Vector2(3.5f, 3.5f), solid: true, onPad: true);
        public static readonly StructureDefinition StoneTower = new StructureDefinition("tower.stone", new Vector2(6f, 0f), solid: true, onPad: true);
        public static readonly StructureDefinition Keep = new StructureDefinition("keep", new Vector2(13f, 11f), solid: true, onPad: true);
        public static readonly StructureDefinition GateArch = new StructureDefinition("gate.arch", new Vector2(4.2f, 0.9f), solid: false, spansRoad: true);
        public static readonly StructureDefinition Gatehouse = new StructureDefinition("gatehouse", new Vector2(6f, 3f), solid: false, spansRoad: true);

        public static readonly StructureDefinition Waygate = Prop("waygate", 4.4f, 1.6f, PlacementIndex.Tall, solid: false);

        /// <summary>
        /// The Haven's waygate, which reaches the lands the Reich holds: the same old road as any other, raised higher and kept better, on three steps between two wings (WaygateDesign.Great,
        /// whose steps this is the size of).
        /// </summary>
        public static readonly StructureDefinition GreatWaygate = Prop("waygate.great", 8.6f, 3.4f, PlacementIndex.Tall, solid: false);

        /// <summary>Where the next land is chosen, in the Haven: a stone table with every land the waygate knows cut into it.</summary>
        public static readonly StructureDefinition MapTable = Prop("table.map", 2.8f, 1.9f, 1.9f);

        /// <summary>Where what's put away is kept: an iron-bound chest under a roof of its own, the same in every village and in the Haven.</summary>
        public static readonly StructureDefinition Strongbox = Prop("strongbox", 2.2f, 1.7f, 1.5f);

        /// <summary>An arena's gate at the foot of its stands: a tall stone arch with a passage behind it into the dark of the pens. What rises out of it, a portcullis, is the game's.</summary>
        public static readonly StructureDefinition ArenaGate = Prop("arena.gate", 3.4f, 1.7f, 7.5f);

        /// <summary>A column round the rim of an arena, with a brazier on top.</summary>
        public static readonly StructureDefinition ArenaColumn = Round("arena.column", 0.8f, 6.4f);
        public static readonly StructureDefinition Altar = Round("altar", 1.8f, 1.6f);
        public static readonly StructureDefinition Grave = Prop("grave", 0.9f, 1.5f, 2f);
        public static readonly StructureDefinition Tomb = Prop("tomb", 3.6f, 5.4f, 5.2f);
        public static readonly StructureDefinition Candles = Round("candles", 0.45f, Thin, solid: false);
        public static readonly StructureDefinition VigilCandles = Round("candles.vigil", 1f, Thin, solid: false);
        public static readonly StructureDefinition CandleStand = Round("candle.stand", 0.4f, Thin);
        public static readonly StructureDefinition DeadTree = Round("pine.dead", 0.8f, PlacementIndex.Tall);

        /// <summary>A nave along local z with its door and tower at the front. The arms of its transept are footprints of their own, <see cref="ChurchArm"/>.</summary>
        public static readonly StructureDefinition Church = new StructureDefinition("church", new Vector2(6.5f, 12.5f), onPad: true);
        public static readonly StructureDefinition Monolith = Round("monolith", 1.5f, PlacementIndex.Tall);
        public static readonly StructureDefinition RuinWall = Prop("ruin.wall", 4.5f, 0.8f, 3.5f);
        public static readonly StructureDefinition RuinColumn = Round("ruin.column", 1.1f, 5f);
        public static readonly StructureDefinition Rubble = Round("ruin.rubble", 2.2f, 1f);
        public static readonly StructureDefinition Cairn = Round("cairn", 1.1f, 2.1f);

        public static readonly StructureDefinition GrainCrop = new StructureDefinition("crop.grain", new Vector2(1.2f, 0f), solid: false, imposterDistance: 45f);
        public static readonly StructureDefinition VegetableCrop = new StructureDefinition("crop.vegetable", new Vector2(1f, 0f), solid: false, imposterDistance: 45f);

        /// <summary>The transept of a church: half its span and width, and how far ahead of the church's centre it crosses the nave.</summary>
        public static readonly Vector2 ChurchTransept = new Vector2(9.5f, 3.4f);
        public const float ChurchTranseptAhead = -2.5f;

        /// <summary>One arm of a church's transept, either side of the nave and not overlapping it.</summary>
        public static Footprint ChurchArm(Vector2 church, Vector2 front, float side)
        {
            Vector2 across = new Vector2(-front.Y, front.X);
            float length = (ChurchTransept.X - Church.HalfExtents.X) * 0.5f;
            Vector2 centre = church + front * ChurchTranseptAhead + across * (side * (Church.HalfExtents.X + length + 0.05f));
            return Footprint.Box(centre, new Vector2(length, ChurchTransept.Y), MathF.Atan2(front.X, front.Y));
        }

        private static readonly Dictionary<string, StructureDefinition> ById = BuildIndex();

        private static Dictionary<string, StructureDefinition> BuildIndex()
        {
            var index = new Dictionary<string, StructureDefinition>();
            foreach (var field in typeof(StructureCatalog).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.GetValue(null) is StructureDefinition definition) index.Add(definition.Id, definition);
            }
            return index;
        }

        /// <summary>The structure an asset id with no style or variant stands for, if it's one. Trees and stones aren't.</summary>
        public static bool TryGetPlain(string assetId, out StructureDefinition definition) => ById.TryGetValue(assetId, out definition!);

        /// <summary>The structure an asset id stands for, and the style and variant in it, if it has them.</summary>
        public static StructureDefinition Parse(string assetId, out BuildingStyle style, out int variant)
        {
            style = BuildingStyle.Timber;
            variant = 0;
            if (ById.TryGetValue(assetId, out StructureDefinition? plain)) return plain;

            int variantDot = assetId.LastIndexOf('.');
            int styleDot = assetId.LastIndexOf('.', variantDot - 1);
            style = Enum.Parse<BuildingStyle>(assetId[(styleDot + 1)..variantDot], ignoreCase: true);
            variant = int.Parse(assetId[(variantDot + 1)..]);
            return ById[assetId[..styleDot]];
        }

        /// <summary>The asset id of a structure: its id, plus style and variant where it has them.</summary>
        public static string AssetId(StructureDefinition definition, BuildingStyle style, int variant)
        {
            if (!definition.Styled) return definition.Id;
            return $"{definition.Id}.{style.ToString().ToLowerInvariant()}.{variant % definition.Variants}";
        }
    }
}
