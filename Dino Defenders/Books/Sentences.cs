using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;

namespace Dino_Defenders.Books
{
    /// <summary>A place on a page that takes a sentence. Each has its own list to choose from.</summary>
    public enum Blank : byte
    {
        From,
        Towards,
        Shape,
        Timing,
        Within,
        Clause,
        Deed,
        Element,
        Measure,
        Ailment,
        Gift,
        Kin,
        Pool,
        Whom,
    }

    /// <summary>Where a page begins.</summary>
    public enum Start : byte
    {
        You,
        Cursor,
        Nearest,
        Random,
        Impact,     // Where the last page struck. Nobody chooses this: it's what a following page is.
    }

    /// <summary>Which way a projectile goes.</summary>
    public enum Bearing : byte
    {
        Cursor,
        You,
        Nearest,
        Random,
        Down,
        Anywhere,
        Onward,
    }

    public enum AreaShape : byte
    {
        Cone,
        Circle,
        Line,
        Rectangle,
    }

    /// <summary>What can happen at an impact.</summary>
    public enum Deed : byte
    {
        Deal,
        Heal,
        Afflict,
        Give,
        Raise,
        Move,
        Water,
        Push,
        Pull,
        Pass,       // Nothing, here: it's only on its way to the next page.
    }

    public enum Whom : byte
    {
        You,
        Friends,
    }

    public enum Pool : byte
    {
        Health,
        Mana,
        Barrier,
    }

    /// <summary>What an Outcome is worked out from.</summary>
    public enum Measure : byte
    {
        Damage,
        Health,
        Mana,
        Barrier,
        Armour,
        Evasion,
        Strength,
        Dexterity,
        Intellect,
    }

    /// <summary>
    /// What a sentence does to the page it's on. <see cref="Added"/> is flat Cost, and all the others are percentages more or less, each a multiplier of its own, which is what those words mean everywhere else
    /// in the game. So however many are stacked, nothing reaches zero.
    /// </summary>
    public readonly record struct Weights(float Added = 0f, float Cost = 0f, float Potency = 0f, float Speed = 0f, float Area = 0f, float Range = 0f, float UseTime = 0f);

    /// <summary>
    /// One thing that can be written on a page. Which of these fields mean anything goes by its <see cref="Blank"/>. Everything it does to the page's numbers is in <see cref="Weights"/>, and something that's
    /// worse for you has to pay for itself there, as <c>check</c> holds. A sentence is only ever data, so a new one is a line in <see cref="Sentences"/>.
    /// </summary>
    public sealed class Sentence
    {
        public ushort Number;
        public string Id = "";
        public Blank Blank;
        public string Words = "";
        public Weights Weights;

        /// <summary>Only makes sense on one kind of page, if it isn't null.</summary>
        public PageKind? Fits;

        /// <summary>Sentences that can't stand together, as a page can't be two at once and three at once.</summary>
        public string? Group;

        /// <summary>Leaves something to chance, which a build can make something of.</summary>
        public bool Wild;

        /// <summary>Only means something on a page that follows another.</summary>
        public bool FollowsOnly;

        public Start Start;
        public Bearing Bearing;
        public AreaShape Shape;
        public float Length;            // Metres: how far a shape reaches, or its radius.
        public float Width;             // Radians for a cone, and otherwise metres either side of the middle.
        public float Duration;          // Seconds, for what lingers.
        public float Scatter;           // Metres.
        public int Projectiles;
        public int Pierce;
        public int Chains;
        public bool Stops;
        public bool Rides;
        public float Cooldown;
        public Deed Deed;
        public DamageType Element;
        public Measure Measure;
        public float Share;             // Of whatever it measures.
        public Ailment Ailment;
        public BuffDefinition? Buff;
        public string? Minion;
        public int MinionCount;
        public Pool Pool;
        public Whom Whom;
    }

    /// <summary>
    /// Everything that can be written. Ids are saved in books, and the order is what's sent, so a new sentence goes on the end of its own group and an old one keeps its id: its words and numbers can change at any
    /// time, and every book there is changes with them. For now every sentence is open to everyone. When they're something to be found, this is the list to narrow.
    /// </summary>
    public static class Sentences
    {
        public const string PlainFrom = "from-you";
        public const string PlainTowards = "towards-cursor";
        public const string PlainShape = "cone";
        public const string PlainTiming = "at-once";
        public const string PlainDeed = "deal";
        public const string PlainElement = "physical";
        public const string PlainMeasure = "of-damage";
        public const string PlainAilment = "burn";
        public const string PlainGift = "stone-skin";
        public const string PlainKin = "bones";
        public const string PlainPool = "health";
        public const string PlainWhom = "you";

        private static readonly List<Sentence> Everything = new();
        private static readonly Dictionary<string, Sentence> ById = new();

        public static IReadOnlyList<Sentence> All => Everything;

        public static Sentence? Find(string? id) => id != null ? ById.GetValueOrDefault(id) : null;

        public static Sentence? Get(int number) => number >= 1 && number <= Everything.Count ? Everything[number - 1] : null;

        /// <summary>What can go in a blank, in the order it's offered.</summary>
        public static IEnumerable<Sentence> For(Blank blank) => Everything.Where(sentence => sentence.Blank == blank);

        /// <summary>What a deed needs saying about it, in the order it reads.</summary>
        public static Blank[] BlanksOf(Deed deed) => deed switch
        {
            Deed.Deal => new[] { Blank.Element, Blank.Measure },
            Deed.Heal => new[] { Blank.Pool, Blank.Whom, Blank.Measure },
            Deed.Afflict => new[] { Blank.Ailment, Blank.Measure },
            Deed.Give => new[] { Blank.Gift, Blank.Whom },
            Deed.Raise => new[] { Blank.Kin },
            _ => Array.Empty<Blank>(),
        };

        private static Sentence Add(Sentence sentence)
        {
            Everything.Add(sentence);
            sentence.Number = (ushort)Everything.Count;
            ById.Add(sentence.Id, sentence);
            return sentence;
        }

        private static void From(string id, string words, Start start, Weights weights, bool wild = false) => Add(new Sentence { Id = id, Blank = Blank.From, Words = words, Start = start, Weights = weights, Wild = wild });

        private static void Towards(string id, string words, Bearing bearing, Weights weights, bool wild = false, bool follows = false) => Add(new Sentence { Id = id, Blank = Blank.Towards, Words = words, Bearing = bearing, Weights = weights, Fits = PageKind.Projectile, Wild = wild, FollowsOnly = follows });

        private static void Shape(string id, string words, AreaShape shape, float length, float width, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Shape, Words = words, Shape = shape, Length = length, Width = width, Weights = weights, Fits = PageKind.Area });

        private static void Timing(string id, string words, float duration, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Timing, Words = words, Duration = duration, Weights = weights, Fits = PageKind.Area });

        private static void Within(string id, float metres, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Within, Words = $"somewhere within {Say.Metres(metres)}", Scatter = metres, Weights = weights, Wild = true });

        private static void Clause(string id, string words, Weights weights, PageKind? fits = null, string? group = null, int projectiles = 0, int pierce = 0, int chains = 0, bool stops = false, bool rides = false, float cooldown = 0f)
        {
            Add(new Sentence { Id = id, Blank = Blank.Clause, Words = words, Weights = weights, Fits = fits, Group = group, Projectiles = projectiles, Pierce = pierce, Chains = chains, Stops = stops, Rides = rides, Cooldown = cooldown });
        }

        private static void Doing(string id, string words, Deed deed, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Deed, Words = words, Deed = deed, Weights = weights });

        private static void Type(string id, DamageType element, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Element, Words = element.ToString(), Element = element, Weights = weights });

        private static void Of(string id, string what, Measure measure, float share) => Add(new Sentence { Id = id, Blank = Blank.Measure, Words = $"{Say.Share(share)} of your {what}", Measure = measure, Share = share });

        private static void Ail(string id, Ailment ailment, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Ailment, Words = ailment.ToString(), Ailment = ailment, Weights = weights });

        private static void Gift(BuffDefinition buff, Weights weights) => Add(new Sentence { Id = buff.Id, Blank = Blank.Gift, Words = buff.Name, Buff = buff, Weights = weights });

        private static void Kin(string id, string minion, int count, string words, Weights weights) => Add(new Sentence { Id = id, Blank = Blank.Kin, Words = words, Minion = minion, MinionCount = count, Weights = weights });

        private static void Restores(string id, Pool pool) => Add(new Sentence { Id = id, Blank = Blank.Pool, Words = pool.ToString(), Pool = pool });

        private static void Who(string id, string words, Whom whom) => Add(new Sentence { Id = id, Blank = Blank.Whom, Words = words, Whom = whom });

        static Sentences()
        {
            // Finding someone by itself is worth something, and leaving it to chance is worth having back.
            From(PlainFrom, "from you", Start.You, default);
            From("from-cursor", "from the cursor", Start.Cursor, new Weights(Added: 2f));
            From("from-nearest", "from the nearest Enemy", Start.Nearest, new Weights(Added: 3f, Potency: -15f));
            From("from-random", "from a random Enemy nearby", Start.Random, new Weights(Added: 1f, Potency: 10f), wild: true);

            Towards(PlainTowards, "towards the cursor", Bearing.Cursor, default);
            Towards("towards-you", "towards you", Bearing.You, default);
            Towards("towards-nearest", "towards the nearest Enemy", Bearing.Nearest, new Weights(Added: 2f, Potency: -15f));
            Towards("towards-random", "towards a random Enemy nearby", Bearing.Random, new Weights(Cost: -15f), wild: true);
            Towards("downwards", "straight down out of the sky", Bearing.Down, new Weights(Added: 2f));
            Towards("anywhere", "in a random direction", Bearing.Anywhere, new Weights(Cost: -50f, Potency: 30f), wild: true);
            Towards("onward", "the way it was going", Bearing.Onward, default, follows: true);

            Shape(PlainShape, "in a Cone", AreaShape.Cone, Rulebook.ConeReach, Rulebook.ConeArc, default);
            Shape("circle", "in a Circle", AreaShape.Circle, Rulebook.CircleRadius, 0f, new Weights(Added: 2f));
            Shape("line", "in a Line", AreaShape.Line, Rulebook.LineLength, Rulebook.LineWidth, new Weights(Added: 2f));
            Shape("rectangle", "in a Rectangle", AreaShape.Rectangle, Rulebook.BoxLength, Rulebook.BoxWidth, new Weights(Added: 2f));

            Timing(PlainTiming, "at once", 0f, default);
            Timing("lingering", $"lingering for {Say.Seconds(Rulebook.LingerShort)}", Rulebook.LingerShort, new Weights(Added: 3f));
            Timing("lingering-long", $"lingering for {Say.Seconds(Rulebook.LingerLong)}", Rulebook.LingerLong, new Weights(Added: 5f));

            Within("within-near", Rulebook.ScatterNear, new Weights(Potency: 8f));
            Within("within-mid", Rulebook.ScatterMid, new Weights(Cost: -10f, Potency: 15f));
            Within("within-far", Rulebook.ScatterFar, new Weights(Cost: -20f, Potency: 25f));

            Clause("two-at-once", "two at once", new Weights(Added: 2f, Potency: -30f), PageKind.Projectile, "count", projectiles: 2);
            Clause("three-at-once", "three at once", new Weights(Added: 3f, Potency: -45f), PageKind.Projectile, "count", projectiles: 3);
            Clause("five-at-once", "five at once", new Weights(Added: 5f, Potency: -60f), PageKind.Projectile, "count", projectiles: 5);
            Clause("pierce-one", "passing through one Enemy", new Weights(Added: 1f, Potency: -10f), PageKind.Projectile, "pierce", pierce: 1);
            Clause("pierce-three", "passing through three Enemies", new Weights(Added: 2f, Potency: -25f), PageKind.Projectile, "pierce", pierce: 3);
            Clause("chain-one", "leaping to one more Enemy", new Weights(Added: 2f, Potency: -15f), PageKind.Projectile, "chain", chains: 1);
            Clause("chain-three", "leaping to three more Enemies", new Weights(Added: 3f, Potency: -30f), PageKind.Projectile, "chain", chains: 3);
            Clause("stops", "stopping at its target", default, PageKind.Projectile, stops: true);
            Clause("swift", "swiftly", new Weights(Potency: -10f, Speed: 60f), PageKind.Projectile, "speed");
            Clause("slow", "slowly", new Weights(Potency: 15f, Speed: -40f), PageKind.Projectile, "speed");
            Clause("far", "a long way", new Weights(Added: 1f, Range: 50f), PageKind.Projectile, "range");
            Clause("short", "a short way", new Weights(Cost: -25f, Potency: 10f, Range: -50f), PageKind.Projectile, "range");
            Clause("rides", "carrying you with it", new Weights(Added: 4f), PageKind.Projectile, rides: true);
            Clause("broad", "on a grand scale", new Weights(Added: 2f, Area: 60f), PageKind.Area, "size");
            Clause("tight", "drawn in tight", new Weights(Potency: 20f, Area: -40f), PageKind.Area, "size");
            Clause("hasty", "hastily", new Weights(Potency: -20f, UseTime: -30f), group: "pace");
            Clause("deliberate", "deliberately", new Weights(Potency: 40f, UseTime: 60f), group: "pace");
            Clause("rest", $"then resting for {Say.Seconds(Rulebook.RestShort)}", new Weights(Potency: 60f), group: "rest", cooldown: Rulebook.RestShort);
            Clause("rest-long", $"then resting for {Say.Seconds(Rulebook.RestLong)}", new Weights(Potency: 130f), group: "rest", cooldown: Rulebook.RestLong);

            Doing(PlainDeed, "deal", Deed.Deal, default);
            Doing("heal", "restore", Deed.Heal, new Weights(Added: 4f));
            Doing("afflict", "inflict", Deed.Afflict, new Weights(Added: 2f));
            Doing("give", "give", Deed.Give, new Weights(Added: 5f));
            Doing("raise", "raise", Deed.Raise, new Weights(Added: 8f));
            Doing("move", "move you there", Deed.Move, new Weights(Added: 4f));
            Doing("water", "make Water there", Deed.Water, new Weights(Added: 3f));
            Doing("push", "throw them back", Deed.Push, new Weights(Added: 1f));
            Doing("pull", "drag them in", Deed.Pull, new Weights(Added: 1f));
            Doing("pass", "do nothing but turn the Page", Deed.Pass, default);

            // Anything but what a weapon already is takes some doing.
            Type(PlainElement, DamageType.Physical, default);
            Type("fire", DamageType.Fire, new Weights(Added: 1f));
            Type("cold", DamageType.Cold, new Weights(Added: 1f));
            Type("lightning", DamageType.Lightning, new Weights(Added: 1f));
            Type("magic", DamageType.Magic, new Weights(Added: 1f));
            Type("poison", DamageType.Poison, new Weights(Added: 1f));

            // What each comes to at the same level is close, so that what's chosen is what a build has most of.
            Of(PlainMeasure, "Damage", Measure.Damage, 1f);
            Of("of-health", "Maximum Health", Measure.Health, 0.1f);
            Of("of-mana", "Maximum Mana", Measure.Mana, 0.1f);
            Of("of-barrier", "Maximum Barrier", Measure.Barrier, 0.2f);
            Of("of-armour", "Armour", Measure.Armour, 0.2f);
            Of("of-evasion", "Evasion", Measure.Evasion, 0.2f);
            Of("of-strength", "Strength", Measure.Strength, 0.5f);
            Of("of-dexterity", "Dexterity", Measure.Dexterity, 0.5f);
            Of("of-intellect", "Intellect", Measure.Intellect, 0.5f);

            Ail(PlainAilment, Ailment.Burn, default);
            Ail("bleed", Ailment.Bleed, default);
            Ail("venom", Ailment.Venom, default);
            Ail("chill", Ailment.Chill, default);
            Ail("freeze", Ailment.Freeze, new Weights(Added: 3f));
            Ail("shock", Ailment.Shock, default);
            Ail("hex", Ailment.Hex, default);
            Ail("stun", Ailment.Stun, new Weights(Added: 2f));

            Gift(BuffCatalog.StoneSkin, default);
            Gift(BuffCatalog.Quickening, default);
            Gift(BuffCatalog.Sanctuary, new Weights(Added: 2f));
            Gift(BuffCatalog.Bloodlust, default);
            Gift(BuffCatalog.Afterglow, new Weights(Added: 2f));

            Kin(PlainKin, "bones", 2, "two Risen Bones", default);
            Kin("hound", "hound", 1, "a Pack Hound", new Weights(Cost: -20f));

            Restores(PlainPool, Pool.Health);
            Restores("mana", Pool.Mana);
            Restores("barrier", Pool.Barrier);

            Who(PlainWhom, "you", Whom.You);
            Who("friends", "friends there", Whom.Friends);
        }
    }
}
