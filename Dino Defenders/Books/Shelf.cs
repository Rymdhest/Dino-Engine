using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Books
{
    /// <summary>
    /// Books that are already written: what creatures fight with, and what keystones and items set off. They're ordinary books, made of the same sentences as a hero's, read by the same <see cref="Scribe"/>, so anything
    /// an enemy does is something a player could have written. Their first page has to start from whoever's using them or from where they're aiming, as a mark on the ground is a promise, and <c>check</c> holds that.
    /// Numbers are places on the shelf, so a new book goes on the end.
    /// </summary>
    public static class Shelf
    {
        private static readonly List<BookPlan> Everything = new();

        public static IReadOnlyList<BookPlan> All => Everything;

        public static BookPlan Get(int number) => Everything[number - 1];

        public static BookPlan? Find(string id) => Everything.Find(book => book.Id == id);

        private static Outcome Deal(string element = Sentences.PlainElement, string measure = Sentences.PlainMeasure) => new Outcome { Element = element, Measure = measure };

        private static Outcome Inflict(string ailment) => new Outcome { Deed = "afflict", Ailment = ailment };

        private static Outcome Give(string gift) => new Outcome { Deed = "give", Gift = gift };

        private static Outcome Do(string deed) => new Outcome { Deed = deed };

        private static Outcome Raise(string kin) => new Outcome { Deed = "raise", Kin = kin };

        private static List<Sticker> Stuck(string[] stickers) => stickers.Select(name => Stickers.Written(name)!).ToList();

        private static Page Area(string shape, string[] stickers, params Outcome[] outcomes) => new Page { Kind = PageKind.Area, Shape = shape, Stickers = Stuck(stickers), Outcomes = outcomes.ToList() };

        private static Page Bolt(string[] stickers, params Outcome[] outcomes) => new Page { Kind = PageKind.Projectile, Stickers = Stuck(stickers), Outcomes = outcomes.ToList() };

        private static Page At(this Page page, string from)
        {
            page.From = from;
            return page;
        }

        private static Page Going(this Page page, string towards)
        {
            page.Towards = towards;
            return page;
        }

        private static Page Lasting(this Page page, string timing)
        {
            page.Timing = timing;
            return page;
        }

        private static readonly string[] Plain = Array.Empty<string>();

        private static string[] Say(params string[] stickers) => stickers;

        // What's set off by something else counts as that, and everything that isn't a hero's own can have numbers a sentence couldn't get to.
        private static BookPlan Write(string id, string name, Page[] pages, Action<PagePlan>? tune = null, bool triggered = false, BookMode mode = BookMode.Active, BookTrigger? when = null)
        {
            var book = new Book { Pages = pages.ToList(), Mode = mode, When = when };
            Scribe.Fix(book);
            BookPlan plan = Scribe.Read(book, name);
            plan.Id = id;
            plan.Source = book;
            foreach (PagePlan page in plan.Pages)
            {
                if (triggered) page.Tags |= Tags.Triggered;
                tune?.Invoke(page);
            }

            Everything.Add(plan);
            plan.Number = (ushort)Everything.Count;
            return plan;
        }

        // What creatures fight with.
        public static readonly BookPlan Strike = Write("strike", "Strike", new[] { Area("cone", Plain, Deal()) });
        public static readonly BookPlan Bite = Write("bite", "Bite", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 1f; page.UseTime = 0.5f; page.Mods = new[] { Chance(Tags.Bleed, 25f) }; });
        public static readonly BookPlan Maul = Write("maul", "Maul", new[] { Area("cone", Say("broad", "deliberate"), Deal()) }, page => { page.Potency = 1.6f; page.Mods = new[] { Chance(Tags.Stun, 50f) }; });
        public static readonly BookPlan Whirl = Write("whirl", "Whirl", new[] { Area("circle", Say("rest"), Deal()) }, page => page.Potency = 1.9f);
        public static readonly BookPlan Shot = Write("shot", "Loose", new[] { Bolt(Plain, Deal()) }, page => page.Speed = 56f);

        public static readonly BookPlan Firebolt = Write("firebolt", "Firebolt", new[] { Bolt(Plain, Deal("fire")) }, page => { page.Speed = 34f; page.Mods = new[] { Chance(Tags.Burn, 25f) }; });
        public static readonly BookPlan Immolate = Write("immolate", "Immolate", new[] { Area("circle", Plain, Deal("fire")) }, page => { page.Potency = 1.3f; page.Mods = new[] { Chance(Tags.Burn, 35f) }; });
        public static readonly BookPlan FrostShards = Write("frost-shards", "Frost Shards", new[] { Bolt(Say("three-at-once", "pierce-one"), Deal("cold")) }, page => page.Potency = 0.6f);
        public static readonly BookPlan FrostNova = Write("frost-nova", "Frost Nova", new[] { Area("circle", Say("rest"), Deal("cold")) }, page => { page.Potency = 1.2f; page.Length = 10f; page.Mods = new[] { Inc(Stat.AilmentEffect, 60f, Tags.Chill), Chance(Tags.Freeze, 15f) }; });
        public static readonly BookPlan Spark = Write("spark", "Forked Spark", new[] { Bolt(Say("swift", "chain-three"), Deal("lightning")) }, page => { page.Potency = 1f; page.Mods = new[] { Chance(Tags.Shock, 30f) }; });
        public static readonly BookPlan VenomSpit = Write("venom-spit", "Venom Spit", new[] { Bolt(Plain, Deal("poison")) }, page => { page.Speed = 32f; page.Mods = new[] { Chance(Tags.Venom, 70f) }; });
        public static readonly BookPlan Miasma = Write("miasma", "Miasma", new[] { Area("circle", Say("rest"), Deal("poison")).At("from-cursor").Lasting("lingering") }, page => { page.Potency = 1f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; });
        public static readonly BookPlan ArcaneDart = Write("arcane-dart", "Arcane Dart", new[] { Bolt(Plain, Deal("magic")) }, page => { page.Speed = 46f; page.Mods = new[] { Chance(Tags.Hex, 20f) }; });
        public static readonly BookPlan Unmaking = Write("unmaking", "Unmaking", new[] { Area("circle", Say("rest"), Deal("magic")).At("from-cursor") }, page => { page.Potency = 2.4f; page.Length = 5.5f; page.Mods = new[] { Chance(Tags.Hex, 60f) }; });

        // What other things set off.
        public static readonly BookPlan CinderTrail = Write("cinder-trail", "Burning Ground", new[] { Area("circle", Plain, Deal("fire")).Lasting("lingering") }, page => { page.Potency = 0.35f; page.Length = 3.2f; page.Duration = 3f; page.Mods = new[] { Chance(Tags.Burn, 40f) }; }, triggered: true);
        public static readonly BookPlan Puddle = Write("puddle", "Standing Water", new[] { Area("circle", Plain, Do("water")).Lasting("lingering-long") }, page => page.Length = 5f, triggered: true);
        public static readonly BookPlan VenomCloud = Write("venom-cloud", "Contagion", new[] { Area("circle", Plain, Deal("poison")) }, page => { page.Potency = 0.15f; page.Length = 8f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }, triggered: true);
        public static readonly BookPlan Retaliate = Write("retaliate", "Retaliation", new[] { Area("circle", Plain, Deal()) }, page => page.Potency = 1.2f, triggered: true);
        public static readonly BookPlan Emberfall = Write("emberfall", "Emberfall", new[] { Bolt(Plain, Do("pass")).At("from-cursor").Going("downwards"), Area("circle", Plain, Deal("fire")) }, page => page.Mods = new[] { Chance(Tags.Burn, 50f) }, triggered: true);

        // A few of the things that can be done, to copy out and to hold the sentences to: BuildLab and NetLab go through all of these, and --books puts them on a hero's bar.
        public static readonly BookPlan Fireball = Write("fireball", "Fireball", new[] { Bolt(Plain, Do("pass")), Area("circle", Say("tight"), Deal("fire")) });
        public static readonly BookPlan Meteor = Write("meteor", "Meteor", new[] { Bolt(Plain, Do("pass")).At("from-cursor").Going("downwards"), Area("circle", Plain, Deal("fire")) });
        public static readonly BookPlan Boomerang = Write("boomerang", "Boomerang", new[] { Bolt(Say("stops"), Deal()), Bolt(Plain, Deal()).Going("towards-you") });
        public static readonly BookPlan ChainLightning = Write("chain-lightning", "Chain Lightning", new[] { Bolt(Say("swift"), Deal("lightning")).Going("towards-nearest"), Bolt(Say("swift"), Deal("lightning")).Going("towards-nearest"), Bolt(Say("swift"), Deal("lightning")).Going("towards-nearest") });
        public static readonly BookPlan Volley = Write("volley", "Volley", new[] { Bolt(Say("five-at-once"), Deal()) });
        public static readonly BookPlan RainOfArrows = Write("rain", "Rain of Arrows", new[] { new Page { Kind = PageKind.Projectile, From = "from-cursor", FromWithin = "within-mid", Towards = "downwards", Stickers = Stuck(Say("five-at-once")), Outcomes = { Deal() } } });
        public static readonly BookPlan Charge = Write("charge", "Charge", new[] { Bolt(Say("rides", "short"), Do("pass")), Area("cone", Plain, Deal()) });
        public static readonly BookPlan Blink = Write("blink", "Blink", new[] { Area("circle", Say("tight"), Do("move")).At("from-cursor") });
        public static readonly BookPlan Lance = Write("lance", "Glacial Lance", new[] { Area("line", Plain, Deal("cold")) });
        public static readonly BookPlan Storm = Write("storm", "Stormcall", new[] { Area("circle", Plain, Deal("lightning")).At("from-cursor").Lasting("lingering") });
        public static readonly BookPlan Mend = Write("mend", "Mend", new[] { Area("circle", Plain, new Outcome { Deed = "heal", Measure = "of-health" }) });
        public static readonly BookPlan StoneSkin = Write("stone-skin", "Stone Skin", new[] { Area("circle", Say("tight", "rest"), Give("stone-skin")) });
        public static readonly BookPlan RaiseBones = Write("raise-bones", "Raise Bones", new[] { Area("circle", Say("tight"), Raise("bones")) });
        public static readonly BookPlan Cleave = Write("cleave", "Cleave", new[] { Area("cone", Say("broad"), Deal()) });
        public static readonly BookPlan Sunder = Write("sunder", "Sunder", new[] { Area("cone", Say("deliberate"), Deal()) }, page => page.Mods = new[] { Flat(Stat.ArmourIgnore, 40f), Chance(Tags.Stun, 60f) });
        public static readonly BookPlan Knives = Write("knives", "Throwing Knives", new[] { Bolt(Say("three-at-once"), Deal()) });
        public static readonly BookPlan Sap = Write("sap", "Sap", new[] { Area("line", Say("tight"), Deal("magic")) }, page => page.Mods = new[] { Flat(Stat.HealthLeech, 12f), Flat(Stat.ManaLeech, 6f) });
        public static readonly BookPlan Tidepool = Write("tidepool", "Tidepool", new[] { Area("circle", Plain, Do("water")).At("from-cursor").Lasting("lingering-long") });
        public static readonly BookPlan Split = Write("split", "Splitting Shot", new[] { Bolt(Plain, Deal()), Bolt(Say("three-at-once"), Deal()).Going("onward") });
        public static readonly BookPlan Inferno = Write("inferno", "Inferno", new[] { Area("circle", Plain, Deal("fire")) }, mode: BookMode.Toggle);
        public static readonly BookPlan Retort = Write("retort", "Retort", new[] { Area("circle", Plain, Deal("lightning")) }, mode: BookMode.Trigger, when: new BookTrigger { When = BookWhen.Moved, Amount = 20f });
    }
}
