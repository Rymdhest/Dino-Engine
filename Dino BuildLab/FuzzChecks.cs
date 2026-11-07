using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.World;
using System.Reflection;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>
    /// What players will do to all of this: put together everything that was never meant to meet. No sum here is checked against a right answer, only
    /// against being a number at all. A NaN that gets into someone's health ruins their character for good, so nothing may ever produce one.
    /// </summary>
    internal static class FuzzChecks
    {
        public static void Run(Report report, int rounds)
        {
            report.Section("Anything can be combined with anything");

            var random = new Random(1234);
            List<Mod> every = Pool();
            report.Note($"{every.Count} modifiers to draw from");

            string? firstBad = null;
            int builds = 0;
            int blows = 0;
            var luck = new WorldRandom(9);

            for (int round = 0; round < rounds && firstBad == null; round++)
            {
                int size = random.Next(1, round % 10 == 0 ? 400 : 40);
                var mods = new List<Mod> { Flat(Stat.Health, 100f), Flat(Stat.Mana, 50f), Flat(Stat.MoveSpeed, 10f) };
                for (int i = 0; i < size; i++) mods.Add(every[random.Next(every.Count)]);

                var weapon = new WeaponProfile(random.NextSingle() * 200f, 200f + random.NextSingle() * 200f, 0.4f + random.NextSingle(), random.NextSingle() * 20f, Tags.Sword);
                var loadout = new Loadout(random.Next(1, 90), weapon, mods, Shelf.All);

                Condition state = (Condition)((ulong)random.NextInt64() & (ulong)((1L << 37) - 1));
                Counters counters = new Counters { Gold = random.Next(0, 1000000), ShrinesKindled = (byte)random.Next(60), RecentKills = (byte)random.Next(200), HealthSpent = (byte)random.Next(200), Minions = (byte)random.Next(30), AlliesNear = (byte)random.Next(5) };
                ActorSheet sheet = loadout.SheetFor(state, counters);
                builds++;

                firstBad = Inspect(sheet, $"round {round}");
                if (firstBad != null) break;

                var target = new Loadout(30, weapon, mods.Take(mods.Count / 2), new[] { Shelf.Strike }).SheetFor(state);
                foreach (BookPlan skill in Shelf.All)
                {
                    PageProfile profile = sheet.Profile(skill)!.First;
                    firstBad = Inspect(profile, $"round {round}, {skill.Id}");
                    if (firstBad != null) break;
                    if (random.Next(4) != 0) continue;

                    Condition foe = (Condition)((ulong)random.NextInt64() & (ulong)((1L << 37) - 1));
                    HitPacket hit = default;
                    Hits.Roll(profile, foe, luck, ref hit, random.NextSingle() * 4f, random.Next(2) == 0);
                    Vitals vitals = Vitals.Full(target);
                    Hits.Mitigate(target, ref hit, state, random.NextSingle() * 60f, random.NextSingle());
                    Hits.Pay(target, ref vitals, hit);
                    Hits.Heal(target, ref vitals, random.NextSingle() * 1000f);
                    Hits.Recover(target, ref vitals, 1f / 60f, random.NextSingle() * 5f);
                    blows++;

                    if (!float.IsFinite(hit.Total) || hit.Total < 0f || !float.IsFinite(vitals.Health) || !float.IsFinite(vitals.Mana) || !float.IsFinite(vitals.Barrier) || !float.IsFinite(vitals.Debt))
                    {
                        firstBad = $"round {round}, {skill.Id}: hit {hit.Total}, vitals {vitals.Health}/{vitals.Mana}/{vitals.Barrier}/{vitals.Debt}";
                        break;
                    }
                }
            }

            report.Check($"{builds} random builds and {blows} blows between them are all made of numbers", firstBad == null, firstBad ?? "");
            Extremes(report);
            TreeWalks(report, random);
        }

        private static List<Mod> Pool()
        {
            var pool = new List<Mod>();
            foreach (TalentNode node in TalentTree.Nodes) pool.AddRange(node.Mods);
            foreach (UniqueDefinition unique in UniqueCatalog.All) pool.AddRange(unique.Mods);
            foreach (Affix affix in AffixCatalog.All) pool.AddRange(affix.Build(affix.Tiers[^1].High));
            foreach (BuffDefinition buff in BuffCatalog.All) pool.AddRange(buff.Mods);
            return pool;
        }

        private static string? Inspect(object target, string where)
        {
            foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                switch (field.GetValue(target))
                {
                    case float number when !float.IsFinite(number):
                        return $"{where}: {field.Name} is {number}";
                    case float number when number < 0f && field.Name is "MaxHealth" or "MaxMana" or "MaxBarrier" or "MoveSpeed" or "UseTime" or "Cooldown" or "Cost" or "Radius" or "Armour":
                        return $"{where}: {field.Name} is {number}";
                    case float[] numbers:
                        foreach (float number in numbers)
                        {
                            if (!float.IsFinite(number)) return $"{where}: something in {field.Name} is {number}";
                        }
                        break;
                    case DamageTable table:
                        for (int i = 0; i < DamageKinds.Count; i++)
                        {
                            if (!float.IsFinite(table[i])) return $"{where}: {field.Name}[{i}] is {table[i]}";
                        }
                        break;
                }
            }

            if (target is PageProfile profile)
            {
                foreach (DamageFlow flow in profile.Flows)
                {
                    if (!float.IsFinite(flow.Min) || !float.IsFinite(flow.Max) || !float.IsFinite(flow.More) || !float.IsFinite(flow.Increased)) return $"{where}: a flow of {flow.Type} isn't a number";
                }
            }
            return null;
        }

        private static void Extremes(Report report)
        {
            ActorSheet nothing = StatChecks.Sheet();
            report.Check("Someone with no modifiers at all is still someone", nothing.MaxHealth >= 1f && nothing.MoveSpeed > 0f);

            HitPacket hit = default;
            hit.Damage[(int)DamageType.Fire] = float.MaxValue / 8f;
            float huge = Hits.Mitigate(nothing, ref hit);
            report.Check("A blow as big as a float can hold doesn't turn into nonsense", float.IsFinite(huge) && huge > 0f, $"{huge}");

            ActorSheet stone = StatChecks.Sheet(Flat(Stat.Armour, 1e30f), Flat(Stat.Evasion, 1e30f), Resist(Tags.None, 1e30f), Flat(Stat.BlockChance, 1e30f));
            HitPacket glance = default;
            glance.Damage[(int)DamageType.Physical] = 1f;
            report.Check("Nor does armour as high as a float can hold", float.IsFinite(Hits.Mitigate(stone, ref glance)));
            report.Check("Nor evasion", Hits.EvadeChance(stone.Evasion, 1f) <= Hits.MostEvasion);

            ActorSheet broken = StatChecks.Sheet(Flat(Stat.Health, 100f), Inc(Stat.Health, -500f), More(Stat.Health, -500f), Inc(Stat.AttackSpeed, -500f), Inc(Stat.CastSpeed, -500f), Inc(Stat.MoveSpeed, -500f), Inc(Stat.BookCost, -500f), Inc(Stat.AreaOfEffect, -500f));
            report.Check("A hero can't be reduced to less than nothing", broken.MaxHealth >= 1f && broken.MoveSpeed > 0f);
            PageProfile slow = broken.Preview(Shelf.Immolate).First;
            report.Check("Nor their books", float.IsFinite(slow.UseTime) && slow.UseTime > 0f && slow.Cost >= 0f && slow.Length >= 0f, $"{slow.UseTime}, {slow.Cost}, {slow.Length}");

            ActorSheet quick = StatChecks.Sheet(More(Stat.CastSpeed, 100000f), More(Stat.AttackSpeed, 100000f));
            report.Check("Nor sped up past what a tick can see", quick.Preview(Shelf.Firebolt).UseTime >= 0.05f);

            ActorSheet many = StatChecks.Sheet(Flat(Stat.ProjectileCount, 100000f), Flat(Stat.Pierce, 1e9f), Flat(Stat.Chain, 1e9f));
            report.Check("A book can't be made to fire more than a server can bear", many.Preview(Shelf.Firebolt).First.Projectiles <= Rulebook.MostMade);
            report.Check("And the numbers that can run away stay integers", many.Preview(Shelf.Firebolt).First.Pierce > 0 && many.Preview(Shelf.Spark).First.Chains > 0);
        }

        // Someone clicking around the tree at random, taking and giving back, can never leave it in a state that doesn't hold together.
        private static void TreeWalks(Report report, Random random)
        {
            string? broken = null;
            for (int walk = 0; walk < 40 && broken == null; walk++)
            {
                var hero = new HeroState { Level = 60 };
                for (int step = 0; step < 600 && broken == null; step++)
                {
                    int node = random.Next(TalentTree.Nodes.Length);
                    if (random.Next(3) != 0)
                    {
                        List<int> path = TalentTree.PathTo(hero, node);
                        if (path.Count == 0 || path.Count > hero.UnspentPoints) continue;
                        foreach (int index in path)
                        {
                            if (!TalentTree.CanAllocate(hero, index))
                            {
                                broken = $"a path to {TalentTree.Nodes[node].Id} stepped on {TalentTree.Nodes[index].Id}, which it couldn't take";
                                break;
                            }
                            hero.Talents.Add(TalentTree.Nodes[index].Id);
                        }
                    }
                    else if (hero.Talents.Count > 0)
                    {
                        int index = TalentTree.Find(hero.Talents[random.Next(hero.Talents.Count)])!.Index;
                        if (TalentTree.CanRefund(hero, index)) hero.Talents.Remove(TalentTree.Nodes[index].Id);
                    }

                    int before = hero.Talents.Count;
                    var copy = new HeroState { Level = 60, Talents = new List<string>(hero.Talents) };
                    TalentTree.Sanitize(copy);
                    if (copy.Talents.Count != before) broken = $"after {step} steps, {before - copy.Talents.Count} talents had come loose";
                }
            }
            report.Check("Forty heroes wandering the tree at random, taking and giving back, always leave it whole", broken == null, broken ?? "");
        }
    }
}
