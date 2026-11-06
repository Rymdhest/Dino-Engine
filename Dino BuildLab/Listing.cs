using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;

namespace Dino_BuildLab
{
    /// <summary>Everything a player can read, in one place, for reading. Text that's generated can be right and still read badly, and only reading it shows that.</summary>
    internal static class Listing
    {
        private static void Print(Card card)
        {
            Console.WriteLine();
            Console.WriteLine($"{card.Title}  [{string.Join(", ", Cards.TagNames(card.Tags))}]  {card.Kind}");
            foreach (CardLine line in card.Lines)
            {
                if (line.Look != LineLook.Gap) Console.WriteLine(line.Value.Length > 0 ? $"    {line.Text}: {line.Value}" : $"    {line.Text}");
            }
            if (card.Details != null) Print(card.Details);
        }

        public static int Run(Options options)
        {
            string what = options.Text("what") ?? "all";

            if (what is "all" or "keystones")
            {
                foreach (Sector sector in TalentContent.Sectors)
                {
                    Console.WriteLine($"\n{sector.Name}");
                    foreach (TalentNode node in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone && TalentContent.Sectors[n.Sector] == sector))
                    {
                        Console.WriteLine($"  {node.Name}");
                        foreach (Mod mod in node.Mods) Console.WriteLine($"      {mod.Describe()}");
                        Console.WriteLine($"      \"{node.Lore}\"");
                    }
                }
            }

            if (what is "all" or "uniques")
            {
                Console.WriteLine("\nUniques");
                foreach (UniqueDefinition unique in UniqueCatalog.All)
                {
                    Console.WriteLine($"  {unique.Name} ({unique.Base})");
                    foreach (Mod mod in unique.Mods) Console.WriteLine($"      {mod.Describe()}");
                    Console.WriteLine($"      \"{unique.Lore}\"");
                }
            }

            if (what is "all" or "notables")
            {
                Console.WriteLine("\nNotables");
                foreach (TalentNode node in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Notable))
                {
                    Console.WriteLine($"  {node.Name}: {string.Join("; ", node.Mods.Select(m => m.Describe()))}");
                }
            }

            if (what is "all" or "affixes")
            {
                Console.WriteLine("\nAffixes");
                foreach (Affix affix in AffixCatalog.All)
                {
                    Console.WriteLine($"  {affix.Id}: {string.Join("; ", affix.Build(affix.Tiers[^1].High).Select(m => m.Describe()))}");
                }
            }

            if (what is "all" or "skills")
            {
                Console.WriteLine("\nSkills");
                foreach (SkillDefinition skill in SkillCatalog.All.Where(s => !s.Hidden))
                {
                    Console.WriteLine($"  {skill.Name}: {skill.Text}");
                    foreach (Mod mod in skill.Mods) Console.WriteLine($"      {mod.Describe()}");
                }
            }

            if (what == "tree")
            {
                foreach (TalentNode node in TalentTree.Nodes) Console.WriteLine($"{node.Id} {node.Position.X:0} {node.Position.Y:0}");
            }

            if (what == "terms")
            {
                foreach (Term term in Lexicon.All) Console.WriteLine($"{term.Name}: {term.Says}");
            }

            if (what is "cards" or "sheet")
            {
                var hero = new HeroState { Level = options.Int("level", 12) };
                hero.Equipped[WornSlot.MainHand] = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = hero.Level, Kind = WeaponKind.Sword, WeaponDamage = 18f };
                hero.Equipped[WornSlot.Body] = new Item { Name = "Coat", Slot = ItemSlot.Body, Level = hero.Level, Armour = 80f };
                ActorSheet sheet = HeroBuild.Compile(hero).SheetFor(Condition.None);

                if (what == "cards")
                {
                    foreach (SkillDefinition skill in SkillCatalog.All) Print(Cards.Skill(sheet.Preview(skill)));
                }
                else
                {
                    foreach (StatRow row in StatBook.Rows) Print(Cards.OfRow(row, sheet));
                }
            }
            return 0;
        }
    }
}
