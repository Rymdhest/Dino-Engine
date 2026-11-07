using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.World;

namespace Dino_BuildLab
{
    /// <summary>
    /// How a fight goes at each level. A sample hero takes the talents a sensible player might, wears what that level drops, and meets what lives there.
    /// It can't say whether anything is fun, but it can say whether a level 30 outlaw takes three hits or thirty.
    /// </summary>
    internal static class Balance
    {
        private sealed record Sample(string Name, BookPlan Book, string[] Sectors, string[] Wanted, WeaponKind? Weapon);

        private static readonly Sample[] Samples =
        {
            new("Fire caster", Shelf.Firebolt, new[] { "pyre", "arcanum", "marrow" }, new[] { "pyre.kindled-bolt" }, WeaponKind.Staff),
            new("Sword fighter", Shelf.Strike, new[] { "forge", "bulwark", "marrow" }, Array.Empty<string>(), WeaponKind.Sword),
            new("Frost caster", Shelf.FrostShards, new[] { "winter", "arcanum", "storm" }, new[] { "winter.splinters" }, WeaponKind.Wand),
            new("Archer", Shelf.Shot, new[] { "hunt", "knave", "wilds" }, new[] { "hunt.steady-draw" }, WeaponKind.Bow),
        };

        private static readonly int[] Levels = { 1, 3, 5, 8, 12, 16, 20, 25, 30, 40, 50, 60 };

        /// <summary>Goes through a sector's best first, nearest the root first, as far as the points go.</summary>
        private static void Spend(HeroState hero, Sample sample)
        {
            foreach (string id in sample.Wanted) Walk(hero, TalentTree.Find(id)!);

            var wishes = new List<TalentNode>();
            for (int i = 0; i < sample.Sectors.Length; i++)
            {
                int sector = Array.FindIndex(TalentContent.Sectors, s => s.Id == sample.Sectors[i]);
                wishes.AddRange(TalentTree.Nodes.Where(n => n.Sector == sector && n.Kind == TalentKind.Notable));
            }

            foreach (TalentNode node in wishes.OrderBy(n => TalentTree.DepthOf[n.Index]))
            {
                if (hero.UnspentPoints <= 0) break;
                Walk(hero, node);
            }
        }

        private static void Walk(HeroState hero, TalentNode node)
        {
            List<int> path = TalentTree.PathTo(hero, node.Index);
            if (path.Count > hero.UnspentPoints) return;
            foreach (int step in path) hero.Talents.Add(TalentTree.Nodes[step].Id);
        }

        /// <summary>What a hero of this level has on: magic and rare things of about their level, none of them chosen with any care.</summary>
        private static void Dress(HeroState hero, Sample sample, WorldRandom random)
        {
            hero.Equipped.Clear();
            foreach (WornSlot place in Enum.GetValues<WornSlot>())
            {
                ItemSlot slot = WornSlots.Holds(place);
                Item? best = null;
                for (int attempt = 0; attempt < 12 && best == null; attempt++)
                {
                    Item item = ItemGenerator.Roll(hero.Level, random.Fork(attempt * 16 + (int)place), 1f, Rarity.Magic, slot);
                    if (item.Unique != null) continue;
                    if (slot == ItemSlot.MainHand && sample.Weapon != item.Kind) continue;
                    best = item;
                }
                if (best != null) hero.Equipped[place] = best;
            }

            if (hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? weapon) && weapon.IsTwoHanded) hero.Equipped.Remove(WornSlot.OffHand);
        }

        private static (float dealt, float taken) Exchange(ActorSheet hero, BookProfile skill, Loadout enemy, int blows = 2000)
        {
            var luck = new WorldRandom(5);
            ActorSheet foe = enemy.SheetFor(Condition.None);
            float dealt = 0f;
            float taken = 0f;
            for (int i = 0; i < blows; i++)
            {
                HitPacket out_ = default;
                Hits.Roll(skill.First, Condition.None, luck, ref out_);
                dealt += Hits.Mitigate(foe, ref out_);

                HitPacket back = default;
                Hits.Roll(foe.Profile(0).First, Condition.None, luck, ref back);
                taken += Hits.Mitigate(hero, ref back);
            }
            return (dealt / blows, taken / blows);
        }

        public static int Run(Options options)
        {
            var bestiary = new Bestiary();
            foreach (Sample sample in Samples)
            {
                Console.WriteLine($"\n{sample.Name}, using {sample.Book.Name}");
                Console.WriteLine("level  health   mana  armour  dps     per hit   foe hp  foe hit  hits to kill  kill in  hits to die  title");

                foreach (int level in Levels)
                {
                    var totals = new float[8];
                    const int Rolls = 12;
                    string title = "";
                    for (int roll = 0; roll < Rolls; roll++)
                    {
                        HeroState hero = HeroState.CreateNew();
                        hero.Level = level;
                        Dress(hero, sample, new WorldRandom(level * 100 + roll));
                        Spend(hero, sample);
                        title = TalentTree.TitleOf(hero);

                        Loadout loadout = HeroBuild.Compile(hero);
                        ActorSheet sheet = loadout.SheetFor(Condition.None);
                        Loadout known = new Loadout(level, loadout.Weapon, loadout.Mods.Concat(loadout.RuleMods), loadout.Books.Append(sample.Book));
                        sheet = known.SheetFor(Condition.None);
                        BookProfile skill = sheet.Profile(sample.Book)!;

                        Loadout enemy = bestiary.Shared(Bestiary.Outlaw, level, Rarity.Normal, Bestiary.NoTraits);
                        (float dealt, float taken) = Exchange(sheet, skill, enemy);
                        ActorSheet foe = enemy.SheetFor(Condition.None);

                        totals[0] += sheet.MaxHealth;
                        totals[1] += sheet.MaxMana;
                        totals[2] += sheet.Armour;
                        totals[3] += dealt / MathF.Max(skill.UseTime, 0.1f);
                        totals[4] += dealt;
                        totals[5] += foe.MaxHealth;
                        totals[6] += taken;
                        totals[7] += 0f;
                    }

                    for (int i = 0; i < totals.Length; i++) totals[i] /= Rolls;
                    float hitsToKill = totals[5] / MathF.Max(totals[4], 0.01f);
                    float killIn = totals[5] / MathF.Max(totals[3], 0.01f);
                    float hitsToDie = totals[0] / MathF.Max(totals[6], 0.01f);
                    Console.WriteLine($"{level,5}  {totals[0],6:0}  {totals[1],5:0}  {totals[2],6:0}  {totals[3],6:0}  {totals[4],7:0}  {totals[5],7:0}  {totals[6],7:0.#}  {hitsToKill,12:0.0}  {killIn,6:0.0}s  {hitsToDie,11:0.0}  {title}");
                }
            }
            return 0;
        }
    }
}
