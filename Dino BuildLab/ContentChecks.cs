using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
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
            Rolling(report);
            Uniques(report);
            Wires(report);

            report.Section("Saves from before all this");
            OldSaves(report);

            report.Section("A hero and an enemy are the same thing");
            Symmetry(report);
        }

        private static void TreeShape(Report report)
        {
            TalentNode[] nodes = TalentTree.Nodes;
            int Count(TalentKind kind) => nodes.Count(n => n.Kind == kind);
            report.Note($"{nodes.Length} nodes: {Count(TalentKind.Small)} small, {Count(TalentKind.Notable)} notable, {Count(TalentKind.Skill)} skills, {Count(TalentKind.Keystone)} keystones, {Count(TalentKind.Gate)} gates");
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

            var skills = nodes.Where(n => n.Kind == TalentKind.Skill).ToList();
            int nearest = TalentContent.Sectors.Select((s, i) => skills.Where(n => n.Sector == i).Min(n => TalentTree.DepthOf[n.Index])).Max();
            report.Check("Every sector has a skill a new hero can reach", nearest <= new HeroState().TalentPoints, $"{nearest} points");
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
            report.Check("A new hero can take the first step", TalentTree.CanAllocate(fresh, Index("pyre.t0")));
            report.Check("But not a step further", !TalentTree.CanAllocate(fresh, Index("pyre.t1")));
            report.Check("Nor anything that's taken", !TalentTree.CanAllocate(Hero(5, "pyre.t0"), Index("pyre.t0")));

            HeroState path = Hero(10, "pyre.t2");
            report.Check("A node in the middle of a path can't be given back", !TalentTree.CanRefund(path, Index("pyre.t1")));
            report.Check("The end of it can", TalentTree.CanRefund(path, Index("pyre.t2")));
            report.Check("Nor can the root ever", !TalentTree.CanRefund(path, TalentTree.Root.Index));

            HeroState looped = Hero(20, "pyre.t2", "pyre.firebolt", "pyre.firebolt.0");
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

            HeroState ordered = Hero(40, "pyre.firebolt", "winter.frost-nova");
            var shuffled = new HeroState { Level = 40, Talents = ordered.Talents.OrderByDescending(t => t).ToList() };
            float a = HeroBuild.Compile(ordered).SheetFor(Condition.None).Profile(SkillCatalog.Firebolt)!.AverageHit;
            float b = HeroBuild.Compile(shuffled).SheetFor(Condition.None).Profile(SkillCatalog.Firebolt)!.AverageHit;
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
            foreach (SkillDefinition skill in SkillCatalog.All)
            {
                foreach (Mod mod in skill.Mods) yield return (skill.Id, mod);
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
            report.Check("Every skill the tree teaches has words to go with it", TalentTree.Nodes.Where(n => n.Kind == TalentKind.Skill).All(n => !string.IsNullOrEmpty(n.Lore)));
            report.Check("No two notables share a name", TalentTree.Nodes.Where(n => n.Kind is TalentKind.Notable or TalentKind.Keystone).Select(n => n.Name).Distinct().Count() == TalentTree.Nodes.Count(n => n.Kind is TalentKind.Notable or TalentKind.Keystone));
        }

        private static void Coverage(Report report)
        {
            var stats = new HashSet<Stat>();
            Rule rules = Rule.None;
            var conditions = Condition.None;
            var scopes = Tags.None;
            foreach ((_, Mod mod) in EveryMod())
            {
                if (mod.Kind <= ModKind.Override) stats.Add(mod.Stat);
                rules |= mod.Rule;
                conditions |= mod.Need | mod.Without | mod.Foe | mod.Grants;
                scopes |= mod.Scope;
            }

            var unused = Enum.GetValues<Stat>().Where(s => s != Stat.Count && !stats.Contains(s)).ToList();
            report.Check("Every stat can be had from something", unused.Count == 0, string.Join(", ", unused));

            var unusedRules = Enum.GetValues<Rule>().Where(r => r != Rule.None && (rules & r) == 0).ToList();
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
            var known = SkillCatalog.All.Where(s => !s.Hidden).ToList();
            report.Note($"{SkillCatalog.All.Count} skills, {known.Count} that can be learned");
            report.Check("Ids are all different", SkillCatalog.All.Select(s => s.Id).Distinct().Count() == SkillCatalog.All.Count);
            report.Check("Every skill that can be learned is taught somewhere", known.Where(s => s != SkillCatalog.Strike && s != SkillCatalog.Whirl).All(s => TalentTree.Nodes.Any(n => n.Mods.Any(m => m.Kind == ModKind.GrantSkill && m.Skill == s.Number))),
                string.Join(", ", known.Where(s => s != SkillCatalog.Strike && s != SkillCatalog.Whirl && !TalentTree.Nodes.Any(n => n.Mods.Any(m => m.Kind == ModKind.GrantSkill && m.Skill == s.Number))).Select(s => s.Id)));
            report.Check("Every one has words to go with it", known.All(s => s.Text.Length > 0));
            report.Check("Every skill is an attack or a spell", SkillCatalog.All.All(s => s.IsAttack != s.IsSpell));

            foreach (DamageType type in DamageKinds.All)
            {
                report.Check($"There's a spell for {type}", SkillCatalog.All.Any(s => s.IsSpell && !s.Hidden && s.Damage.Any(d => d.Type == type)));
            }

            foreach (SkillForm form in Enum.GetValues<SkillForm>())
            {
                report.Check($"Something has the form {form}", known.Any(s => s.Form == form));
            }

            ActorSheet sheet = new Loadout(1, WeaponProfile.Fists, new[] { Flat(Stat.Health, 100f) }, SkillCatalog.All).SheetFor(Condition.None);
            var broken = new List<string>();
            foreach (SkillDefinition skill in SkillCatalog.All)
            {
                SkillProfile profile = sheet.Profile(skill)!;
                if (!float.IsFinite(profile.UseTime) || profile.UseTime <= 0f || !float.IsFinite(profile.AverageHit)) broken.Add(skill.Id);
                if (skill.Form == SkillForm.Zone && (profile.Duration <= 0f || profile.Radius <= 0f)) broken.Add(skill.Id);
                if (skill.Form == SkillForm.Summon && Bestiary.Minion(skill.Minion) == null) broken.Add(skill.Id);
                if (skill.Form is SkillForm.Burst or SkillForm.Nova && profile.Radius <= 0f) broken.Add(skill.Id);
            }
            report.Check("Every skill compiles to something usable", broken.Count == 0, string.Join(", ", broken));
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
                if (item.Slot == ItemSlot.Weapon && item.WeaponDamage <= 0f) odd++;
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
            report.Note($"Common {rarity[0]}, magic {rarity[1]}, rare {rarity[2]}, unique {rarity[3]}");
            report.Check("Rarities come out in order, with uniques scarce", rarity[0] > rarity[1] && rarity[1] > rarity[2] && rarity[2] > rarity[3] && rarity[3] is > 0 and < 1000);
        }

        private static void Uniques(Report report)
        {
            report.Note($"{UniqueCatalog.All.Count} uniques");
            report.Check("There are plenty", UniqueCatalog.All.Count >= 25);
            report.Check("Each is built on something that exists", UniqueCatalog.All.All(u => ItemGenerator.HasBase(u.Base)), string.Join(", ", UniqueCatalog.All.Where(u => !ItemGenerator.HasBase(u.Base)).Select(u => u.Base)));
            report.Check("Each has a line of lore and something to do", UniqueCatalog.All.All(u => u.Lore.Length > 0 && u.Mods.Length > 0));

            var random = new WorldRandom(5);
            foreach (ItemSlot slot in Enum.GetValues<ItemSlot>())
            {
                report.Check($"There's a unique for {slot}", UniqueCatalog.All.Any(u => ItemGenerator.Plain(u.Base, 1, random).Slot == slot));
            }

            int changing = UniqueCatalog.All.Count(u => u.Mods.Any(m => m.Kind is ModKind.Rule or ModKind.Grant or ModKind.Divert or ModKind.Trigger or ModKind.GrantSkill || m.Stat == Stat.Convert || m.Per != Per.None || m.Need != Condition.None));
            report.Check("Most of them change how a hero plays, rather than only adding to it", changing * 4 >= UniqueCatalog.All.Count * 3, $"{changing} of {UniqueCatalog.All.Count}");

            Item shell = UniqueCatalog.Make(UniqueCatalog.Find("drowned-king")!, 20, random);
            var hero = HeroState.CreateNew();
            hero.Inventory.Add(shell);
            hero.Equip(shell);
            hero.Talents.AddRange(TalentTree.PathTo(hero, TalentTree.Find("key.tidewalker")!.Index).Select(i => TalentTree.Nodes[i].Id));
            hero.Level = 40;
            ActorSheet sheet = HeroBuild.Compile(hero).SheetFor(Condition.None);
            float dry = new Loadout(40, HeroBuild.WeaponOf(hero), TalentTree.Mods(hero), new[] { SkillCatalog.Strike }).SheetFor(Condition.None).Profile(0).AverageHit;
            report.Check("A unique can change what another talent means: the Drowned King's shell keeps a Tidewalker in the water", sheet.Profile(SkillCatalog.Strike)!.AverageHit > dry * 1.7f, $"{sheet.Profile(SkillCatalog.Strike)!.AverageHit} against {dry}");
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
                    if (left[i].Name != right[i].Name || left[i].Unique != right[i].Unique || left[i].Kind != right[i].Kind || left[i].WeaponDamage != right[i].WeaponDamage || left[i].Evasion != right[i].Evasion || left[i].Barrier != right[i].Barrier) return false;
                    if (left[i].Lines.Count != right[i].Lines.Count) return false;
                    for (int j = 0; j < left[i].Lines.Count; j++)
                    {
                        if (left[i].Lines[j].Affix != right[i].Lines[j].Affix || left[i].Lines[j].Value != right[i].Lines[j].Value) return false;
                    }
                }
                return true;
            }

            return a.Level == b.Level && a.Gold == b.Gold && a.Talents.SequenceEqual(b.Talents) && a.Bar.SequenceEqual(b.Bar)
                && Items(a.Inventory, b.Inventory) && Items(a.Equipped.OrderBy(p => p.Key).Select(p => p.Value), b.Equipped.OrderBy(p => p.Key).Select(p => p.Value));
        }

        private static void Wires(Report report)
        {
            var random = new WorldRandom(8);
            HeroState hero = Hero(30, "pyre.firebolt", "key.slow-burn");
            hero.Bar[3] = "firebolt";
            hero.Equipped[ItemSlot.Weapon] = UniqueCatalog.Make(UniqueCatalog.Find("ashen-promise")!, 20, random);
            for (int i = 0; i < 30; i++) hero.Inventory.Add(ItemGenerator.Roll(30, random.Fork(i), 2f));

            var writer = new BitWriter();
            Protocol.WriteHero(writer, hero);
            HeroState back = Protocol.ReadHero(new BitReader(writer.ToArray()));
            report.Check("A hero comes through the wire exactly", Same(hero, back));
            report.Note($"A full hero with 30 items is {writer.ByteLength} bytes");

            ActorSheet here = HeroBuild.Compile(hero).SheetFor(Condition.None);
            ActorSheet there = HeroBuild.Compile(back).SheetFor(Condition.None);
            report.Check("And adds up to exactly the same on the other side", here.MaxHealth == there.MaxHealth && here.Profile(SkillCatalog.Firebolt)!.AverageHit == there.Profile(SkillCatalog.Firebolt)!.AverageHit);

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
            forged.WriteByte((byte)ItemSlot.Ring);
            forged.WriteByte((byte)Rarity.Rare);
            forged.WriteVarInt(1);
            forged.WriteByte(0);
            for (int i = 0; i < 4; i++) forged.WriteFloat(0f);
            forged.WriteBool(true);
            forged.WriteString("not-a-unique");
            forged.WriteCount(1);
            forged.WriteString("not-an-affix");
            forged.WriteFloat(99999f);
            var items = new BitWriter();
            items.WriteCount(1);
            items.Append(forged);
            List<Item> arrived = Protocol.ReadItems(new BitReader(items.ToArray()));
            report.Check("An affix or a unique that doesn't exist does nothing, whatever a stranger sends", arrived.Count == 1 && arrived[0].Unique == null && arrived[0].Lines.Count == 0);
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

        private static void OldSaves(Report report)
        {
            SaveGame? save = SaveGame.Read(OldSave);
            report.Check("A save from before talents loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("With the hero as they were", hero.Level == 3 && hero.Gold == 368 && hero.Experience == 76 && hero.Inventory.Count == 2 && hero.Equipped.Count == 2);
            report.Check("With the world as it was", save.WorldSeed == 12345 && save.Sites.Count == 1 && save.Sites[0].Kindled && save.Objectives[0] == 1);
            report.Check("Every point goes back into talents", hero.Talents.Count == 0 && hero.UnspentPoints == 7, $"{hero.UnspentPoints}");
            report.Check("A sword someone edited by hand stays as it was", hero.Equipped[ItemSlot.Weapon].WeaponDamage == 700f);

            Item hood = hero.Inventory[0];
            report.Check("Item lines come across, value for value", hood.Lines.Count == 2 && hood.Lines[0].Affix == "armour" && MathF.Abs(hood.Lines[0].Value - 4.534136f) < 1e-4f && hood.Lines[1].Affix == "health");
            report.Check("Every old stat has somewhere to go", hero.Inventory[1].Lines.Count == 10 && hero.Inventory[1].Lines.All(l => AffixCatalog.Find(l.Affix) != null));

            ActorSheet sheet = HeroBuild.Compile(hero).SheetFor(Condition.None);
            hero.Equip(hero.Inventory[1]);
            ActorSheet ringed = HeroBuild.Compile(hero).SheetFor(Condition.None);
            report.Check("And mean what they meant", ringed.MoveSpeed > sheet.MoveSpeed && ringed.HealthRegen > sheet.HealthRegen && ringed.ItemRarity == 12f && ringed.GoldFind == 30f && ringed.Profile(SkillCatalog.Strike)!.AverageHit > sheet.Profile(SkillCatalog.Strike)!.AverageHit);
            report.Check("A new hero has a bar to play with", hero.Bar[0] == "strike" && hero.Bar[2] == "whirl");

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
            report.Check("And what's written back is the new kind", written.Contains("\"Version\": 2") && written.Contains("\"Lines\"") && !written.Contains("\"Modifiers\""));
            report.Check("An old save put back by hand still loads", restored != null && restored.Hero.Level == upgraded?.Hero.Level);
            report.Check("And leaves the first copy of it alone", firstCopy);
            report.Check("Not managing to keep a copy doesn't cost the hero", uncopied != null);

            string? damaged = OldSave.Replace("\"Stat\": \"Armour\"", "\"Stat\": \"SomethingWeNeverHad\"");
            SaveGame? survived = SaveGame.Read(damaged);
            report.Check("A stat nobody has heard of costs a line, not the hero", survived != null && survived.Hero.Inventory[0].Lines.Count == 1);

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
                    report.Check("  and compiles", check.MaxHealth > 0f && float.IsFinite(check.Profile(SkillCatalog.Strike)!.AverageHit));
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
            var hero = new Actor(Team.Heroes, new Loadout(10, sword, mods, new[] { SkillCatalog.Strike }), Vector2.Zero, 1f);
            var enemy = new Actor(Team.Enemies, new Loadout(10, sword, mods, new[] { SkillCatalog.Strike }), Vector2.Zero, 1f);

            float Blow(Actor from, Actor to)
            {
                var luck = new WorldRandom(77);
                HitPacket hit = default;
                Hits.Roll(from.Sheet.Profile(0), Condition.None, luck, ref hit);
                return Hits.Mitigate(to.Sheet, ref hit);
            }

            report.Check("The same modifiers on either side deal the same blow and take it the same way", Blow(hero, enemy) == Blow(enemy, hero), $"{Blow(hero, enemy)} and {Blow(enemy, hero)}");

            var bestiary = new Bestiary();
            Loadout first = bestiary.Shared(Bestiary.Outlaw, 5, Dino_Defenders.Gameplay.EnemyRank.Normal, Bestiary.NoTrait);
            Loadout second = bestiary.Shared(Bestiary.Outlaw, 5, Dino_Defenders.Gameplay.EnemyRank.Normal, Bestiary.NoTrait);
            report.Check("Enemies of a kind share one set of numbers", ReferenceEquals(first, second) && ReferenceEquals(first.SheetFor(Condition.None), second.SheetFor(Condition.Burning)));

            Loadout boss = Bestiary.Boss(Bestiary.Warlord, 12, new WorldRandom(4), out Item[] gear, out string? title);
            report.Check("A boss wears real gear, and walks the real tree", gear.Length > 0 && title != null, $"{gear.Length} items, {title}");
            report.Note($"This one: {title}, wearing {string.Join(", ", gear.Select(g => g.Name))}");

            int uniques = 0;
            for (int i = 0; i < 400; i++)
            {
                Bestiary.Boss(Bestiary.Warlord, 20, new WorldRandom(i), out Item[] worn, out _);
                if (worn.Any(g => g.Unique != null)) uniques++;
            }
            report.Check("A good share of them are wearing something unique", uniques is > 80 and < 360, $"{uniques} of 400");

            int fair = 0;
            foreach (TalentNode keystone in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone))
            {
                Loadout load = new Loadout(20, sword, mods.Concat(keystone.Mods), new[] { SkillCatalog.Strike, SkillCatalog.Firebolt });
                ActorSheet sheet = load.SheetFor(Condition.None);
                if (float.IsFinite(sheet.Profile(0).AverageHit) && sheet.MaxHealth > 0f) fair++;
            }
            report.Check("Every keystone works on anyone", fair == TalentTree.Nodes.Count(n => n.Kind == TalentKind.Keystone));
        }
    }
}
