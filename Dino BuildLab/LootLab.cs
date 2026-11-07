using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.World;

namespace Dino_BuildLab
{
    /// <summary>
    /// What the loot tables come to, by running the real rolls a great many times. The tables are numbers someone made up, so this is what stands behind them: that each step up
    /// really pays better than the last, that a world gives what it's meant to, and that nobody walks through hundreds of enemies with nothing to show for it.
    /// </summary>
    internal static class LootLab
    {
        private const int Rolls = 200000;

        // What a world is made of, roughly, by what stands in it: most of a pack is fodder. `Dino NetLab pace` kills a good part of this at the speed a plain hero does.
        internal static readonly (LootTier tier, int count)[] World =
        {
            (LootTier.Fodder, 900), (LootTier.Soldier, 200), (LootTier.Champion, 60), (LootTier.Magic, 70), (LootTier.Rare, 20), (LootTier.Boss, 1),
            (LootTier.Cache, 30), (LootTier.Chest, 8), (LootTier.Hoard, 1),
        };

        private readonly record struct Tally(float Finds, float[] Rarities, float Lines, float LineShare, float Worth, float Level);

        // What one find is worth, on a scale where each rarity is several of the last: this is what "much better" has to mean, in one number.
        internal static float WorthOf(Item item)
        {
            float rarity = item.Rarity switch
            {
                Rarity.Unique => 80f,
                Rarity.Rare => 8f,
                Rarity.Magic => 2f,
                _ => 0.25f,
            };
            return rarity * (1f + 0.5f * Shares(item)) * (1f + item.Level * 0.02f);
        }

        // How far up what its level can reach an item's lines came out, from 0 to 1.
        private static float Shares(Item item)
        {
            if (item.Lines.Count == 0) return 0f;
            float total = 0f;
            foreach (ItemLine line in item.Lines)
            {
                Affix affix = AffixCatalog.Find(line.Affix)!;
                Tier top = affix.Tiers[0];
                foreach (Tier tier in affix.Tiers)
                {
                    if (tier.MinLevel <= item.Level) top = tier;
                }
                total += Math.Clamp(line.Value / top.High, 0f, 1f);
            }
            return total / item.Lines.Count;
        }

        private static Tally Measure(LootTier tier, int level, int rolls, ulong seed)
        {
            var random = new WorldRandom(seed);
            var rarities = new float[Enum.GetValues<Rarity>().Length];
            double finds = 0, lines = 0, share = 0, worth = 0, levels = 0, rated = 0;
            for (int i = 0; i < rolls; i++)
            {
                // A luck of its own each time: this is what a source gives by itself, with nothing owed to it.
                foreach (Item item in Loot.Roll(tier, level, level, 0f, new LootLuck(), random.Fork(i)))
                {
                    finds++;
                    rarities[(int)item.Rarity]++;
                    worth += WorthOf(item);
                    levels += item.Level;
                    if (item.Lines.Count == 0) continue;
                    lines += item.Lines.Count;
                    share += Shares(item);
                    rated++;
                }
            }

            for (int i = 0; i < rarities.Length; i++) rarities[i] /= rolls;
            return new Tally((float)(finds / rolls), rarities, (float)(lines / Math.Max(rated, 1)), (float)(share / Math.Max(rated, 1)), (float)(worth / rolls), (float)(levels / Math.Max(finds, 1)));
        }

        public static int Run(Options options)
        {
            int level = options.Int("level", 12);
            Console.WriteLine($"What one of each gives at level {level}, over {Rolls} of them\n");
            Console.WriteLine("tier        finds  normal   magic    rare  unique   lines  rolled   level     worth");
            foreach (LootTier tier in Enum.GetValues<LootTier>())
            {
                Tally t = Measure(tier, level, tier is LootTier.Boss or LootTier.Hoard ? Rolls / 10 : Rolls, 1);
                Console.WriteLine($"{tier,-9} {t.Finds,7:0.000} {t.Rarities[0],7:0.0000} {t.Rarities[1],7:0.0000} {t.Rarities[2],7:0.0000} {t.Rarities[3],7:0.0000} {t.Lines,7:0.00} {t.LineShare,6:0%} {t.Level,7:0.0} {t.Worth,9:0.000}");
            }

            var world = new WorldRandom(7);
            const int Worlds = 400;
            var rarities = new double[Enum.GetValues<Rarity>().Length];
            for (int w = 0; w < Worlds; w++)
            {
                var luck = new LootLuck();
                foreach ((LootTier tier, int count) in World)
                {
                    for (int i = 0; i < count; i++)
                    {
                        foreach (Item item in Loot.Roll(tier, level, level, 0f, luck, world.Fork(w * 4096 + i))) rarities[(int)item.Rarity]++;
                    }
                }
            }

            Console.WriteLine($"\nA whole world cleared, on average: {rarities[0] / Worlds:0.0} normal, {rarities[1] / Worlds:0.0} magic, {rarities[2] / Worlds:0.0} rare, {rarities[3] / Worlds:0.0} unique.");

            (int kills, int rare, int unique) = Droughts(level, 1000000, 3);
            Console.WriteLine($"The longest anyone went, in a million kills of what packs are made of: {kills} kills without a find, {rare} without a rare or better, {unique} without a unique.");
            return 0;
        }

        // The longest dry spells in a long run of ordinary killing, with no chests and no bosses to help.
        private static (int kills, int rare, int unique) Droughts(int level, int kills, ulong seed)
        {
            var random = new WorldRandom(seed);
            var luck = new LootLuck();
            int longestDry = 0, longestRare = 0, longestUnique = 0;
            int dry = 0, sinceRare = 0, sinceUnique = 0;
            for (int i = 0; i < kills; i++)
            {
                LootTier tier = (i % 11) switch { 0 or 1 or 2 => LootTier.Soldier, 3 => LootTier.Champion, _ => LootTier.Fodder };
                List<Item> items = Loot.Roll(tier, level, level, 0f, luck, random.Fork(i));
                dry = items.Count > 0 ? 0 : dry + 1;
                sinceRare++;
                sinceUnique++;
                foreach (Item item in items)
                {
                    Rarity rarity = item.Rarity;
                    if (rarity >= Rarity.Rare) sinceRare = 0;
                    if (rarity == Rarity.Unique) sinceUnique = 0;
                }
                longestDry = Math.Max(longestDry, dry);
                longestRare = Math.Max(longestRare, sinceRare);
                longestUnique = Math.Max(longestUnique, sinceUnique);
            }
            return (longestDry, longestRare, longestUnique);
        }

        /// <summary>What has to stay true of the tables, whatever their numbers become.</summary>
        public static void Check(Report report)
        {
            report.Section("Loot");

            const int Level = 12;
            const int Trials = 40000;
            LootTier[] enemies = { LootTier.Fodder, LootTier.Soldier, LootTier.Champion, LootTier.Magic, LootTier.Rare, LootTier.Boss };
            LootTier[] chests = { LootTier.Cache, LootTier.Chest, LootTier.Hoard };
            var measured = new Dictionary<LootTier, Tally>();
            foreach (LootTier tier in Enum.GetValues<LootTier>()) measured[tier] = Measure(tier, Level, tier is LootTier.Boss or LootTier.Hoard ? Trials / 10 : Trials, 11);

            foreach (LootTier[] ladder in new[] { enemies, chests })
            {
                for (int i = 1; i < ladder.Length; i++)
                {
                    Tally low = measured[ladder[i - 1]];
                    Tally high = measured[ladder[i]];
                    report.Check($"{ladder[i]} pays at least twice what {ladder[i - 1]} does", high.Worth >= low.Worth * 2f, $"{high.Worth:0.000} against {low.Worth:0.000}");
                    report.Check($"And rolls at least as high", high.LineShare >= low.LineShare - 0.01f, $"{high.LineShare:0.00} against {low.LineShare:0.00}");
                }
            }

            float bestElse = measured.Where(pair => pair.Key != LootTier.Boss).Max(pair => pair.Value.Worth);
            report.Check("Nothing pays like a boss", measured[LootTier.Boss].Worth > bestElse, $"{measured[LootTier.Boss].Worth:0.0} against {bestElse:0.0}");

            var boss = new WorldRandom(5);
            bool everyBossPays = true;
            for (int i = 0; i < 2000; i++)
            {
                List<Item> items = Loot.Roll(LootTier.Boss, Level, Level, 0f, new LootLuck(), boss.Fork(i));
                everyBossPays &= items.Count >= 5 && items.All(item => item.Rarity >= Rarity.Rare) && items.Any(item => item.Rarity == Rarity.Unique);
            }
            report.Check("Every boss gives five rare finds or better, and a unique", everyBossPays);

            bool noTwins = true;
            for (int i = 0; i < 3000; i++)
            {
                List<string> uniques = Loot.Roll(LootTier.Boss, 30, 30, 0f, new LootLuck(), boss.Fork(i + 5000)).Where(item => item.Unique != null).Select(item => item.Unique!).ToList();
                noTwins &= uniques.Distinct().Count() == uniques.Count;
            }
            report.Check("Nothing gives the same unique twice", noTwins);

            report.Check("The tiers come from health", LootTables.TierOf(Rarity.Normal, Bestiary.Cutthroat.Health) == LootTier.Fodder && LootTables.TierOf(Rarity.Normal, Bestiary.Outlaw.Health) == LootTier.Soldier && LootTables.TierOf(Rarity.Normal, Bestiary.Bear.Health) == LootTier.Champion);
            report.Check("Every kind of enemy is in a sensible tier", Bestiary.Everyone.All(kind => LootTables.TierOf(Rarity.Normal, kind.Health) != LootTier.Boss));
            report.Check("Rarity outranks health", LootTables.TierOf(Rarity.Magic, 0.3f) == LootTier.Magic && LootTables.TierOf(Rarity.Rare, 0.3f) == LootTier.Rare && LootTables.TierOf(Rarity.Unique, 0.3f) == LootTier.Boss);

            report.Check("Going after what's above you pays", Loot.Daring(12, 8) > 1.2f);
            report.Check("Farming what's beneath you doesn't", Loot.Daring(4, 20) < 0.5f);
            report.Check("But it's never nothing, and never silly", Loot.Daring(1, 100) > 0f && Loot.Daring(100, 1) <= 2f);

            var random = new WorldRandom(9);
            bool everyRarity = true;
            foreach (Rarity rarity in Enum.GetValues<Rarity>())
            {
                // A unique at a level none reaches is the one thing that can't be what was asked for, and it comes out as the next best thing.
                for (int i = 0; i < 300; i++) everyRarity &= ItemGenerator.Forge(20, random.Fork(i + 20000), rarity, 0.5f).Rarity == rarity;
            }
            report.Check("A rarity asked for is the rarity that comes out", everyRarity);
            ItemSlot none = Enum.GetValues<ItemSlot>().First(slot => slot < ItemSlot.Book && UniqueCatalog.All.All(u => u.MinLevel > 1 || ItemGenerator.Plain(u.Base, 1, new WorldRandom(1)).Slot != slot));
            report.Check("With no unique to be, a unique is a rare", ItemGenerator.Forge(1, random.Fork(1), Rarity.Unique, 0f, none).Rarity == Rarity.Rare, $"{none}");

            float plain = 0f, fine = 0f;
            for (int i = 0; i < 4000; i++)
            {
                plain += Shares(ItemGenerator.Forge(30, random.Fork(i), Rarity.Rare, 0f));
                fine += Shares(ItemGenerator.Forge(30, random.Fork(i), Rarity.Rare, 1f));
            }
            report.Check("Quality really does roll higher", fine > plain * 1.15f, $"{fine / 4000f:0.00} against {plain / 4000f:0.00}");

            // Luck: it has to help, and it has to let go.
            var luck = new LootLuck();
            float start = luck.Finds;
            for (int i = 0; i < 60; i++) luck.Missed();
            report.Check("Going without makes a find likelier", luck.Finds > start * 3f, $"{luck.Finds:0.0}");
            luck.Found(Rarity.Normal);
            report.Check("Finding something lets go of that", luck.Finds == start);
            for (int i = 0; i < 20; i++) luck.Found(Rarity.Normal);
            report.Check("Normal finds don't stop the wait for a good one", luck.Good > 3f, $"{luck.Good:0.0}");
            luck.Found(Rarity.Rare);
            report.Check("A good one ends it", luck.Good == 1f);

            (int dryKills, int dryRare, int dryUnique) = Droughts(Level, 200000, 13);
            report.Check("Nobody goes a hundred kills with nothing", dryKills < 100, $"{dryKills}");
            report.Check("Nobody goes a thousand kills without a rare", dryRare < 1000, $"{dryRare}");
            report.Check("Nobody goes twenty thousand kills without a unique", dryUnique < 20000, $"{dryUnique}");

        }
    }
}
