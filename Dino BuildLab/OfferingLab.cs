using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using Dino_Engine.World;

namespace Dino_BuildLab
{
    /// <summary>
    /// What an offering comes to, by running the real rolls. Peril has one job, which is to be worth it, and that's a sum: what a kill pays has to climb faster than what a kill takes, or nobody
    /// who could climb would want to. The price has to keep up as well, or gold stops meaning anything. Run `offering` for the table, and `check` for what must stay true of it.
    /// </summary>
    internal static class OfferingLab
    {
        private const int WalkedShare = 2;      // A player walks about one enemy in this many before they're done with a world.
        private const int ClimbWorthIt = 15;    // For a hero no stronger than before. Past here it's the hero who has to have grown, which is what the rest of the climb is.

        private static readonly int[] Ladder = { 0, 1, 2, 3, 5, 8, 10, 12, 15, 20, 25, 30 };

        /// <summary>What one world pays out, on average, if all of it is cleared.</summary>
        internal readonly record struct Payout(float Worth, float Kills, float Good, float Legendary, float Gold);

        internal static Payout Measure(int level, in LootBoon boon, int worlds, ulong seed)
        {
            var random = new WorldRandom(seed);
            double worth = 0, good = 0, legendary = 0, gold = 0;
            int kills = 0;
            for (int w = 0; w < worlds; w++)
            {
                var luck = new LootLuck();
                foreach ((LootTier tier, int count) in LootLab.World)
                {
                    if (tier < LootTier.Cache) kills += count;
                    for (int i = 0; i < count; i++)
                    {
                        WorldRandom draw = random.Fork(w * 4096 + i + (int)tier * 1024);
                        foreach (Item item in Loot.Roll(tier, level, level, 0f, luck, draw, boon))
                        {
                            worth += LootLab.WorthOf(item);
                            LootGrade grade = Loot.GradeOf(item);
                            if (grade >= LootGrade.Rare) good++;
                            if (grade == LootGrade.Legendary) legendary++;
                        }
                        gold += Loot.RollGold(tier, level, 0f, draw, boon.Gold);
                    }
                }
            }
            return new Payout((float)(worth / worlds), (float)kills / worlds, (float)(good / worlds), (float)(legendary / worlds), (float)(gold / worlds));
        }

        public static int Run(Options options)
        {
            int level = options.Int("level", 12);
            int worlds = options.Int("worlds", 30);
            Payout usual = Measure(level, LootBoon.None, worlds, 5);

            Console.WriteLine($"What each step of peril comes to on a land of level {level}, over {worlds} whole worlds of the usual size\n");
            Console.WriteLine("peril  health  damage  worth/kill  good/world  legend/world   gold/world     price   price/gold   paid per time");
            foreach (int peril in Ladder)
            {
                var offering = new Offering { Peril = peril };
                Payout payout = Measure(level, offering.Boon(), worlds, 5);
                float worthPerKill = payout.Worth / payout.Kills;
                float perTime = worthPerKill / (usual.Worth / usual.Kills) / offering.EnemyHealth;
                long price = offering.Price(level);
                Console.WriteLine($"{peril,5} {offering.EnemyHealth,6:0.0}x {offering.EnemyDamage,6:0.0}x {worthPerKill,11:0.00} {payout.Good,11:0.0} {payout.Legendary,13:0.0} {payout.Gold,12:0} {price,9} {price / (payout.Gold / WalkedShare),11:0%} {perTime,12:0.00}x");
            }

            Console.WriteLine("\nprice/gold is against half a world's gold, as most people don't clear all of one. paid per time is worth per kill over how much longer a kill takes, for a hero no stronger than before.");

            Console.WriteLine("\nWhat more of them costs, at peril 0, and what it does");
            for (int teeming = 0; teeming <= Offering.MostCrowd; teeming++)
            {
                for (int overrun = 0; overrun <= Offering.MostCrowd; overrun++)
                {
                    if (teeming != overrun && teeming != 0 && overrun != 0) continue;
                    var offering = new Offering { Teeming = teeming, Overrun = overrun };
                    float crowd = (1f + offering.MorePeople) * (1f + offering.MorePacks);
                    Console.WriteLine($"  Teeming {teeming}, Overrun {overrun}: about {crowd:0.0}x the enemies for {offering.Price(level)} gold, against {usual.Gold * crowd / WalkedShare:0} from walking half of them");
                }
            }
            return 0;
        }

        /// <summary>What must stay true.</summary>
        public static void Checks(Report report)
        {
            report.Section("Offerings");

            report.Check("Omens have ids of their own", Omens.All.Select(o => o.Id).Distinct().Count() == Omens.All.Count);
            report.Check("Omens have names of their own", Omens.All.Select(o => o.Name).Distinct().Count() == Omens.All.Count);
            report.Check("Every omen is numbered where it stands", Omens.All.Select((omen, index) => omen.Number == index).All(right => right));
            report.Check("Every omen says what it does", Omens.All.All(o => o.Describe().Length > 12 && !o.Describe().Contains("  ")), string.Join("; ", Omens.All.Select(o => o.Describe()).Where(text => text.Length <= 12)));
            report.Check("Every omen costs something", Omens.All.All(o => o.Toll > 0f));
            report.Check("Whatever makes an enemy worse pays for it", Omens.All.Where(o => o.Mods.Length > 0).All(o => o.Quantity > 0f && o.Rarity > 0f));

            // What it says on the tin. A crowd is only gold's worth of more enemies, and nothing can make it count for more than that.
            report.Check("Nothing offered costs nothing", new Offering().Price(1) == 0 && new Offering().Price(100000) == 0);
            report.Check("Nothing offered pays nothing", new Offering().Boon() == LootBoon.None);
            var crowd = new Offering { Teeming = Offering.MostCrowd, Overrun = Offering.MostCrowd };
            report.Check("A crowd makes nobody stronger", crowd.EnemyMods().Count == 0 && crowd.EnemyHealth == 1f && crowd.EnemyDamage == 1f);
            report.Check("And pays no more for each of them", crowd.Boon() == LootBoon.None);
            report.Check("And can't be failed", !crowd.AtStake);

            long last = 0;
            bool rising = true;
            for (int peril = 0; peril <= Offering.MostPeril; peril++)
            {
                long price = new Offering { Peril = peril, Teeming = 1 }.Price(10);
                rising &= price > last;
                last = price;
            }
            report.Check("Every step of peril costs more than the last", rising);
            report.Check("Every step of a crowd does", Enumerable.Range(1, Offering.MostCrowd).All(step => new Offering { Teeming = step }.Price(10) > new Offering { Teeming = step - 1, Peril = 0 }.Price(10)));
            report.Check("Every omen adds to the price", Omens.All.All(o => new Offering { Peril = 6, Omens = { o.Id } }.Price(10) > new Offering { Peril = 6 }.Price(10)));
            report.Check("A dearer land costs more for the same", new Offering { Peril = 3 }.Price(20) > new Offering { Peril = 3 }.Price(10));
            long most = new Offering { Peril = Offering.MostPeril, Teeming = Offering.MostCrowd, Overrun = Offering.MostCrowd, Omens = Omens.All.Take(Offering.MostOmens).Select(o => o.Id).ToList() }.Price(100000);
            report.Check("The dearest thing that can be asked for is still a number", most > int.MaxValue && most < long.MaxValue / 2, most.ToString());

            report.Check("The first omen needs no peril", Offering.OmenSlots(0) == 1);
            report.Check("More peril carries more, up to a limit", Offering.OmenSlots(3) == 2 && Offering.OmenSlots(Offering.MostPeril) == Offering.MostOmens);
            var hero = new HeroState();
            report.Check("A new hero can reach a little", Offering.Reach(hero) == Offering.ReachBeyondProven);
            hero.PerilProven = Offering.MostPeril;
            report.Check("Nobody reaches past the top", Offering.Reach(hero) == Offering.MostPeril);

            var unfit = new Offering { Peril = 900, Teeming = -2, Overrun = 40, Omens = { "no-such-omen", "ironhide", "ironhide", "keen", "volatile", "rallying", "night", "warhost", "gilded" } };
            unfit.Sanitize();
            report.Check("Whatever a save says comes out as something that can be", unfit.Peril == Offering.MostPeril && unfit.Teeming == 0 && unfit.Overrun == Offering.MostCrowd && unfit.Omens.Count == Offering.MostOmens && unfit.Omens[0] == "ironhide" && unfit.Omens.Distinct().Count() == unfit.Omens.Count);
            var thin = new Offering { Peril = 0, Omens = { "ironhide", "keen" } };
            thin.Sanitize();
            report.Check("No more omens than the peril carries", thin.Omens.Count == 1);

            report.Section("What an offering is worth");
            Worth(report);
            RollsStayTheSame(report);
            NumbersAreReal(report);
            Wire(report);
            Saved(report);
        }

        // Each step has to pay for itself, measured over whole worlds rather than taken from the constants.
        private static void Worth(Report report)
        {
            const int level = 12;
            const int worlds = 8;
            Payout usual = Measure(level, LootBoon.None, worlds, 21);
            float lastWorth = usual.Worth;
            bool climbing = true;
            bool outruns = true;
            int[] steps = { 1, 3, 5, 8, 10, 15, 20, 30 };
            foreach (int peril in steps)
            {
                var offering = new Offering { Peril = peril };
                Payout payout = Measure(level, offering.Boon(), worlds, 21);
                climbing &= payout.Worth > lastWorth;
                if (peril <= ClimbWorthIt) outruns &= payout.Worth / usual.Worth >= offering.EnemyHealth;
                report.Note($"Peril {peril}: {payout.Worth / usual.Worth:0.0}x the worth for {offering.EnemyHealth:0.0}x the health, {payout.Good:0} good finds a world, {payout.Legendary:0.0} legendary");
                lastWorth = payout.Worth;
            }
            report.Check("Every step of peril pays more than the last", climbing);
            report.Check("What a kill pays outruns what a kill takes, as far as anyone plays", outruns);

            LootBoon top = new Offering { Peril = Offering.MostPeril, Omens = Omens.All.Where(o => o.Quantity > 0f).Take(Offering.MostOmens).Select(o => o.Id).ToList() }.Boon();
            var random = new WorldRandom(9);
            int finds = 0, legendary = 0;
            for (int i = 0; i < 2000; i++)
            {
                foreach (Item item in Loot.Roll(LootTier.Boss, level, level, 0f, new LootLuck(), random.Fork(i), top))
                {
                    finds++;
                    if (Loot.GradeOf(item) == LootGrade.Legendary) legendary++;
                }
            }
            report.Check("Even at the top a boss drops more than legendaries", (float)legendary / finds < 0.6f, $"{legendary} of {finds}");
            report.Check("And not an impossible heap of them", finds / 2000f < 40f, $"{finds / 2000f:0.0} a boss");

            var plain = new Offering { Peril = 3 }.Boon();
            var draw = new WorldRandom(11);
            int plainFinds = 0;
            for (int i = 0; i < 4000; i++)
            {
                plainFinds += Loot.Roll(LootTier.Soldier, level, level, 0f, new LootLuck(), draw.Fork(i), plain).Count(item => Loot.GradeOf(item) == LootGrade.Plain);
            }
            report.Check("Past a point nothing plain drops", plainFinds == 0, $"{plainFinds} did");
        }

        // Everything that isn't an offering has to come out as it did before there were any.
        private static void RollsStayTheSame(Report report)
        {
            bool same = true;
            for (int i = 0; i < 500; i++)
            {
                List<Item> before = Loot.Roll(LootTier.Elite, 10, 10, 0.3f, new LootLuck(), new WorldRandom((ulong)i));
                List<Item> after = Loot.Roll(LootTier.Elite, 10, 10, 0.3f, new LootLuck(), new WorldRandom((ulong)i), new Offering().Boon());
                same &= before.Select(item => item.Name + item.Level).SequenceEqual(after.Select(item => item.Name + item.Level));
            }
            report.Check("A land with nothing paid on it rolls what it always did", same);
        }

        // What the interface promises against what a sheet comes to.
        private static void NumbersAreReal(Report report)
        {
            var offering = new Offering { Peril = 10, Omens = { "ironhide", "bloodthirst" } };
            var usual = new Bestiary();
            var worse = new Bestiary(offering.EnemyMods());
            ActorSheet before = usual.Shared(Bestiary.Outlaw, 12, EnemyRank.Normal, Bestiary.NoTrait).SheetFor(Condition.None);
            ActorSheet after = worse.Shared(Bestiary.Outlaw, 12, EnemyRank.Normal, Bestiary.NoTrait).SheetFor(Condition.None);
            report.Near("Enemies have the health it says", after.MaxHealth / before.MaxHealth, offering.EnemyHealth, 0.002f);
            report.Near("And the damage", after.Profile(SkillCatalog.Strike)!.AverageHit / before.Profile(SkillCatalog.Strike)!.AverageHit, offering.EnemyDamage, 0.002f);

            ActorSheet elite = worse.Shared(Bestiary.Outlaw, 12, EnemyRank.Elite, 4).SheetFor(Condition.None);
            report.Check("An elite has it on top of what it is", elite.MaxHealth > after.MaxHealth * 2f);

            Loadout boss = Bestiary.Boss(Bestiary.Warlord, 12, new WorldRandom(4), out _, out _);
            Loadout worseBoss = Bestiary.Boss(Bestiary.Warlord, 12, new WorldRandom(4), out _, out _, worse.Worn);
            report.Near("And so does a boss", worseBoss.SheetFor(Condition.None).MaxHealth / boss.SheetFor(Condition.None).MaxHealth, offering.EnemyHealth, 0.002f);

            report.Check("Everything an omen names, an enemy really wears", Omens.All.Where(o => o.Mods.Length > 0).All(omen =>
            {
                Loadout worn = new Bestiary(omen.Mods).Shared(Bestiary.Outlaw, 12, EnemyRank.Normal, Bestiary.NoTrait);
                ActorSheet sheet = worn.SheetFor(Condition.None);
                return omen.Mods.All(mod => mod.Kind == ModKind.Rule ? sheet.Has(mod.Rule) : worn.Mods.Contains(mod));
            }));
        }

        private static void Wire(Report report)
        {
            var offering = new Offering { Peril = 12, Teeming = 2, Overrun = 3, Omens = { "volatile", "ironhide" } };
            var writer = new BitWriter(64);
            Protocol.WriteOffering(writer, offering);
            Offering read = Protocol.ReadOffering(new BitReader(writer.ToArray()));
            report.Check("An offering comes across whole", read.Peril == 12 && read.Teeming == 2 && read.Overrun == 3 && read.Omens.Count == 2 && read.Has(Omens.Volatile) && read.Has(Omens.Ironhide));

            bool Refused(int peril, int teeming, int overrun, params int[] omens)
            {
                var nonsense = new BitWriter(64);
                nonsense.WriteVarInt(peril);
                nonsense.WriteVarInt(teeming);
                nonsense.WriteVarInt(overrun);
                nonsense.WriteCount(omens.Length);
                foreach (int omen in omens) nonsense.WriteVarInt(omen);
                try
                {
                    Protocol.ReadOffering(new BitReader(nonsense.ToArray()));
                    return false;
                }
                catch (NetReadException)
                {
                    return true;
                }
            }

            report.Check("Peril past the top is nonsense", Refused(Offering.MostPeril + 1, 0, 0));
            report.Check("So is less than none", Refused(-1, 0, 0));
            report.Check("So is more of a crowd than there can be", Refused(0, Offering.MostCrowd + 1, 0) && Refused(0, 0, Offering.MostCrowd + 1));
            report.Check("So is an omen that doesn't exist", Refused(1, 0, 0, Omens.All.Count));

            var hero = new HeroState { PerilProven = 7 };
            var summary = new BitWriter(64);
            Protocol.WriteHeroSummary(summary, hero);
            var back = new HeroState();
            Protocol.ReadHeroSummary(new BitReader(summary.ToArray()), back);
            report.Check("How far a hero has come goes with them", back.PerilProven == 7);
        }

        private static void Saved(Report report)
        {
            SaveGame? before = SaveGame.Read("{ \"Version\": 3, \"WorldSeed\": 8 }");
            report.Check("A save from before offerings has none", before != null && !before.Offering.Any && before.Hero.PerilProven == 0 && before.OfferingFalls == 0 && !before.OfferingBroken);

            string file = Path.GetTempFileName();
            SaveGame.PathOverride = file;
            try
            {
                var save = new SaveGame { Offering = new Offering { Peril = 4, Teeming = 1, Omens = { "keen" } }, OfferingFalls = 2, OfferingBroken = true, Hero = new HeroState { PerilProven = 3 } };
                save.Store();
                SaveGame? loaded = SaveGame.Load();
                report.Check("An offering keeps", loaded != null && loaded.Offering.Peril == 4 && loaded.Offering.Teeming == 1 && loaded.Offering.Has(Omens.Keen));
                report.Check("So does how it stands", loaded != null && loaded.OfferingFalls == 2 && loaded.OfferingBroken);
                report.Check("And how far the hero has come", loaded != null && loaded.Hero.PerilProven == 3);
                report.Check("Nothing worked out from it is written down", !File.ReadAllText(file).Contains("EnemyHealth") && !File.ReadAllText(file).Contains("AtStake"));
            }
            finally
            {
                SaveGame.PathOverride = null;
                File.Delete(file);
            }
        }
    }
}
