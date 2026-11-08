using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_BuildLab
{
    /// <summary>
    /// What a land comes to, by running the real rolls. A land holds nothing but what orders put on it, so the sums are of orders: what each rarity of them puts on a land at each level, how much worse
    /// their suffixes make it and how much their prefixes pay. The climb is the game, so they have to say that deeper orders roll stronger, that a reward never comes without a danger past what a
    /// kind of orders always brings, and that a level can always be tried again. Run `lands` for the table, and `check` for what must stay true of it. Read LANDS.md.
    /// </summary>
    internal static class LandLab
    {
        private static readonly int[] Levels = { 1, 2, 3, 4, 6, 8, 10, 14, 18, 24, 30, 38, 46, 60, 100 };

        /// <summary>What orders of one rarity put on a land, on average over many rolls at one level.</summary>
        internal readonly record struct Depth(float Omens, float Health, float Damage, float Quantity, float Rarity, float People, float Packs, float Larger);

        // Orders of a rarity at a level, given to a land with nothing else on it.
        private static LandTerms Given(int level, Rarity rarity, WorldRandom random)
        {
            var terms = new LandTerms();
            terms.Give(OrdersMaker.Roll(level, random, rarity));
            return terms;
        }

        /// <summary>How far up its tiers each line of orders of this level and rarity is, on average, as a share of the most it can be.</summary>
        private static float Reach(int level, Rarity rarity, int rolls, ulong seed)
        {
            var random = new WorldRandom(seed);
            double reach = 0;
            int lines = 0;
            for (int i = 0; i < rolls; i++)
            {
                foreach (ItemLine line in OrdersMaker.Roll(level, random.Fork(i), rarity).Lines)
                {
                    reach += line.Value / Omens.Find(line.Affix)!.Tiers[^1].High;
                    lines++;
                }
            }
            return (float)(reach / Math.Max(lines, 1));
        }

        internal static Depth Survey(int level, Rarity rarity, int rolls, ulong seed)
        {
            var random = new WorldRandom(seed);
            double omens = 0, health = 0, damage = 0, quantity = 0, found = 0, people = 0, packs = 0, larger = 0;
            for (int i = 0; i < rolls; i++)
            {
                LandTerms terms = Given(level, rarity, random.Fork(i));
                LootBoon boon = terms.Boon();
                omens += terms.Count;
                health += terms.EnemyHealth;
                damage += terms.EnemyDamage;
                quantity += boon.Quantity;
                found += boon.Rarity;
                people += terms.MorePeople;
                packs += terms.MorePacks;
                larger += terms.Larger;
            }
            return new Depth((float)(omens / rolls), (float)(health / rolls), (float)(damage / rolls), (float)(quantity / rolls), (float)(found / rolls), (float)(people / rolls), (float)(packs / rolls), (float)(larger / rolls));
        }

        /// <summary>What one world's loot is worth with this paid on it, on the scale <see cref="LootLab.WorthOf"/> uses, averaged over a few worlds of the usual size.</summary>
        internal static float WorldWorth(int level, in LootBoon boon, int worlds, ulong seed)
        {
            var random = new WorldRandom(seed);
            double worth = 0;
            for (int w = 0; w < worlds; w++)
            {
                var luck = new LootLuck();
                foreach ((LootTier tier, int count) in LootLab.World)
                {
                    for (int i = 0; i < count; i++)
                    {
                        foreach (Item item in Loot.Roll(tier, level, level, 0f, luck, random.Fork(w * 4096 + i + (int)tier * 1024), boon)) worth += LootLab.WorthOf(item);
                    }
                }
            }
            return (float)(worth / worlds);
        }

        public static int Run(Options options)
        {
            int rolls = options.Int("rolls", 2000);
            foreach (Rarity rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
            {
                Console.WriteLine($"\nWhat {rarity} orders put on a land of each level, over {rolls} rolls of each\n");
                Console.WriteLine("level  omens  health  damage  more items  more rarity  larger packs  more packs  larger land");
                foreach (int level in Levels)
                {
                    Depth depth = Survey(level, rarity, rolls, 3);
                    Console.WriteLine($"{level,5} {depth.Omens,6:0.0} {depth.Health,6:0.00}x {depth.Damage,6:0.00}x {depth.Quantity - 1f,11:0%} {depth.Rarity - 1f,12:0%} {depth.People,13:0%} {depth.Packs,11:0%} {depth.Larger,12:0%}");
                }
            }

            Console.WriteLine("\nWhat each omen does at the top of each tier, alone on a land of the level that opens it, against a land with nothing on it");
            Console.WriteLine("omen                 side    tier  level  value  health  damage  worth of a world");
            foreach (OmenDefinition omen in Omens.All)
            {
                for (int t = 0; t < omen.Tiers.Length; t++)
                {
                    Tier tier = omen.Tiers[t];
                    var alone = new LandTerms { Omens = { new ItemLine(omen.Id, tier.High) } };
                    int level = Math.Max(tier.MinLevel, 1);
                    float bare = WorldWorth(level, LootBoon.None, 2, 17);
                    float paid = WorldWorth(level, alone.Boon(), 2, 17);
                    Console.WriteLine($"{omen.Name,-20} {omen.Side,-7} {t + 1,4} {level,6} {tier.High,6:0} {alone.EnemyHealth,6:0.00}x {alone.EnemyDamage,6:0.00}x {paid / bare,10:0.00}x");
                }
            }

            Console.WriteLine("\nWhat each unique orders pays, alone");
            foreach (UniqueOrders unique in OrdersCatalog.All)
            {
                var terms = new LandTerms { Orders = { unique.Id } };
                Console.WriteLine($"  {unique.Name}: {terms.Pays()}, opening {unique.Climb} level{(unique.Climb == 1 ? "" : "s")}");
            }

            Console.WriteLine("\nWhat three other lands cost, by level");
            foreach (int level in Levels) Console.WriteLine($"{level,5} {Lands.RerollCost(level),9} gold");
            return 0;
        }

        /// <summary>What must stay true.</summary>
        public static void Checks(Report report)
        {
            report.Section("Omens");
            OmensHold(report);
            NumbersAreReal(report);

            report.Section("Lands");
            Rolls(report);
            Offers(report);
            Arenas(report);
            Climb(report);
            Caps(report);
            Terms(report);
            RollsStayTheSame(report);

            report.Section("Orders");
            OrdersHold(report);

            report.Section("The stash");
            StashHolds(report);

            report.Section("Lands on the wire and in saves");
            Wire(report);
            Saved(report);
        }

        private static void OmensHold(Report report)
        {
            report.Check("Omens have ids of their own", Omens.All.Select(o => o.Id).Distinct().Count() == Omens.All.Count);
            report.Check("And names of their own", Omens.All.Select(o => o.Name).Distinct().Count() == Omens.All.Count);
            report.Check("And words of their own in the names of orders", Omens.All.Select(o => o.Affix).Distinct().Count() == Omens.All.Count && Omens.All.All(o => o.Affix.Length > 0));
            report.Check("Every omen is numbered where it stands", Omens.All.Select((omen, index) => omen.Number == index).All(right => right));
            report.Check("There are rewards and dangers to roll", Omens.Prefixes.Count() >= 6 && Omens.Suffixes.Count() >= 12, $"{Omens.Prefixes.Count()} and {Omens.Suffixes.Count()}");

            var unsaid = new List<string>();
            foreach (OmenDefinition omen in Omens.All)
            {
                foreach (Tier tier in omen.Tiers)
                {
                    foreach (float value in new[] { tier.Low, tier.High, tier.High * Omens.MostStacked })
                    {
                        string text = omen.Describe(value);
                        if (text.Length <= 12 || text.Contains("  ") || text.Contains("NaN") || text.Contains("Infinity")) unsaid.Add($"{omen.Id} at {value}: '{text}'");
                    }
                }
            }
            report.Check("Every omen says what it does, at every tier and stacked past it", unsaid.Count == 0, string.Join("; ", unsaid.Take(4)));

            var badTiers = Omens.All.Where(omen => omen.Tiers.Length == 0 || omen.Tiers[0].MinLevel < 1 || omen.Tiers.Any(t => !(t.Low > 0f && t.Low <= t.High))
                || omen.Tiers.Zip(omen.Tiers.Skip(1)).Any(pair => pair.Second.MinLevel <= pair.First.MinLevel || pair.Second.Low <= pair.First.High)).Select(o => o.Id).ToList();
            report.Check("Each tier opens deeper than the last and holds more", badTiers.Count == 0, string.Join(", ", badTiers));

            // A prefix is what a land gives and nothing else, and a suffix what it asks and nothing else: Gilded pays through chests, Pedigreed through who's worth more, and Trophied through the tyrant.
            static bool Pays(OmenDefinition o) => o.Quantity > 0f || o.Rarity > 0f || o.Gold > 0f || o.Experience > 0f || o.People > 0f || o.Packs > 0f || o.Size > 0f;
            static bool Harms(OmenDefinition o, float v) => o.Foes(v).Length > 0 || o.Heroes(v).Length > 0;
            var paidBySight = new[] { Omens.Gilded, Omens.Bloodlines, Omens.Trophies };
            var idle = Omens.Prefixes.Where(o => !Pays(o) && !paidBySight.Contains(o)).Select(o => o.Id).ToList();
            report.Check("Every prefix gives something", idle.Count == 0, string.Join(", ", idle));
            var harmful = Omens.Prefixes.Where(o => o.Tiers.Any(t => Harms(o, t.High))).Select(o => o.Id).ToList();
            report.Check("And makes nothing harder", harmful.Count == 0, string.Join(", ", harmful));
            var paying = Omens.Suffixes.Where(Pays).Select(o => o.Id).ToList();
            report.Check("Every suffix pays nothing: it's the price, not the reward", paying.Count == 0, string.Join(", ", paying));
            var said = new[] { Omens.Warhost, Omens.Night, Omens.Wrath };
            var harmless = Omens.Suffixes.Where(o => !Harms(o, o.Tiers[0].Low) && !said.Contains(o)).Select(o => o.Id).ToList();
            report.Check("And makes enemies or heroes worse", harmless.Count == 0, string.Join(", ", harmless));
            report.Check("Each omen grows with its number", Omens.All.All(o => o.Says != null || o.Foes(o.Tiers[0].Low).Length + o.Heroes(o.Tiers[0].Low).Length == 0 || o.Describe(o.Tiers[0].Low) != o.Describe(o.Tiers[^1].High)));
            report.Check("Every tier of every omen can be reached by some orders", Omens.All.All(o => o.Tiers[^1].MinLevel <= HeroState.MaxLevel));

            // What a land does to a hero, both ends put on in the same order, so it has to be in what a hero is compiled from.
            HeroState hero = HeroState.CreateNew();
            Loadout bare = HeroBuild.Compile(hero);
            ActorSheet bareSheet = bare.SheetFor(Condition.None);
            var unworn = new List<string>();
            foreach (OmenDefinition omen in Omens.All.Where(o => o.Heroes(o.Tiers[^1].High).Length > 0))
            {
                var terms = new LandTerms { Omens = { new ItemLine(omen.Id, omen.Tiers[^1].High) } };
                Loadout worn = HeroBuild.Compile(hero, null, terms.HeroMods());
                ActorSheet sheet = worn.SheetFor(Condition.None);
                foreach (Mod mod in omen.Heroes(omen.Tiers[^1].High))
                {
                    bool there = worn.Mods.Contains(mod) || worn.RuleMods.Contains(mod);
                    bool counts = mod.Kind == ModKind.Rule || sheet.Total(mod.Stat, 100f, mod.Scope) != bareSheet.Total(mod.Stat, 100f, mod.Scope);
                    if (!there || !counts) unworn.Add(omen.Id);
                }
            }
            report.Check("Everything a land does to a hero, a hero really wears", unworn.Count == 0, string.Join(", ", unworn));
        }

        // What the interface promises against what a sheet comes to.
        private static void NumbersAreReal(Report report)
        {
            var terms = new LandTerms { Omens = { new ItemLine(Omens.Ironhide.Id, 40f), new ItemLine(Omens.Bloodthirst.Id, 30f), new ItemLine(Omens.Wrath.Id, 50f) }, Orders = { OrdersCatalog.Wager.Id } };
            var usual = new Bestiary();
            var worse = new Bestiary(terms.FoeMods());
            ActorSheet before = usual.Shared(Bestiary.Outlaw, 12, Rarity.Normal, Bestiary.NoTraits).SheetFor(Condition.None);
            ActorSheet after = worse.Shared(Bestiary.Outlaw, 12, Rarity.Normal, Bestiary.NoTraits).SheetFor(Condition.None);
            report.Near("Enemies have the Health a land says", after.MaxHealth / before.MaxHealth, terms.EnemyHealth, 0.002f);
            report.Near("And the Damage", after.Preview(Shelf.Strike).First.AverageHit / before.Preview(Shelf.Strike).First.AverageHit, terms.EnemyDamage, 0.002f);

            ActorSheet rare = worse.Shared(Bestiary.Outlaw, 12, Rarity.Rare, Bestiary.Trait(4)).SheetFor(Condition.None);
            report.Check("A rare has it on top of what it is", rare.MaxHealth > after.MaxHealth * 2f);

            // The tyrant wears what everyone does, and its wrath and the wager on top, which is what the server reads its worth through.
            Loadout boss = Bestiary.Boss(Bestiary.Warlord, 12, Bestiary.NoTraits, new WorldRandom(4), out _, out _);
            Loadout tyrant = Bestiary.Boss(Bestiary.Warlord, 12, Bestiary.NoTraits, new WorldRandom(4), out _, out _, worse.Worn.Concat(terms.TyrantMods()).ToList());
            float wrath = (1f + terms.ValueOf(Omens.Wrath) / 100f) * (1f + OrdersCatalog.WagerTyrant / 100f);
            report.Near("And the tyrant its wrath and the wager on top of that", tyrant.SheetFor(Condition.None).MaxHealth / boss.SheetFor(Condition.None).MaxHealth, terms.EnemyHealth * wrath, 0.002f);
            report.Check("A tyrant in its arena is stronger again", Rulebook.ArenaTyrantHealth >= 100f && Rulebook.ArenaTyrantDamage > 0f);

            var missing = new List<string>();
            foreach (OmenDefinition omen in Omens.All)
            {
                Mod[] mods = omen.Foes(omen.Tiers[^1].High);
                if (mods.Length == 0) continue;
                Loadout worn = new Bestiary(mods).Shared(Bestiary.Outlaw, 12, Rarity.Normal, Bestiary.NoTraits);
                ActorSheet sheet = worn.SheetFor(Condition.None);
                if (!mods.All(mod => mod.Kind == ModKind.Rule ? sheet.Has(mod.Rule) : worn.Mods.Contains(mod))) missing.Add(omen.Id);
            }
            report.Check("Everything an omen names, an enemy really wears", missing.Count == 0, string.Join(", ", missing));

            // Health is what a find's worth is read from, so what a land adds has to be taken out again, or its omens would make fodder drop like champions.
            float threatBare = before.MaxHealth / Bestiary.HealthAt(12);
            float threatHere = after.MaxHealth / (Bestiary.HealthAt(12) * terms.EnemyHealth);
            report.Near("What a kill is worth is read past what a land adds to its Health", threatHere, threatBare, 0.002f);
        }

        private static void Rolls(Report report)
        {
            var wrong = new List<string>();
            bool firstLandsAreSmall = true;
            for (int level = 1; level <= HeroState.MaxLevel; level++)
            {
                var random = new WorldRandom((ulong)(level * 7919));
                for (int i = 0; i < 100; i++)
                {
                    LandTerms terms = Lands.Roll(level, random.Fork(i));
                    if (terms.Any) wrong.Add($"something on a land at level {level}");
                    if (terms.Places < LandTerms.LeastPlaces || terms.Places > LandTerms.MostPlaces) wrong.Add($"{terms.Places} places at level {level}");
                    if (level <= 2 && terms.Places != LandTerms.LeastPlaces) firstLandsAreSmall = false;
                }
            }
            report.Check("A land holds nothing but what orders put on it", wrong.Count == 0, string.Join("; ", wrong.Distinct().Take(5)));
            report.Check("The first lands ask for the fewest places", firstLandsAreSmall);

            // Deeper orders roll further up their tiers, and pay more for it.
            float[] reach = new[] { 1, 5, 24, 60 }.Select(level => Reach(level, Rarity.Rare, 600, 9)).ToArray();
            report.Check("Deeper orders roll further up their tiers", reach[0] < reach[1] && reach[1] < reach[2] && reach[2] < reach[3], string.Join(", ", reach.Select(r => r.ToString("0.00"))));
            Depth first = Survey(1, Rarity.Rare, 600, 9);
            Depth deep = Survey(24, Rarity.Rare, 600, 9);
            Depth deepest = Survey(60, Rarity.Rare, 600, 9);
            report.Check("And pay more", first.Quantity + first.Rarity < deep.Quantity + deep.Rarity && deep.Quantity + deep.Rarity <= deepest.Quantity + deepest.Rarity, $"{first.Quantity:0.00}, {deep.Quantity:0.00}, {deepest.Quantity:0.00}");
            Depth magic = Survey(24, Rarity.Magic, 600, 9);
            Depth normal = Survey(24, Rarity.Normal, 600, 9);
            report.Check("Rarer orders put more on a land", normal.Omens < magic.Omens && magic.Omens < deep.Omens, $"{normal.Omens:0.0}, {magic.Omens:0.0}, {deep.Omens:0.0}");
            report.Check("Normal orders make nothing harder", normal.Health == 1f && normal.Damage == 1f);
            report.Note($"Rare orders at level 1: {first.Omens:0.0} omens, {first.Health:0.00}x Health, {first.Damage:0.00}x Damage, {first.Quantity - 1f:0%} more Items, {first.Rarity - 1f:0%} more Rarity");
            report.Note($"Rare orders at level 24: {deep.Omens:0.0} omens, {deep.Health:0.00}x Health, {deep.Damage:0.00}x Damage, {deep.Quantity - 1f:0%} more Items, {deep.Rarity - 1f:0%} more Rarity");
            report.Note($"Rare orders at level 60: {deepest.Omens:0.0} omens, {deepest.Health:0.00}x Health, {deepest.Damage:0.00}x Damage, {deepest.Quantity - 1f:0%} more Items, {deepest.Rarity - 1f:0%} more Rarity");

            // A pile can't be made of nothing but uniques however much is given for a land, or the rest of the pile would stop meaning anything.
            LandTerms worst = new LandTerms();
            foreach (OmenDefinition omen in Omens.All) worst.Omens.Add(new ItemLine(omen.Id, omen.Most));
            worst.Orders.AddRange(OrdersCatalog.All.Select(o => o.Id));
            worst.Sanitize();
            LootBoon richest = worst.Boon();
            var draw = new WorldRandom(9);
            int finds = 0, uniques = 0;
            for (int i = 0; i < 2000; i++)
            {
                foreach (Item item in Loot.Roll(LootTier.Boss, 40, 40, 0f, new LootLuck(), draw.Fork(i), richest))
                {
                    finds++;
                    if (item.Rarity == Rarity.Unique) uniques++;
                }
            }
            report.Check("Even the richest land's tyrant drops more than uniques", (float)uniques / finds < 0.6f, $"{uniques} of {finds}");
            report.Check("And not an impossible heap of them", finds / 2000f < 60f, $"{finds / 2000f:0.0} a tyrant");
        }

        private static void Offers(Report report)
        {
            HeroState hero = HeroState.CreateNew();
            hero.BestLand = 12;
            IReadOnlyList<LandOffer> offers = Lands.Offer(hero, 13);
            report.Check("The map table shows three lands", offers.Count == Lands.Choices);
            report.Check("Each a different kind of world", offers.Select(o => o.Request.ArchetypeId).Distinct().Count() == offers.Count);
            report.Check("Never the Haven, nor an arena, between tenth levels", offers.All(o => !Lands.IsHaven(o.Request) && !o.Arena && o.Request.ArchetypeId != null));
            report.Check("All at the level asked for", offers.All(o => o.Request.Level == 13 && o.Plan.Level == 13));
            report.Check("Each says who holds it as its tyrant will be", offers.All(o => o.Herald == Chronicle.HeraldOf(o.Request.Seed)));
            report.Check("And none comes with an omen of its own", offers.All(o => !o.Terms.Any));

            IReadOnlyList<LandOffer> again = Lands.Offer(hero, 13);
            report.Check("They're the same however often they're looked at, and however many tries they take", offers.Zip(again).All(pair => SameRequest(pair.First.Request, pair.Second.Request) && Same(pair.First.Terms, pair.Second.Terms)));
            IReadOnlyList<LandOffer> shallower = Lands.Offer(hero, 7);
            report.Check("Each level has lands of its own", shallower.All(o => o.Request.Level == 7) && !shallower.Select(o => o.Request.Seed).SequenceEqual(offers.Select(o => o.Request.Seed)));

            var other = HeroState.CreateNew();
            other.BestLand = 12;
            report.Check("Another hero is shown other lands", other.Offers != hero.Offers && !Lands.Offer(other, 13).Select(o => o.Request.Seed).SequenceEqual(offers.Select(o => o.Request.Seed)));

            hero.Reroll(13);
            IReadOnlyList<LandOffer> paid = Lands.Offer(hero, 13);
            report.Check("Gold shows three others", paid.All(o => offers.All(old => old.Request.Seed != o.Request.Seed)));
            report.Check("For that level alone", Lands.Offer(hero, 7).Select(o => o.Request.Seed).SequenceEqual(shallower.Select(o => o.Request.Seed)));

            hero.Freed(7, 1);
            report.Check("Freeing a level shows three others for it next time", Lands.Offer(hero, 7).All(o => shallower.All(old => old.Request.Seed != o.Request.Seed)));
            report.Check("And leaves every other level as it was", Lands.Offer(hero, 13).Select(o => o.Request.Seed).SequenceEqual(paid.Select(o => o.Request.Seed)) && hero.BestLand == 12);

            report.Check("Three other lands cost more the deeper they are", Lands.RerollCost(5) < Lands.RerollCost(60) && Lands.RerollCost(5) > 0 && Enumerable.Range(1, 999).All(l => Lands.RerollCost(l + 1) > Lands.RerollCost(l)),
                $"{Lands.RerollCost(5)} at level 5, {Lands.RerollCost(60)} at level 60");
            report.Check("And never more than a number holds", Lands.RerollCost(Protocol.MostLandLevel) > 0);

            // Over many heroes, every kind of world turns up, and no world twice.
            var kinds = new HashSet<string>();
            var seeds = new HashSet<int>();
            bool unique = true;
            ulong offersFrom = 1;
            for (int i = 0; i < 300; i++)
            {
                var someone = new HeroState { Offers = offersFrom = HeroState.NewOffers(offersFrom), BestLand = 40 };
                int level = 1 + i % 40;
                if (Lands.IsBossLevel(level)) continue;
                foreach (LandOffer offer in Lands.Offer(someone, level))
                {
                    kinds.Add(offer.Request.ArchetypeId!);
                    unique &= seeds.Add(offer.Request.Seed);
                }
            }
            report.Check("Every kind of world is offered", ArchetypeCatalog.All.All(kind => kinds.Contains(kind.Id)), string.Join(", ", ArchetypeCatalog.All.Select(k => k.Id).Except(kinds)));
            report.Check("And the same world is never offered twice", unique);
        }

        private static void Arenas(Report report)
        {
            report.Check("Every tenth level is a tyrant's arena, and nothing else is", Enumerable.Range(1, 200).All(l => Lands.IsBossLevel(l) == (l % Lands.BossEvery == 0)) && !Lands.IsBossLevel(0));
            var wrong = new List<string>();
            var kindsSeen = new HashSet<string>();
            for (int i = 0; i < 60; i++)
            {
                var hero = new HeroState { Offers = HeroState.NewOffers((ulong)(i + 1)), BestLand = 200 };
                int level = Lands.BossEvery * (1 + i % 12);
                IReadOnlyList<LandOffer> offers = Lands.Offer(hero, level);
                if (offers.Count != Lands.Choices) wrong.Add($"{offers.Count} arenas at {level}");
                if (!offers.All(o => o.Arena && Lands.IsArena(o.Request) && ArenaCatalog.Is(o.Request.ArchetypeId))) wrong.Add($"something that isn't an arena at {level}");
                if (offers.Select(o => o.Herald.BossKind).Distinct().Count() != offers.Count) wrong.Add($"the same tyrant twice at {level}");
                if (!offers.All(o => o.Herald.BossKind == Chronicle.BossKindOf(o.Request.Seed))) wrong.Add($"a tyrant that isn't the one its land grows at {level}");
                if (offers.Select(o => o.Request.Seed).Distinct().Count() != offers.Count) wrong.Add($"the same arena twice at {level}");
                foreach (LandOffer offer in offers) kindsSeen.Add(offer.Herald.BossKind);
            }
            report.Check("Each tenth level offers three arenas, each with a different tyrant", wrong.Count == 0, string.Join("; ", wrong.Take(4)));
            report.Check("Every kind of tyrant is fought in one", Chronicle.TyrantKinds.All(kindsSeen.Contains));
            report.Check("An arena is always the size it is, whatever's asked", WorldPlan.Create(new WorldRequest(5, ArenaCatalog.Colosseum.Id, 10, larger: 1f)).Size == WorldPlan.Create(new WorldRequest(5, ArenaCatalog.Colosseum.Id, 10)).Size);
            report.Check("Arenas are never drawn from a seed", !ArchetypeCatalog.All.Any(a => a.Arena) && ArenaCatalog.All.All(a => a.Arena && !a.Peaceful && ArchetypeCatalog.Get(a.Id) == a));
            report.Check("Their handlers let in the tyrant's own brood", Chronicle.TyrantKinds.All(kind => Warbands.ArenaRecipe(kind).Core.Length > 0));
            report.Check("Its gates open more than once in a fight", ArenaSystem.MostWaves >= 2 && Rulebook.ArenaWaveSize > 0 && Rulebook.ArenaWaveGates > 0 && Rulebook.ArenaWaveRareChance > 0f);
        }

        private static void Climb(Report report)
        {
            HeroState hero = HeroState.CreateNew();
            report.Check("A new hero starts at the first level", hero.LandLevel == 1 && hero.BestLand == 0 && !hero.HasGuidingWind && hero.CanOpen(1) && !hero.CanOpen(2) && !hero.CanOpen(0));
            int points = hero.TalentPoints;

            hero.Freed(1, 1);
            report.Check("Freeing a land opens the next level", hero.LandLevel == 2 && hero.BestLand == 1 && hero.WorldsCleared == 1 && hero.CanOpen(1) && hero.CanOpen(2));
            report.Check("And gives a talent point", hero.TalentPoints == points + 1);
            report.Check("And the guiding wind", hero.HasGuidingWind);

            hero.Freed(1, 1);
            report.Check("Freeing a land as deep again gives nothing more", hero.TalentPoints == points + 1 && hero.LandLevel == 2);
            report.Check("Nothing can lower a climb: there's no way to lose a level", typeof(HeroState).GetMethod("Lost") == null && typeof(HeroState).GetProperty(nameof(HeroState.LandLevel))!.CanWrite == false);

            var high = new HeroState { BestLand = 9 };
            high.Freed(4, 1);
            report.Check("Freeing a friend's lower land costs nobody their own climb", high.LandLevel == 10 && high.BestLand == 9);
            var low = new HeroState { BestLand = 1 };
            low.Freed(8, 1);
            report.Check("And a friend's deeper land takes them on from there", low.LandLevel == 9 && low.BestLand == 8);

            var wager = new HeroState { BestLand = 4 };
            var terms = new LandTerms { Orders = { OrdersCatalog.Wager.Id } };
            wager.Freed(5, terms.Climb);
            report.Check("The Kommandant's Wager opens two levels", wager.BestLand == 6 && wager.LandLevel == 7);
            wager.Freed(9, terms.Climb);
            report.Check("But never past an arena that hasn't been fought", wager.BestLand == 9 && wager.LandLevel == 10);
            wager.Freed(10, terms.Climb);
            report.Check("And from an arena it goes on past it", wager.BestLand == 11);
            report.Check("Every other land opens one", new LandTerms().Climb == 1 && OrdersCatalog.All.Where(o => o != OrdersCatalog.Wager).All(o => new LandTerms { Orders = { o.Id } }.Climb == 1));

            var slots = new HeroState();
            report.Check("The map table takes one orders at first", Lands.OrderSlots(slots) == 1);
            slots.Freed(9, 1);
            report.Check("Still one before the first arena", Lands.OrderSlots(slots) == 1 && slots.LandLevel == 10);
            slots.Freed(10, 1);
            report.Check("Two after it", Lands.OrderSlots(slots) == 2);
            slots.BestLand = 39;
            report.Check("And one more for each arena after that", Lands.OrderSlots(slots) == 4);
            slots.BestLand = 100000;
            report.Check("Though never more than a pack holds", Lands.OrderSlots(slots) == Lands.MostOrderSlots);
        }

        private static void Caps(Report report)
        {
            var hero = new HeroState { Level = HeroState.MaxLevel, BestLand = 400 };
            report.Check("A hero's level ends at a hundred", HeroState.MaxLevel == 100);
            report.Check("Lands freed past the hundredth count for no more talent points", hero.TalentPoints == 1 + HeroState.PointsPerLevel * HeroState.MaxLevel + Lands.MostCountedLand);
            report.Check("Nor does experience go anywhere at the last level", hero.GainExperience(int.MaxValue) == 0 && hero.Level == HeroState.MaxLevel && hero.Experience == 0);
            var climbing = new HeroState { Level = HeroState.MaxLevel - 1 };
            climbing.GainExperience(int.MaxValue);
            climbing.GainExperience(int.MaxValue);
            report.Check("And a great deal of it can't overflow", climbing.Level == HeroState.MaxLevel && climbing.Experience >= 0);
            report.Check("What a kill is worth stops growing where heroes stop", Bestiary.ExperienceAt(400, 20f) == Bestiary.ExperienceAt(HeroState.MaxLevel, 20f) && Bestiary.ExperienceAt(HeroState.MaxLevel, 20f) > 0);

            var random = new WorldRandom(71);
            var levels = new List<int>();
            for (int i = 0; i < 200; i++)
            {
                levels.Add(ItemGenerator.Roll(250, random.Fork(i)).Level);
                levels.Add(ItemGenerator.Folio(250, random.Fork(1000 + i), Rarity.Magic).Level);
                foreach (Item item in Loot.Roll(LootTier.Boss, 250, 250, 0f, new LootLuck(), random.Fork(2000 + i))) if (!item.IsOrders) levels.Add(item.Level);
            }
            report.Check("Nothing worn or put in a book is found past the hundredth level, however deep the land", levels.All(level => level <= ItemGenerator.MostItemLevel) && levels.Contains(ItemGenerator.MostItemLevel), $"{levels.Max()} at most");
            report.Check("Health and damage go on growing past it, so deeper lands stay harder", Bestiary.HealthAt(250) > Bestiary.HealthAt(100) && float.IsFinite(Bestiary.HealthAt(1000)) && float.IsFinite(Bestiary.DamageAt(1000)));
        }

        private static void Terms(Report report)
        {
            var crowd = new LandTerms { Omens = { new ItemLine(Omens.Teeming.Id, 50f), new ItemLine(Omens.Overrun.Id, 30f), new ItemLine(Omens.Expanse.Id, 40f) } };
            WorldRequest shaped = crowd.Shape(new WorldRequest(12, "isle", 7));
            report.Near("Teeming makes packs larger", shaped.MorePeople, 0.5f, 0.0001f);
            report.Near("And Swarming makes more of them", shaped.MorePacks, 0.3f, 0.0001f);
            report.Near("And Sprawling makes the land larger", shaped.Larger, 0.4f, 0.0001f);
            report.Check("And nothing else about the world changes", shaped.Seed == 12 && shaped.ArchetypeId == "isle" && shaped.Level == 7);
            report.Check("A crowd makes nobody stronger", crowd.FoeMods().Count == 0 && crowd.EnemyHealth == 1f && crowd.EnemyDamage == 1f && crowd.HeroMods().Count == 0);

            WorldPlan usual = WorldPlan.Create(new WorldRequest(12, "isle", 7));
            WorldPlan wide = WorldPlan.Create(shaped);
            report.Check("A larger land is larger, but never past the most its kind can be", wide.Size > usual.Size && wide.Size <= usual.Archetype.Size.Max + 8f, $"{usual.Size} and {wide.Size}");
            int smaller = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                WorldPlan plan = WorldPlan.Create(new WorldRequest(seed, null, 5));
                if (plan.Size < plan.Archetype.Size.Max * WorldPlan.UsualExtent.Max + 8f) smaller++;
            }
            report.Check("A land is usually smaller than the most its kind can be", smaller == 40, $"{smaller} of 40");

            var garrison = new LandTerms { Orders = { OrdersCatalog.Garrison.Id } };
            WorldRequest held = garrison.Shape(new WorldRequest(12, "isle", 7));
            report.Check("The Last Garrison leaves the roads and the wild empty", held.Emptied && held.MorePeople == OrdersCatalog.GarrisonPeople);

            var stacked = new LandTerms { Omens = { new ItemLine(Omens.Ironhide.Id, 70f), new ItemLine(Omens.Ironhide.Id, 70f) } };
            report.Near("The same omen twice is the two added up, as far as it can go", stacked.ValueOf(Omens.Ironhide), Omens.Ironhide.Most, 0.0001f);
            report.Check("And counts as one", stacked.Count == 1);

            report.Check("A land with nothing on it pays nothing more", new LandTerms().Boon() == LootBoon.None && new LandTerms().Pays().Length == 0);
            report.Check("Nor do Teeming, Swarming and Sprawling pay more for each kill", crowd.Boon() == LootBoon.None && crowd.Pays().Length > 0);
            report.Check("A deeper tier pays more for the same omen", Omens.All.Where(o => o.Quantity > 0f).All(o => new LandTerms { Omens = { new ItemLine(o.Id, o.Tiers[^1].High) } }.Boon().Quantity > new LandTerms { Omens = { new ItemLine(o.Id, o.Tiers[0].Low) } }.Boon().Quantity));
            var dangerous = new LandTerms { Omens = { new ItemLine(Omens.Ironhide.Id, 60f), new ItemLine(Omens.Frail.Id, 10f) } };
            report.Check("And no danger pays anything", dangerous.Boon() == LootBoon.None);

            var unfit = new LandTerms { Places = 40, Orders = { "no-such-orders", OrdersCatalog.Writ.Id, OrdersCatalog.Writ.Id } };
            unfit.Omens.Add(new ItemLine("no-such-omen", 20f));
            unfit.Omens.Add(new ItemLine(Omens.Keen.Id, float.NaN));
            unfit.Omens.Add(new ItemLine(Omens.Keen.Id, -5f));
            unfit.Omens.Add(new ItemLine(Omens.Ironhide.Id, 1e9f));
            for (int i = 0; i < Lands.MostOmenLines + 30; i++) unfit.Omens.Add(new ItemLine(Omens.Quickened.Id, 1f));
            unfit.Sanitize();
            report.Check("Whatever a save says comes out as something that can be", unfit.Places == LandTerms.MostPlaces && unfit.Orders.SequenceEqual(new[] { OrdersCatalog.Writ.Id }) && unfit.Omens.Count == Lands.MostOmenLines
                && unfit.Omens[0].Affix == Omens.Ironhide.Id && unfit.Omens[0].Value == Omens.Ironhide.Most && unfit.Omens.All(line => Omens.Find(line.Affix) != null && line.Value > 0f));
            var few = new LandTerms { Places = -3 };
            few.Sanitize();
            report.Check("And never asks for fewer places than there must be", few.Places == LandTerms.LeastPlaces);

            LandTerms copy = crowd.Copy();
            copy.Omens[0].Value = 1f;
            copy.Omens.Add(new ItemLine(Omens.Keen.Id, 10f));
            copy.Orders.Add(OrdersCatalog.Ledger.Id);
            report.Check("A copy of a land's terms is its own", crowd.Omens.Count == 3 && crowd.Omens[0].Value == 50f && crowd.Orders.Count == 0);
        }

        // Everything that isn't a land's terms has to come out as it did before there were any.
        private static void RollsStayTheSame(Report report)
        {
            bool same = true;
            for (int i = 0; i < 500; i++)
            {
                List<Item> before = Loot.Roll(LootTier.Rare, 10, 10, 0.3f, new LootLuck(), new WorldRandom((ulong)i));
                List<Item> after = Loot.Roll(LootTier.Rare, 10, 10, 0.3f, new LootLuck(), new WorldRandom((ulong)i), new LandTerms().Boon());
                same &= before.Select(item => item.Name + item.Level).SequenceEqual(after.Select(item => item.Name + item.Level));
            }
            report.Check("A land with nothing on it rolls what it always did", same);
        }

        private static void OrdersHold(Report report)
        {
            report.Check("Unique orders have ids of their own", OrdersCatalog.All.Select(o => o.Id).Distinct().Count() == OrdersCatalog.All.Count);
            report.Check("And names", OrdersCatalog.All.Select(o => o.Name).Distinct().Count() == OrdersCatalog.All.Count);
            report.Check("Each is a kind of orders there is", OrdersCatalog.All.All(o => OrdersMaker.Bases.Any(b => b.Name == o.Base)));
            report.Check("Each says what it gives, what it costs and where it came from", OrdersCatalog.All.All(o => o.Gives.Length > 12 && o.Costs.Length > 12 && o.Lore.Length > 12));
            report.Check("And can be found somewhere", OrdersCatalog.All.All(o => o.MinLevel >= 1 && o.MinLevel <= HeroState.MaxLevel && o.Climb >= 1));
            report.Check("Each kind of orders brings a reward of its own", OrdersMaker.Bases.All(b => b.Brings.IsPrefix) && OrdersMaker.Bases.Select(b => b.Brings).Distinct().Count() == OrdersMaker.Bases.Length);
            report.Check("No unique orders punishes a land that's lost", OrdersCatalog.All.All(o => !o.Costs.Contains("lowers") && !o.Costs.Contains("Land Level") && !o.Costs.Contains("leaving it")));

            var wrong = new List<string>();
            int[] counts = new int[4];
            var random = new WorldRandom(23);
            for (int i = 0; i < 4000; i++)
            {
                int level = 1 + i % HeroState.MaxLevel;
                Rarity rarity = (Rarity)(i % 4);
                Item orders = OrdersMaker.Roll(level, random.Fork(i), rarity);
                counts[(int)orders.Rarity]++;
                string what = $"{orders.Rarity} {orders.Name} at {level}";
                if (!orders.IsOrders || orders.Level != Math.Max(level, OrdersCatalog.Find(orders.Unique)?.MinLevel ?? 1)) wrong.Add($"{what} isn't orders of its level");
                if (orders.Rarity == Rarity.Unique)
                {
                    if (orders.Lines.Count > 0 || OrdersCatalog.Find(orders.Unique) == null) wrong.Add($"{what} isn't a unique there is");
                    continue;
                }
                OmenDefinition brings = OrdersMaker.BaseOf(orders)!.Brings;
                int each = OrdersMaker.PerSide(orders.Rarity);
                var omens = orders.Lines.Select(line => Omens.Find(line.Affix)).ToList();
                if (omens.Any(o => o == null)) { wrong.Add($"{what} has no such omen"); continue; }
                int prefixes = omens.Count(o => o!.IsPrefix);
                int suffixes = omens.Count(o => !o!.IsPrefix);
                if (prefixes != each || suffixes != each) wrong.Add($"{what} has {prefixes} and {suffixes}");
                if (orders.Lines.Count > 0 && Omens.Find(orders.Lines[0].Affix)!.IsPrefix == false) wrong.Add($"{what} doesn't lead with its prefixes");
                if (omens.Contains(brings)) wrong.Add($"{what} brings its own omen twice");
                if (omens.Distinct().Count() != omens.Count) wrong.Add($"{what} has an omen twice");
                if (omens.Any(o => o!.FirstLevel > level)) wrong.Add($"{what} has an omen too deep for it");
                if (orders.Lines.Any(line => line.Value > Omens.Find(line.Affix)!.Tiers[^1].High || line.Value <= 0f)) wrong.Add($"{what} rolled past its omen");

                Item fixedCopy = Copy(orders);
                OrdersMaker.Fix(fixedCopy);
                if (!SameOrders(orders, fixedCopy)) wrong.Add($"{what} was changed by fixing");
                if (orders.Rarity == Rarity.Magic && !(orders.Name.StartsWith(omens.First(o => o!.IsPrefix)!.Affix) && orders.Name.EndsWith(omens.First(o => !o!.IsPrefix)!.Affix))) wrong.Add($"{what} isn't named for what it brings");
            }
            report.Check("Normal orders bring only what they're for, Magic a prefix and a suffix, Rare two of each, and a Unique what it is", wrong.Count == 0, string.Join("; ", wrong.Take(4)));
            report.Check("Fixing leaves orders within the rules as they are", !wrong.Any(w => w.Contains("fixing")));
            report.Check("Orders of every rarity can be rolled", counts.All(count => count > 0), string.Join(", ", counts));

            // A save or a stranger: lines that aren't omens, the same one twice, the kind's own, too many of one side, out of order, and numbers that aren't.
            var broken = new Item
            {
                Name = "Something rude", Base = "Patrol Orders", Slot = ItemSlot.Orders, Rarity = Rarity.Rare, Level = 30, Seed = 77,
                Lines = { new ItemLine("no-such-omen", 10f), new ItemLine(Omens.Keen.Id, 10f), new ItemLine(Omens.Overrun.Id, 30f), new ItemLine(Omens.Fortune.Id, 20f), new ItemLine(Omens.Fortune.Id, 20f), new ItemLine(Omens.Plated.Id, float.NaN), new ItemLine(Omens.Ironhide.Id, 1e6f), new ItemLine(Omens.Quickened.Id, 10f), new ItemLine(Omens.Hexing.Id, 20f) },
            };
            Item brokenToo = Copy(broken);
            OrdersMaker.Fix(broken);
            OrdersMaker.Fix(brokenToo);
            var fixedOmens = broken.Lines.Select(line => Omens.Find(line.Affix)).ToList();
            report.Check("Broken orders come out as orders that could have been rolled", fixedOmens.All(o => o != null && o != Omens.Overrun) && fixedOmens.Count(o => o!.IsPrefix) == 2 && fixedOmens.Count(o => !o!.IsPrefix) == 2
                && fixedOmens.Distinct().Count() == 4 && fixedOmens[0]!.IsPrefix && fixedOmens[1]!.IsPrefix && broken.Lines.All(line => line.Value <= Omens.Find(line.Affix)!.Tiers[^1].High)
                && broken.Lines.Any(line => line.Affix == Omens.Keen.Id) && broken.Lines.Any(line => line.Affix == Omens.Fortune.Id), string.Join(", ", broken.Lines.Select(l => l.Affix)));
            report.Check("The same each time", SameOrders(broken, brokenToo));
            report.Check("Named for what they are, not what they said", broken.Name == OrdersMaker.NameOf(broken) && !broken.Name.Contains("rude"));

            var gone = new Item { Name = "Gone", Base = "Hunting Orders", Slot = ItemSlot.Orders, Rarity = Rarity.Unique, Unique = "no-such-orders", Level = 20, Seed = 5 };
            OrdersMaker.Fix(gone);
            report.Check("Unique orders that are gone become Rare ones", gone.Rarity == Rarity.Rare && gone.Unique == null && gone.Lines.Count == 4);
            var real = new Item { Name = "Something else", Base = "Muster Orders", Slot = ItemSlot.Orders, Rarity = Rarity.Unique, Unique = OrdersCatalog.Writ.Id, Level = 20, Seed = 5, Lines = { new ItemLine(Omens.Keen.Id, 10f) } };
            OrdersMaker.Fix(real);
            report.Check("And unique ones that aren't are only ever what they are", real.Lines.Count == 0 && real.Name == OrdersCatalog.Writ.Name && real.Base == OrdersCatalog.Writ.Base);

            var nameless = new Item { Name = "", Base = "No such kind", Slot = ItemSlot.Orders, Rarity = Rarity.Normal, Level = 3, Seed = 9 };
            OrdersMaker.Fix(nameless);
            report.Check("Orders of a kind there isn't become a kind there is", OrdersMaker.BaseOf(nameless) != null && nameless.Name == nameless.Base);

            // What orders put on a land, alone and together.
            Item magic = OrdersMaker.Roll(20, new WorldRandom(8), Rarity.Magic);
            var given = new LandTerms();
            given.Give(magic);
            OmenDefinition kind = OrdersMaker.BaseOf(magic)!.Brings;
            report.Check("Orders put on the land what their kind brings, and what's rolled on them", given.Has(kind) && given.ValueOf(kind) == kind.Fixed(20) && magic.Lines.All(line => given.Has(Omens.Find(line.Affix)!)) && given.Orders.Count == 0);
            Item rare = OrdersMaker.Roll(20, new WorldRandom(81), Rarity.Rare);
            given.Give(rare);
            report.Check("And several orders put all of theirs on it", rare.Lines.All(line => given.Has(Omens.Find(line.Affix)!)) && magic.Lines.All(line => given.Has(Omens.Find(line.Affix)!)));
            Item wager = OrdersMaker.Make(OrdersCatalog.Wager, 1, new WorldRandom(3));
            Item writ = OrdersMaker.Make(OrdersCatalog.Writ, 1, new WorldRandom(4));
            var wagered = new LandTerms();
            wagered.Give(wager);
            wagered.Give(writ);
            wagered.Give(wager);
            report.Check("And unique orders what they are, each once, however many are given", wagered.Orders.SequenceEqual(new[] { OrdersCatalog.Wager.Id, OrdersCatalog.Writ.Id }) && wagered.Count == 0 && wager.Level == OrdersCatalog.Wager.MinLevel);
            report.Check("Every unique orders pays something, or opens more", OrdersCatalog.All.All(o => o.Quantity > 0f || o.Rarity > 0f || o.Climb > 1 || o == OrdersCatalog.Garrison));

            // Orders are carried and never worn, so anything that asks where something would be worn has to leave them be.
            HeroState carrier = HeroState.CreateNew();
            carrier.Inventory[4] = OrdersMaker.Roll(10, new WorldRandom(14), Rarity.Rare);
            bool asked = true;
            try
            {
                asked = carrier.WornBeside(carrier.Inventory[4]!) == null && !carrier.Equip(4) && !carrier.Move(ItemPlace.Cell(4), ItemPlace.Worn(WornSlot.MainHand)) && carrier.Inventory[4]!.IsOrders;
            }
            catch (Exception)
            {
                asked = false;
            }
            report.Check("Orders can't be worn, and asking where they would be breaks nothing", asked);

            // Where orders come from: now and then among what drops, and always on a tyrant.
            var drops = new WorldRandom(31);
            int found = 0, all = 0;
            for (int i = 0; i < 20000; i++)
            {
                foreach (Item item in Loot.Roll(LootTier.Rare, 20, 20, 0f, new LootLuck(), drops.Fork(i)))
                {
                    all++;
                    if (item.IsOrders) found++;
                }
            }
            report.Check("Orders turn up among what drops, but not often", found > 0 && found < all * 0.12f, $"{found} of {all}");
            var tyrants = new int[4];
            for (int i = 0; i < 4000; i++) tyrants[(int)Loot.OrdersFrom(20, drops.Fork(100000 + i), LootBoon.None).Rarity]++;
            report.Check("A tyrant carries orders of every rarity", tyrants.All(count => count > 0), string.Join(", ", tyrants));
        }

        private static void StashHolds(Report report)
        {
            var stash = new Stash();
            report.Check("A stash is four tabs of cells, all empty", stash.Tabs.Count == Stash.TabCount && stash.Tabs.All(tab => tab.Length == Stash.TabSize) && !stash.Kept().Any());

            var random = new WorldRandom(41);
            HeroState hero = HeroState.CreateNew();
            Item sword = ItemGenerator.Roll(10, random.Fork(1));
            Item ring = ItemGenerator.Roll(10, random.Fork(2));
            hero.Inventory[3] = sword;
            report.Check("Something put away leaves the pack for the cell it was put in", stash.Stow(hero, ItemPlace.Cell(3), 1, 5) && hero.Inventory[3] == null && stash.At(1, 5) == sword);
            hero.Inventory[7] = ring;
            report.Check("Putting something where something is swaps them", stash.Stow(hero, ItemPlace.Cell(7), 1, 5) && hero.Inventory[7] == sword && stash.At(1, 5) == ring);

            Item worn = ItemGenerator.Roll(10, random.Fork(3));
            WornSlot place = hero.PlaceFor(worn);
            hero.Equipped[place] = worn;
            report.Check("Nothing worn is swapped for something it wasn't chosen for", !stash.Stow(hero, ItemPlace.Worn(place), 1, 5) && hero.Equipped[place] == worn && stash.At(1, 5) == ring);
            report.Check("But it can go in an empty cell", stash.Stow(hero, ItemPlace.Worn(place), 2, 0) && !hero.Equipped.ContainsKey(place) && stash.At(2, 0) == worn);
            report.Check("Nothing goes where there's no cell", !stash.Stow(hero, ItemPlace.Cell(7), 9, 0) && !stash.Stow(hero, ItemPlace.Cell(7), 0, Stash.TabSize) && !stash.Stow(hero, ItemPlace.Cell(8), 0, 0));

            report.Check("Taking out goes to the cell asked for, swapping", stash.Take(hero, 1, 5, 7) && hero.Inventory[7] == ring && stash.At(1, 5) == sword);
            report.Check("Or to the first free one", stash.Take(hero, 2, 0) && hero.Inventory[0] == worn && stash.At(2, 0) == null);
            for (int i = 0; i < HeroState.InventorySize; i++) hero.Inventory[i] ??= ItemGenerator.Roll(5, random.Fork(100 + i));
            report.Check("And stays put if the pack is full", !stash.Take(hero, 1, 5) && stash.At(1, 5) == sword);

            report.Check("Moving within the stash swaps", stash.Shift(1, 5, 3, 47) && stash.At(3, 47) == sword && stash.At(1, 5) == null);
            report.Check("But not nothing, nor to where it is, nor off the end", !stash.Shift(1, 5, 0, 0) && !stash.Shift(3, 47, 3, 47) && !stash.Shift(3, 47, 4, 0) && !stash.Shift(3, 47, 0, -1));

            // Whatever a file says, the right shape comes out of it with nothing lost.
            var odd = new Stash { Tabs = new List<Item?[]> { new Item?[60], new Item?[2] } };
            for (int i = 0; i < 60; i++) odd.Tabs[0][i] = i % 3 == 0 ? ItemGenerator.Roll(5, random.Fork(500 + i)) : null;
            odd.Tabs.Add(new Item?[] { ItemGenerator.Roll(5, random.Fork(600)) });
            for (int t = 0; t < 4; t++) odd.Tabs.Add(new Item?[] { ItemGenerator.Roll(5, random.Fork(700 + t)) });
            int before = odd.Tabs.Sum(tab => tab.Count(item => item != null));
            odd.Sanitize();
            report.Check("A stash of the wrong shape comes out the right one with nothing lost", odd.Tabs.Count == Stash.TabCount && odd.Tabs.All(tab => tab.Length == Stash.TabSize) && odd.Kept().Count() == before, $"{odd.Kept().Count()} of {before}");

            // Thousands of moves of every kind, sense and nonsense: nothing is ever made or lost, and nothing is in two places. Something worn is among it all, as that's a place a move can start from.
            var fuzzHero = new HeroState();
            var fuzz = new Stash();
            var everything = new HashSet<Item>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < 20; i++)
            {
                Item item = ItemGenerator.Roll(5, random.Fork(900 + i));
                everything.Add(item);
                if (i < 12) fuzzHero.Inventory[i * 2] = item;
                else fuzz.Tabs[i % Stash.TabCount][i] = item;
            }
            Item helm = ItemGenerator.Roll(5, random.Fork(950));
            fuzzHero.Equipped[fuzzHero.PlaceFor(helm)] = helm;
            everything.Add(helm);
            IEnumerable<Item> OnHero() => fuzzHero.Inventory.OfType<Item>().Concat(fuzzHero.Equipped.Values).Concat(fuzzHero.Bar.OfType<Item>());
            WorldRandom moves = random.Fork("moves");
            bool kept = true;
            for (int step = 0; step < 20000 && kept; step++)
            {
                int tab = moves.Int(-1, Stash.TabCount + 1), cell = moves.Int(-2, Stash.TabSize + 2);
                switch (moves.Int(3))
                {
                    case 0:
                        fuzz.Stow(fuzzHero, new ItemPlace(moves.Int(-2, HeroState.InventorySize + 20)), tab, cell);
                        break;
                    case 1:
                        fuzz.Take(fuzzHero, tab, cell, moves.Chance(0.5f) ? moves.Int(-2, HeroState.InventorySize + 2) : null);
                        break;
                    default:
                        fuzz.Shift(tab, cell, moves.Int(-1, Stash.TabCount + 1), moves.Int(-2, Stash.TabSize + 2));
                        break;
                }
                var now = OnHero().Concat(fuzz.Kept()).ToList();
                kept = now.Count == everything.Count && now.All(everything.Contains) && new HashSet<Item>(now, ReferenceEqualityComparer.Instance).Count == now.Count;
            }
            report.Check("Twenty thousand moves, sense and nonsense, and nothing made, lost or doubled", kept);

            // Kept on this machine, and by only one copy of the game at once.
            string folder = Path.Combine(Path.GetTempPath(), $"dino-stash-{Environment.ProcessId}");
            string file = Path.Combine(folder, "stash.json");
            try
            {
                StashStore.Claim(file);
                report.Check("The stash is this copy's to keep", StashStore.Held);
                bool locked;
                try
                {
                    using var other = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    locked = false;
                }
                catch (IOException)
                {
                    locked = true;
                }
                report.Check("And no other copy can have it while it's held", locked);

                var mine = new Stash();
                mine.Tabs[2][17] = sword;
                mine.Tabs[0][0] = OrdersMaker.Roll(12, new WorldRandom(4), Rarity.Rare);
                StashStore.Store(mine);
                Stash loaded = StashStore.Load();
                report.Check("What's put away keeps, where it was put", loaded.At(2, 17)?.Name == sword.Name && loaded.At(2, 17)?.Seed == sword.Seed && loaded.At(0, 0)?.IsOrders == true && loaded.Kept().Count() == 2);
                report.Check("Orders keep their lines", loaded.At(0, 0) is Item orders && SameOrders(orders, mine.Tabs[0][0]!));

                File.WriteAllText(file, "{ this isn't a stash");
                Stash unread = StashStore.Load();
                report.Check("A stash that can't be read is kept aside rather than written over", !unread.Kept().Any() && Directory.GetFiles(folder, "stash.json.unread-*").Length == 1);
            }
            finally
            {
                StashStore.Claim(null);
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }

        private static void Wire(Report report)
        {
            var terms = new LandTerms { Places = 5, Orders = { OrdersCatalog.Ledger.Id, OrdersCatalog.Wager.Id }, Omens = { new ItemLine(Omens.Volatile.Id, 50f), new ItemLine(Omens.Ironhide.Id, 37f), new ItemLine(Omens.Ironhide.Id, 12f) } };
            var writer = new BitWriter(64);
            Protocol.WriteTerms(writer, terms);
            LandTerms read = Protocol.ReadTerms(new BitReader(writer.ToArray()));
            report.Check("A land's terms come across whole, every unique orders among them", Same(read, terms) && read.ValueOf(Omens.Ironhide) == 49f && read.Orders.Count == 2);

            bool Refused(Action<BitWriter> write)
            {
                var nonsense = new BitWriter(64);
                write(nonsense);
                try
                {
                    Protocol.ReadTerms(new BitReader(nonsense.ToArray()));
                    return false;
                }
                catch (NetReadException)
                {
                    return true;
                }
            }

            report.Check("An omen that doesn't exist is nonsense", Refused(w => { w.WriteCount(1); w.WriteVarInt(Omens.All.Count); w.WriteFloat(10f); w.WriteCount(0); w.WriteVarInt(4); }));
            report.Check("So are orders that don't", Refused(w => { w.WriteCount(0); w.WriteCount(1); w.WriteVarInt(OrdersCatalog.All.Count); w.WriteVarInt(4); }));
            report.Check("So are more unique orders than there are", Refused(w => { w.WriteCount(0); w.WriteCount(OrdersCatalog.All.Count + 1); for (int i = 0; i <= OrdersCatalog.All.Count; i++) w.WriteVarInt(0); w.WriteVarInt(4); }));
            report.Check("So is more than a land can hold", Refused(w => { w.WriteCount(Lands.MostOmenLines + 1); for (int i = 0; i <= Lands.MostOmenLines; i++) { w.WriteVarInt(0); w.WriteFloat(1f); } w.WriteCount(0); w.WriteVarInt(4); }));
            var odd = new BitWriter(64);
            odd.WriteCount(2);
            odd.WriteVarInt(Omens.Keen.Number);
            odd.WriteFloat(-4f);
            odd.WriteVarInt(Omens.Ironhide.Number);
            odd.WriteFloat(1e9f);
            odd.WriteCount(2);
            odd.WriteVarInt(1);
            odd.WriteVarInt(1);
            odd.WriteVarInt(99);
            LandTerms tamed = Protocol.ReadTerms(new BitReader(odd.ToArray()));
            report.Check("And what's merely out of bounds is brought within them", !tamed.Has(Omens.Keen) && tamed.ValueOf(Omens.Ironhide) == Omens.Ironhide.Most && tamed.Places == LandTerms.MostPlaces && tamed.Orders.Count == 1);

            var hero = new HeroState { BestLand = 13, Offers = 0xDEADBEEFCAFEUL, Rerolls = { [13] = 2, [4] = 1 } };
            var summary = new BitWriter(64);
            Protocol.WriteHeroSummary(summary, hero);
            var back = new HeroState();
            Protocol.ReadHeroSummary(new BitReader(summary.ToArray()), back);
            report.Check("How far a hero has climbed goes with them, and which lands they're shown at each level", back.LandLevel == 14 && back.BestLand == 13 && back.Offers == hero.Offers && back.RerollsAt(13) == 2 && back.RerollsAt(4) == 1 && back.RerollsAt(5) == 0);

            var stash = new Stash();
            stash.Tabs[3][40] = ItemGenerator.Roll(30, new WorldRandom(6));
            stash.Tabs[1][0] = OrdersMaker.Make(OrdersCatalog.Writ, 20, new WorldRandom(7));
            var stashWriter = new BitWriter(256);
            Protocol.WriteStash(stashWriter, stash);
            Stash stashRead = Protocol.ReadStash(new BitReader(stashWriter.ToArray()));
            report.Check("A stash comes across whole, gaps and all", stashRead.At(3, 40)?.Seed == stash.Tabs[3][40]!.Seed && stashRead.At(1, 0)?.Unique == OrdersCatalog.Writ.Id && stashRead.Kept().Count() == 2);

            var ordersWriter = new BitWriter(64);
            Item rare = OrdersMaker.Roll(25, new WorldRandom(12), Rarity.Rare);
            Protocol.WriteItem(ordersWriter, rare);
            Item rareRead = Protocol.ReadItem(new BitReader(ordersWriter.ToArray()));
            report.Check("Orders come across as they were", SameOrders(rare, rareRead) && rareRead.Level == rare.Level);
        }

        private static void Saved(Report report)
        {
            // A hero from before lands: the world they were in goes, and they wake in the Haven with the same talent points and a climb as far as they'd come.
            SaveGame? before = SaveGame.Read("{ \"Version\": 8, \"WorldSeed\": 8, \"WorldLevel\": 6, \"Hero\": { \"Level\": 10, \"WorldsCleared\": 5 }, \"Sites\": [ { \"Index\": 2, \"Cleared\": true } ], \"PlayerX\": 40 }");
            report.Check("A hero from before lands wakes in the Haven", before != null && !before.InLand && before.Sites.Count == 0 && before.PlayerX < 0f);
            report.Check("With the same talent points as before", before != null && before.Hero.TalentPoints == 1 + HeroState.PointsPerLevel * 10 + 5);
            report.Check("And a climb as far as they'd come", before != null && before.Hero.BestLand == 5 && before.Hero.LandLevel == 6 && before.Hero.Offers != 0);

            // A hero from before arenas, in a land that had dropped their climb: every level they'd reached is theirs to open, and the land they were in is gone.
            SaveGame? climbed = SaveGame.Read("{ \"Version\": 10, \"InLand\": true, \"WorldSeed\": 8, \"WorldArchetype\": \"isle\", \"WorldLevel\": 9, \"Terms\": { \"Omens\": [ { \"Affix\": \"ironhide\", \"Value\": 30 } ], \"Unique\": \"twin-pens\", \"Places\": 5 }, \"Hero\": { \"Level\": 20, \"LandLevel\": 9, \"BestLand\": 6, \"WorldsCleared\": 8, \"Offers\": 5 }, \"Sites\": [ { \"Index\": 2, \"Cleared\": true } ], \"PlayerX\": 40 }");
            report.Check("A hero from before arenas wakes in the Haven", climbed != null && !climbed.InLand && climbed.Sites.Count == 0 && climbed.PlayerX < 0f && !climbed.Terms.Any);
            report.Check("With every level they'd climbed to theirs to open", climbed != null && climbed.Hero.BestLand == 8 && climbed.Hero.LandLevel == 9 && climbed.Hero.Offers == 5);
            SaveGame? dropped = SaveGame.Read("{ \"Version\": 10, \"Hero\": { \"Level\": 20, \"LandLevel\": 3, \"BestLand\": 6 } }");
            report.Check("And a climb a fall had lowered is back where it was", dropped != null && dropped.Hero.BestLand == 6 && dropped.Hero.LandLevel == 7);

            string file = Path.GetTempFileName();
            SaveGame.PathOverride = file;
            try
            {
                var terms = new LandTerms { Places = 6, Orders = { OrdersCatalog.TwinPens.Id, OrdersCatalog.Writ.Id }, Omens = { new ItemLine(Omens.Keen.Id, 20f), new ItemLine(Omens.Teeming.Id, 30f) } };
                var save = new SaveGame { InLand = true, WorldSeed = 99, WorldArchetype = "isle", WorldLevel = 12, Terms = terms, Hero = new HeroState { BestLand = 11, Offers = 42, Rerolls = { [12] = 3 } } };
                save.Store();
                SaveGame? loaded = SaveGame.Load();
                report.Check("A land's terms keep", loaded != null && loaded.InLand && Same(loaded.Terms, terms) && SameRequest(loaded.Land(), new WorldRequest(99, "isle", 12)));
                report.Check("And so does the climb, with which lands each level shows", loaded != null && loaded.Hero.LandLevel == 12 && loaded.Hero.BestLand == 11 && loaded.Hero.Offers == 42 && loaded.Hero.RerollsAt(12) == 3);
                string json = File.ReadAllText(file);
                report.Check("Nothing worked out from them is written down", !json.Contains("EnemyHealth") && !json.Contains("MorePeople") && !json.Contains("Climb") && !json.Contains("\"Any\"") && !json.Contains("\"Count\"") && !json.Contains("LandLevel") && !json.Contains("Larger"));
            }
            finally
            {
                SaveGame.PathOverride = null;
                File.Delete(file);
            }
        }

        private static bool SameRequest(in WorldRequest a, in WorldRequest b) => a.Seed == b.Seed && a.ArchetypeId == b.ArchetypeId && a.Level == b.Level && a.MorePeople == b.MorePeople && a.MorePacks == b.MorePacks && a.Larger == b.Larger;

        private static bool Same(LandTerms a, LandTerms b) => a.Places == b.Places && a.Orders.SequenceEqual(b.Orders) && a.Chosen().SequenceEqual(b.Chosen());

        private static bool SameOrders(Item a, Item b) => a.Name == b.Name && a.Base == b.Base && a.Rarity == b.Rarity && a.Unique == b.Unique
            && a.Lines.Select(line => (line.Affix, line.Value)).SequenceEqual(b.Lines.Select(line => (line.Affix, line.Value)));

        private static Item Copy(Item orders) => new Item
        {
            Name = orders.Name, Base = orders.Base, Slot = orders.Slot, Rarity = orders.Rarity, Level = orders.Level, Seed = orders.Seed, Unique = orders.Unique,
            Lines = orders.Lines.Select(line => new ItemLine(line.Affix, line.Value)).ToList(),
        };
    }
}
