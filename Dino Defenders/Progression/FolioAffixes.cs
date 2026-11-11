using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// A thing a book or a page can roll, as gear does: Magic has one prefix and one suffix, Rare two of each. On a book a prefix is what it holds and what using it asks of you, and a suffix what its
    /// pages do; a page is the same, for itself alone. What a book rolls counts on every page in it, and what a page rolls on that page wherever it goes (<see cref="Scribe.Bind"/>), so all of it is
    /// modifiers the page reads, and nothing anywhere else has to know. Ids are saved in items, so they stay. Read BOOKS.md.
    /// </summary>
    public sealed class FolioAffix
    {
        public string Id = "";
        public bool Prefix;
        public string[] Names = Array.Empty<string>();

        /// <summary>A book, a page, or both.</summary>
        public ItemSlot[] Slots = Array.Empty<ItemSlot>();

        /// <summary>The only kind of page it means anything on, if it's for a page and not every one: an Area on a Projectile Page is nothing.</summary>
        public PageKind? Kind;

        /// <summary>Whether it's about what a page does to someone, which means nothing on a Summon Page, as all that does is raise Minions beside you.</summary>
        public bool Blow;
        public Tier[] Tiers = Array.Empty<Tier>();

        /// <summary>What it is, for this much of it, to every page it counts on.</summary>
        public Func<float, Mod[]> Build = _ => Array.Empty<Mod>();

        /// <summary>Whether each point of it is one more place: for a Page on a book, and for a Sentence on a page. Every roll is a whole number, as a card reads better for it.</summary>
        public bool Places;

        /// <summary>How often it's rolled, against the others on its side.</summary>
        public float Weight = 1f;

        public bool Fits(Item item) => Slots.Contains(item.Slot) && (Kind == null || !item.IsPage || item.Page?.Kind == Kind) && !(Blow && item.IsPage && item.Page?.Kind == PageKind.Summon);
    }

    /// <summary>Everything a book or a page can roll, and the rules for it. An item says what it holds as lines of an id and a value, exactly as gear does.</summary>
    public static class FolioAffixes
    {
        private static readonly ItemSlot[] Book = { ItemSlot.Book };
        private static readonly ItemSlot[] Page = { ItemSlot.Page };
        private static readonly ItemSlot[] Both = { ItemSlot.Book, ItemSlot.Page };
        private static readonly ItemSlot[] Said = { ItemSlot.Sentence };

        /// <summary>How often a book or a page rolls what makes room in it, against each of the others on its side, as room is what one is worth.</summary>
        private const float RoomWeight = 3f;

        private static readonly Dictionary<string, FolioAffix> ById = new();
        private static readonly List<FolioAffix> Everything = new();

        public static IReadOnlyList<FolioAffix> All => Everything;

        public static FolioAffix? Find(string id) => ById.GetValueOrDefault(id);

        private static Tier[] Ladder(params (int level, float low, float high)[] steps) => steps.Select(s => new Tier(s.level, s.low, s.high)).ToArray();

        private static void Add(FolioAffix affix)
        {
            ById.Add(affix.Id, affix);
            Everything.Add(affix);
        }

        // What one more of something is, at each level it can first be had: one more at the start, then one more again at each of these.
        private static Tier[] Steps(params int[] levels) => levels.Select((level, i) => new Tier(level, i + 1, i + 1)).ToArray();

        private static readonly Tier[] Cheaper = Ladder((1, 6, 10), (10, 11, 16), (22, 17, 24), (36, 25, 32));
        private static readonly Tier[] Quicker = Ladder((1, 4, 8), (10, 9, 13), (22, 14, 19), (36, 20, 26));
        private static readonly Tier[] Rested = Ladder((5, 6, 12), (18, 13, 22), (32, 23, 32));
        private static readonly Tier[] Stronger = Ladder((1, 5, 9), (10, 10, 15), (22, 16, 22), (36, 23, 30));
        private static readonly Tier[] Harder = Ladder((1, 10, 18), (10, 19, 30), (22, 31, 45), (36, 46, 62));
        private static readonly Tier[] Wider = Ladder((1, 6, 10), (12, 11, 17), (26, 18, 26));
        private static readonly Tier[] Keener = Ladder((3, 10, 20), (14, 21, 35), (28, 36, 55));
        private static readonly Tier[] Likelier = Ladder((1, 5, 10), (12, 11, 18), (26, 19, 28));
        private static readonly Tier[] Graver = Ladder((1, 10, 18), (12, 19, 30), (26, 31, 45));

        static FolioAffixes()
        {
            // A book's prefixes: what it holds, and what using it asks of you.
            Add(new FolioAffix { Id = "book-pages", Prefix = true, Places = true, Weight = RoomWeight, Slots = Book, Names = new[] { "Bound", "Thick", "Weighty", "Great", "Boundless" }, Tiers = Steps(1, 12, 25, 40, 60) });
            Add(new FolioAffix { Id = "book-cost", Prefix = true, Slots = Book, Names = new[] { "Thrifty", "Frugal", "Sparing", "Miserly" }, Tiers = Cheaper, Build = v => new[] { Inc(Stat.BookCost, -v) } });
            Add(new FolioAffix { Id = "book-use-speed", Prefix = true, Slots = Book, Names = new[] { "Brisk", "Quick", "Hasty", "Racing" }, Tiers = Quicker, Build = v => new[] { Inc(Stat.UseSpeed, v) } });
            Add(new FolioAffix { Id = "book-cooldown", Prefix = true, Slots = Book, Names = new[] { "Restful", "Renewing", "Tireless" }, Tiers = Rested, Build = v => new[] { Inc(Stat.CooldownRecovery, v) } });

            // A page's: how many sentences it takes, and what it asks of you.
            Add(new FolioAffix { Id = "page-sentences", Prefix = true, Places = true, Weight = RoomWeight, Slots = Page, Names = new[] { "Wide", "Broad", "Ample", "Sprawling" }, Tiers = Steps(1, 15, 32, 50) });
            Add(new FolioAffix { Id = "page-cost", Prefix = true, Slots = Page, Names = new[] { "Cheap", "Spare", "Lean", "Bare" }, Tiers = Cheaper, Build = v => new[] { Inc(Stat.BookCost, -v) } });
            Add(new FolioAffix { Id = "page-use-speed", Prefix = true, Slots = Page, Names = new[] { "Hurried", "Rapid", "Fleeting", "Headlong" }, Tiers = Quicker, Build = v => new[] { Inc(Stat.UseSpeed, v) } });

            // Suffixes, on either: what its pages do.
            Add(new FolioAffix { Id = "folio-potency", Slots = Both, Blow = true, Names = new[] { "of Ink", "of Eloquence", "of Rhetoric", "of Scripture" }, Tiers = Stronger, Build = v => new[] { Inc(Stat.Potency, v) } });
            Add(new FolioAffix { Id = "folio-damage", Slots = Both, Blow = true, Names = new[] { "of Harm", "of Wounding", "of Ruin", "of Havoc" }, Tiers = Harder, Build = v => new[] { Inc(Stat.Damage, v) } });
            Add(new FolioAffix { Id = "folio-crit", Slots = Both, Blow = true, Names = new[] { "of the Margin", "of the Sharp Quill", "of the Last Word" }, Tiers = Keener, Build = v => new[] { Inc(Stat.CritChance, v) } });
            Add(new FolioAffix { Id = "book-area", Slots = Book, Names = new[] { "of Spreading", "of Spilling", "of the Flood" }, Tiers = Wider, Build = v => new[] { Inc(Stat.AreaOfEffect, v) } });
            Add(new FolioAffix { Id = "book-projectile-speed", Slots = Book, Names = new[] { "of Flight", "of the Swift Hand", "of the Hawk's Word" }, Tiers = Quicker, Build = v => new[] { Inc(Stat.ProjectileSpeed, v) } });
            Add(new FolioAffix { Id = "book-duration", Slots = Book, Names = new[] { "of Lingering", "of Long Breath", "of Ages" }, Tiers = Wider, Build = v => new[] { Inc(Stat.PageDuration, v) } });
            Add(new FolioAffix { Id = "page-area", Slots = Page, Kind = PageKind.Area, Names = new[] { "of Breadth", "of Sweep", "of the Expanse" }, Tiers = Wider, Build = v => new[] { Inc(Stat.AreaOfEffect, v) } });
            Add(new FolioAffix { Id = "page-minions", Slots = Page, Kind = PageKind.Summon, Names = new[] { "of the Pack", "of the Horde" }, Tiers = Steps(1, 30), Build = v => new[] { Flat(Stat.MinionCount, v) } });
            Add(new FolioAffix { Id = "page-minion-duration", Slots = Page, Kind = PageKind.Summon, Names = new[] { "of Lasting", "of the Long Watch", "of the Undying" }, Tiers = Ladder((1, 15, 25), (14, 26, 40), (30, 41, 60)), Build = v => new[] { Inc(Stat.MinionDuration, v) } });
            // A sentence's, which count on the page it's written on, as a page's own do: what it asks of you, and what the page does. Nothing that only one kind of page could use, as a sentence
            // can go on either.
            Add(new FolioAffix { Id = "sentence-cost", Prefix = true, Slots = Said, Names = new[] { "Brief", "Terse", "Curt", "Laconic" }, Tiers = Cheaper, Build = v => new[] { Inc(Stat.BookCost, -v) } });
            Add(new FolioAffix { Id = "sentence-use-speed", Prefix = true, Slots = Said, Names = new[] { "Quick", "Fluent", "Rattled", "Breathless" }, Tiers = Quicker, Build = v => new[] { Inc(Stat.UseSpeed, v) } });
            Add(new FolioAffix { Id = "sentence-cooldown", Prefix = true, Slots = Said, Names = new[] { "Patient", "Measured", "Unhurried" }, Tiers = Rested, Build = v => new[] { Inc(Stat.CooldownRecovery, v) } });
            Add(new FolioAffix { Id = "sentence-potency", Slots = Said, Blow = true, Names = new[] { "of Emphasis", "of Conviction", "of Command", "of Decree" }, Tiers = Stronger, Build = v => new[] { Inc(Stat.Potency, v) } });
            Add(new FolioAffix { Id = "sentence-damage", Slots = Said, Blow = true, Names = new[] { "of Threat", "of Menace", "of Wrath", "of Doom" }, Tiers = Harder, Build = v => new[] { Inc(Stat.Damage, v) } });
            Add(new FolioAffix { Id = "sentence-crit", Slots = Said, Blow = true, Names = new[] { "of the Point", "of Wit", "of the Barb" }, Tiers = Keener, Build = v => new[] { Inc(Stat.CritChance, v) } });
            // A sentence can make its page leave something behind: how likely each ailment is, how strong it is and how long it lasts, wherever each means anything. Chill comes with every
            // cold hit, so there's no chance of it to raise, and a Freeze or a Stun is only ever as long as it lasts.
            Add(new FolioAffix { Id = "sentence-burn-chance", Slots = Said, Blow = true, Names = new[] { "of Sparks", "of Ignition", "of Wildfire" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Burn, v) } });
            Add(new FolioAffix { Id = "sentence-burn-effect", Slots = Said, Blow = true, Names = new[] { "of Searing", "of Scorching", "of Cremation" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Burn) } });
            Add(new FolioAffix { Id = "sentence-burn-duration", Slots = Said, Blow = true, Names = new[] { "of Smouldering", "of Embers", "of Slow Fire" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Burn) } });
            Add(new FolioAffix { Id = "sentence-bleed-chance", Slots = Said, Blow = true, Names = new[] { "of Nicks", "of Gashes", "of Opened Veins" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Bleed, v) } });
            Add(new FolioAffix { Id = "sentence-bleed-effect", Slots = Said, Blow = true, Names = new[] { "of Laceration", "of Haemorrhage", "of Exsanguination" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Bleed) } });
            Add(new FolioAffix { Id = "sentence-bleed-duration", Slots = Said, Blow = true, Names = new[] { "of Seeping", "of Weeping Wounds", "of the Unstaunched" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Bleed) } });
            Add(new FolioAffix { Id = "sentence-venom-chance", Slots = Said, Blow = true, Names = new[] { "of Stings", "of Fangs", "of the Adder" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Venom, v) } });
            Add(new FolioAffix { Id = "sentence-venom-effect", Slots = Said, Blow = true, Names = new[] { "of Toxins", "of Virulence", "of Black Venom" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Venom) } });
            Add(new FolioAffix { Id = "sentence-venom-duration", Slots = Said, Blow = true, Names = new[] { "of Lingering Poison", "of Slow Venom", "of the Long Sickness" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Venom) } });
            Add(new FolioAffix { Id = "sentence-chill-effect", Slots = Said, Blow = true, Names = new[] { "of Frost", "of Rime", "of Deep Cold" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Chill) } });
            Add(new FolioAffix { Id = "sentence-chill-duration", Slots = Said, Blow = true, Names = new[] { "of Lasting Frost", "of the Long Winter", "of Permafrost" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Chill) } });
            Add(new FolioAffix { Id = "sentence-freeze-chance", Slots = Said, Blow = true, Names = new[] { "of Numbing", "of Freezing", "of the Glacier" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Freeze, v) } });
            Add(new FolioAffix { Id = "sentence-freeze-duration", Slots = Said, Blow = true, Names = new[] { "of Still Ice", "of Hard Frost", "of the Frozen Age" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Freeze) } });
            Add(new FolioAffix { Id = "sentence-shock-chance", Slots = Said, Blow = true, Names = new[] { "of Jolts", "of Shocks", "of Lightning Strikes" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Shock, v) } });
            Add(new FolioAffix { Id = "sentence-shock-effect", Slots = Said, Blow = true, Names = new[] { "of Voltage", "of Galvanism", "of the Thunderclap" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Shock) } });
            Add(new FolioAffix { Id = "sentence-shock-duration", Slots = Said, Blow = true, Names = new[] { "of Crackling", "of Static", "of the Storm's Memory" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Shock) } });
            Add(new FolioAffix { Id = "sentence-hex-chance", Slots = Said, Blow = true, Names = new[] { "of Ill Omen", "of Hexing", "of the Curse" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Hex, v) } });
            Add(new FolioAffix { Id = "sentence-hex-effect", Slots = Said, Blow = true, Names = new[] { "of Malediction", "of Doom-Saying", "of the Black Word" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentEffect, v, Tags.Hex) } });
            Add(new FolioAffix { Id = "sentence-hex-duration", Slots = Said, Blow = true, Names = new[] { "of Lingering Curses", "of Old Grudges", "of the Long Curse" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Hex) } });
            Add(new FolioAffix { Id = "sentence-stun-chance", Slots = Said, Blow = true, Names = new[] { "of Knocks", "of Blows", "of the Hammer" }, Tiers = Likelier, Build = v => new[] { Chance(Tags.Stun, v) } });
            Add(new FolioAffix { Id = "sentence-stun-duration", Slots = Said, Blow = true, Names = new[] { "of Dazing", "of Reeling", "of Senselessness" }, Tiers = Graver, Build = v => new[] { Inc(Stat.AilmentDuration, v, Tags.Stun) } });
            Add(new FolioAffix { Id = "page-projectile-speed", Slots = Page, Kind = PageKind.Projectile, Names = new[] { "of the Arrow", "of the Falcon", "of the Shooting Star" }, Tiers = Quicker, Build = v => new[] { Inc(Stat.ProjectileSpeed, v) } });
        }

        /// <summary>The places something holds before anything's rolled on it: one Page in a book, one Sentence on a page.</summary>
        public const int Implicit = 1;

        /// <summary>How many places a book or a page has, by what's rolled on it.</summary>
        public static int PlacesOf(Item item)
        {
            int places = Implicit;
            foreach (ItemLine line in item.Lines)
            {
                if (Find(line.Affix) is { Places: true } affix && affix.Fits(item)) places += (int)MathF.Round(line.Value);
            }
            return Math.Clamp(places, Implicit, item.IsBook ? Rulebook.MostPages : Rulebook.MostSentences);
        }

        /// <summary>How many places it holds in all: as many as <see cref="PlacesOf"/> says, and on a Summon Page a weapon's and a book's after those.</summary>
        public static int HeldOf(Item item) => PlacesOf(item) + (item.Summons ? Item.MinionPlaces : 0);

        /// <summary>What a book or a page gives every page it counts on, in the order it's written.</summary>
        public static Mod[] ModsOf(Item item)
        {
            if (item.Lines.Count == 0) return Array.Empty<Mod>();
            var mods = new List<Mod>(item.Lines.Count);
            foreach (ItemLine line in item.Lines)
            {
                if (Find(line.Affix) is FolioAffix affix && affix.Fits(item)) mods.AddRange(affix.Build(line.Value));
            }
            return mods.ToArray();
        }

        /// <summary>What the implicit line says: what it holds before anything's rolled on it.</summary>
        public static string Holds(Item item) => item.IsBook ? $"Holds {Say.Whole(Implicit)} Page" : $"Holds {Say.Whole(Implicit)} Sentence";

        /// <summary>What one line says, for a card.</summary>
        public static IEnumerable<string> Says(FolioAffix affix, float value)
        {
            if (affix.Places)
            {
                int more = (int)MathF.Round(value);
                string what = affix.Slots.Contains(ItemSlot.Book) ? more == 1 ? "Page" : "Pages" : more == 1 ? "Sentence" : "Sentences";
                yield return $"Holds {Say.Whole(more)} more {what}";
                yield break;
            }
            foreach (Mod mod in affix.Build(value)) yield return mod.Describe();
        }

        /// <summary>Rolls what a book or a page of its rarity has, prefixes first, so a card reads the same way round.</summary>
        public static void Deal(Item item, WorldRandom random, float quality = 0f)
        {
            int each = AffixCatalog.PerSide(item.Rarity);
            int level = Math.Max(item.Level, 1);
            var dealt = new List<ItemLine>(each * 2);
            foreach (bool prefix in new[] { true, false })
            {
                List<ItemLine> side = item.Lines.Where(line => Find(line.Affix)!.Prefix == prefix).ToList();
                while (side.Count < each && Pick(item, prefix, level, side, random) is FolioAffix affix)
                {
                    int top = TopTier(affix, level);
                    int tier = top;
                    while (tier > 0 && random.Chance(0.4f * (1f - quality))) tier--;
                    Tier rolled = affix.Tiers[tier];
                    float share = 1f - MathF.Pow(1f - random.Float(), 1f + quality * 2.5f);
                    float value = rolled.Low + (rolled.High - rolled.Low) * share;
                    side.Add(new ItemLine(affix.Id, MathF.Round(value)));
                }
                dealt.AddRange(side);
            }
            item.Lines = dealt;
        }

        private static FolioAffix? Pick(Item item, bool prefix, int level, List<ItemLine> taken, WorldRandom random)
        {
            var options = new List<FolioAffix>();
            var weights = new List<float>();
            foreach (FolioAffix affix in Everything)
            {
                if (affix.Prefix != prefix || !affix.Fits(item) || affix.Tiers[0].MinLevel > level || taken.Any(line => line.Affix == affix.Id)) continue;
                options.Add(affix);
                weights.Add(affix.Weight);
            }
            return options.Count == 0 ? null : options[random.PickWeighted(weights)];
        }

        private static int TopTier(FolioAffix affix, int level)
        {
            int top = 0;
            for (int i = 0; i < affix.Tiers.Length; i++)
            {
                if (affix.Tiers[i].MinLevel <= level) top = i;
            }
            return top;
        }

        private static int TierOf(FolioAffix affix, float value)
        {
            int tier = 0;
            for (int i = 0; i < affix.Tiers.Length; i++)
            {
                if (affix.Tiers[i].Low <= value) tier = i;
            }
            return tier;
        }

        /// <summary>What a book or a page is called: plain if there's nothing on it, and otherwise by the first of each side, as gear is.</summary>
        /// <param name="suffix">Whether a suffix goes on the end, which it doesn't on a sentence, whose name already says "of" something.</param>
        /// <param name="plain">Whether what has nothing on it is called plain, which a sentence isn't, as what it says is its name.</param>
        public static string Name(Item item, string what, bool suffix = true, bool plain = true)
        {
            if (item.Lines.Count == 0) return plain ? $"Plain {what}" : what;
            string before = "";
            string after = "";
            foreach (ItemLine line in item.Lines)
            {
                if (Find(line.Affix) is not FolioAffix affix) continue;
                string word = affix.Names[Math.Min(TierOf(affix, line.Value), affix.Names.Length - 1)];
                if (affix.Prefix && before.Length == 0) before = word + " ";
                if (!affix.Prefix && after.Length == 0 && suffix) after = " " + word;
            }
            return before + what + after;
        }

        /// <summary>
        /// Brings a sentence's lines up to the rules, as gear's are: a line stays if it can be on a sentence and its side has room, and what's missing is drawn from its own seed, so a Magic sentence from
        /// before sentences rolled lines comes out with a prefix and a suffix, the same ones each time. A Unique one has none, as it's what it says.
        /// </summary>
        private static bool FixLines(Item item)
        {
            item.Lines ??= new List<ItemLine>();
            int each = item.Rarity == Rarity.Unique ? 0 : AffixCatalog.PerSide(item.Rarity);
            var kept = new List<ItemLine>(each * 2);
            int prefixes = 0, suffixes = 0;
            foreach (ItemLine line in item.Lines)
            {
                if (Find(line.Affix) is not FolioAffix affix || !affix.Fits(item) || kept.Any(other => other.Affix == line.Affix)) continue;
                if ((affix.Prefix ? prefixes : suffixes) >= each) continue;
                if (affix.Prefix) prefixes++;
                else suffixes++;
                kept.Add(line);
            }
            bool changed = kept.Count != item.Lines.Count;
            item.Lines = kept;
            if (kept.Count < each * 2)
            {
                Deal(item, new WorldRandom(item.LookSeed()).Fork("folio-lines"));
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Brings a book or a page from a save up to the rules. A line stays if it can be on this and its side has room, and what's left to fill is drawn from the item's own seed, so it comes out the same each
        /// time. Anything from before books and pages rolled lines keeps the places it had, as a line that gives them, and is Magic at least if it had room for more than one. Then it has exactly as many places as
        /// its lines say, and what was in a place that's gone goes too, which only something no rules made can have.
        /// </summary>
        /// <returns>Whether its lines changed, and so its name should.</returns>
        public static bool Fix(Item item)
        {
            if (item.IsSentence) return FixLines(item);
            item.Held ??= new Item?[Implicit];

            // What kind of page it is decides what it can roll, so that's settled first. A page that only now became a Summon Page has no places yet for what its Minions fight with.
            bool wasSummoning = item.Summons;
            if (item.Page != null) Scribe.Fix(item.Page);
            int holding = item.Summons && wasSummoning ? Math.Max(item.Held.Length - Item.MinionPlaces, Implicit) : item.Held.Length;
            if (item.Rarity == Rarity.Unique) item.Rarity = Rarity.Rare;
            int each = AffixCatalog.PerSide(item.Rarity);

            var kept = new List<ItemLine>(each * 2);
            int prefixes = 0, suffixes = 0;
            item.Lines ??= new List<ItemLine>();
            foreach (ItemLine line in item.Lines)
            {
                if (Find(line.Affix) is not FolioAffix affix || !affix.Fits(item) || kept.Any(other => other.Affix == line.Affix)) continue;
                if ((affix.Prefix ? prefixes : suffixes) >= each) continue;
                if (affix.Prefix) prefixes++;
                else suffixes++;
                kept.Add(affix.Places ? new ItemLine(line.Affix, MathF.Max(MathF.Round(line.Value), 1f)) : line);
            }

            bool changed = kept.Count != item.Lines.Count || kept.Zip(item.Lines).Any(pair => pair.First.Affix != pair.Second.Affix || pair.First.Value != pair.Second.Value);
            if (kept.Count == 0 && (item.Rarity != Rarity.Normal || holding > Implicit))
            {
                // From before lines: what it held becomes a line that holds as much.
                int extra = Math.Clamp(holding, Implicit, item.IsBook ? Rulebook.MostPages : Rulebook.MostSentences) - Implicit;
                if (extra > 0 && item.Rarity == Rarity.Normal) item.Rarity = Rarity.Magic;
                each = AffixCatalog.PerSide(item.Rarity);
                FolioAffix? room = Everything.FirstOrDefault(affix => affix.Places && affix.Fits(item));
                if (extra > 0 && room != null) kept.Add(new ItemLine(room.Id, extra));
                changed = true;
            }

            item.Lines = kept;
            if (kept.Count < each * 2)
            {
                Deal(item, new WorldRandom(item.LookSeed()).Fork("folio-lines"));
                changed = true;
            }

            int places = HeldOf(item);
            if (item.Held.Length != places)
            {
                var held = new Item?[places];
                Array.Copy(item.Held, held, Math.Min(places, item.Held.Length));
                item.Held = held;

                // A Summon Page from when its Minions had one hand had its book where the other hand now is.
                if (item.Summons && held[item.MinionOffHandPlace] is { IsBook: true } book && held[item.MinionBookPlace] == null)
                {
                    held[item.MinionBookPlace] = book;
                    held[item.MinionOffHandPlace] = null;
                }
            }
            return changed;
        }
    }
}
