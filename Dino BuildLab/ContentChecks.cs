using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>What the game is made of: the tree, the items, the skills. A sum can be right and the content still make no sense.</summary>
    internal static class ContentChecks
    {
        public static void Run(Report report)
        {
            report.Section("The tree");
            TreeShape(report);
            TreeRules(report);

            report.Section("Everything can say what it does");
            Descriptions(report);

            report.Section("Everything is used");
            Coverage(report);

            report.Section("Skills");
            SkillShape(report);

            report.Section("Items");
            EnemyRarities(report);
            Rolling(report);
            Affixes(report);
            Fixing(report);
            Uniques(report);
            Wires(report);
            GearChecks.Run(report);
            PackChecks.Run(report);

            report.Section("Saves from before all this");
            OldSaves(report);
            PackContent(report);

            report.Section("A hero and an enemy are the same thing");
            Symmetry(report);
        }

        private static void TreeShape(Report report)
        {
            TalentNode[] nodes = TalentTree.Nodes;
            int Count(TalentKind kind) => nodes.Count(n => n.Kind == kind);
            report.Note($"{nodes.Length} nodes: {Count(TalentKind.Small)} small, {Count(TalentKind.Notable)} notable, {Count(TalentKind.Keystone)} keystones, {Count(TalentKind.Gate)} gates");
            report.Check("It's big", nodes.Length >= 800, $"{nodes.Length}");
            report.Check("Ids are all different", nodes.Select(n => n.Id).Distinct().Count() == nodes.Length);
            report.Check("Everything can be walked to", TalentTree.DepthOf.All(d => d >= 0), $"{TalentTree.DepthOf.Count(d => d < 0)} can't");
            report.Check("Every link goes both ways", nodes.All(n => n.Links.All(l => nodes[l].Links.Contains(n.Index))));
            report.Check("Nothing links to itself or twice", nodes.All(n => !n.Links.Contains(n.Index) && n.Links.Distinct().Count() == n.Links.Count));
            report.Check("Every sector has three keystones and a gate", TalentContent.Sectors.Select((s, i) => nodes.Count(n => n.Sector == i && n.Kind == TalentKind.Keystone) == 3 && nodes.Count(n => n.Sector == i && n.Kind == TalentKind.Gate) == 1).All(x => x));
            report.Check("Gates come in pairs", nodes.Where(n => n.Kind == TalentKind.Gate).All(n => n.Twin >= 0 && nodes[n.Twin].Twin == n.Index));

            float closest = float.PositiveInfinity;
            string pair = "";
            foreach (TalentNode a in nodes)
            {
                foreach (TalentNode b in nodes)
                {
                    if (b.Index <= a.Index) continue;
                    float distance = (a.Position - b.Position).Length;
                    if (distance >= closest) continue;
                    closest = distance;
                    pair = $"{a.Id} and {b.Id}";
                }
            }
            report.Check("No two nodes sit on each other", closest >= 34f, $"{closest:0} between {pair}");
            report.Note($"Closest pair of nodes: {closest:0} units");

            float furthest = nodes.Max(n => n.Position.Length);
            report.Note($"The tree is {furthest * 2f:0} units across");

            var keystones = nodes.Where(n => n.Kind == TalentKind.Keystone).ToList();
            int deepest = keystones.Max(k => TalentTree.DepthOf[k.Index]);
            int shallowest = keystones.Min(k => TalentTree.DepthOf[k.Index]);
            report.Check("Keystones can be had well before the end of the game", deepest <= 30, $"{deepest}");
            report.Note($"A keystone is {shallowest} to {deepest} points from the root; a hero at level 60 has {new HeroState { Level = 60 }.TalentPoints}");

            var notables = nodes.Where(n => n.Kind == TalentKind.Notable).ToList();
            int nearest = TalentContent.Sectors.Select((s, i) => notables.Where(n => n.Sector == i).Min(n => TalentTree.DepthOf[n.Index])).Max();
            report.Check("Every sector has a notable that's soon reached", nearest <= new HeroState { Level = 2 }.TalentPoints, $"{nearest} points");
        }

        private static HeroState Hero(int level, params string[] talents)
        {
            var hero = new HeroState { Level = level };
            foreach (string id in talents)
            {
                foreach (int step in TalentTree.PathTo(hero, TalentTree.Find(id)!.Index)) hero.Talents.Add(TalentTree.Nodes[step].Id);
            }
            return hero;
        }

        private static void TreeRules(Report report)
        {
            int Index(string id) => TalentTree.Find(id)!.Index;

            HeroState fresh = HeroState.CreateNew();
            report.Check("A new hero has one book, and it's free", HeroBuild.Compile(fresh).Books.Length == 1 && HeroBuild.Compile(fresh).SheetFor(Condition.None).Profile(0).Cost == 0f);
            report.Check("The wind doesn't answer them until a land is free", !fresh.HasGuidingWind && new HeroState { WorldsCleared = 1 }.HasGuidingWind);
            report.Check("A new hero can take the first step", TalentTree.CanAllocate(fresh, Index("pyre.t0")));
            report.Check("But not a step further", !TalentTree.CanAllocate(fresh, Index("pyre.t1")));
            report.Check("Nor anything that's taken", !TalentTree.CanAllocate(Hero(5, "pyre.t0"), Index("pyre.t0")));

            HeroState path = Hero(10, "pyre.t2");
            report.Check("A node in the middle of a path can't be given back", !TalentTree.CanRefund(path, Index("pyre.t1")));
            report.Check("The end of it can", TalentTree.CanRefund(path, Index("pyre.t2")));
            report.Check("Nor can the root ever", !TalentTree.CanRefund(path, TalentTree.Root.Index));

            HeroState looped = Hero(20, "pyre.t2", "pyre.kindled-bolt", "pyre.kindled-bolt.0");
            report.Check("A node a loop goes around can be given back", TalentTree.CanRefund(looped, Index("pyre.t1")));

            HeroState rim = Hero(60, "forge.gate");
            int twin = TalentTree.Find("forge.gate")!.Twin;
            report.Check("A lit gate opens onto its twin", TalentTree.CanAllocate(rim, twin));
            report.Check("Which is far away", (TalentTree.Nodes[twin].Position - TalentTree.Find("forge.gate")!.Position).Length > 1500f);
            rim.Talents.Add(TalentTree.Nodes[twin].Id);
            string beyond = TalentTree.Nodes[TalentTree.Nodes[twin].Links[0]].Id;
            report.Check("And past it, the tree goes on from there", TalentTree.CanAllocate(rim, TalentTree.Find(beyond)!.Index));

            rim.Talents.Add(beyond);
            report.Check("The gate you came through can't be given back while the far side hangs from it", !TalentTree.CanRefund(rim, Index("forge.gate")));

            int walked = TalentTree.DepthOf[Index("key.slow-burn")];
            HeroState gatewalker = Hero(60, "forge.gate");
            int stepped = TalentTree.PathTo(gatewalker, Index("key.slow-burn")).Count;
            report.Check("A gate is a real shortcut", stepped < walked, $"{stepped} from the far gate, {walked} from the root");
            report.Note($"From the far side of the Forge's gate, a Pyre keystone is {stepped} points; from the root it's {walked}");

            HeroState unwise = Hero(1);
            unwise.Talents.AddRange(new[] { "no.such.node", "pyre.t3", "root", "pyre.t0", "pyre.t0" });
            TalentTree.Sanitize(unwise);
            report.Check("Nonsense, things cut off, and duplicates are all thrown out", unwise.Talents.Count == 1 && unwise.Talents[0] == "pyre.t0", string.Join(",", unwise.Talents));

            HeroState broke = Hero(60, "key.slow-burn", "key.tidewalker");
            broke.Level = 2;
            TalentTree.Sanitize(broke);
            report.Check("More talents than the level allows are trimmed, and what's left still holds together", broke.Talents.Count <= broke.TalentPoints && Enumerable.Range(0, TalentTree.Nodes.Length).All(i => !TalentTree.Allocated(broke).Contains(i) || i == 0 || TalentTree.CanRefund(broke, i) || TalentTree.Nodes[i].Links.Count > 0), $"{broke.Talents.Count} of {broke.TalentPoints}");

            HeroState ordered = Hero(40, "pyre.kindled-bolt", "winter.white-ring");
            var shuffled = new HeroState { Level = 40, Talents = ordered.Talents.OrderByDescending(t => t).ToList() };
            float a = HeroBuild.Compile(ordered).SheetFor(Condition.None).Preview(Shelf.Firebolt).First.AverageHit;
            float b = HeroBuild.Compile(shuffled).SheetFor(Condition.None).Preview(Shelf.Firebolt).First.AverageHit;
            report.Check("The order talents were taken in makes no difference to what they add up to", a == b, $"{a} and {b}");

            report.Check("A wanderer becomes something", TalentTree.TitleOf(new HeroState()) == "Wanderer" && TalentTree.TitleOf(Hero(30, "pyre.t5")) == "Ember Pyromancer", TalentTree.TitleOf(Hero(30, "pyre.t5")));
            report.Check("Two sectors mix", TalentTree.TitleOf(Hero(40, "pyre.t5", "winter.t3")) == "Frost Pyromancer", TalentTree.TitleOf(Hero(40, "pyre.t5", "winter.t3")));
            report.Check("A keystone can have the last word", TalentTree.TitleOf(Hero(40, "key.tidewalker")) == "Tidewalker");
        }

        private static IEnumerable<(string where, Mod mod)> EveryMod()
        {
            foreach (TalentNode node in TalentTree.Nodes)
            {
                foreach (Mod mod in node.Mods) yield return (node.Id, mod);
            }
            foreach (Affix affix in AffixCatalog.All)
            {
                foreach (Mod mod in affix.Build(affix.Tiers[^1].High)) yield return (affix.Id, mod);
            }
            foreach (UniqueDefinition unique in UniqueCatalog.All)
            {
                foreach (Mod mod in unique.Mods) yield return (unique.Id, mod);
            }
            foreach (OmenDefinition omen in Omens.All)
            {
                foreach (Mod mod in omen.Mods(omen.Tiers[^1].High)) yield return (omen.Id, mod);
            }
            foreach (BookPlan book in Shelf.All)
            {
                foreach (PagePlan page in book.Pages)
                {
                    foreach (Mod mod in page.Mods) yield return (book.Id, mod);
                }
            }
            foreach (BuffDefinition buff in BuffCatalog.All)
            {
                foreach (Mod mod in buff.Mods) yield return (buff.Id, mod);
            }
            foreach (EnemyTrait trait in Bestiary.Traits)
            {
                foreach (Mod mod in trait.Mods) yield return (trait.Name, mod);
            }
        }

        private static void Descriptions(Report report)
        {
            var bad = new List<string>();
            int count = 0;
            var lines = new HashSet<string>();
            foreach ((string where, Mod mod) in EveryMod())
            {
                count++;
                string text = mod.Describe();
                lines.Add(text);
                if (string.IsNullOrWhiteSpace(text) || text.Contains("  ") || text.StartsWith(' ') || text.EndsWith(' ') || text.Contains("NaN")) bad.Add($"{where}: '{text}'");
            }
            report.Check($"All {count} modifiers read as something", bad.Count == 0, string.Join("; ", bad.Take(8)));
            report.Note($"{lines.Count} different lines of text");

            report.Check("Every keystone says something of its own", TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone).All(n => n.Mods.Length > 0 && !string.IsNullOrEmpty(n.Lore)));
            report.Check("No two notables share a name", TalentTree.Nodes.Where(n => n.Kind is TalentKind.Notable or TalentKind.Keystone).Select(n => n.Name).Distinct().Count() == TalentTree.Nodes.Count(n => n.Kind is TalentKind.Notable or TalentKind.Keystone));
        }

        private static void Coverage(Report report)
        {
            var stats = new HashSet<Stat>();
            var rules = new HashSet<Rule>();
            var conditions = Condition.None;
            var scopes = Tags.None;
            foreach ((_, Mod mod) in EveryMod())
            {
                if (mod.Kind <= ModKind.Override) stats.Add(mod.Stat);
                if (mod.Kind == ModKind.Rule) rules.Add(mod.Rule);
                conditions |= mod.Need | mod.Without | mod.Foe | mod.Grants;
                scopes |= mod.Scope;
            }

            var unused = Enum.GetValues<Stat>().Where(s => s != Stat.Count && !stats.Contains(s)).ToList();
            report.Check("Every stat can be had from something", unused.Count == 0, string.Join(", ", unused));

            var unusedRules = Enum.GetValues<Rule>().Where(r => r is not (Rule.None or Rule.Count) && !rules.Contains(r)).ToList();
            report.Check("Every rule can be had from something", unusedRules.Count == 0, string.Join(", ", unusedRules));

            Condition[] land = { Condition.InWater, Condition.UnderTrees, Condition.OnRoad, Condition.InDarkness, Condition.HotLand, Condition.ColdLand, Condition.WetLand, Condition.HighLand };
            var ignored = land.Where(c => (conditions & c) == 0).ToList();
            report.Check("Everything the land can say matters to some build", ignored.Count == 0, string.Join(", ", ignored));

            report.Check("Each sector's small nodes are worth having", TalentContent.Sectors.All(s => s.Small.All(m => m.Length > 0)));
            int more = 0;
            int common = 0;
            foreach (TalentNode node in TalentTree.Nodes)
            {
                if (node.Kind == TalentKind.Keystone) continue;
                foreach (Mod mod in node.Mods)
                {
                    if (mod.Kind == ModKind.More) more++;
                    else if (mod.Kind is ModKind.Increased or ModKind.Flat) common++;
                }
            }
            report.Check("\"More\" and \"less\" are rare: only keystones have them on the tree", more == 0, $"{more} of {more + common}");
        }

        private static void SkillShape(Report report)
        {
            report.Note($"{Shelf.All.Count} written books");
            report.Check("Ids are all different", Shelf.All.Select(s => s.Id).Distinct().Count() == Shelf.All.Count);

            var deals = Shelf.All.SelectMany(book => book.Pages).SelectMany(page => page.Damage).Select(part => part.Type).ToHashSet();
            foreach (DamageType type in DamageKinds.All)
            {
                report.Check($"Something written deals {type}", deals.Contains(type));
            }

            ActorSheet sheet = new Loadout(1, WeaponProfile.Fists, new[] { Flat(Stat.Health, 100f) }, Shelf.All).SheetFor(Condition.None);
            var forms = Shelf.All.SelectMany(book => sheet.Preview(book).Pages).Select(page => page.Sign.Form).ToHashSet();
            foreach (PageForm form in Enum.GetValues<PageForm>())
            {
                report.Check($"Something written looks like {form}", forms.Contains(form));
            }

            var broken = new List<string>();
            foreach (BookPlan skill in Shelf.All)
            {
                BookProfile book = sheet.Preview(skill);
                if (!float.IsFinite(book.UseTime) || book.UseTime <= 0f) broken.Add(skill.Id);
                foreach (PageProfile page in book.Pages)
                {
                    if (!float.IsFinite(page.AverageHit)) broken.Add(skill.Id);
                    if (page.Page.Lingers && (page.Duration <= 0f || page.Length <= 0f)) broken.Add(skill.Id);
                    if (page.Page.Minion != null && Bestiary.Minion(page.Page.Minion) == null) broken.Add(skill.Id);
                    if (page.Page.Kind == PageKind.Area && page.Length <= 0f) broken.Add(skill.Id);
                }
            }
            report.Check("Every written book compiles to something usable", broken.Count == 0, string.Join(", ", broken));

            // A mark is a promise, so what an enemy does has to be known before it's chosen anyone.
            var unmarkable = Bestiary.Everyone.SelectMany(kind => kind.Books).Where(book => book.First.From is not (Start.You or Start.Cursor)).Select(book => book.Id).Distinct().ToList();
            report.Check("Everything an enemy uses starts from them or from where they're aiming", unmarkable.Count == 0, string.Join(", ", unmarkable));
        }

        private static void Rolling(Report report)
        {
            var random = new WorldRandom(99);
            var seen = new HashSet<string>();
            var rarity = new int[4];
            int odd = 0;
            int unknown = 0;
            for (int i = 0; i < 40000; i++)
            {
                int level = 1 + i % 60;
                Item item = ItemGenerator.Roll(level, random.Fork(i));
                rarity[(int)item.Rarity]++;
                if (string.IsNullOrEmpty(item.Name) || !float.IsFinite(item.WeaponDamage) || !float.IsFinite(item.Armour)) odd++;
                if (item.Slot == ItemSlot.MainHand && item.WeaponDamage <= 0f) odd++;
                foreach (ItemLine line in item.Lines)
                {
                    seen.Add(line.Affix);
                    Affix? affix = AffixCatalog.Find(line.Affix);
                    if (affix == null)
                    {
                        unknown++;
                        continue;
                    }
                    if (!affix.Slots.Contains(item.Slot)) odd++;
                    Tier top = affix.Tiers.Last(t => t.MinLevel <= level);
                    if (line.Value > top.High || line.Value < affix.Tiers[0].Low) odd++;
                }
                if (item.Lines.Select(l => l.Affix).Distinct().Count() != item.Lines.Count) odd++;
                foreach (Mod _ in item.Mods()) { }
            }
            report.Check("40000 items roll sensibly, at every level", odd == 0 && unknown == 0, $"{odd} odd, {unknown} unknown");
            report.Check("Every affix turns up", seen.Count == AffixCatalog.All.Count, $"{seen.Count} of {AffixCatalog.All.Count}");
            report.Note($"Normal {rarity[0]}, magic {rarity[1]}, rare {rarity[2]}, unique {rarity[3]}");
            report.Check("Rarities come out in order, with uniques scarce", rarity[0] > rarity[1] && rarity[1] > rarity[2] && rarity[2] > rarity[3] && rarity[3] is > 0 and < 1000);
        }

        // What a rarity is and where a stat can go are the rules of the whole game's loot, so these read the tables from outside rather than asking the generator what it meant.
        private static void Affixes(Report report)
        {
            report.Check("There are four rarities and no others", Enum.GetNames<Rarity>().SequenceEqual(new[] { "Normal", "Magic", "Rare", "Unique" }), string.Join(", ", Enum.GetNames<Rarity>()));

            List<Stat> onNeither = Enum.GetValues<Stat>().Where(stat => stat != Stat.Count && StatSides.Defensive.Contains(stat) == StatSides.Offensive.Contains(stat)).ToList();
            report.Check("Every stat is a prefix's or a suffix's, and never both", onNeither.Count == 0, string.Join(", ", onNeither));

            List<string> wrongSide = AffixCatalog.All.Where(affix => affix.Build(1f).Any(mod => (affix.Prefix ? StatSides.Offensive : StatSides.Defensive).Contains(mod.Stat))).Select(affix => affix.Id).ToList();
            report.Check("A prefix is always defensive and a suffix always offensive", wrongSide.Count == 0, string.Join(", ", wrongSide));
            report.Check("And each reads as what it is", AffixCatalog.All.All(affix => affix.Names.All(name => name.StartsWith("of ") != affix.Prefix)));

            ItemSlot[] slots = Enum.GetValues<ItemSlot>().Where(slot => slot < ItemSlot.Book).ToArray();
            int most = AffixCatalog.PerSide(Rarity.Rare);
            foreach (ItemSlot slot in slots)
            {
                bool Has(bool prefix) => AffixCatalog.All.Count(affix => affix.Prefix == prefix && affix.Slots.Contains(slot) && affix.Tiers[0].MinLevel <= 1) >= most;
                report.Check($"{slot} has enough of each to be rare at the first level", Has(true) && Has(false));
            }

            report.Check("Nothing can roll everywhere", AffixCatalog.All.All(affix => affix.Slots.Length < slots.Length), string.Join(", ", AffixCatalog.All.Where(affix => affix.Slots.Length >= slots.Length).Select(affix => affix.Id)));
            report.Check("Everything can roll somewhere", AffixCatalog.All.All(affix => affix.Slots.Length > 0 && affix.Slots.All(slot => slots.Contains(slot))));
            report.Check("Some stats go on several slots", AffixCatalog.All.Count(affix => affix.Slots.Length > 1) >= AffixCatalog.All.Count / 2);
            string PoolOf(ItemSlot slot) => string.Join(",", AffixCatalog.All.Where(affix => affix.Slots.Contains(slot)).Select(affix => affix.Id));
            report.Check("No two slots roll the same things", slots.Select(PoolOf).Distinct().Count() == slots.Length);

            bool OnlyOn(ItemSlot slot, params Stat[] stats) => AffixCatalog.All.Where(affix => affix.Build(1f).Any(mod => stats.Contains(mod.Stat))).All(affix => affix.Slots.SequenceEqual(new[] { slot }));
            report.Check("Resistances are for rings", OnlyOn(ItemSlot.Finger, Stat.Resistance));
            report.Check("Movement speed is for boots", OnlyOn(ItemSlot.Feet, Stat.MoveSpeed));
            report.Check("Attack speed is for gloves", OnlyOn(ItemSlot.Hands, Stat.AttackSpeed));

            var random = new WorldRandom(101);
            int wrongCount = 0, wrongPlace = 0, unnamed = 0, wordy = 0, rolled = 0;
            foreach (Rarity rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
            {
                foreach (ItemSlot slot in slots)
                {
                    for (int level = 1; level <= 60; level++)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            Item item = ItemGenerator.Forge(level, random.Fork(rolled++), rarity, i * 0.25f, slot);
                            List<Affix> lines = item.Lines.Select(line => AffixCatalog.Find(line.Affix)!).ToList();
                            int each = rarity switch { Rarity.Magic => 1, Rarity.Rare => 2, _ => 0 };
                            if (item.Rarity != rarity || lines.Count(affix => affix.Prefix) != each || lines.Count(affix => !affix.Prefix) != each) wrongCount++;
                            if (lines.Distinct().Count() != lines.Count || lines.Any(affix => !affix.Slots.Contains(slot))) wrongPlace++;
                            if (lines.Select(affix => affix.Prefix).SkipWhile(prefix => prefix).Any(prefix => prefix)) wrongPlace++;
                            if (each > 0 && (!item.Name.Contains(" of ") || !item.Name.EndsWith(lines.First(affix => !affix.Prefix).Names.First(name => item.Name.EndsWith(name))))) unnamed++;
                            if (item.PlainName() != item.Base) wordy++;
                        }
                    }
                }
            }
            report.Check($"{rolled} items, at every level on every slot: nothing on Normal, one of each on Magic, two of each on Rare", wrongCount == 0, $"{wrongCount}");
            report.Check("Each line once, on a slot that can have it, prefixes first", wrongPlace == 0, $"{wrongPlace}");
            report.Check("Named by what they are", unnamed == 0, $"{unnamed}");
            report.Check("But on the ground, only by what they're made from", wordy == 0, $"{wordy}");

            Item forged = ItemGenerator.Forge(20, random.Fork(rolled), Rarity.Rare, 0f, ItemSlot.Neck);
            string madeFrom = forged.Base;
            forged.Base = "";
            report.Check("Even if they're too old to say, which their name does", forged.PlainName() == madeFrom, forged.PlainName());
            forged.Name = "Cruel Keepsake of Haste";
            report.Check("And a base that's gone leaves the name it had, as a label can't be blank", forged.PlainName() == forged.Name, forged.PlainName());

            Item unique = UniqueCatalog.Make(UniqueCatalog.All.First(), 30, random.Fork(1));
            unique.Lines.Add(new ItemLine("health", 40f));
            Item folio = ItemGenerator.Folio(20, random.Fork(2), Rarity.Rare);
            Item starter = HeroState.CreateNew().Equipped[WornSlot.Body]!;
            report.Check("A name that wasn't rolled stays, whatever an old save has on it", unique.PlainName() == unique.Name && folio.PlainName() == folio.Name && starter.Name != starter.Base && starter.PlainName() == starter.Name, $"{unique.PlainName()}, {folio.PlainName()}, {starter.PlainName()}");

            int bought = 0;
            for (int i = 0; i < 4000; i++)
            {
                Item item = ItemGenerator.Roll(1 + i % 60, random.Fork(rolled + i), 0.4f);
                if (item.Unique == null && item.Lines.Count != AffixCatalog.PerSide(item.Rarity) * 2) bought++;
            }
            report.Check("What a merchant or a boss rolls is the same", bought == 0, $"{bought}");
            report.Check("A Unique has nothing rolled on it", UniqueCatalog.All.All(unique => UniqueCatalog.Make(unique, 30, random.Fork(1)).Lines.Count == 0));
            report.Check("A Unique can be anything on anything, and nothing stops it having what no affix gives", UniqueCatalog.All.All(unique => UniqueCatalog.Make(unique, 30, random.Fork(1)).Rarity == Rarity.Unique));
        }

        private static void Fixing(Report report)
        {
            var random = new WorldRandom(102);
            static int Side(Item item, bool prefix) => item.Lines.Count(line => AffixCatalog.Find(line.Affix)!.Prefix == prefix);
            static string Written(Item item) => item.Name + string.Join(",", item.Lines.Select(line => $"{line.Affix}={line.Value}"));

            int moved = 0;
            for (int i = 0; i < 3000; i++)
            {
                Item item = ItemGenerator.Roll(1 + i % 60, random.Fork(i));
                string before = Written(item);
                ItemGenerator.Fix(item);
                if (Written(item) != before) moved++;
            }
            report.Check("Anything rolled by these rules comes through a fix untouched", moved == 0, $"{moved}");

            var old = new Item { Name = "Keen Ring of Haste", Base = "Ring", Rarity = Rarity.Rare, Slot = ItemSlot.Finger, Level = 20, Seed = 5 };
            foreach (string id in new[] { "move-speed", "health", "attack-speed", "added-fire", "added-cold", "added-lightning", "fire-resistance", "fire-resistance", "not-an-affix" }) old.Lines.Add(new ItemLine(id, 9f));
            Item twin = new Item { Name = old.Name, Base = old.Base, Rarity = old.Rarity, Slot = old.Slot, Level = old.Level, Seed = old.Seed, Lines = old.Lines.Select(line => new ItemLine(line.Affix, line.Value)).ToList() };
            ItemGenerator.Fix(old);
            ItemGenerator.Fix(twin);
            string once = Written(old);
            ItemGenerator.Fix(old);

            report.Check("A rare from before this is a rare within the rules", old.Rarity == Rarity.Rare && Side(old, true) == 2 && Side(old, false) == 2 && old.Lines.All(line => AffixCatalog.Find(line.Affix)!.Slots.Contains(ItemSlot.Finger)), string.Join(", ", old.Lines.Select(line => line.Affix)));
            report.Check("What could stay did, and once only", old.Lines.Count(line => line.Affix == "fire-resistance" && line.Value == 9f) == 1 && old.Lines.Any(line => line.Affix == "added-fire") && old.Lines.Any(line => line.Affix == "added-cold") && !old.Lines.Any(line => line.Affix == "added-lightning"));
            report.Check("Comes out the same every time", Written(old) == Written(twin) && Written(old) == once);
            report.Check("And is called what it now is", old.Name.Contains("Ring") && !old.Name.Contains("Haste") && !old.Name.Contains("Keen"), old.Name);

            var plain = new Item { Name = "Ring", Base = "Ring", Rarity = Rarity.Normal, Slot = ItemSlot.Finger, Level = 5, Lines = { new ItemLine("gold-find", 9f) } };
            ItemGenerator.Fix(plain);
            report.Check("Nothing normal keeps a line", plain.Lines.Count == 0 && plain.Name == "Ring");

            var noSuch = new Item { Name = "Gone", Base = "Ring", Rarity = Rarity.Unique, Slot = ItemSlot.Finger, Level = 5, Unique = "not-a-unique" };
            ItemGenerator.Fix(noSuch);
            report.Check("Nothing says it's unique unless it really is", noSuch.Rarity == Rarity.Rare && noSuch.Unique == null && noSuch.Lines.Count == 4);

            Item real = UniqueCatalog.Make(UniqueCatalog.All[0], 20, random.Fork(9000));
            real.Lines.Add(new ItemLine("move-speed", 9f));
            string asItWas = Written(real);
            ItemGenerator.Fix(real);
            report.Check("But a real unique answers to none of this", Written(real) == asItWas && real.Rarity == Rarity.Unique);

            var fabled = new Item { Name = "Fabled Trigger Book", Slot = ItemSlot.Book, Rarity = Rarity.Unique, Level = 5, Held = new Item?[6] };
            var masterful = new Item { Name = "Masterful Area Page", Slot = ItemSlot.Page, Rarity = Rarity.Rare, Level = 5, Held = new Item?[4] };
            ItemGenerator.Fix(fabled);
            ItemGenerator.Fix(masterful);
            report.Check("What used to be better than rare in a Book is rare, and keeps its places", fabled.Rarity == Rarity.Rare && fabled.Places == 6 && masterful.Places == 4);
            report.Check("And has no word for what's gone", fabled.Name == "Gilded Trigger Book" && masterful.Name == "Gilded Area Page", $"{fabled.Name}, {masterful.Name}");

            SaveGame? save = SaveGame.Read(BeforeAffixes);
            report.Check("A save from before the four rarities loads", save != null);
            if (save == null) return;

            Item[] carried = save.Hero.Carried().ToArray();
            Item page = save.Hero.Bar[0]!.Held![0]!;
            report.Check("Every word that's gone means what it did, however deep", carried[0].Rarity == Rarity.Normal && carried[1].Rarity == Rarity.Unique && save.Hero.Bar[0]!.Rarity == Rarity.Normal && page.Rarity == Rarity.Rare && page.Held![0]!.Rarity == Rarity.Rare, $"{page.Rarity}, {page.Held?[0]?.Rarity}");
            report.Check("A six line rare is a four line rare", carried[2].Lines.Count == 4 && Side(carried[2], true) == 2 && Side(carried[2], false) == 2, string.Join(", ", carried[2].Lines.Select(line => line.Affix)));
            report.Check("It knows it's up to date", save.Version == SaveGame.CurrentVersion);

            string path = Path.Combine(Path.GetTempPath(), $"buildlab-affixes-{Environment.ProcessId}.json");
            File.WriteAllText(path, BeforeAffixes);
            SaveGame.PathOverride = path;
            SaveGame? loaded = SaveGame.Load();
            bool kept = File.Exists(path + ".before-affixes") && File.ReadAllText(path + ".before-affixes") == BeforeAffixes;
            loaded?.Store();
            string written = File.ReadAllText(path);
            SaveGame? again = SaveGame.Load();
            File.Delete(path + ".before-affixes");
            File.Delete(path);
            SaveGame.PathOverride = null;

            report.Check("Bringing it up to date keeps a copy of how it was", kept);
            report.Check("And what's written back has no word that's gone", !written.Contains("\"Common\"") && !written.Contains("\"Legendary\""));
            report.Check("Which comes back the same", again != null && Same(loaded!.Hero, again.Hero));
        }

        // As saved by 0.1.10: rarities by their old names, a masterwork, and a book with what's better than rare inside it.
        private const string BeforeAffixes = """
            {
              "Version": 7,
              "Hero": {
                "Level": 30, "Gold": 20, "Talents": [],
                "Bar": [ { "Name": "Worn Book", "Slot": "Book", "Rarity": "Common", "Level": 1, "Seed": 7, "Lines": [],
                           "Held": [ { "Name": "Fabled Area Page", "Slot": "Page", "Rarity": "Legendary", "Level": 9, "Seed": 8, "Lines": [],
                                       "Page": { "Kind": "Area" },
                                       "Held": [ { "Name": "Sticker of Might", "Slot": "Sticker", "Rarity": "Legendary", "Level": 9, "Seed": 9, "Lines": [],
                                                   "Sticker": { "Good": "Potency", "GoodBy": 30, "Bad": "Cost", "BadBy": 10 } } ] } ] },
                         null, null, null, null, null ],
                "Inventory": [
                  { "Name": "Longsword", "Base": "Longsword", "Slot": "MainHand", "Rarity": "Common", "Level": 9, "Seed": 10, "WeaponDamage": 20, "Lines": [] },
                  { "Name": "Two Coins", "Base": "Ring", "Slot": "Finger", "Rarity": "Legendary", "Level": 6, "Seed": 11, "Lines": [], "Unique": "two-coins" },
                  { "Name": "Sharp Plate Harness of the Ox", "Base": "Plate Harness", "Slot": "Body", "Rarity": "Rare", "Level": 30, "Seed": 12, "Armour": 40, "Lines": [
                    { "Affix": "damage", "Value": 40 }, { "Affix": "armour", "Value": 30 }, { "Affix": "armour-percent", "Value": 30 }, { "Affix": "health", "Value": 90 },
                    { "Affix": "strength", "Value": 20 }, { "Affix": "fire-resistance", "Value": 20 } ] }
                ],
                "Equipped": { }
              },
              "WorldSeed": 7, "WorldLevel": 3
            }
            """;

        private static void Uniques(Report report)
        {
            report.Note($"{UniqueCatalog.All.Count} uniques");
            report.Check("There are plenty", UniqueCatalog.All.Count >= 140);
            report.Check("Each is built on something that exists", UniqueCatalog.All.All(u => ItemGenerator.HasBase(u.Base)), string.Join(", ", UniqueCatalog.All.Where(u => !ItemGenerator.HasBase(u.Base)).Select(u => u.Base)));
            report.Check("Each has a line of lore, something it gives and something it costs", UniqueCatalog.All.All(u => u.Lore.Length > 0 && u.Boon.Length > 0 && u.Burden.Length > 0), string.Join(", ", UniqueCatalog.All.Where(u => u.Boon.Length == 0 || u.Burden.Length == 0).Select(u => u.Id)));
            report.Check("And everything on it is one or the other", UniqueCatalog.All.All(u => u.Mods.SequenceEqual(u.Boon.Concat(u.Burden))));

            // A price nobody notices is no price. Each line of what a unique costs has to make something worse, by enough to matter, or be a rule nobody would want.
            var free = UniqueCatalog.All.Where(u => !u.Burden.All(Hurts)).Select(u => $"{u.Id}: {string.Join(" / ", u.Burden.Where(mod => !Hurts(mod)).Select(mod => mod.Describe()))}").ToList();
            report.Check("Everything a unique costs really costs", free.Count == 0, string.Join(" | ", free.Take(10)));
            report.Check("And nothing it gives is a cost in disguise", UniqueCatalog.All.All(u => !u.Boon.Any(mod => mod.Kind == ModKind.Rule && ModText.Of(mod.Rule)?.Hurts == true)), string.Join(", ", UniqueCatalog.All.Where(u => u.Boon.Any(mod => mod.Kind == ModKind.Rule && ModText.Of(mod.Rule)?.Hurts == true)).Select(u => u.Id)));

            // What the tree already gives is no reason to want an item.
            var keystoneRules = TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone).SelectMany(n => n.Mods).Where(mod => mod.Kind == ModKind.Rule).Select(mod => mod.Rule).ToHashSet();
            var borrowed = UniqueCatalog.All.Where(u => u.Boon.Any(mod => mod.Kind == ModKind.Rule && keystoneRules.Contains(mod.Rule))).Select(u => $"{u.Id} ({string.Join(", ", u.Boon.Where(mod => mod.Kind == ModKind.Rule && keystoneRules.Contains(mod.Rule)).Select(mod => mod.Rule))})").ToList();
            report.Check("No unique gives what a keystone gives", borrowed.Count == 0, string.Join(", ", borrowed));

            var random = new WorldRandom(5);
            foreach (ItemSlot slot in Enum.GetValues<ItemSlot>().Where(slot => slot < ItemSlot.Book))
            {
                report.Check($"There's a unique for {slot}", UniqueCatalog.All.Any(u => ItemGenerator.Plain(u.Base, 1, random).Slot == slot));
            }

            // Truly one of a kind: something on it that nothing else worn can do, which no affix can roll and no other unique has. What it costs can be what makes it so.
            var rollable = new HashSet<Stat>(AffixCatalog.All.SelectMany(affix => affix.Build(1f)).Where(mod => mod.Kind != ModKind.Rule).Select(mod => mod.Stat));
            var holders = new Dictionary<string, int>();
            foreach (string mechanic in UniqueCatalog.All.SelectMany(u => u.Mods.SelectMany(Mechanics).Distinct())) holders[mechanic] = holders.GetValueOrDefault(mechanic) + 1;
            List<string> ordinary = UniqueCatalog.All.Where(u => !u.Mods.SelectMany(Mechanics).Any(mechanic => holders[mechanic] == 1)).Select(u => u.Id).ToList();
            report.Check("Each one does something no other item can", ordinary.Count == 0, string.Join(", ", ordinary));

            IEnumerable<string> Mechanics(Mod mod)
            {
                switch (mod.Kind)
                {
                    case ModKind.Rule:
                        yield return mod.Rule.ToString();
                        yield break;
                    case ModKind.Grant:
                        yield return $"Grant {mod.Grants}";
                        yield break;
                    case ModKind.Divert or ModKind.Echo:
                        yield return $"{mod.Kind} {mod.Stat} {mod.Into}";
                        yield break;
                    case ModKind.Trigger:
                        yield return $"{mod.On} {mod.Book} {mod.Gift} {mod.Spends}";
                        yield break;
                }

                bool narrowed = mod.Per != Per.None || mod.Need != Condition.None || mod.Foe != Condition.None || (mod.Scope & (Tags.Opening | Tags.Turned | Tags.Closing | Tags.Wild | Tags.Falling)) != 0;
                // More of something and less of it are opposites, and no two items share either.
                if (narrowed || mod.Kind == ModKind.Override || !rollable.Contains(mod.Stat)) yield return $"{mod.Stat} {mod.Kind} {MathF.Sign(mod.Value)} {mod.Scope} {mod.From} {mod.To} {mod.Need} {mod.Without} {mod.Foe} {mod.Per}";
            }

            Item shell = UniqueCatalog.Make(UniqueCatalog.Find("drowned-king")!, 20, random);
            var hero = HeroState.CreateNew();
            hero.Pickup(shell);
            hero.Equip(shell);
            hero.Talents.AddRange(TalentTree.PathTo(hero, TalentTree.Find("key.tidewalker")!.Index).Select(i => TalentTree.Nodes[i].Id));
            hero.Level = 40;
            ActorSheet sheet = HeroBuild.Compile(hero).SheetFor(Condition.None);
            float dry = new Loadout(40, HeroBuild.WeaponOf(hero), TalentTree.Mods(hero), new[] { Shelf.Strike }).SheetFor(Condition.None).Profile(0).First.AverageHit;
            report.Check("A unique can change what another talent means: the Drowned King's shell keeps a Tidewalker in the water", sheet.Preview(Shelf.Strike).First.AverageHit > dry * 1.7f, $"{sheet.Preview(Shelf.Strike).First.AverageHit} against {dry}");
        }

        // Which stats a build wants less of. Everything else, more is better.
        private static readonly Stat[] Unwanted = { Stat.DamageTaken, Stat.BookCost, Stat.AilmentTaken, Stat.BarrierDelay, Stat.DodgeCooldown, Stat.HealthDrain };

        // Whether a line makes whoever wears it worse off, by enough that they'd notice.
        private static bool Hurts(Mod mod)
        {
            bool unwanted = Unwanted.Contains(mod.Stat);
            switch (mod.Kind)
            {
                case ModKind.Rule:
                    return ModText.Of(mod.Rule)?.Hurts == true;
                case ModKind.Override:
                    return !unwanted;
                case ModKind.Flat or ModKind.Increased or ModKind.More:
                    if (mod.Value * (unwanted ? 1f : -1f) <= 0f) return false;
                    float size = mod.Per != Per.None ? mod.PerCap : MathF.Abs(mod.Value);
                    float least = mod.Kind != ModKind.Flat ? 15f : mod.Stat switch
                    {
                        Stat.Resistance => 20f,
                        Stat.MaxResistance => 10f,
                        Stat.Stealth => 30f,
                        Stat.HealthDrain => 1f,
                        _ => 15f,
                    };
                    return size >= least;
                default:
                    return false;
            }
        }

        private static bool Same(HeroState a, HeroState b)
        {
            bool Items(IEnumerable<Item> x, IEnumerable<Item> y)
            {
                List<Item> left = x.ToList();
                List<Item> right = y.ToList();
                if (left.Count != right.Count) return false;
                for (int i = 0; i < left.Count; i++)
                {
                    if (left[i].Name != right[i].Name || left[i].Seed != right[i].Seed || left[i].Base != right[i].Base || left[i].Unique != right[i].Unique || left[i].Kind != right[i].Kind || left[i].WeaponDamage != right[i].WeaponDamage || left[i].Evasion != right[i].Evasion || left[i].Barrier != right[i].Barrier) return false;
                    if (left[i].Lines.Count != right[i].Lines.Count) return false;
                    for (int j = 0; j < left[i].Lines.Count; j++)
                    {
                        if (left[i].Lines[j].Affix != right[i].Lines[j].Affix || left[i].Lines[j].Value != right[i].Lines[j].Value) return false;
                    }
                }
                return true;
            }

            return a.Level == b.Level && a.Gold == b.Gold && a.Talents.SequenceEqual(b.Talents)
                && a.Bar.Select(Written).SequenceEqual(b.Bar.Select(Written)) && a.Carried().Select(Written).SequenceEqual(b.Carried().Select(Written))
                && Items(a.Carried(), b.Carried()) && Items(a.Equipped.OrderBy(p => p.Key).Select(p => p.Value), b.Equipped.OrderBy(p => p.Key).Select(p => p.Value));
        }

        // All that's in a book, as text, so that two can be compared.
        private static string Written(Item? item) => item?.Held == null ? "" : System.Text.Json.JsonSerializer.Serialize(item);

        private static void Wires(Report report)
        {
            var random = new WorldRandom(8);
            HeroState hero = Hero(30, "pyre.kindled-bolt", "key.slow-burn");
            hero.Bar[3] = ItemGenerator.Copied(Shelf.Meteor.Source!, "Meteor", 7);
            hero.Equipped[WornSlot.MainHand] = UniqueCatalog.Make(UniqueCatalog.Find("ashen-promise")!, 20, random);
            for (int i = 0; i < 30; i++) hero.Pickup(ItemGenerator.Roll(30, random.Fork(i), 2f));

            var writer = new BitWriter();
            Protocol.WriteHero(writer, hero);
            HeroState back = Protocol.ReadHero(new BitReader(writer.ToArray()));
            report.Check("A hero comes through the wire exactly", Same(hero, back));
            report.Note($"A full hero with 30 items is {writer.ByteLength} bytes");

            ActorSheet here = HeroBuild.Compile(hero).SheetFor(Condition.None);
            ActorSheet there = HeroBuild.Compile(back).SheetFor(Condition.None);
            report.Check("And adds up to exactly the same on the other side", here.MaxHealth == there.MaxHealth && here.Preview(Shelf.Firebolt).First.AverageHit == there.Preview(Shelf.Firebolt).First.AverageHit);

            var save = new SaveGame { Hero = hero, WorldSeed = 4 };
            string path = Path.Combine(Path.GetTempPath(), $"buildlab-{Environment.ProcessId}.json");
            SaveGame.PathOverride = path;
            save.Store();
            SaveGame? loaded = SaveGame.Load();
            File.Delete(path);
            SaveGame.PathOverride = null;
            report.Check("And through a save", loaded != null && Same(hero, loaded.Hero));

            var forged = new BitWriter();
            forged.WriteString("Forged");
            forged.WriteByte((byte)ItemSlot.Finger);
            forged.WriteByte((byte)Rarity.Rare);
            forged.WriteVarInt(1);
            forged.WriteByte(0);
            forged.WriteVarULong(7);
            forged.WriteString("not-a-base");
            for (int i = 0; i < 4; i++) forged.WriteFloat(0f);
            forged.WriteBool(true);
            forged.WriteString("not-a-unique");
            forged.WriteCount(1);
            forged.WriteString("not-an-affix");
            forged.WriteFloat(99999f);
            forged.WriteBool(false);
            forged.WriteBool(false);
            forged.WriteByte(0);
            forged.WriteBool(false);
            forged.WriteCount(0);
            List<Item> arrived = new List<Item> { Protocol.ReadItem(new BitReader(forged.ToArray())) };
            report.Check("An affix, a unique or a base that doesn't exist does nothing, whatever a stranger sends", arrived.Count == 1 && arrived[0].Unique == null && arrived[0].Lines.Count == 0 && arrived[0].Base.Length == 0);
        }

        // As saved by 0.1.3: attributes, and items with a stat and a value. The sword is one someone's edited by hand, which is a thing people do.
        private const string OldSave = """
            {
              "Hero": {
                "Level": 3, "Experience": 76, "Gold": 368, "Might": 5, "Finesse": 5, "Spirit": 5, "UnspentPoints": 6, "Flasks": 0, "WorldsCleared": 0,
                "Inventory": [
                  { "Name": "Sturdy Hood of the Ox", "Slot": "Head", "Rarity": "Magic", "Level": 2, "WeaponDamage": 0, "Armour": 3.2673187,
                    "Modifiers": [ { "Stat": "Armour", "Value": 4.534136 }, { "Stat": "Health", "Value": 10.840562 } ], "SellValue": 28, "BuyValue": 112 },
                  { "Name": "Ring of Everything", "Slot": "Ring", "Rarity": "Legendary", "Level": 9, "WeaponDamage": 0, "Armour": 0,
                    "Modifiers": [ { "Stat": "Damage", "Value": 3 }, { "Stat": "DamagePercent", "Value": 20 }, { "Stat": "CritChance", "Value": 2.5 }, { "Stat": "CritDamage", "Value": 15 },
                      { "Stat": "AttackSpeed", "Value": 6 }, { "Stat": "MoveSpeed", "Value": 8 }, { "Stat": "Regeneration", "Value": 1.5 }, { "Stat": "LifeOnHit", "Value": 2 },
                      { "Stat": "GoldFind", "Value": 30 }, { "Stat": "ItemFind", "Value": 12 } ], "SellValue": 1, "BuyValue": 4 }
                ],
                "Equipped": {
                  "Weapon": { "Name": "Rusted Blade", "Slot": "Weapon", "Rarity": "Common", "Level": 1, "WeaponDamage": 700, "Armour": 0, "Modifiers": [], "SellValue": 4, "BuyValue": 16 },
                  "Body": { "Name": "Travelling Tunic", "Slot": "Body", "Rarity": "Common", "Level": 1, "WeaponDamage": 0, "Armour": 6, "Modifiers": [], "SellValue": 4, "BuyValue": 16 }
                },
                "ExperienceToNext": 776
              },
              "WorldSeed": 12345, "WorldArchetype": null, "WorldLevel": 2,
              "Sites": [ { "Index": 3, "Discovered": true, "Cleared": false, "Kindled": true, "Rewarded": false, "Dead": [1, 2], "Opened": [] } ],
              "Objectives": [ 1, 0 ], "PlayerX": 100, "PlayerZ": 200, "Health": 90, "RespawnX": 100, "RespawnZ": 200
            }
            """;

        // An enemy's rarity is the same four as an item's, and means the same thing: how much more there is to it.
        private static void EnemyRarities(Report report)
        {
            report.Section("Enemy rarity");

            Rarity[] rarities = Enum.GetValues<Rarity>();
            EnemyPower[] powers = rarities.Select(Bestiary.PowerOf).ToArray();
            bool climbing = true;
            for (int i = 1; i < powers.Length; i++) climbing &= powers[i].Health > powers[i - 1].Health && powers[i].Damage > powers[i - 1].Damage && powers[i].Traits > powers[i - 1].Traits && powers[i].Scale > powers[i - 1].Scale;
            report.Check("Each rarity is harder to kill, hits harder, is bigger and does more than the last", climbing);
            report.Check("Nothing normal has any traits", powers[0].Traits == 0 && powers[0].Health == 1f && powers[0].Damage == 1f);

            var random = new WorldRandom(201);
            int wrong = 0;
            foreach (Rarity rarity in rarities)
            {
                for (int i = 0; i < 400; i++)
                {
                    int traits = Bestiary.Fill(Bestiary.NoTraits, rarity, random.Fork(i));
                    if (Bestiary.TraitsOf(traits).Count() != Bestiary.PowerOf(rarity).Traits) wrong++;
                }
            }
            report.Check("Each comes out with exactly as many traits as it should", wrong == 0, $"{wrong}");
            int shared = Bestiary.Fill(Bestiary.Trait(3), Rarity.Rare, random.Fork("shared"));
            report.Check("A leader keeps the trait it shares with its pack", (shared & Bestiary.Trait(3)) != 0 && Bestiary.TraitsOf(shared).Count() == 2);

            List<string> idle = Bestiary.Traits.Where(trait => trait.Book.First.From is not (Start.You or Start.Cursor)).Select(trait => trait.Name).ToList();
            report.Check("Everything a trait gives starts from them or from where they're aiming", idle.Count == 0, string.Join(", ", idle));
            ActorSheet plain = new Loadout(20, new WeaponProfile(10f, 10f, 0.6f, 0f, Tags.Sword), new[] { Flat(Stat.Health, 100f), Flat(Stat.Mana, 1000f) }, Bestiary.Traits.Select(trait => trait.Book).Distinct()).SheetFor(Condition.None);
            List<string> unusable = Enumerable.Range(0, Bestiary.Traits.Select(trait => trait.Book).Distinct().Count()).Select(i => plain.Profile(i)).Where(book => (book.First.Tags & Tags.Triggered) != 0 || book.Sign.Form is PageForm.Buff or PageForm.Summon or PageForm.Leap).Select(book => book.Book.Name).ToList();
            report.Check("And is something an enemy will really do", unusable.Count == 0, string.Join(", ", unusable));
            report.Check("Each trait gives something different", Bestiary.Traits.Select(trait => trait.Book).Distinct().Count() == Bestiary.Traits.Length);

            var bestiary = new Bestiary();
            int Books(Rarity rarity, int traits) => bestiary.Shared(Bestiary.Outlaw, 10, rarity, traits).Books.Length;
            int swift = Bestiary.Trait(Array.FindIndex(Bestiary.Traits, trait => trait.Name == "Swift"));
            int burning = Bestiary.Trait(Array.FindIndex(Bestiary.Traits, trait => trait.Name == "Burning"));
            report.Check("The more traits, the more it can do", Books(Rarity.Normal, 0) < Books(Rarity.Magic, swift) && Books(Rarity.Magic, swift) < Books(Rarity.Rare, swift | burning));
            report.Check("What a trait gave it comes first, and is counted", bestiary.Shared(Bestiary.Outlaw, 10, Rarity.Magic, swift).Books[0] == Shelf.Knives && Bestiary.AbilitiesOf(Bestiary.Outlaw, swift) == 1);
            int fearsome = Bestiary.Trait(Array.FindIndex(Bestiary.Traits, trait => trait.Name == "Fearsome"));
            report.Check("What it could already do isn't a new thing to do", Bestiary.AbilitiesOf(Bestiary.Brute, fearsome) == 0 && bestiary.Shared(Bestiary.Brute, 10, Rarity.Magic, fearsome).Books.Length == Bestiary.Brute.Books.Length);
            report.Check("Two of one kind and rarity with different traits don't share numbers", !ReferenceEquals(bestiary.Shared(Bestiary.Outlaw, 10, Rarity.Magic, swift), bestiary.Shared(Bestiary.Outlaw, 10, Rarity.Magic, burning)));
            report.Check("A name says all of what it is", Bestiary.NameOf(Bestiary.Outlaw, burning | swift) == "Burning Swift Outlaw" && Bestiary.NameOf(Bestiary.Outlaw, Bestiary.NoTraits) == "Outlaw");

            ActorSheet Sheet(Rarity rarity) => bestiary.Shared(Bestiary.Outlaw, 10, rarity, Bestiary.NoTraits).SheetFor(Condition.None);
            report.Check("Rarity reaches the sheet", Sheet(Rarity.Magic).MaxHealth > Sheet(Rarity.Normal).MaxHealth && Sheet(Rarity.Rare).MaxHealth > Sheet(Rarity.Magic).MaxHealth);

            Loadout boss = Bestiary.Boss(Bestiary.Warlord, 12, swift | burning, new WorldRandom(4), out _, out _);
            // What it wears can set books off as well, which aren't what it does.
            report.Check("A boss can do what its traits give it as well", boss.Books.Count(book => (book.First.Tags & Tags.Triggered) == 0) == Bestiary.Warlord.Books.Length + 2 && boss.Books[0] != Shelf.Whirl);

            bool apart = true;
            foreach (Rarity rarity in rarities)
            {
                var one = new Dino_Defenders.UI.InterfaceSettings { NormalMarks = rarity == Rarity.Normal, MagicMarks = rarity == Rarity.Magic, RareMarks = rarity == Rarity.Rare, UniqueMarks = rarity == Rarity.Unique };
                apart &= rarities.All(other => one.Marks(other) == (other == rarity));
            }
            report.Check("Each rarity's marks go by its own setting and nobody else's", apart);

            var settings = new Dino_Defenders.UI.InterfaceSettings();
            report.Check("Normal enemies' attacks aren't marked until a player asks", !settings.Marks(Rarity.Normal) && settings.Marks(Rarity.Magic) && settings.Marks(Rarity.Rare) && settings.Marks(Rarity.Unique));
            settings.NormalMarks = true;
            settings.UniqueMarks = false;
            report.Check("Each is a setting of its own", settings.Marks(Rarity.Normal) && settings.Marks(Rarity.Magic) && !settings.Marks(Rarity.Unique));
            string kept = System.Text.Json.JsonSerializer.Serialize(settings);
            var back = System.Text.Json.JsonSerializer.Deserialize<Dino_Defenders.UI.InterfaceSettings>(kept)!;
            report.Check("And is kept", back.Marks(Rarity.Normal) && !back.Marks(Rarity.Unique));
            var older = System.Text.Json.JsonSerializer.Deserialize<Dino_Defenders.UI.InterfaceSettings>("{ \"PoolsOverHero\": false }")!;
            report.Check("Settings from before this have the same start as anyone", !older.PoolsOverHero && !older.Marks(Rarity.Normal) && older.Marks(Rarity.Rare));
        }

        private static void PackContent(Report report)
        {
            report.Section("Packs");
            var recipes = Warbands.All().ToList();
            var named = new HashSet<EnemyKind>(recipes.SelectMany(r => r.Leaders.Concat(r.Core).Concat(r.Support)));
            report.Check("Everyone in a pack is someone the bestiary knows", named.All(k => Bestiary.Everyone.Contains(k)));
            report.Check("Every kind has a name of its own", Bestiary.Everyone.Select(k => k.Id).Distinct().Count() == Bestiary.Everyone.Length);
            report.Check("Every pack has a core to it", recipes.All(r => r.Core.Length > 0));
            report.Check("A pack has support if it says it does", recipes.All(r => (r.SupportShare > 0f) == (r.Support.Length > 0)));
            report.Check("Anything that fights can do so", Bestiary.Everyone.All(k => k.Books.Length > 0 && k.Books.Any(book => book.First.Hostile)));

            // What a pack is mostly made of goes down quickly. That's what clearing one is, and what a leader isn't.
            report.Check("The core of a pack is less than one ordinary enemy", recipes.All(r => r.Core.Min(k => k.Health) <= 1f && r.Core.Average(k => k.Health) < 1f));
            report.Check("What leads it is more than what follows", recipes.Where(r => r.Leaders.Length > 0).All(r => r.Leaders.Average(k => k.Health) > r.Core.Average(k => k.Health)));

            var looks = new[] { "bandit", "cutthroat", "brute", "skeleton", "skeleton:bow", "revenant", "wolf", "direwolf", "boar", "bear", "warlord", "reich:legionary", "reich:marksman", "reich:wrangler" };
            bool Drawn(EnemyKind k) => looks.Contains(k.Look) || k.Look.StartsWith("robed:") || k.Look.StartsWith("acolyte:") || Dino_Engine.Modelling.Procedural.Saurian.SaurianSpec.Parse(k.Look) != null;
            report.Check("Everything is drawn as something", Bestiary.Everyone.All(Drawn), string.Join(" ", Bestiary.Everyone.Where(k => !Drawn(k)).Select(k => k.Look)));

            // A tyrant is only ever what holds a land, and each land's story names one the bestiary has.
            report.Check("Every land's tyrant is a kind there is", Enumerable.Range(0, 200).All(seed => Bestiary.Find(Chronicle.BossKindOf(seed)) != null));

            report.Check("Even the least kill is worth something", Bestiary.ExperienceAt(1, 0.42f) >= 1);
            report.Check("A leader is worth more than a follower", Bestiary.ExperienceAt(5, 2.6f) > Bestiary.ExperienceAt(5, 0.5f));
        }

        private static void OldSaves(Report report)
        {
            SaveGame? save = SaveGame.Read(OldSave);
            report.Check("A save from before talents loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("With the hero as they were", hero.Level == 3 && hero.Gold == 368 && hero.Experience == 76 && hero.Carried().Count() == 2 && hero.Equipped.Count == 2);
            report.Check("Waking in the Haven, as the world it was in isn't a land", !save.InLand && save.Sites.Count == 0 && save.Objectives.Count == 0);
            report.Check("Every point goes back into talents", hero.Talents.Count == 0 && hero.UnspentPoints == 7, $"{hero.UnspentPoints}");
            report.Check("A sword someone edited by hand stays as it was", hero.Equipped[WornSlot.MainHand].WeaponDamage == 700f);

            Item hood = hero.Inventory[0]!;
            report.Check("Item lines come across, value for value", hood.Lines.Count == 2 && hood.Lines[0].Affix == "armour" && MathF.Abs(hood.Lines[0].Value - 4.534136f) < 1e-4f);
            report.Check("With no second prefix on something magic, and a suffix to make up for it", !AffixCatalog.Find(hood.Lines[1].Affix)!.Prefix, hood.Lines[1].Affix);

            // Nothing rolled can have ten lines any more. A unique can have anything, which is the way to see all of them come across.
            HeroState untouched = SaveGame.Read(OldSave.Replace("\"Name\": \"Ring of Everything\",", "\"Name\": \"Ring of Everything\", \"Unique\": \"two-coins\","))!.Hero;
            report.Check("Every old stat has somewhere to go", untouched.Inventory[1]!.Lines.Count == 10 && untouched.Inventory[1]!.Lines.All(l => AffixCatalog.Find(l.Affix) != null));

            ActorSheet sheet = HeroBuild.Compile(untouched).SheetFor(Condition.None);
            untouched.Equip(1);
            ActorSheet ringed = HeroBuild.Compile(untouched).SheetFor(Condition.None);
            report.Check("And mean what they meant", ringed.MoveSpeed > sheet.MoveSpeed && ringed.HealthRegen > sheet.HealthRegen && ringed.ItemRarity == 12f && ringed.GoldFind == 30f && ringed.Preview(Shelf.Strike).First.AverageHit > sheet.Preview(Shelf.Strike).First.AverageHit);
            report.Check("An old ring of everything is a rare ring of what a ring can have", hero.Inventory[1]!.Rarity == Rarity.Rare && hero.Inventory[1]!.Lines.Count == 4 && hero.Inventory[1]!.Lines.Any(l => l.Affix == "gold-find" && l.Value == 30f), string.Join(", ", hero.Inventory[1]!.Lines.Select(l => l.Affix)));
            report.Check("A hero from before books has one on the bar, and it's the plain one", hero.Bar[0] is { IsBook: true } && hero.Bar.Skip(1).All(s => s == null));

            string path = Path.Combine(Path.GetTempPath(), $"buildlab-old-{Environment.ProcessId}.json");
            File.WriteAllText(path, OldSave);
            SaveGame.PathOverride = path;
            SaveGame? upgraded = SaveGame.Load();
            upgraded?.Store();
            bool kept = File.Exists(path + ".before-talents") && File.ReadAllText(path + ".before-talents") == OldSave;
            string written = File.ReadAllText(path);

            // Nothing in the game writes an old save again, but a person does: the launcher's backups are the way back, and they're older than this copy.
            File.WriteAllText(path, OldSave);
            File.WriteAllText(path + ".before-talents", "The first copy");
            SaveGame? restored = SaveGame.Load();
            bool firstCopy = File.ReadAllText(path + ".before-talents") == "The first copy";

            File.Delete(path + ".before-talents");
            Directory.CreateDirectory(path + ".before-talents");
            SaveGame? uncopied = SaveGame.Load();
            Directory.Delete(path + ".before-talents");

            SaveGame.PathOverride = null;
            File.Delete(path);
            report.Check("Bringing a save up to date keeps a copy of how it was", kept);
            report.Check("And what's written back is the new kind", written.Contains($"\"Version\": {SaveGame.CurrentVersion}") && written.Contains("\"Lines\"") && !written.Contains("\"Modifiers\""));
            report.Check("An old save put back by hand still loads", restored != null && restored.Hero.Level == upgraded?.Hero.Level);
            report.Check("And leaves the first copy of it alone", firstCopy);
            report.Check("Not managing to keep a copy doesn't cost the hero", uncopied != null);

            string? damaged = OldSave.Replace("\"Stat\": \"Armour\"", "\"Stat\": \"SomethingWeNeverHad\"");
            SaveGame? survived = SaveGame.Read(damaged);
            report.Check("A stat nobody has heard of costs a line, not the hero", survived != null && survived.Hero.Inventory[0]!.Lines[0].Affix == "health");

            string? folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string backups = Path.Combine(folder, "DinoDefenders", "backups");
            if (!Directory.Exists(backups)) return;

            int read = 0;
            foreach (string file in Directory.GetFiles(backups, "*.json"))
            {
                SaveGame? real = SaveGame.Read(File.ReadAllText(file));
                report.Check($"A real save, {Path.GetFileName(file)}, loads", real != null && real.Hero.Level >= 1);
                if (real != null)
                {
                    ActorSheet check = HeroBuild.Compile(real.Hero).SheetFor(Condition.None);
                    report.Check("  and compiles", check.MaxHealth > 0f && float.IsFinite(check.Preview(Shelf.Strike).First.AverageHit));
                }
                read++;
            }
            report.Note($"{read} real saves from past releases");
        }

        private static void Symmetry(Report report)
        {
            var random = new WorldRandom(3);
            WeaponProfile sword = new WeaponProfile(20f, 20f, 0.6f, 0f, Tags.Sword);
            Mod[] mods = { Flat(Stat.Health, 500f), Inc(Stat.Damage, 40f), Convert(DamageType.Physical, DamageType.Fire, 50f), Flat(Stat.Armour, 200f), Resist(Tags.Fire, 30f) };
            var hero = new Actor(Team.Heroes, new Loadout(10, sword, mods, new[] { Shelf.Strike }), Vector2.Zero, 1f);
            var enemy = new Actor(Team.Enemies, new Loadout(10, sword, mods, new[] { Shelf.Strike }), Vector2.Zero, 1f);

            float Blow(Actor from, Actor to)
            {
                var luck = new WorldRandom(77);
                HitPacket hit = default;
                Hits.Roll(from.Sheet.Profile(0).First, Condition.None, luck, ref hit);
                return Hits.Mitigate(to.Sheet, ref hit);
            }

            report.Check("The same modifiers on either side deal the same blow and take it the same way", Blow(hero, enemy) == Blow(enemy, hero), $"{Blow(hero, enemy)} and {Blow(enemy, hero)}");

            var bestiary = new Bestiary();
            Loadout first = bestiary.Shared(Bestiary.Outlaw, 5, Rarity.Normal, Bestiary.NoTraits);
            Loadout second = bestiary.Shared(Bestiary.Outlaw, 5, Rarity.Normal, Bestiary.NoTraits);
            report.Check("Enemies of a kind share one set of numbers", ReferenceEquals(first, second) && ReferenceEquals(first.SheetFor(Condition.None), second.SheetFor(Condition.Burning)));

            Loadout boss = Bestiary.Boss(Bestiary.Warlord, 12, Bestiary.NoTraits, new WorldRandom(4), out Item[] gear, out string? title);
            report.Check("A boss wears real gear, and walks the real tree", gear.Length > 0 && title != null, $"{gear.Length} items, {title}");
            report.Note($"This one: {title}, wearing {string.Join(", ", gear.Select(g => g.Name))}");

            int uniques = 0;
            int heroOnly = 0;
            for (int i = 0; i < 400; i++)
            {
                Bestiary.Boss(Bestiary.Warlord, 20, Bestiary.NoTraits, new WorldRandom(i), out Item[] worn, out _);
                if (worn.Any(g => g.Unique != null)) uniques++;
                if (worn.Any(g => g.Unique != null && UniqueCatalog.Find(g.Unique)!.HeroOnly)) heroOnly++;
            }
            report.Check("A good share of them are wearing something unique", uniques is > 80 and < 360, $"{uniques} of 400");
            report.Check("But never one that only means anything in a player's hands", heroOnly == 0, $"{heroOnly} of 400");

            int fair = 0;
            foreach (TalentNode keystone in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone))
            {
                Loadout load = new Loadout(20, sword, mods.Concat(keystone.Mods), new[] { Shelf.Strike, Shelf.Firebolt });
                ActorSheet sheet = load.SheetFor(Condition.None);
                if (float.IsFinite(sheet.Profile(0).First.AverageHit) && sheet.MaxHealth > 0f) fair++;
            }
            report.Check("Every keystone works on anyone", fair == TalentTree.Nodes.Count(n => n.Kind == TalentKind.Keystone));
        }
    }
}
