using Dino_Defenders.Progression;
using Dino_Defenders.Books;
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
                foreach (Region sector in TalentContent.Regions)
                {
                    Console.WriteLine($"\n{sector.Name}");
                    foreach (TalentNode node in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone && TalentContent.Regions[n.Region] == sector))
                    {
                        Console.WriteLine($"  {node.Name}  ({node.Id}, {TalentTree.DepthOf[node.Index]} points out, {node.Position.Length:0} from the root)");
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
                    foreach (Mod mod in unique.Boon) Console.WriteLine($"      {mod.Describe()}");
                    foreach (Mod mod in unique.Burden) Console.WriteLine($"    - {mod.Describe()}");
                    Console.WriteLine($"      \"{unique.Lore}\"");
                }
            }

            if (what is "all" or "notables")
            {
                Console.WriteLine("\nNotables");
                foreach (TalentNode node in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Notable))
                {
                    Console.WriteLine($"  {node.Name} ({node.Id}): {string.Join("; ", node.Mods.Select(m => m.Describe()))}");
                }
            }

            if (what is "ids")
            {
                foreach (TalentNode node in TalentTree.Nodes) Console.WriteLine(node.Id);
            }

            // Every node exactly where it was grown, with what it's joined to, so that two builds can be compared for a tree that came out the same.
            if (what is "layout")
            {
                var invariant = System.Globalization.CultureInfo.InvariantCulture;
                foreach (TalentNode node in TalentTree.Nodes)
                {
                    string links = string.Join(",", node.Links.Select(l => TalentTree.Nodes[l].Id));
                    Console.WriteLine(string.Create(invariant, $"{node.Index} {node.Id} {node.Kind} {node.Position.X:R} {node.Position.Y:R} {node.Orbit.X:R} {node.Orbit.Y:R} {node.OrbitRadius:R} {node.Twin} {links}"));
                }
            }

            if (what is "all" or "smalls")
            {
                Console.WriteLine("\nSmall nodes, by what they're called");
                foreach (var group in TalentTree.Nodes.Where(n => n.Kind == TalentKind.Small).GroupBy(Cards.TitleOf).OrderBy(g => g.Key))
                {
                    string[] gives = group.Select(n => string.Join("; ", n.Mods.Select(m => m.Describe()))).Distinct().ToArray();
                    Console.WriteLine($"  {group.Key} ({group.Count()}): {string.Join(" | ", gives)}");
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

            if (what is "all" or "slots")
            {
                foreach (ItemSlot slot in Enum.GetValues<ItemSlot>().Where(slot => slot < ItemSlot.Book))
                {
                    Console.WriteLine($"\n{slot}");
                    foreach (bool prefix in new[] { true, false })
                    {
                        Console.WriteLine($"  {(prefix ? "Prefixes" : "Suffixes")}: {string.Join(", ", AffixCatalog.All.Where(a => a.Prefix == prefix && a.Slots.Contains(slot)).Select(a => a.Id))}");
                    }
                }
            }

            if (what == "items")
            {
                var random = new Dino_Engine.World.WorldRandom(1);
                foreach (ItemSlot slot in Enum.GetValues<ItemSlot>().Where(slot => slot < ItemSlot.Book))
                {
                    foreach (Rarity rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
                    {
                        Item item = ItemGenerator.Forge(30, random.Fork((int)slot * 4 + (int)rarity), rarity, 0.5f, slot);
                        Console.WriteLine($"\n{item.Name} ({Cards.KindOf(item)})");
                        foreach (Mod mod in item.RolledMods()) Console.WriteLine($"  {mod.Describe()}");
                    }
                }
            }

            if (what is "all" or "phrases")
            {
                foreach (Blank blank in Enum.GetValues<Blank>())
                {
                    Console.WriteLine($"\n{BookText.NameOf(blank)}");
                    foreach (Phrase phrase in Phrases.For(blank)) Console.WriteLine($"  {phrase.Words}: {string.Join(", ", BookText.Trade(phrase.Weights))}");
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
                    foreach (BookPlan skill in Shelf.All) Print(Cards.Book(sheet.Preview(skill)));

                    // Books and pages as they're found, each with what's rolled on it, and a book with a page in it.
                    var random = new Dino_Engine.World.WorldRandom(5);
                    foreach (Rarity rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
                    {
                        Item book = ItemGenerator.Folio(hero.Level, random.Fork($"book{rarity}"), rarity, ItemSlot.Book);
                        Item page = ItemGenerator.Folio(hero.Level, random.Fork($"page{rarity}"), rarity, ItemSlot.Page);
                        Print(Cards.Item(page, sheet, "Page"));
                        if (book.Mode != BookMode.Toggle || page.Page!.Kind == PageKind.Area) book.Held![0] = page;
                        Print(Cards.Item(book, sheet, "Book"));
                        Print(Cards.Item(ItemGenerator.Folio(hero.Level, random.Fork($"sentence{rarity}"), rarity, ItemSlot.Sentence), sheet, "Sentence"));
                    }
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
