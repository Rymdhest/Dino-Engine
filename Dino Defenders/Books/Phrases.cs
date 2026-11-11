using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;

namespace Dino_Defenders.Books
{
    /// <summary>A place on a page that takes a phrase. Each has its own list, which is what a page can be found with.</summary>
    public enum Blank : byte
    {
        Shape,
        Timing,
        Deed,
        Measure,
        Ailment,
        Gift,
        Kin,
        Pool,
        Whom,
    }

    /// <summary>Where a page begins. The first starts from you and every other where the last struck, unless a sentence says otherwise.</summary>
    public enum Start : byte
    {
        You,
        Cursor,
        Nearest,
        Random,
        Impact,     // Where the last page struck.
    }

    /// <summary>Which way a projectile goes. The first goes towards the cursor and every other on the way the last was going, unless a sentence says otherwise.</summary>
    public enum Bearing : byte
    {
        Cursor,
        You,
        Nearest,
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
    /// What a phrase does to the page it's on. <see cref="Added"/> is flat Cost, and all the others are percentages more or less, each a multiplier of its own, which is what those words mean everywhere else
    /// in the game. So however many are stacked, nothing reaches zero.
    /// </summary>
    public readonly record struct Weights(float Added = 0f, float Cost = 0f, float Potency = 0f, float Speed = 0f, float Area = 0f, float Range = 0f, float UseTime = 0f, float Duration = 0f);

    /// <summary>
    /// One thing that can be written on a page. Which of these fields mean anything goes by its <see cref="Blank"/>. Everything it does to the page's numbers is in <see cref="Weights"/>, and something that's
    /// worse for you has to pay for itself there, as <c>check</c> holds. A phrase is only ever data, so a new one is a line in <see cref="Phrases"/>.
    /// </summary>
    public sealed class Phrase
    {
        public ushort Number;
        public string Id = "";
        public Blank Blank;
        public string Words = "";
        public Weights Weights;

        /// <summary>Only makes sense on one kind of page, if it isn't null.</summary>
        public PageKind? Fits;

        /// <summary>How often a found page has it, against everything else in its blank, or never if nothing. What's only for written books has none.</summary>
        public float Found;

        public AreaShape Shape;
        public float Length;            // Metres: how far a shape reaches, or its radius.
        public float Width;             // Radians for a cone, and otherwise metres either side of the middle.
        public float Duration;          // Seconds, for what lingers.
        public Deed Deed;
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
    /// Everything that can be written. Ids are saved in books, and the order is what's sent, so a new phrase goes on the end of its own group and an old one keeps its id: its words and numbers can change at any
    /// time, and every book there is changes with them. Nobody writes anything: a page is found with what it says (<see cref="Scribe.Roll"/>), and only what has a <see cref="Phrase.Found"/> share turns up on one.
    /// The rest is for books that are already written, in <see cref="Shelf"/>.
    /// </summary>
    public static class Phrases
    {
        public const string PlainShape = "cone";
        public const string PlainTiming = "at-once";
        public const string PlainDeed = "deal";
        public const string PlainMeasure = "of-damage";
        public const string PlainAilment = "burn";
        public const string PlainGift = "stone-skin";
        public const string PlainKin = "bones";
        public const string PlainPool = "health";
        public const string PlainWhom = "you";

        private static readonly List<Phrase> Everything = new();
        private static readonly Dictionary<string, Phrase> ById = new();

        public static IReadOnlyList<Phrase> All => Everything;

        public static Phrase? Find(string? id) => id != null ? ById.GetValueOrDefault(id) : null;

        public static Phrase? Get(int number) => number >= 1 && number <= Everything.Count ? Everything[number - 1] : null;

        /// <summary>What can go in a blank, in the order it's offered.</summary>
        public static IEnumerable<Phrase> For(Blank blank) => Everything.Where(phrase => phrase.Blank == blank);

        /// <summary>What a deed needs saying about it, in the order it reads. Dealing needs nothing, as what a page deals is all of whichever Damage its <see cref="Hand"/> says.</summary>
        public static Blank[] BlanksOf(Deed deed) => deed switch
        {
            Deed.Heal => new[] { Blank.Pool, Blank.Whom, Blank.Measure },
            Deed.Afflict => new[] { Blank.Ailment, Blank.Measure },
            Deed.Give => new[] { Blank.Gift, Blank.Whom },
            Deed.Raise => new[] { Blank.Kin },
            _ => Array.Empty<Blank>(),
        };

        private static Phrase Add(Phrase phrase)
        {
            Everything.Add(phrase);
            phrase.Number = (ushort)Everything.Count;
            ById.Add(phrase.Id, phrase);
            return phrase;
        }

        private static void Shape(string id, string words, AreaShape shape, float length, float width, Weights weights, float found) => Add(new Phrase { Id = id, Blank = Blank.Shape, Words = words, Shape = shape, Length = length, Width = width, Weights = weights, Fits = PageKind.Area, Found = found });

        private static void Timing(string id, string words, float duration, Weights weights, float found) => Add(new Phrase { Id = id, Blank = Blank.Timing, Words = words, Duration = duration, Weights = weights, Fits = PageKind.Area, Found = found });

        private static void Doing(string id, string words, Deed deed, Weights weights, float found = 0f) => Add(new Phrase { Id = id, Blank = Blank.Deed, Words = words, Deed = deed, Weights = weights, Found = found });

        private static void Of(string id, string what, Measure measure, float share) => Add(new Phrase { Id = id, Blank = Blank.Measure, Words = $"{Say.Share(share)} of your {what}", Measure = measure, Share = share });

        private static void Ail(string id, Ailment ailment, Weights weights) => Add(new Phrase { Id = id, Blank = Blank.Ailment, Words = ailment.ToString(), Ailment = ailment, Weights = weights });

        private static void Gift(BuffDefinition buff, Weights weights) => Add(new Phrase { Id = buff.Id, Blank = Blank.Gift, Words = buff.Name, Buff = buff, Weights = weights });

        private static void Kin(string id, string minion, int count, string words, Weights weights) => Add(new Phrase { Id = id, Blank = Blank.Kin, Words = words, Minion = minion, MinionCount = count, Weights = weights });

        private static void Restores(string id, Pool pool) => Add(new Phrase { Id = id, Blank = Blank.Pool, Words = pool.ToString(), Pool = pool });

        private static void Who(string id, string words, Whom whom) => Add(new Phrase { Id = id, Blank = Blank.Whom, Words = words, Whom = whom });

        static Phrases()
        {
            // How often each turns up on a found page is against the rest of its own list.
            Shape(PlainShape, "in a Cone", AreaShape.Cone, Rulebook.ConeReach, Rulebook.ConeArc, default, 1f);
            Shape("circle", "in a Circle", AreaShape.Circle, Rulebook.CircleRadius, 0f, new Weights(Added: 2f), 1f);
            Shape("line", "in a Line", AreaShape.Line, Rulebook.LineLength, Rulebook.LineWidth, new Weights(Added: 2f), 1f);
            Shape("rectangle", "in a Rectangle", AreaShape.Rectangle, Rulebook.BoxLength, Rulebook.BoxWidth, new Weights(Added: 2f), 1f);

            Timing(PlainTiming, "at once", 0f, default, 6f);
            // How long it lingers is LingerTime, and only sentences make it longer or shorter: there was once a long one as well, which a save that has it reads as this.
            Timing("lingering", "lingering", Rulebook.LingerTime, new Weights(Added: 3f), 2f);

            // Dealing damage is what nearly every page does. Raising and moving are rarer finds, and the rest only written books do. Moving goes by a projectile that reached someone, as an area that starts from
            // you would leave you where you are.
            Doing(PlainDeed, "deal", Deed.Deal, default, 20f);
            Doing("heal", "restore", Deed.Heal, new Weights(Added: 4f));
            Doing("afflict", "inflict", Deed.Afflict, new Weights(Added: 2f));
            Doing("give", "give", Deed.Give, new Weights(Added: 5f));
            Doing("raise", "raise", Deed.Raise, new Weights(Added: 8f), 1f);
            Doing("move", "move you there", Deed.Move, new Weights(Added: 4f), 1f);
            Doing("water", "make Water there", Deed.Water, new Weights(Added: 3f));
            Doing("push", "throw them back", Deed.Push, new Weights(Added: 1f));
            Doing("pull", "drag them in", Deed.Pull, new Weights(Added: 1f));
            Doing("pass", "do nothing but turn the Page", Deed.Pass, default);

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
            // A page once raised a Pack Hound too. It's gone, as a page's Minion is a skeleton, and a save that says so reads as raising Risen Bones, as anything unknown does.
            Kin("turret", "turret", 1, "a Turret", default);

            Restores(PlainPool, Pool.Health);
            Restores("mana", Pool.Mana);
            Restores("barrier", Pool.Barrier);

            Who(PlainWhom, "you", Whom.You);
            Who("friends", "friends there", Whom.Friends);
        }
    }
}
