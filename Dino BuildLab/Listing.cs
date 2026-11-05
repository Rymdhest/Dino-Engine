using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;

namespace Dino_BuildLab
{
    /// <summary>Everything a player can read, in one place, for reading. Text that's generated can be right and still read badly, and only reading it shows that.</summary>
    internal static class Listing
    {
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
            return 0;
        }
    }
}
